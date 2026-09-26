using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VideoCall.Client.Media;
using VideoCall.Client.Services;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Networking;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.ViewModels;

/// äãæĞÌ ÇáÚÑÖ (ViewModel) ÇáãÓÄæá Úä ÅÏÇÑÉ æÇÌåÉ ÇáãßÇáãÉ ÇáÍíÉ.
/// íÑÈØ Èíä ÅÔÇÑÇÊ ÇáÔÈßÉ (TCP)¡ äŞá ÇáæÓÇÆØ (UDP)¡ æÎÏãÇÊ ÇáÊŞÇØ/ÊÔÛíá ÇáÕæÊ æÇáİíÏíæ.
public sealed class CallViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkClient _network;
    private readonly string _serverHost;
    private readonly Guid _callId;

    // áÅáÛÇÁ ÇáÚãáíÇÊ ÛíÑ ÇáãÊÒÇãäÉ (ãËá ãåÇã ÇáÅÑÓÇá) ÚäÏ ÅäåÇÁ ÇáãßÇáãÉ
    private readonly CancellationTokenSource _stop = new();

    // ãÌãÚÇÊ ÍÒã ÇáÈíÇäÇÊ ÇáæÇÑÏÉ áÅÚÇÏÉ ÈäÇÁ ÇáÅØÇÑÇÊ æÇáŞØÚ ÇáÕæÊíÉ
    private readonly VideoFrameReassembler _reassembler = new();
    private readonly AudioChunkReassembler _audioReassembler = new();

    // ÊÊÈÚ ãåÇã ÇáÅÑÓÇá ÇáÍÇáíÉ áÖãÇä ÇßÊãÇáåÇ Ãæ ÅáÛÇÆåÇ ÈÃãÇä ÚäÏ ÇáÅÛáÇŞ
    private readonly ConcurrentBag<Task> _sendTasks = new();

    private IMediaTransport? _udp;
    private AudioCaptureService? _audioCapture;
    private AudioPlaybackService? _audioPlayback;
    private VideoCaptureService? _videoCapture;
    private BitmapSource? _localVideo;
    private BitmapSource? _remoteVideo;
    private bool _isMuted;
    private bool _isCameraOn = true;
    private string _stateText = "ÌÇÑò ÇáÇÊÕÇá...";

    // ÇÓÊÎÏÇã ãÊÛíÑÇÊ ÑŞãíÉ áÖãÇä ÓáÇãÉ ÇáÎíæØ (Thread-safety) ÈÇÓÊÎÏÇã Interlocked
    private int _mediaStarted;
    private int _closed;

    public string OtherParty { get; }
    public BitmapSource? LocalVideo { get => _localVideo; private set => SetField(ref _localVideo, value); }
    public BitmapSource? RemoteVideo { get => _remoteVideo; private set => SetField(ref _remoteVideo, value); }
    public bool IsMuted { get => _isMuted; private set => SetField(ref _isMuted, value); }
    public bool IsCameraOn { get => _isCameraOn; private set => SetField(ref _isCameraOn, value); }
    public string StateText { get => _stateText; private set => SetField(ref _stateText, value); }

    public ICommand ToggleMuteCommand { get; }
    public ICommand ToggleCameraCommand { get; }
    public ICommand EndCallCommand { get; }
    public event Action? CallClosed;

    public CallViewModel(INetworkClient network, string serverHost, string otherParty, Guid callId)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _serverHost = serverHost ?? throw new ArgumentNullException(nameof(serverHost));
        _callId = callId;
        OtherParty = otherParty?.Trim() ?? throw new ArgumentNullException(nameof(otherParty));
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ToggleCameraCommand = new RelayCommand(ToggleCamera);
        EndCallCommand = new AsyncCommand(EndCallAsync);

        // ÇáÇÔÊÑÇß İí ÃÍÏÇË ÇáÔÈßÉ ÇáÎÇÕÉ ÈÇáãßÇáãÉ
        _network.CallAccepted += OnCallAccepted;
        _network.CallEnded += OnCallEnded;
        _network.RoomMediaStarted += OnMediaStarted;
        _network.Disconnected += OnDisconnected;
    }

    private void OnCallAccepted(CallAcceptedPayload payload)
    {
        if (payload.CallId != _callId)
            return;
        RunOnUi(() => StateText = "Êã ŞÈæá ÇáÇÊÕÇá¡ ÌÇÑí ÊÔÛíá ÇáæÓÇÆØ...");
    }

    private void OnMediaStarted(RoomMediaPayload payload)
    {
        if (!string.Equals(payload.RoomId, _callId.ToString("N"), StringComparison.OrdinalIgnoreCase))
            return;

        // ÖãÇä ÊäİíĞ ÊåíÆÉ ÇáæÓÇÆØ ãÑÉ æÇÍÏÉ İŞØ ÈÇÓÊÎÏÇã Interlocked
        if (payload.MediaId == Guid.Empty || Interlocked.Exchange(ref _mediaStarted, 1) != 0)
            return;

        RunOnUi(() => StateText = "ãÊÕá");
        StartMediaPipeline(payload.MediaId);
    }

    /// ÊåíÆÉ ãÓÇÑ ÇáæÓÇÆØ: ÈÏÁ Úãíá UDP¡ ÎÏãÇÊ ÇáÕæÊ¡ æÎÏãÇÊ ÇáİíÏíæ.
    private void StartMediaPipeline(Guid mediaId)
    {
        if (_network.SessionToken is not { } token || string.IsNullOrWhiteSpace(_network.Username))
        {
            RunOnUi(() => StateText = "ÌáÓÉ ÇáãÓÊÎÏã ÛíÑ ÕÇáÍÉ");
            return;
        }

        try
        {
            // 1. ÊåíÆÉ Úãíá UDP áÇÓÊŞÈÇá æÅÑÓÇá ÇáæÓÇÆØ
            _udp = new UdpMediaClient(_serverHost, token, mediaId, _network.Username);
            _udp.AudioPacketReceived += packet =>
            {
                var complete = _audioReassembler.Accept(packet);
                if (complete is not null)
                    _audioPlayback?.Enqueue(complete);
            };
            _udp.VideoPacketReceived += OnRemoteVideo;
            _udp.TransportError += ex => RunOnUi(() => StateText = $"ÎØÃ UDP: {ex.Message}");
            _udp.Start();

            // 2. ÊåíÆÉ ÎÏãÇÊ ÇáÕæÊ ãÚ ÊİÚíá ÎæÇÑÒãíÉ ÅáÛÇÁ ÇáÕÏì (AEC)
            _audioPlayback = new AudioPlaybackService();
            // ÊãÑíÑ ÇáãÑÌÚ ÇáÕæÊí (EchoReference) ÇáÎÇÕ ÈÇáÊÔÛíá Åáì ÎÏãÉ ÇáÊŞÇØ ÇáãÇíßÑæİæä¡
            // áíÊã ØÑÍ ÕæÊ ÇáÓãÇÚÇÊ ãä ÕæÊ ÇáãÇíßÑæİæä æãäÚ ÍÏæË ÇáÕÏì.
            _audioCapture = new AudioCaptureService(_audioPlayback.EchoReference) { IsMuted = IsMuted };
            _audioCapture.ChunkCaptured += chunk => TrackSend(_udp.SendAudioAsync(chunk, _stop.Token));
            _audioCapture.Start();

            // 3. ÊåíÆÉ ÎÏãÉ ÇáÊŞÇØ ÇáİíÏíæ (ÇáßÇãíÑÇ)
            _videoCapture = new VideoCaptureService();
            _videoCapture.FrameCaptured += OnLocalFrame;
            _videoCapture.Start();
        }
        catch (Exception ex)
        {
            RunOnUi(() => StateText = $"ÊÚĞÑ ÊÔÛíá ÇáßÇãíÑÇ/ÇáÕæÊ: {ex.Message}");
        }
    }

    private void OnLocalFrame(byte[] encodedFrame, byte[] preview)
    {
        if (!IsCameraOn)
            return;
        RunOnUi(() =>
        {
            try
            { LocalVideo = FrameCodec.BytesToBitmapSource(preview); }
            catch { }
        });

        // ÅÑÓÇá ÇáÅØÇÑ ÇáãÔİÑ ááØÑİ ÇáÂÎÑ æÅÖÇİÉ ÇáãåãÉ ááãÊÊÈÚ
        if (_udp is not null)
            TrackSend(_udp.SendVideoFrameAsync(encodedFrame, _stop.Token));
    }

    private void OnRemoteVideo(MediaPacket packet)
    {
        var complete = _reassembler.Accept(packet);
        if (complete is null)
            return;

        // İß ÊÔİíÑ ÇáÅØÇÑ ÇáãÓÊáã æÚÑÖå ÈÃãÇä Úáì ÎíØ æÇÌåÉ ÇáãÓÊÎÏã (UI Thread)
        RunOnUi(() =>
        {
            try
            { RemoteVideo = FrameCodec.BytesToBitmapSource(complete); }
            catch { }
        });
    }

    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        if (_audioCapture is not null)
            _audioCapture.IsMuted = IsMuted;
    }

    private void ToggleCamera()
    {
        IsCameraOn = !IsCameraOn;
        _videoCapture?.SetCameraOn(IsCameraOn);
        if (!IsCameraOn)
            LocalVideo = null; // ãÓÍ ÇáÕæÑÉ ÇáãÍáíÉ ÚäÏ ÇáÅÛáÇŞ
    }

    private void OnCallEnded(CallEndedPayload payload)
    {
        if (payload.CallId != _callId)
            return;
        RunOnUi(() => _ = CloseAsync("ÇäÊåÊ ÇáãßÇáãÉ"));
    }

    private void OnDisconnected() => RunOnUi(() => _ = CloseAsync("ÇäŞØÚ ÇáÇÊÕÇá ÈÇáÎÇÏã"));

    private async Task EndCallAsync()
    {
        if (_callId != Guid.Empty)
        {
            try
            { await _network.EndCallAsync(_callId).ConfigureAwait(false); }
            catch { }
        }
        await CloseAsync("ÇäÊåÊ ÇáãßÇáãÉ").ConfigureAwait(false);
    }

    private async Task CloseAsync(string message)
    {
        // ÖãÇä ÊäİíĞ ÚãáíÉ ÇáÅÛáÇŞ ãÑÉ æÇÍÏÉ İŞØ
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;

        RunOnUi(() => StateText = message);
        _stop.Cancel(); // ÅáÛÇÁ Ãí ãåÇã ÅÑÓÇá ãÚáŞÉ

        // ÅíŞÇİ ÇáãäÊÌíä (ÇáßÇãíÑÇ æÇáãÇíß) ÃæáÇğº áãäÚ ÅÖÇİÉ ãåÇã ÅÑÓÇá ÌÏíÏÉ 
        // ÈíäãÇ íäÊÙÑ ßæÏ ÇáÅÛáÇŞ ÇßÊãÇá ÇáãåÇã ÇáŞÏíãÉ.
        _videoCapture?.Dispose();
        _audioCapture?.Dispose();

        // ÇäÊÙÇÑ ÇßÊãÇá ÇáãåÇã ÇáÍÇáíÉ ÈÃãÇä æÊÌÇåá ÇÓÊËäÇÁÇÊ ÇáÅáÛÇÁ
        try
        { await Task.WhenAll(_sendTasks.ToArray()).ConfigureAwait(false); }
        catch (OperationCanceledException) { }

        _audioPlayback?.Dispose();
        _udp?.Dispose();

        // ÅáÛÇÁ ÇáÇÔÊÑÇß İí ÇáÃÍÏÇË áãäÚ ÊÓÑÈ ÇáĞÇßÑÉ (Memory Leaks)
        _network.CallAccepted -= OnCallAccepted;
        _network.CallEnded -= OnCallEnded;
        _network.RoomMediaStarted -= OnMediaStarted;
        _network.Disconnected -= OnDisconnected;

        CallClosed?.Invoke();
    }

    private void TrackSend(Task task) => _sendTasks.Add(task);

    // Âãä ááÇÓÊÏÚÇÁ Ïæä ÇáŞáŞ ãä ÊÌãíÏ ÇáæÇÌåÉ: ßá ÎØæÉ İí CloseAsync ÊÓÊÎÏã ConfigureAwait(false)¡
    // ããÇ íÚäí Ãä ÇáÇÓÊßãÇáÇÊ (Continuations) ÊÚãá Úáì Thread Pool æáÇ ÊÍÊÇÌ ááÚæÏÉ Åáì ÎíØ ÇáæÇÌåÉ (UI thread).
    // åĞÇ ÇáÊÕãíã íÊÌäÈ ãÔßáÉ ÇáÜ Deadlock ÇáÔåíÑÉ İí WPF.
    public void Dispose() => CloseAsync("Êã ÅÛáÇŞ ÇáãßÇáãÉ").GetAwaiter().GetResult();

    public Task DisposeAsync() => CloseAsync("Êã ÅÛáÇŞ ÇáãßÇáãÉ");

    /// ÏÇáÉ ãÓÇÚÏÉ áÖãÇä ÊäİíĞ ÇáÅÌÑÇÁÇÊ ÇáãÑÊÈØÉ ÈæÇÌåÉ ÇáãÓÊÎÏã Úáì ÎíØ ÇáÜ UI ÈÔßá Âãä.
    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}
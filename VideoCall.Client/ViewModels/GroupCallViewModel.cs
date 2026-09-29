using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VideoCall.Client.Media;
using VideoCall.Client.Services;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Networking;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.ViewModels;

/// <summary>
/// äãæÐÌ ÇáÚÑÖ (ViewModel) ÇáãÓÄæá Úä ÅÏÇÑÉ ÇáãßÇáãÇÊ ÇáÌãÇÚíÉ æÑÈØ ÇáÃÚÖÇÁ ÇáãÊÚÏÏíä.
/// íÊæáì ÊäÓíÞ ÏÝÞ ÇáÝíÏíæ/ÇáÕæÊ áÚÏÉ ãÔÇÑßíä ÈÔßá ãÊÒÇãä¡ æÅÏÇÑÉ ÞäÇÉ ÅÑÓÇá ÇáßÇãíÑÇ¡ æÇáÊÚÇãá ãÚ ÇáÎÇÏã ÚÈÑ UDP/TCP.
/// </summary>
public sealed class GroupCallViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkClient _network;
    private readonly string _serverHost;
    private readonly string _roomId;
    private readonly Guid _mediaId;

    // ÅáÛÇÁ ßÇÝÉ ÇáÚãáíÇÊ ÇáÎáÝíÉ ÛíÑ ÇáãÊÒÇãäÉ ÚäÏ ÅÛáÇÞ ÇáÛÑÝÉ
    private readonly CancellationTokenSource _stop = new();

    // ÞäÇÉ (Channel) ãÍÏæÏÉ ÈÓÚÉ 2 ÅØÇÑ áÅÑÓÇá ÇáÝíÏíæ ÈÔßá Âãä æÛíÑ ãÊÒÇãä Ïæä ÍÌÈ ÎíØ ÇáÇáÊÞÇØ¡
    // ãÚ ÇáÊÎáÕ ÊáÞÇÆíÇð ãä ÇáÅØÇÑÇÊ ÇáÞÏíãÉ ÚäÏ ÍÏæË ÈØÁ Ýí ÇáÔÈßÉ (DropOldest).
    private readonly Channel<byte[]> _videoQueue = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(2) { FullMode = BoundedChannelFullMode.DropOldest });

    // ÞÇÆãÉ ÞÇÈáÉ ááãáÇÍÙÉ ÊÍÊæí Úáì ÈíÇäÇÊ ÌãíÚ ÇáãÔÇÑßíä ÇáÈÚíÏíä Ýí ÇáãßÇáãÉ áÚÑÖåã Ýí æÇÌåÉ WPF
    private readonly ObservableCollection<RemoteParticipantViewModel> _participants = new();

    // ÞÇãæÓ áÊÌãíÚ ÍÒã ÝíÏíæ ßá ãÔÇÑß ÈÚíÏ ÈÔßá ãÓÊÞá ÈäÇÁð Úáì ÇÓã ÇáãÓÊÎÏã ÇáÎÇÕ Èå
    private readonly Dictionary<string, VideoFrameReassembler> _reassemblers = new(StringComparer.OrdinalIgnoreCase);
    private readonly AudioChunkReassembler _audioReassembler = new();

    private IMediaTransport? _udp;
    private AudioCaptureService? _audioCapture;
    private AudioPlaybackService? _audioPlayback;
    private VideoCaptureService? _videoCapture;
    private Task? _videoSender;
    private BitmapSource? _localVideo;
    private bool _muted;
    private bool _cameraOn = true;
    private string _statusMessage = "ÌÇÑí ÊåíÆÉ ÇáãÍÇÏËÉ...";
    private int _closed;

    public string RoomId => _roomId;
    public Guid MediaId => _mediaId;
    public BitmapSource? LocalVideo { get => _localVideo; private set => SetField(ref _localVideo, value); }
    public bool IsMuted { get => _muted; private set => SetField(ref _muted, value); }
    public bool IsCameraOn { get => _cameraOn; private set => SetField(ref _cameraOn, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public ObservableCollection<RemoteParticipantViewModel> Participants => _participants;

    public ICommand ToggleMuteCommand { get; }
    public ICommand ToggleCameraCommand { get; }
    public ICommand LeaveCommand { get; }
    public event Action? Closed;

    public GroupCallViewModel(
        INetworkClient network,
        string serverHost,
        string roomId,
        Guid mediaId,
        IEnumerable<string> members)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _serverHost = serverHost;
        _roomId = roomId;
        _mediaId = mediaId;

        // ÅÏÑÇÌ ÇáãÔÇÑßíä ÇáãÊæÇÌÏíä Ýí ÇáÛÑÝÉ ÈÇÓÊËäÇÁ ÇáãÓÊÎÏã ÇáÍÇáí
        foreach (var member in members.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!member.Equals(_network.Username, StringComparison.OrdinalIgnoreCase))
                AddParticipant(member);
        }

        // ÇáÇÔÊÑÇß Ýí ÃÍÏÇË ÊæÞÝ ÇáæÓÇÆØ æÇäÞØÇÚ ÇáÇÊÕÇá
        _network.RoomMediaStopped += OnRoomMediaStopped;
        _network.RoomUpdated += OnRoomUpdated;
        _network.Disconnected += OnDisconnected;
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ToggleCameraCommand = new RelayCommand(ToggleCamera);
        LeaveCommand = new AsyncCommand(LeaveAsync);
        StartMediaPipeline();
    }

    /// <summary>
    /// ÊåíÆÉ æÈÏÁ ãÓÇÑ äÞá ÇáæÓÇÆØ ÇáÌãÇÚíÉ (UDP ÇáÕæÊ/ÇáÝíÏíæ¡ ÎæÇÑÒãíÉ AEC¡ æãåãÉ ÅÑÓÇá ÇáÝíÏíæ ÇáÎáÝíÉ).
    /// </summary>
    private void StartMediaPipeline()
    {
        if (_network.SessionToken is not { } token || string.IsNullOrWhiteSpace(_network.Username))
        {
            StatusMessage = "ÌáÓÉ ÇáãÓÊÎÏã ÛíÑ ÕÇáÍÉ.";
            return;
        }

        // 1. ÅÚÏÇÏ Úãíá UDP áãÚÇáÌÉ æÇÓÊÞÈÇá ÍÒã ÇáÕæÊ æÇáÝíÏíæ
        _udp = new UdpMediaClient(_serverHost, token, _mediaId, _network.Username);
        _udp.AudioPacketReceived += packet =>
        {
            var complete = _audioReassembler.Accept(packet);
            if (complete is not null)
                _audioPlayback?.Enqueue(complete);
        };
        _udp.VideoPacketReceived += OnRemoteVideo;
        _udp.TransportError += ex => RunOnUi(() => StatusMessage = $"ÎØÃ UDP: {ex.Message}");
        _udp.Start();

        // 2. ÊåíÆÉ ÎÏãÇÊ ÇáÕæÊ æÊãÑíÑ ãÑÌÚ ÅáÛÇÁ ÇáÕÏì ÇáÕæÊí (AEC)
        _audioPlayback = new AudioPlaybackService();
        // ÊãÑíÑ ÇáãÑÌÚ ÇáÕæÊí ááÊÔÛíá Åáì ÎÏãÉ ÇáÊÞÇØ ÇáãÇíßÑæÝæä áÎÕã ÕæÊ ÇáÓãÇÚÇÊ æãäÚ ÇáÕÏì.
        _audioCapture = new AudioCaptureService(_audioPlayback.EchoReference);
        _audioCapture.ChunkCaptured += chunk => _ = _udp.SendAudioAsync(chunk);
        _audioCapture.Start();

        // 3. ÊåíÆÉ ÎÏãÉ ÇáßÇãíÑÇ æÍáÞÉ ÅÑÓÇá ÇáÝíÏíæ ÛíÑ ÇáãÊÒÇãäÉ
        try
        {
            _videoCapture = new VideoCaptureService();
            _videoCapture.FrameCaptured += OnLocalFrame;
            _videoCapture.Start();
            _videoSender = SendVideoLoopAsync(_stop.Token);
            StatusMessage = "ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ ãÊÕáÉ.";
        }
        catch (InvalidOperationException)
        {
            IsCameraOn = false;
            StatusMessage = "ÇáÕæÊ ãÊÕá¡ áßä ÇáßÇãíÑÇ ÛíÑ ãÊÇÍÉ.";
        }
    }

    private void OnLocalFrame(byte[] encodedFrame, byte[] preview)
    {
        // ÅÖÇÝÉ ÇáÅØÇÑ ÇáãÔÝÑ Åáì ÞäÇÉ ÇáÅÑÓÇá
        _videoQueue.Writer.TryWrite(encodedFrame);
        RunOnUi(() =>
        {
            try
            { LocalVideo = FrameCodec.BytesToBitmapSource(preview); }
            catch { }
        });
    }

    /// <summary>
    /// ÍáÞÉ ÎáÝíÉ ãÓÊãÑÉ ÊÞÑÃ ÅØÇÑÇÊ ÇáÝíÏíæ ãä ÇáÞäÇÉ æÊãÑÑåÇ áÚãíá UDP áÅÑÓÇáåÇ.
    /// </summary>
    private async Task SendVideoLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _videoQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (_udp is not null)
                    await _udp.SendVideoFrameAsync(frame, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    /// <summary>
    /// ÇÓÊÞÈÇá ÅØÇÑÇÊ ÇáÝíÏíæ ãä ÇáãÔÇÑßíä ÇáÂÎÑíä æÊÌãíÏåÇ æÚÑÖåÇ ÈäÇÁð Úáì ÇÓã ßá ãÑÓá.
    /// </summary>
    private void OnRemoteVideo(MediaPacket packet)
    {
        if (!_reassemblers.TryGetValue(packet.SenderUsername, out var reassembler))
        {
            reassembler = new VideoFrameReassembler();
            _reassemblers[packet.SenderUsername] = reassembler;
        }

        var complete = reassembler.Accept(packet);
        if (complete is null)
            return;
        RunOnUi(() =>
        {
            try
            {
                var participant = AddParticipant(packet.SenderUsername);
                participant.Video = FrameCodec.BytesToBitmapSource(complete);
                participant.IsCameraOn = true;
            }
            catch { }
        });
    }

    /// <summary>
    /// ÅÏÑÇÌ ãÔÇÑß ÌÏíÏ Ýí ÇáÞÇÆãÉ ÇáÞÇÈáÉ ááãáÇÍÙÉ ÅÐÇ áã íßä ãæÌæÏÇð ãä ÞÈá.
    /// </summary>
    private RemoteParticipantViewModel AddParticipant(string username)
    {
        var existing = _participants.FirstOrDefault(x =>
            x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;
        var participant = new RemoteParticipantViewModel(username);
        _participants.Add(participant);
        return participant;
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
            LocalVideo = null;
    }

    private void OnRoomUpdated(RoomUpdatePayload payload)
    {
        if (!payload.RoomId.Equals(_roomId, StringComparison.OrdinalIgnoreCase))
            return;

        RunOnUi(() =>
        {
            var activeMembers = payload.Members
                .Where(member => !member.Equals(_network.Username, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var removed = _participants
                .Where(participant => !activeMembers.Contains(participant.Username))
                .Select(participant => participant.Username)
                .ToList();

            foreach (var username in removed)
            {
                var participant = _participants.FirstOrDefault(x =>
                    x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
                if (participant is not null)
                    _participants.Remove(participant);
                _reassemblers.Remove(username);
            }

            if (removed.Count > 0)
                StatusMessage = removed.Count == 1
                    ? $"غادر {removed[0]} المجموعة."
                    : $"غادر {string.Join(" و", removed)} المجموعة.";
        });
    }

    private void OnRoomMediaStopped(RoomMediaPayload payload)
    {
        if (!payload.RoomId.Equals(_roomId, StringComparison.OrdinalIgnoreCase))
            return;
        RunOnUi(() => _ = CloseAsync("Êã ÅíÞÇÝ ÇáãÍÇÏËÉ ãä ÇáãÖíÝ."));
    }

    private void OnDisconnected() => RunOnUi(() => _ = CloseAsync("ÇäÞØÚ ÇáÇÊÕÇá ÈÇáÎÇÏã."));

    private async Task LeaveAsync() => await CloseAsync("ÊãÊ ãÛÇÏÑÉ ÇáãÍÇÏËÉ.");

    /// <summary>
    /// ÇáÊÎáí Úä ÇáãæÇÑÏ¡ ÅíÞÇÝ ÇáÞäæÇÊ¡ æÇáÊäÙíÝ ÇáÔÇãá áÌãíÚ ÇáãßæäÇÊ ÚäÏ ÇáÎÑæÌ ãä ÇáãßÇáãÉ ÇáÌãÇÚíÉ.
    /// </summary>
    private async Task CloseAsync(string message)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;
        StatusMessage = message;
        _stop.Cancel();
        _videoQueue.Writer.TryComplete();

        // ÅíÞÇÝ ÌãíÚ ÇáãäÊÌíä ÃæáÇð ÞÈá ÇäÊÙÇÑ ÇßÊãÇá ÚãáíÇÊ ÇáÅÑÓÇá ÇáãÚáÞÉ
        _videoCapture?.Dispose();
        _audioCapture?.Dispose();
        if (_videoSender is not null)
        {
            try
            { await _videoSender.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        try
        { await _network.LeaveRoomAsync(_roomId).ConfigureAwait(false); }
        catch { }

        _audioPlayback?.Dispose();
        _udp?.Dispose();

        _network.RoomMediaStopped -= OnRoomMediaStopped;
        _network.RoomUpdated -= OnRoomUpdated;
        _network.Disconnected -= OnDisconnected;
        Closed?.Invoke();
    }

    // Âãä ááÇÓÊÏÚÇÁ ÇáÍÇÕÑ: ßá ÎØæÉ ãäÊÙÑÉ ÃÚáÇå ÊÓÊÎÏã ConfigureAwait(false)¡
    // æÈÇáÊÇáí áÇ ÊÍÊÇÌ áÇÓÊÆäÇÝ ÇáÎíØ Úáì ÎíØ ÇáæÇÌåÉ (UI)¡ ããÇ íãäÚ ÍÏæË ÇáÌãæÏ (Deadlock) Ýí WPF.
    public void Dispose() => CloseAsync("Êã ÅÛáÇÞ äÇÝÐÉ ÇáãÍÇÏËÉ.").GetAwaiter().GetResult();

    public Task DisposeAsync() => CloseAsync("Êã ÅÛáÇÞ äÇÝÐÉ ÇáãÍÇÏËÉ.");

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}

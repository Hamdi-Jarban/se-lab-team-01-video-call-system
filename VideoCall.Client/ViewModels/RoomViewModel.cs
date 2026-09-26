using System.Collections.ObjectModel;
using System.Windows.Input;
using VideoCall.Client.Services;
using VideoCall.Shared.Messages;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.ViewModels;

// ÇáäãæĞÌ ÇáãÓÄæá Úä ÅÏÇÑÉ ÊİÇÕíá ÇáÛÑİÉ¡ ÇáÃÚÖÇÁ¡ æÇáÊÍßã ÈÇáæÓÇÆØ (ááãÖíİ æÇáãÔÇÑßíä)
public sealed class RoomViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkClient _network;
    private string _roomId = string.Empty;
    private string _host = string.Empty;
    private string _newMemberUsername = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _hasRoom;
    private bool _isMediaActive;
    private Guid _mediaId;

    // ŞÇÆãÉ ÈÃÓãÇÁ ÇáÃÚÖÇÁ ÇáãÊæÇÌÏíä ÍÇáíÇğ İí ÇáÛÑİÉ ááÑÈØ ãÚ æÇÌåÉ ÇáãÓÊÎÏã
    public ObservableCollection<string> Members { get; } = new();

    public string RoomId { get => _roomId; set => SetField(ref _roomId, value); }
    public string Host { get => _host; private set => SetField(ref _host, value); }
    public string NewMemberUsername { get => _newMemberUsername; set => SetField(ref _newMemberUsername, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool HasRoom { get => _hasRoom; private set => SetField(ref _hasRoom, value); }
    public bool IsMediaActive { get => _isMediaActive; private set => SetField(ref _isMediaActive, value); }
    public Guid MediaId { get => _mediaId; private set => SetField(ref _mediaId, value); }

    // ÇáÊÍŞŞ ããÇ ÅĞÇ ßÇä ÇáãÓÊÎÏã ÇáÍÇáí åæ ãÖíİ ÇáÛÑİÉ (Host)
    public bool IsHost => string.Equals(Host, _network.Username, StringComparison.OrdinalIgnoreCase);

    // ÃæÇãÑ ÇáÊÍßã ÇáÃÓÇÓíÉ ÈÇáÛÑİÉ
    public ICommand CreateRoomCommand { get; }
    public ICommand JoinRoomCommand { get; }
    public ICommand AddUserCommand { get; }
    public ICommand StartMediaCommand { get; }
    public ICommand StopMediaCommand { get; }
    public ICommand LeaveRoomCommand { get; }

    // ÃÍÏÇË áÊäÈíå ÇáæÇÌåÉ ÚäÏ ÈÏÁ Ãæ ÅíŞÇİ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ
    public event Action<RoomMediaPayload>? GroupMediaStarted;
    public event Action<RoomMediaPayload>? GroupMediaStopped;

    public RoomViewModel(INetworkClient network)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));

        // ÇáÇÔÊÑÇß İí ÃÍÏÇË ÇáÔÈßÉ ÇáÎÇÕÉ ÈÇáÛÑİÉ ÇáæÇÑÏÉ ãä ÇáÎÇÏã
        _network.RoomUpdated += OnRoomUpdated;
        _network.RoomError += OnRoomError;
        _network.RoomMediaStarted += OnRoomMediaStarted;
        _network.RoomMediaStopped += OnRoomMediaStopped;

        CreateRoomCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom && !string.IsNullOrWhiteSpace(RoomId))
            {
                StatusMessage = "ÌÇÑí ÅäÔÇÁ ÇáÛÑİÉ...";
                await _network.CreateRoomAsync(RoomId.Trim());
            }
        });

        JoinRoomCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom && !string.IsNullOrWhiteSpace(RoomId))
            {
                StatusMessage = "ÌÇÑí ÇáÇäÖãÇã Åáì ÇáÛÑİÉ...";
                await _network.JoinRoomAsync(RoomId.Trim());
            }
        });

        // ÅÖÇİÉ ãÓÊÎÏã ÌÏíÏ ááÛÑİÉ
        AddUserCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom || IsMediaActive || string.IsNullOrWhiteSpace(NewMemberUsername))
                return;
            await _network.AddUserToRoomAsync(RoomId.Trim(), NewMemberUsername.Trim());
            NewMemberUsername = string.Empty;
        });

        // ÈÏÁ ÈË ÇáæÓÇÆØ ÇáÌãÇÚíÉ (íõÓãÍ ááãÖíİ İŞØ)
        StartMediaCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom || !IsHost || IsMediaActive)
                return;
            StatusMessage = "ÌÇÑí ÈÏÁ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ...";
            await _network.StartRoomMediaAsync(RoomId.Trim());
        });

        // ÅíŞÇİ ÈË ÇáæÓÇÆØ ÇáÌãÇÚíÉ (íõÓãÍ ááãÖíİ İŞØ)
        StopMediaCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom || !IsHost || !IsMediaActive)
                return;
            await _network.StopRoomMediaAsync(RoomId.Trim(), MediaId);
        });

        LeaveRoomCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom)
                return;
            await _network.LeaveRoomAsync(RoomId.Trim());
            ResetRoom("ÊãÊ ãÛÇÏÑÉ ÇáÛÑİÉ.");
        });
    }

    // ÊÍÏíË ÍÇáÉ ÇáÛÑİÉ æŞÇÆãÉ ÇáÃÚÖÇÁ æãÖíİåÇ ÚäÏ ÊáŞí ÊÍÏíË ãä ÇáÎÇÏã
    private void OnRoomUpdated(RoomUpdatePayload payload)
    {
        if (HasRoom && !string.Equals(payload.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))
            return;
        RoomId = payload.RoomId;
        Host = payload.Host;
        Members.Clear();
        foreach (var member in payload.Members.Distinct(StringComparer.OrdinalIgnoreCase))
            Members.Add(member);
        HasRoom = true;
        Raise(nameof(IsHost)); // ÊÍÏíË ÍÇáÉ IsHost İí ÇáæÇÌåÉ
        StatusMessage = IsHost ? "ÃäÊ ãÖíİ ÇáÛÑİÉ." : "Êã ÊÍÏíË ÃÚÖÇÁ ÇáÛÑİÉ.";
    }

    // ãÚÇáÌÉ ÍÏË ÈÏÁ ÇáæÓÇÆØ æÅØáÇŞ ÍÏË ááæÇÌåÉ áÈÏÁ ÇáÚÑÖ
    private void OnRoomMediaStarted(RoomMediaPayload payload)
    {
        if (!string.Equals(payload.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))
            return;
        MediaId = payload.MediaId;
        IsMediaActive = true;
        StatusMessage = "ÈÏÃÊ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ.";
        GroupMediaStarted?.Invoke(payload);
    }

    // ãÚÇáÌÉ ÍÏË ÅíŞÇİ ÇáæÓÇÆØ
    private void OnRoomMediaStopped(RoomMediaPayload payload)
    {
        if (!string.Equals(payload.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))
            return;
        IsMediaActive = false;
        MediaId = Guid.Empty;
        StatusMessage = "Êã ÅíŞÇİ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ.";
        GroupMediaStopped?.Invoke(payload);
    }

    // ÊÍæíá ÑãæÒ ÇáÃÎØÇÁ ÇáŞÇÏãÉ ãä ÇáÎÇÏã Åáì ÑÓÇÆá æÇÖÍÉ æãŞÑæÁÉ ááãÓÊÎÏã
    private void OnRoomError(RoomErrorPayload payload)
    {
        StatusMessage = payload.ErrorCode switch
        {
            ErrorCodes.RoomAlreadyExists => "ÇáÛÑİÉ ãæÌæÏÉ ÈÇáİÚá.",
            ErrorCodes.RoomNotFound => "ÇáÛÑİÉ ÛíÑ ãæÌæÏÉ.",
            ErrorCodes.RoomFull => "ÇáÛÑİÉ ããÊáÆÉ.",
            ErrorCodes.UserNotFound => "ÇáãÓÊÎÏã ÛíÑ ãÊÕá.",
            ErrorCodes.NotRoomMember => "ÃäÊ áÓÊ ÚÖæğÇ Ãæ áÇ Êãáß ÇáÕáÇÍíÉ.",
            ErrorCodes.MediaAlreadyStarted => "ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ ÈÏÃÊ ÈÇáİÚá.",
            _ => payload.Message
        };
    }

    // ÅÚÇÏÉ ÖÈØ ÌãíÚ ãÊÛíÑÇÊ ÇáÛÑİÉ Åáì ÍÇáÊåÇ ÇáÇİÊÑÇÖíÉ ÚäÏ ÇáãÛÇÏÑÉ
    private void ResetRoom(string message)
    {
        HasRoom = false;
        IsMediaActive = false;
        MediaId = Guid.Empty;
        RoomId = string.Empty;
        Host = string.Empty;
        Members.Clear();
        StatusMessage = message;
    }

    // ÊÍÑíÑ ÇáãæÇÑÏ æÅáÛÇÁ ÇáÇÔÊÑÇß ãä ÃÍÏÇË ÇáÔÈßÉ áãäÚ ÊÓÑÈ ÇáĞÇßÑÉ (Memory Leaks)
    public void Dispose()
    {
        _network.RoomUpdated -= OnRoomUpdated;
        _network.RoomError -= OnRoomError;
        _network.RoomMediaStarted -= OnRoomMediaStarted;
        _network.RoomMediaStopped -= OnRoomMediaStopped;
    }
}
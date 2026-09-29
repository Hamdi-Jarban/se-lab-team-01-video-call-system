using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using VideoCall.Client.Services;
using VideoCall.Shared.Messages;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.ViewModels;

// ÇáäãæÐÌ ÇáãÓÄæá Úä ÅÏÇÑÉ ÊÝÇÕíá ÇáÛÑÝÉ¡ ÇáÃÚÖÇÁ¡ æÇáÊÍßã ÈÇáæÓÇÆØ (ááãÖíÝ æÇáãÔÇÑßíä)
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

    // ÞÇÆãÉ ÈÃÓãÇÁ ÇáÃÚÖÇÁ ÇáãÊæÇÌÏíä ÍÇáíÇð Ýí ÇáÛÑÝÉ ááÑÈØ ãÚ æÇÌåÉ ÇáãÓÊÎÏã
    public ObservableCollection<string> Members { get; } = new();
    public ObservableCollection<string> OnlineUsers { get; }

    public string RoomId { get => _roomId; set => SetField(ref _roomId, value); }
    public string Host { get => _host; private set => SetField(ref _host, value); }
    public string NewMemberUsername { get => _newMemberUsername; set => SetField(ref _newMemberUsername, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool HasRoom { get => _hasRoom; private set => SetField(ref _hasRoom, value); }
    public bool IsMediaActive { get => _isMediaActive; private set => SetField(ref _isMediaActive, value); }
    public Guid MediaId { get => _mediaId; private set => SetField(ref _mediaId, value); }

    // ÇáÊÍÞÞ ããÇ ÅÐÇ ßÇä ÇáãÓÊÎÏã ÇáÍÇáí åæ ãÖíÝ ÇáÛÑÝÉ (Host)
    public bool IsHost => string.Equals(Host, _network.Username, StringComparison.OrdinalIgnoreCase);

    // ÃæÇãÑ ÇáÊÍßã ÇáÃÓÇÓíÉ ÈÇáÛÑÝÉ
    public ICommand CreateRoomCommand { get; }
    public ICommand JoinRoomCommand { get; }
    public ICommand AddUserCommand { get; }
    public ICommand StartMediaCommand { get; }
    public ICommand StopMediaCommand { get; }
    public ICommand LeaveRoomCommand { get; }

    // ÃÍÏÇË áÊäÈíå ÇáæÇÌåÉ ÚäÏ ÈÏÁ Ãæ ÅíÞÇÝ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ
    public event Action<RoomMediaPayload>? GroupMediaStarted;
    public event Action<RoomMediaPayload>? GroupMediaStopped;

    public RoomViewModel(INetworkClient network, ObservableCollection<string>? onlineUsers = null)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        OnlineUsers = onlineUsers ?? new ObservableCollection<string>();

        // ÇáÇÔÊÑÇß Ýí ÃÍÏÇË ÇáÔÈßÉ ÇáÎÇÕÉ ÈÇáÛÑÝÉ ÇáæÇÑÏÉ ãä ÇáÎÇÏã
        _network.RoomUpdated += OnRoomUpdated;
        _network.OnlineUsersUpdated += OnOnlineUsersUpdated;
        _network.RoomError += OnRoomError;
        _network.RoomMediaStarted += OnRoomMediaStarted;
        _network.RoomMediaStopped += OnRoomMediaStopped;

        CreateRoomCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom && !string.IsNullOrWhiteSpace(RoomId))
            {
                StatusMessage = "ÌÇÑí ÅäÔÇÁ ÇáÛÑÝÉ...";
                await _network.CreateRoomAsync(RoomId.Trim());
            }
        });

        JoinRoomCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom && !string.IsNullOrWhiteSpace(RoomId))
            {
                StatusMessage = "ÌÇÑí ÇáÇäÖãÇã Åáì ÇáÛÑÝÉ...";
                await _network.JoinRoomAsync(RoomId.Trim());
            }
        });

        // ÅÖÇÝÉ ãÓÊÎÏã ÌÏíÏ ááÛÑÝÉ
        AddUserCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom || IsMediaActive || string.IsNullOrWhiteSpace(NewMemberUsername))
                return;
            await _network.AddUserToRoomAsync(RoomId.Trim(), NewMemberUsername.Trim());
            NewMemberUsername = string.Empty;
        });

        // ÈÏÁ ÈË ÇáæÓÇÆØ ÇáÌãÇÚíÉ (íõÓãÍ ááãÖíÝ ÝÞØ)
        StartMediaCommand = new AsyncCommand(async () =>
        {
            if (!HasRoom || !IsHost || IsMediaActive)
                return;
            StatusMessage = "ÌÇÑí ÈÏÁ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ...";
            await _network.StartRoomMediaAsync(RoomId.Trim());
        });

        // ÅíÞÇÝ ÈË ÇáæÓÇÆØ ÇáÌãÇÚíÉ (íõÓãÍ ááãÖíÝ ÝÞØ)
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
            ResetRoom("ÊãÊ ãÛÇÏÑÉ ÇáÛÑÝÉ.");
        });
    }

    // ÊÍÏíË ÍÇáÉ ÇáÛÑÝÉ æÞÇÆãÉ ÇáÃÚÖÇÁ æãÖíÝåÇ ÚäÏ ÊáÞí ÊÍÏíË ãä ÇáÎÇÏã
    private void OnOnlineUsersUpdated(OnlineUsersUpdatePayload payload)
    {
        var update = new Action(() =>
        {
            OnlineUsers.Clear();
            foreach (var username in payload.Usernames
                         .Where(user => !user.Equals(_network.Username, StringComparison.OrdinalIgnoreCase))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
                OnlineUsers.Add(username);
        });

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) update();
        else dispatcher.BeginInvoke(update);
    }

    private void OnRoomUpdated(RoomUpdatePayload payload)
    {
        if (HasRoom && !string.Equals(payload.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))
            return;
        var previousMembers = Members.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var updatedMembers = payload.Members.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var removedMembers = previousMembers.Except(updatedMembers, StringComparer.OrdinalIgnoreCase).ToList();

        RoomId = payload.RoomId;
        Host = payload.Host;
        Members.Clear();
        foreach (var member in updatedMembers)
            Members.Add(member);
        HasRoom = true;
        Raise(nameof(IsHost)); // ÊÍÏíË ÍÇáÉ IsHost Ýí ÇáæÇÌåÉ
        StatusMessage = removedMembers.Count == 1
            ? $"غادر {removedMembers[0]} الغرفة."
            : IsHost ? "أنت مضيف الغرفة." : "تم تحديث أعضاء الغرفة.";
    }

    // ãÚÇáÌÉ ÍÏË ÈÏÁ ÇáæÓÇÆØ æÅØáÇÞ ÍÏË ááæÇÌåÉ áÈÏÁ ÇáÚÑÖ
    private void OnRoomMediaStarted(RoomMediaPayload payload)
    {
        if (!string.Equals(payload.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))
            return;
        MediaId = payload.MediaId;
        IsMediaActive = true;
        StatusMessage = "ÈÏÃÊ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ.";
        GroupMediaStarted?.Invoke(payload);
    }

    // ãÚÇáÌÉ ÍÏË ÅíÞÇÝ ÇáæÓÇÆØ
    private void OnRoomMediaStopped(RoomMediaPayload payload)
    {
        if (!string.Equals(payload.RoomId, RoomId, StringComparison.OrdinalIgnoreCase))
            return;
        IsMediaActive = false;
        MediaId = Guid.Empty;
        StatusMessage = "Êã ÅíÞÇÝ ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ.";
        GroupMediaStopped?.Invoke(payload);
    }

    // ÊÍæíá ÑãæÒ ÇáÃÎØÇÁ ÇáÞÇÏãÉ ãä ÇáÎÇÏã Åáì ÑÓÇÆá æÇÖÍÉ æãÞÑæÁÉ ááãÓÊÎÏã
    private void OnRoomError(RoomErrorPayload payload)
    {
        StatusMessage = payload.ErrorCode switch
        {
            ErrorCodes.RoomAlreadyExists => "ÇáÛÑÝÉ ãæÌæÏÉ ÈÇáÝÚá.",
            ErrorCodes.RoomNotFound => "ÇáÛÑÝÉ ÛíÑ ãæÌæÏÉ.",
            ErrorCodes.RoomFull => "ÇáÛÑÝÉ ããÊáÆÉ.",
            ErrorCodes.UserNotFound => "ÇáãÓÊÎÏã ÛíÑ ãÊÕá.",
            ErrorCodes.NotRoomMember => "ÃäÊ áÓÊ ÚÖæðÇ Ãæ áÇ Êãáß ÇáÕáÇÍíÉ.",
            ErrorCodes.MediaAlreadyStarted => "ÇáãÍÇÏËÉ ÇáÌãÇÚíÉ ÈÏÃÊ ÈÇáÝÚá.",
            _ => payload.Message
        };
    }

    // ÅÚÇÏÉ ÖÈØ ÌãíÚ ãÊÛíÑÇÊ ÇáÛÑÝÉ Åáì ÍÇáÊåÇ ÇáÇÝÊÑÇÖíÉ ÚäÏ ÇáãÛÇÏÑÉ
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

    // ÊÍÑíÑ ÇáãæÇÑÏ æÅáÛÇÁ ÇáÇÔÊÑÇß ãä ÃÍÏÇË ÇáÔÈßÉ áãäÚ ÊÓÑÈ ÇáÐÇßÑÉ (Memory Leaks)
    public void Dispose()
    {
        _network.RoomUpdated -= OnRoomUpdated;
        _network.OnlineUsersUpdated -= OnOnlineUsersUpdated;
        _network.RoomError -= OnRoomError;
        _network.RoomMediaStarted -= OnRoomMediaStarted;
        _network.RoomMediaStopped -= OnRoomMediaStopped;
    }
}

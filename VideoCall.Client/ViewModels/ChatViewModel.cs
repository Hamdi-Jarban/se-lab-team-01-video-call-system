using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using VideoCall.Client.Contracts;
using VideoCall.Shared.Messages;

namespace VideoCall.Client.ViewModels;

// منطق شاشة المحادثات: قائمة المحادثات + السجل + الإرسال الفوري + إدارة المجموعات.
// كل البيانات تأتي من الخادم (SQL Server)؛ لا توجد رسائل أو مستخدمون وهميون هنا.
public sealed class ChatViewModel : ViewModelBase, IDisposable
{
    private const int PageSize = 50;
    private static readonly char[] MemberSeparators = { ',', '،', ';', ' ', '\n', '\t' };

    private readonly INetworkClient _network;

    private ConversationItemViewModel? _selected;
    private string _messageInput = string.Empty;
    private string _status = string.Empty;
    private string _newChatUsername = string.Empty;
    private string _newGroupName = string.Empty;
    private string _newGroupMembers = string.Empty;
    private string _newMemberUsername = string.Empty;
    private string? _selectedMember;
    private bool _isConnected;
    private bool _hasMoreHistory;
    private bool _loadingOlder;
    private ChatMessageItemViewModel? _editing;

    public ChatViewModel(INetworkClient network, ObservableCollection<string>? onlineUsers = null)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        OnlineUsers = onlineUsers ?? new ObservableCollection<string>();
        _isConnected = network.IsConnected;

        var view = CollectionViewSource.GetDefaultView(Conversations);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ConversationItemViewModel.Category)));
        view.SortDescriptions.Add(new SortDescription(nameof(ConversationItemViewModel.CategoryOrder), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(ConversationItemViewModel.LastActivityTicks), ListSortDirection.Descending));
        ConversationsView = view;

        SendCommand = Command(SendAsync, () => IsConnected && SelectedConversation is not null && !string.IsNullOrWhiteSpace(MessageInput));
        OpenPrivateChatCommand = Command(OpenPrivateFromInputAsync, () => IsConnected && !string.IsNullOrWhiteSpace(NewChatUsername));
        CreateGroupCommand = Command(CreateGroupAsync, () => IsConnected && !string.IsNullOrWhiteSpace(NewGroupName));
        AddMemberCommand = Command(AddMemberAsync, () => IsConnected && IsAdmin && !string.IsNullOrWhiteSpace(NewMemberUsername));
        RemoveMemberCommand = Command(RemoveMemberAsync, () => IsConnected && IsAdmin && !string.IsNullOrWhiteSpace(SelectedMember));
        LeaveGroupCommand = Command(LeaveGroupAsync, () => IsConnected && IsGroupSelected && !IsAdmin);
        CancelEditCommand = Command(() => { CancelEdit(); MessageInput = string.Empty; return Task.CompletedTask; }, () => IsEditing);
        CallCommand = Command(() => { CallRequested?.Invoke(PrivatePeerName); return Task.CompletedTask; }, () => IsConnected && IsPrivateSelected);
        LoadOlderCommand = Command(LoadOlderAsync, () => IsConnected && HasMoreHistory && SelectedConversation is not null);
        RefreshCommand = Command(_network.GetConversationsAsync, () => IsConnected);

        _network.ConversationsLoaded += OnConversationsLoaded;
        _network.OnlineUsersUpdated += OnOnlineUsersUpdated;
        _network.ConversationOpened += OnConversationOpened;
        _network.ConversationMembersUpdated += OnMembersUpdated;
        _network.ConversationRemoved += OnConversationRemoved;
        _network.ChatMessageReceived += OnMessageReceived;
        _network.MessagesLoaded += OnMessagesLoaded;
        _network.ChatErrorReceived += OnChatError;
        _network.MessageEdited += OnMessageEdited;
        _network.MessageDeleted += OnMessageDeleted;
        _network.Disconnected += OnDisconnected;

        // عند تسجيل الدخول: تحميل محادثات المستخدم من SQL
        if (_isConnected)
            _ = RunAsync(_network.GetConversationsAsync);
    }

    // ---------- الحالة المعروضة ----------

    public ObservableCollection<ConversationItemViewModel> Conversations { get; } = new();

    public ObservableCollection<string> OnlineUsers { get; }

    public ICollectionView ConversationsView { get; }

    public ObservableCollection<ChatMessageItemViewModel> Messages { get; } = new();

    public string CurrentUsername => _network.Username ?? string.Empty;

    public ConversationItemViewModel? SelectedConversation
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            OnSelectionChanged();
        }
    }

    public bool IsConversationSelected => _selected is not null;

    public bool IsGroupSelected => _selected?.IsGroup == true;

    public bool IsPrivateSelected => _selected is { IsGroup: false };

    // اسم الطرف الآخر في المحادثة الخاصة (هو اسم المحادثة نفسها)
    private string PrivatePeerName => _selected?.Name ?? string.Empty;

    public bool IsEditing => _editing is not null;

    public bool IsAdmin =>
        _selected is { IsGroup: true } c &&
        string.Equals(c.CreatedBy, CurrentUsername, StringComparison.OrdinalIgnoreCase);

    public string SelectedTitle => _selected?.Name ?? string.Empty;

    public string MessageInput
    {
        get => _messageInput;
        set { if (SetField(ref _messageInput, value)) SendCommand.Refresh(); }
    }

    public string NewChatUsername
    {
        get => _newChatUsername;
        set { if (SetField(ref _newChatUsername, value)) OpenPrivateChatCommand.Refresh(); }
    }

    public string NewGroupName
    {
        get => _newGroupName;
        set { if (SetField(ref _newGroupName, value)) CreateGroupCommand.Refresh(); }
    }

    public string NewGroupMembers { get => _newGroupMembers; set => SetField(ref _newGroupMembers, value); }

    public string NewMemberUsername
    {
        get => _newMemberUsername;
        set { if (SetField(ref _newMemberUsername, value)) AddMemberCommand.Refresh(); }
    }

    public string? SelectedMember
    {
        get => _selectedMember;
        set { if (SetField(ref _selectedMember, value)) RemoveMemberCommand.Refresh(); }
    }

    public string Status { get => _status; private set => SetField(ref _status, value); }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetField(ref _isConnected, value))
                RefreshCommands();
        }
    }

    public bool HasMoreHistory
    {
        get => _hasMoreHistory;
        private set { if (SetField(ref _hasMoreHistory, value)) LoadOlderCommand.Refresh(); }
    }

    public AsyncCommand SendCommand { get; }
    public AsyncCommand CancelEditCommand { get; }
    public AsyncCommand CallCommand { get; }
    public AsyncCommand OpenPrivateChatCommand { get; }
    public AsyncCommand CreateGroupCommand { get; }
    public AsyncCommand AddMemberCommand { get; }
    public AsyncCommand RemoveMemberCommand { get; }
    public AsyncCommand LeaveGroupCommand { get; }
    public AsyncCommand LoadOlderCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    // تُطلق عند وصول رسالة من مستخدم آخر (لعرض إشعار في النافذة الرئيسية)
    public event Action<string, string>? MessageArrived;

    // طلب بدء مكالمة خاصة مع مستخدم (تنفذها النافذة الرئيسية)
    public event Action<string>? CallRequested;

    // تطلب من النافذة التمرير إلى آخر رسالة
    public event Action? ScrollToEndRequested;

    // ---------- أوامر عامة ----------

    // تُستدعى من النافذة الرئيسية ("مراسلة" بجانب المستخدم المتصل)
    public Task OpenPrivateChatWithAsync(string username) =>
        string.IsNullOrWhiteSpace(username)
            ? Task.CompletedTask
            : RunAsync(() => _network.OpenPrivateChatAsync(username.Trim()));

    // ---------- تنفيذ الأوامر ----------

    private async Task SendAsync()
    {
        var conversation = _selected;
        var text = MessageInput.Trim();
        if (conversation is null || text.Length == 0) return;

        var editing = _editing;
        MessageInput = string.Empty;

        if (editing is not null)
        {
            // نعرض التعديل عند وصول MessageEdited من الخادم بعد حفظه في SQL
            CancelEdit();
            if (!string.Equals(editing.Content, text, StringComparison.Ordinal))
                await _network.EditChatMessageAsync(conversation.ConversationId, editing.MessageId, text);
            return;
        }

        // لا نضيف الرسالة محليًا؛ تظهر عند وصول MessageReceived من الخادم بعد حفظها في SQL
        await _network.SendChatMessageAsync(conversation.ConversationId, text);
    }

    // ---------- تعديل/حذف رسالة (للمرسل فقط؛ الخادم يتحقق أيضًا) ----------

    public void BeginEdit(ChatMessageItemViewModel message)
    {
        if (!message.IsMine) return;

        _editing = message;
        MessageInput = message.Content;
        Raise(nameof(IsEditing));
        CancelEditCommand.Refresh();
    }

    public void CancelEdit()
    {
        if (_editing is null) return;

        _editing = null;
        Raise(nameof(IsEditing));
        CancelEditCommand.Refresh();
    }

    public async Task DeleteMessageAsync(ChatMessageItemViewModel message)
    {
        var conversation = _selected;
        if (conversation is null || !message.IsMine) return;

        if (ReferenceEquals(_editing, message))
        {
            CancelEdit();
            MessageInput = string.Empty;
        }

        await RunAsync(() => _network.DeleteChatMessageAsync(conversation.ConversationId, message.MessageId));
    }

    private async Task OpenPrivateFromInputAsync()
    {
        var username = NewChatUsername.Trim();
        if (username.Length == 0) return;

        NewChatUsername = string.Empty;
        await _network.OpenPrivateChatAsync(username);
    }

    private async Task CreateGroupAsync()
    {
        var name = NewGroupName.Trim();
        if (name.Length == 0) return;

        var members = ParseUsernames(NewGroupMembers);
        NewGroupName = string.Empty;
        NewGroupMembers = string.Empty;
        await _network.CreateGroupAsync(name, members);
    }

    private async Task AddMemberAsync()
    {
        var conversation = _selected;
        var usernames = ParseUsernames(NewMemberUsername);
        if (conversation is null || usernames.Count == 0) return;

        NewMemberUsername = string.Empty;
        await _network.AddGroupMembersAsync(conversation.ConversationId, usernames);
    }

    private async Task RemoveMemberAsync()
    {
        var conversation = _selected;
        var member = SelectedMember;
        if (conversation is null || string.IsNullOrWhiteSpace(member)) return;

        await _network.RemoveGroupMemberAsync(conversation.ConversationId, member);
    }

    private async Task LeaveGroupAsync()
    {
        var conversation = _selected;
        if (conversation is null || !conversation.IsGroup) return;

        await _network.RemoveGroupMemberAsync(conversation.ConversationId, CurrentUsername);
    }

    private async Task LoadOlderAsync()
    {
        var conversation = _selected;
        var oldest = Messages.FirstOrDefault();
        if (conversation is null || oldest is null) return;

        _loadingOlder = true;
        await _network.GetMessagesAsync(conversation.ConversationId, oldest.MessageId, PageSize);
    }

    private void OnOnlineUsersUpdated(OnlineUsersUpdatePayload payload)
    {
        var update = new Action(() =>
        {
            OnlineUsers.Clear();
            foreach (var username in payload.Usernames
                         .Where(user => !user.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
                OnlineUsers.Add(username);
        });

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) update();
        else dispatcher.BeginInvoke(update);
    }

    // ---------- أحداث الشبكة (تصل على خيط الواجهة) ----------

    private void OnSelectionChanged()
    {
        Messages.Clear();
        HasMoreHistory = false;
        _loadingOlder = false;
        SelectedMember = null;
        CancelEdit();

        Raise(nameof(IsConversationSelected));
        Raise(nameof(IsPrivateSelected));
        Raise(nameof(IsGroupSelected));
        Raise(nameof(IsAdmin));
        Raise(nameof(SelectedTitle));
        RefreshCommands();

        var conversation = _selected;
        if (conversation is null) return;

        conversation.UnreadCount = 0;
        _ = RunAsync(() => _network.GetMessagesAsync(conversation.ConversationId, null, PageSize));
    }

    private void OnConversationsLoaded(GetConversationsResponsePayload payload)
    {
        var incoming = payload.Conversations ?? new List<ConversationDto>();

        foreach (var gone in Conversations
                     .Where(c => incoming.All(d => d.ConversationId != c.ConversationId))
                     .ToList())
        {
            Conversations.Remove(gone);
        }

        foreach (var dto in incoming)
            Upsert(dto);

        if (_selected is not null && !Conversations.Contains(_selected))
            SelectedConversation = null;

        ConversationsView.Refresh();
    }

    private void OnConversationOpened(ConversationOpenedPayload payload)
    {
        var item = Upsert(payload.Conversation);
        ConversationsView.Refresh();

        // المستخدم الذي طلب الفتح/الإنشاء تُفتح له المحادثة مباشرة
        if (string.Equals(payload.OpenedBy, CurrentUsername, StringComparison.OrdinalIgnoreCase))
            SelectedConversation = item;
    }

    private void OnMembersUpdated(ConversationMembersPayload payload)
    {
        var item = Find(payload.ConversationId);
        if (item is null) return;

        item.SetMembers(payload.Members);
        if (ReferenceEquals(item, _selected))
        {
            Raise(nameof(IsAdmin));
            RefreshCommands();
        }
    }

    private void OnConversationRemoved(ConversationRemovedPayload payload)
    {
        var item = Find(payload.ConversationId);
        if (item is null) return;

        Conversations.Remove(item);
        if (ReferenceEquals(item, _selected))
            SelectedConversation = null;

        ConversationsView.Refresh();
    }

    private void OnMessageReceived(MessageReceivedPayload payload)
    {
        var dto = payload.Message;
        var conversation = Find(dto.ConversationId);
        var mine = string.Equals(dto.SenderUsername, CurrentUsername, StringComparison.OrdinalIgnoreCase);

        if (conversation is null)
        {
            // محادثة غير معروفة بعد (مثلًا أُضفنا إلى مجموعة أثناء عدم الاتصال): نحدّث القائمة من الخادم
            _ = RunAsync(_network.GetConversationsAsync);
        }
        else
        {
            conversation.SetLastMessage(dto.Content, dto.SentAtUtc);
        }

        if (ReferenceEquals(conversation, _selected) && conversation is not null)
        {
            if (Messages.All(m => m.MessageId != dto.MessageId))
            {
                Messages.Add(new ChatMessageItemViewModel(dto, mine));
                ScrollToEndRequested?.Invoke();
            }
        }
        else if (conversation is not null && !mine)
        {
            conversation.UnreadCount++;
        }

        if (!mine)
            MessageArrived?.Invoke(dto.SenderUsername, conversation?.Name ?? dto.SenderUsername);

        ConversationsView.Refresh();
    }

    private void OnMessagesLoaded(GetMessagesResponsePayload payload)
    {
        if (_selected is null || payload.ConversationId != _selected.ConversationId) return;

        var items = (payload.Messages ?? new List<ChatMessageDto>())
            .Select(m => new ChatMessageItemViewModel(m, IsMine(m)))
            .ToList();

        if (_loadingOlder)
        {
            _loadingOlder = false;
            for (var i = items.Count - 1; i >= 0; i--)
                Messages.Insert(0, items[i]);
        }
        else
        {
            // رسائل وصلت لحظيًا قبل وصول السجل نحتفظ بها إن لم تكن ضمنه
            var lastHistoryId = items.Count > 0 ? items[^1].MessageId : 0;
            var pending = Messages.Where(m => m.MessageId > lastHistoryId).ToList();

            Messages.Clear();
            foreach (var item in items) Messages.Add(item);
            foreach (var extra in pending)
                if (Messages.All(m => m.MessageId != extra.MessageId))
                    Messages.Add(extra);

            ScrollToEndRequested?.Invoke();
        }

        HasMoreHistory = payload.HasMore;
    }

    private void OnMessageEdited(MessageEditedPayload payload)
    {
        if (_selected?.ConversationId != payload.ConversationId) return;
        Messages.FirstOrDefault(m => m.MessageId == payload.MessageId)?.ApplyEdit(payload.Content);
    }

    private void OnMessageDeleted(MessageDeletedPayload payload)
    {
        if (_selected?.ConversationId != payload.ConversationId) return;

        var item = Messages.FirstOrDefault(m => m.MessageId == payload.MessageId);
        if (item is null) return;

        if (ReferenceEquals(item, _editing))
        {
            CancelEdit();
            MessageInput = string.Empty;
        }

        Messages.Remove(item);
    }

    private void OnChatError(ChatErrorPayload error) =>
        Status = ErrorTexts.ForCode(error.ErrorCode, error.Message);

    private void OnDisconnected()
    {
        IsConnected = false;
        Status = "انقطع الاتصال بالخادم.";
    }

    // ---------- مساعدات ----------

    private ConversationItemViewModel? Find(int conversationId) =>
        Conversations.FirstOrDefault(c => c.ConversationId == conversationId);

    private ConversationItemViewModel Upsert(ConversationDto dto)
    {
        var existing = Find(dto.ConversationId);
        if (existing is not null)
        {
            existing.Apply(dto);
            if (ReferenceEquals(existing, _selected))
            {
                Raise(nameof(SelectedTitle));
                Raise(nameof(IsAdmin));
            }

            return existing;
        }

        var created = new ConversationItemViewModel(dto);
        Conversations.Add(created);
        return created;
    }

    private bool IsMine(ChatMessageDto dto) =>
        string.Equals(dto.SenderUsername, CurrentUsername, StringComparison.OrdinalIgnoreCase);

    private static List<string> ParseUsernames(string text) =>
        (text ?? string.Empty)
            .Split(MemberSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void RefreshCommands()
    {
        SendCommand.Refresh();
        OpenPrivateChatCommand.Refresh();
        CreateGroupCommand.Refresh();
        AddMemberCommand.Refresh();
        RemoveMemberCommand.Refresh();
        LeaveGroupCommand.Refresh();
        LoadOlderCommand.Refresh();
        RefreshCommand.Refresh();
        CancelEditCommand.Refresh();
        CallCommand.Refresh();
    }

    private AsyncCommand Command(Func<Task> execute, Func<bool>? canExecute = null)
    {
        var command = new AsyncCommand(execute, canExecute);
        command.Failed += ex => Status = ex.Message;
        return command;
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public void Dispose()
    {
        _network.ConversationsLoaded -= OnConversationsLoaded;
        _network.OnlineUsersUpdated -= OnOnlineUsersUpdated;
        _network.ConversationOpened -= OnConversationOpened;
        _network.ConversationMembersUpdated -= OnMembersUpdated;
        _network.ConversationRemoved -= OnConversationRemoved;
        _network.ChatMessageReceived -= OnMessageReceived;
        _network.MessagesLoaded -= OnMessagesLoaded;
        _network.ChatErrorReceived -= OnChatError;
        _network.MessageEdited -= OnMessageEdited;
        _network.MessageDeleted -= OnMessageDeleted;
        _network.Disconnected -= OnDisconnected;
    }
}

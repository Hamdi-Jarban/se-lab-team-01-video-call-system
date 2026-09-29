using System.Collections.ObjectModel;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Models;

namespace VideoCall.Client.ViewModels;

// عنصر في قائمة المحادثات
public sealed class ConversationItemViewModel : ViewModelBase
{
    private string _name;
    private string _createdBy;
    private string _preview = string.Empty;
    private DateTime? _lastActivityUtc;
    private int _unreadCount;

    public ConversationItemViewModel(ConversationDto dto)
    {
        ConversationId = dto.ConversationId;
        Type = dto.Type;
        _name = dto.Name;
        _createdBy = dto.CreatedBy;
        Apply(dto);
    }

    public int ConversationId { get; }

    public ConversationType Type { get; }

    public bool IsGroup => Type == ConversationType.Group;

    // عنوان القسم في القائمة
    public string Category => IsGroup ? "المجموعات" : "المحادثات الخاصة";

    public int CategoryOrder => IsGroup ? 1 : 0;

    public string Name { get => _name; private set => SetField(ref _name, value); }

    public string CreatedBy { get => _createdBy; private set => SetField(ref _createdBy, value); }

    public string Preview { get => _preview; private set => SetField(ref _preview, value); }

    public long LastActivityTicks => (_lastActivityUtc ?? DateTime.MinValue).Ticks;

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (SetField(ref _unreadCount, value))
                Raise(nameof(HasUnread));
        }
    }

    public bool HasUnread => _unreadCount > 0;

    public ObservableCollection<string> Members { get; } = new();

    // تحديث البيانات من الخادم (لا نمسح آخر رسالة إذا لم يرسلها الخادم في هذا الإشعار)
    public void Apply(ConversationDto dto)
    {
        Name = dto.Name;
        CreatedBy = dto.CreatedBy;
        SetMembers(dto.Members);

        if (dto.LastMessageAtUtc is not null)
            SetLastMessage(dto.LastMessagePreview ?? string.Empty, dto.LastMessageAtUtc.Value);
    }

    public void SetMembers(IEnumerable<string> members)
    {
        Members.Clear();
        foreach (var member in members)
            Members.Add(member);
    }

    public void SetLastMessage(string content, DateTime sentAtUtc)
    {
        var singleLine = content.Replace('\r', ' ').Replace('\n', ' ');
        Preview = singleLine.Length <= 60 ? singleLine : singleLine[..60] + "…";
        _lastActivityUtc = sentAtUtc;
        Raise(nameof(LastActivityTicks));
    }
}

// رسالة معروضة داخل المحادثة
public sealed class ChatMessageItemViewModel : ViewModelBase
{
    private string _content;
    private bool _isEdited;

    public ChatMessageItemViewModel(ChatMessageDto dto, bool isMine)
    {
        MessageId = dto.MessageId;
        Sender = string.IsNullOrWhiteSpace(dto.SenderDisplayName) ? dto.SenderUsername : dto.SenderDisplayName;
        _content = dto.Content;
        _isEdited = dto.EditedAtUtc is not null;
        IsMine = isMine;

        var local = dto.SentAtUtc.Kind == DateTimeKind.Utc ? dto.SentAtUtc.ToLocalTime() : dto.SentAtUtc;
        TimeText = local.Date == DateTime.Today
            ? local.ToString("HH:mm")
            : local.ToString("yyyy-MM-dd HH:mm");
    }

    public long MessageId { get; }

    public string Sender { get; }

    public string Content { get => _content; private set => SetField(ref _content, value); }

    public bool IsEdited { get => _isEdited; private set => SetField(ref _isEdited, value); }

    public void ApplyEdit(string content)
    {
        Content = content;
        IsEdited = true;
    }

    public string TimeText { get; }

    public bool IsMine { get; }
}

namespace VideoCall.Shared.Models;

// يعتمد نظام طلب الاتصال على هذا النموذج لإنشاء محادثة خاصة
// بحالة Created ومنع الطلبات المتزامنة.

public enum ConversationType
{
    Private,
    Group
}

public enum ConversationState
{
    Created,
    Active,
    Ended
}

public sealed class Conversation
{
    public string Id { get; init; } = string.Empty;

    public ConversationType Type { get; init; }

    public string Host { get; set; } = string.Empty;

    // Issue #3 يستخدم Members للتحقق من انشغال caller أو callee.
    public HashSet<string> Members { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Issue #3 ينشئ الطلب أولاً بحالة Created.
    public ConversationState State { get; set; } =
        ConversationState.Created;

    // يعتمد عليه كود الوسائط، وليس ضمن نطاق Issue #3.
    public Guid? MediaId { get; set; }
}

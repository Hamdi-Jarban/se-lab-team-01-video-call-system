using VideoCall.Shared.Models;

namespace VideoCall.Server.Domain.Repositories;

/// <summary>
/// يحدد نتائج عمليات إنشاء وتعديل المحادثات والغرف.
/// </summary>
public enum ConversationOperation
{
    Success,
    AlreadyExists,
    NotFound,
    AlreadyMember,
    NotMember,
    Full,
    InvalidType,
    NotHost,
    InvalidState,
    PrivateConversationMustHaveTwoMembers
}

/// <summary>
/// يدير المحادثات الخاصة والغرف الجماعية وأعضاءها وحالة الوسائط المرتبطة بها.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// ينشئ محادثة خاصة بين مستخدمين.
    /// </summary>
    ConversationOperation CreatePrivate(string conversationId, string caller, string callee, out Conversation? conversation);

    /// <summary>
    /// ينشئ غرفة جماعية جديدة.
    /// </summary>
    ConversationOperation CreateGroup(string conversationId, string host, out Conversation? conversation);

    /// <summary>
    /// يضيف عضوًا إلى محادثة أو غرفة.
    /// </summary>
    ConversationOperation AddMember(string conversationId, string requestingUser, string memberUsername, out Conversation? conversation);

    /// <summary>
    /// يضيف مستخدمًا إلى غرفة موجودة.
    /// </summary>
    ConversationOperation Join(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// يزيل مستخدمًا من المحادثة.
    /// </summary>
    ConversationOperation Leave(string conversationId, string username, out Conversation? conversation, out bool removed);

    /// <summary>
    /// يبدأ الوسائط داخل المحادثة.
    /// </summary>
    ConversationOperation StartMedia(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// ينهي محادثة خاصة ويعيد أعضاءها.
    /// </summary>
    ConversationOperation EndPrivate(string conversationId, string username, out IReadOnlyList<string> members);

    /// <summary>
    /// ينهي محادثة أو غرفة جماعية.
    /// </summary>
    ConversationOperation End(string conversationId, string username, out IReadOnlyList<string> members);

    /// <summary>
    /// يفعّل الوسائط لمستخدم عضو في المحادثة.
    /// </summary>
    ConversationOperation ActivateMedia(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// يوقف الوسائط داخل المحادثة.
    /// </summary>
    ConversationOperation StopMedia(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// يبحث عن محادثة باستخدام معرفها.
    /// </summary>
    bool TryGet(string conversationId, out Conversation conversation);

    /// <summary>
    /// يتحقق من عضوية مستخدم في محادثة.
    /// </summary>
    bool IsMember(string conversationId, string username);

    /// <summary>
    /// يبحث عن محادثة نشطة باستخدام معرف الوسائط.
    /// </summary>
    bool TryGetActiveConversationByMediaId(Guid mediaId, out Conversation conversation);

    /// <summary>
    /// يتحقق من تشغيل الوسائط داخل محادثة.
    /// </summary>
    bool IsMediaActive(string conversationId);

    /// <summary>
    /// يعيد نسخة من أعضاء المحادثة الحالية.
    /// </summary>
    IReadOnlyList<string> GetMembersSnapshot(string conversationId);

    /// <summary>
    /// يزيل المستخدم من جميع المحادثات المرتبط بها.
    /// </summary>
    IReadOnlyList<Conversation> RemoveUserFromAll(string username);

    /// <summary>
    /// يعيد نسخة للقراءة فقط من جميع المحادثات الحالية.
    /// </summary>
    IReadOnlyList<Conversation> GetAllSnapshot();
}

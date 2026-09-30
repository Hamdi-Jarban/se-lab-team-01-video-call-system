using VideoCall.Shared.Models;

namespace VideoCall.Server.Domain.Repositories;

/// <summary>
/// íÍÏÏ äÊÇÆÌ ÚãáíÇÊ ÅäÔÇÁ æÊÚÏíá ÇáãÍÇÏËÇÊ æÇáÛÑİ.
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
/// íÏíÑ ÇáãÍÇÏËÇÊ ÇáÎÇÕÉ æÇáÛÑİ ÇáÌãÇÚíÉ æÃÚÖÇÁåÇ æÍÇáÉ ÇáæÓÇÆØ ÇáãÑÊÈØÉ ÈåÇ.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// íäÔÆ ãÍÇÏËÉ ÎÇÕÉ Èíä ãÓÊÎÏãíä.
    /// </summary>
    ConversationOperation CreatePrivate(string conversationId, string caller, string callee, out Conversation? conversation);

    /// <summary>
    /// íäÔÆ ÛÑİÉ ÌãÇÚíÉ ÌÏíÏÉ.
    /// </summary>
    ConversationOperation CreateGroup(string conversationId, string host, out Conversation? conversation);

    /// <summary>
    /// íÖíİ ÚÖæğÇ Åáì ãÍÇÏËÉ Ãæ ÛÑİÉ.
    /// </summary>
    ConversationOperation AddMember(string conversationId, string requestingUser, string memberUsername, out Conversation? conversation);

    /// <summary>
    /// íÖíİ ãÓÊÎÏãğÇ Åáì ÛÑİÉ ãæÌæÏÉ.
    /// </summary>
    ConversationOperation Join(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// íÒíá ãÓÊÎÏãğÇ ãä ÇáãÍÇÏËÉ.
    /// </summary>
    ConversationOperation Leave(string conversationId, string username, out Conversation? conversation, out bool removed);

    /// <summary>
    /// íÈÏÃ ÇáæÓÇÆØ ÏÇÎá ÇáãÍÇÏËÉ.
    /// </summary>
    ConversationOperation StartMedia(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// íäåí ãÍÇÏËÉ ÎÇÕÉ æíÚíÏ ÃÚÖÇÁåÇ.
    /// </summary>
    ConversationOperation EndPrivate(string conversationId, string username, out IReadOnlyList<string> members);

    /// <summary>
    /// íäåí ãÍÇÏËÉ Ãæ ÛÑİÉ ÌãÇÚíÉ.
    /// </summary>
    ConversationOperation End(string conversationId, string username, out IReadOnlyList<string> members);

    /// <summary>
    /// íİÚøá ÇáæÓÇÆØ áãÓÊÎÏã ÚÖæ İí ÇáãÍÇÏËÉ.
    /// </summary>
    ConversationOperation ActivateMedia(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// íæŞİ ÇáæÓÇÆØ ÏÇÎá ÇáãÍÇÏËÉ.
    /// </summary>
    ConversationOperation StopMedia(string conversationId, string username, out Conversation? conversation);

    /// <summary>
    /// íÈÍË Úä ãÍÇÏËÉ ÈÇÓÊÎÏÇã ãÚÑİåÇ.
    /// </summary>
    bool TryGet(string conversationId, out Conversation conversation);

    /// <summary>
    /// íÊÍŞŞ ãä ÚÖæíÉ ãÓÊÎÏã İí ãÍÇÏËÉ.
    /// </summary>
    bool IsMember(string conversationId, string username);

    /// <summary>
    /// íÈÍË Úä ãÍÇÏËÉ äÔØÉ ÈÇÓÊÎÏÇã ãÚÑİ ÇáæÓÇÆØ.
    /// </summary>
    bool TryGetActiveConversationByMediaId(Guid mediaId, out Conversation conversation);

    /// <summary>
    /// íÊÍŞŞ ãä ÊÔÛíá ÇáæÓÇÆØ ÏÇÎá ãÍÇÏËÉ.
    /// </summary>
    bool IsMediaActive(string conversationId);

    /// <summary>
    /// íÚíÏ äÓÎÉ ãä ÃÚÖÇÁ ÇáãÍÇÏËÉ ÇáÍÇáíÉ.
    /// </summary>
    IReadOnlyList<string> GetMembersSnapshot(string conversationId);

    /// <summary>
    /// íÒíá ÇáãÓÊÎÏã ãä ÌãíÚ ÇáãÍÇÏËÇÊ ÇáãÑÊÈØ ÈåÇ.
    /// </summary>
    IReadOnlyList<Conversation> RemoveUserFromAll(string username);

    /// <summary>
    /// íÚíÏ äÓÎÉ ááŞÑÇÁÉ İŞØ ãä ÌãíÚ ÇáãÍÇÏËÇÊ ÇáÍÇáíÉ.
    /// </summary>
    IReadOnlyList<Conversation> GetAllSnapshot();
}

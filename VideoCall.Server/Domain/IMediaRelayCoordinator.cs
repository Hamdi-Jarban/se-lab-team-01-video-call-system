namespace VideoCall.Server.Domain;

/// <summary>
/// íæİøÑ ÇáÚãáíÇÊ ÇááÇÒãÉ áÅÏÇÑÉ äŞÇØ ÇÊÕÇá UDP ÇáãÑÊÈØÉ ÈÇáãÍÇÏËÇÊ.
/// </summary>
public interface IMediaRelayCoordinator
{
    /// <summary>
    /// íÍĞİ äŞØÉ ÇÊÕÇá ãÓÊÎÏã ãä ãÍÇÏËÉ ãÍÏÏÉ.
    /// </summary>
    /// <param name="conversationId">ãÚÑİ ÇáãÍÇÏËÉ.</param>
    /// <param name="username">ÇÓã ÇáãÓÊÎÏã.</param>
    void RemoveEndpoint(string conversationId, string username);

    /// <summary>
    /// íÍĞİ ÌãíÚ äŞÇØ ÇáÇÊÕÇá ÇáãÑÊÈØÉ ÈÇáãÍÇÏËÉ.
    /// </summary>
    /// <param name="conversationId">ãÚÑİ ÇáãÍÇÏËÉ.</param>
    void ForgetConversation(string conversationId);
}

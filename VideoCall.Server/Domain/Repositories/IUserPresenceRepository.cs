using VideoCall.Server.Domain;

namespace VideoCall.Server.Domain.Repositories;

/// <summary>
/// íÏíÑ ÇáãÓÊÎÏãíä ÇáãÊÕáíä æÇáÌáÓÇÊ ÇáãÑÊÈØÉ Èåã.
/// </summary>
public interface IUserPresenceRepository
{
    /// <summary>
    /// íÖíİ ãÓÊÎÏãğÇ Åáì ŞÇÆãÉ ÇáãÊÕáíä.
    /// </summary>
    /// <param name="username">ÇÓã ÇáãÓÊÎÏã.</param>
    /// <param name="session">ÌáÓÉ ÇáãÓÊÎÏã.</param>
    /// <returns>
    /// true ÚäÏ äÌÇÍ ÇáÅÖÇİÉ¡ æfalse ÅĞÇ ßÇä ÇáãÓÊÎÏã ãæÌæÏğÇ ãÓÈŞğÇ.
    /// </returns>
    bool TryAdd(string username, IClientHandler session);

    /// <summary>
    /// íÈÍË Úä ÌáÓÉ ãÓÊÎÏã ãÊÕá.
    /// </summary>
    /// <param name="username">ÇÓã ÇáãÓÊÎÏã.</param>
    /// <param name="session">ÇáÌáÓÉ ÇáãÑÊÈØÉ ÈÇáãÓÊÎÏã.</param>
    /// <returns>true ÅĞÇ Êã ÇáÚËæÑ Úáì ÇáãÓÊÎÏã.</returns>
    bool TryGet(
        string username,
        out IClientHandler session);

    /// <summary>
    /// íÊÍŞŞ ãä ÇÊÕÇá ÇáãÓÊÎÏã ÍÇáíğÇ.
    /// </summary>
    /// <param name="username">ÇÓã ÇáãÓÊÎÏã.</param>
    /// <returns>true ÅĞÇ ßÇä ÇáãÓÊÎÏã ãÊÕáğÇ.</returns>
    bool IsOnline(string username);

    /// <summary>
    /// íÒíá ÇáãÓÊÎÏã ãä ŞÇÆãÉ ÇáãÊÕáíä ÅĞÇ ßÇäÊ ÇáÌáÓÉ ÇáÍÇáíÉ
    /// åí äİÓåÇ ÇáÌáÓÉ ÇáãÊæŞÚÉ.
    /// </summary>
    /// <param name="username">ÇÓã ÇáãÓÊÎÏã.</param>
    /// <param name="expectedSession">ÇáÌáÓÉ ÇáãÑÇÏ ÇáÊÍŞŞ ãäåÇ.</param>
    /// <returns>true ÅĞÇ ÊãÊ ÇáÅÒÇáÉ ÈäÌÇÍ.</returns>
    bool Remove(
        string username,
        IClientHandler expectedSession);

    /// <summary>
    /// íÚíÏ ÃÓãÇÁ ÇáãÓÊÎÏãíä ÇáãÊÕáíä ÍÇáíğÇ.
    /// </summary>
    IReadOnlyList<string> GetUsernames();

    /// <summary>
    /// íÚíÏ ÌáÓÇÊ ÇáãÓÊÎÏãíä ÇáãÊÕáíä ÍÇáíğÇ.
    /// </summary>
    IReadOnlyList<IClientHandler> GetSessions();
}

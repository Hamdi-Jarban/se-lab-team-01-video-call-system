namespace VideoCall.Server.Domain;

/// <summary>
/// ãÓÄæá Úä ÇáÊÍŞŞ ãä ÕÍÉ ÇÓã ÇáãÓÊÎÏã æßáãÉ ÇáãÑæÑ.
/// </summary>
public interface ICredentialValidator
{
    /// <summary>
    /// íÊÍŞŞ ãä ÈíÇäÇÊ ÏÎæá ÇáãÓÊÎÏã.
    /// </summary>
    /// <param name="username">ÇÓã ÇáãÓÊÎÏã.</param>
    /// <param name="password">ßáãÉ ÇáãÑæÑ.</param>
    /// <returns>
    /// true ÅĞÇ ßÇäÊ ÇáÈíÇäÇÊ ÕÍíÍÉ¡ æÅáÇ false.
    /// </returns>
    bool Validate(string username, string password);
}

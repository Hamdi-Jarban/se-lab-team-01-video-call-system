using VideoCall.Server.Domain;

namespace VideoCall.Server.Infrastructure.Security;

/// <summary>
/// ÎÏãÉ ÊÍŞŞ ãÎÕÕÉ áÈíÆÉ ÇáÊØæíÑ æÇáÇÎÊÈÇÑ İŞØ.
/// áÇ ÊÓÊÎÏã åĞå ÇáÎÏãÉ İí ÈíÆÉ ÇáÅäÊÇÌ áÃäåÇ ÊÚÊãÏ Úáì ßáãÇÊ ãÑæÑ
/// ãÍİæÙÉ ßäÕ ãÈÇÔÑ ÏÇÎá ÇáĞÇßÑÉ.
///
/// ÚäÏ ÇáÇäÊŞÇá Åáì ÇáÅäÊÇÌ¡ íÌÈ ÇÓÊÈÏÇáåÇ ÈÊäİíĞ íÚÊãÏ Úáì ŞÇÚÏÉ ÈíÇäÇÊ
/// æÊÎÒíä ßáãÇÊ ÇáãÑæÑ ÈÇÓÊÎÏÇã Hash Âãä. æÈãÇ Ãä ÈŞíÉ ÇáäÙÇã íÚÊãÏ Úáì
/// ICredentialValidator¡ İÅä ÇÓÊÈÏÇá ÂáíÉ ÇáÊÍŞŞ áÇ íÊØáÈ ÊÚÏíá ãäØŞ ÇáÊØÈíŞ.
/// </summary>
public sealed class DevelopmentCredentialValidator : ICredentialValidator
{
    // íÍÊæí Úáì ÍÓÇÈÇÊ ÇáÊØæíÑ ÇáãÓãæÍ ÈÇÓÊÎÏÇãåÇ ÃËäÇÁ ÊÔÛíá ÇáÎÇÏã.
    private readonly IReadOnlyDictionary<string, string> _accounts;

    /// <summary>
    /// íäÔÆ ÎÏãÉ ÇáÊÍŞŞ ÈÇÓÊÎÏÇã ÍÓÇÈÇÊ ÇáÊØæíÑ.
    /// </summary>
    /// <param name="accounts">ŞÇãæÓ ÃÓãÇÁ ÇáãÓÊÎÏãíä æßáãÇÊ ÇáãÑæÑ.</param>
    public DevelopmentCredentialValidator(
        IReadOnlyDictionary<string, string> accounts)
    {
        // äÓÎ ÇáÈíÇäÇÊ ãÚ ÊÌÇåá ÍÇáÉ ÇáÃÍÑİ İí ÃÓãÇÁ ÇáãÓÊÎÏãíä.
        _accounts = new Dictionary<string, string>(
            accounts,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// íÊÍŞŞ ãä ÊØÇÈŞ ÇÓã ÇáãÓÊÎÏã æßáãÉ ÇáãÑæÑ.
    /// </summary>
    /// <param name="username">ÇÓã ÇáãÓÊÎÏã.</param>
    /// <param name="password">ßáãÉ ÇáãÑæÑ.</param>
    /// <returns>
    /// true ÅĞÇ ßÇäÊ ÈíÇäÇÊ ÇáÏÎæá ÕÍíÍÉ¡ æÅáÇ false.
    /// </returns>
    public bool Validate(
        string username,
        string password) =>
        !string.IsNullOrWhiteSpace(username)
        && _accounts.TryGetValue(
            username.Trim(),
            out var expectedPassword)
        && string.Equals(
            expectedPassword,
            password,
            StringComparison.Ordinal);
}

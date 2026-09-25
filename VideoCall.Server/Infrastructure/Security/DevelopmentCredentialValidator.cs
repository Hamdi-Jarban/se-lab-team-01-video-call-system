using VideoCall.Server.Domain;

namespace VideoCall.Server.Infrastructure.Security;

/// <summary>
/// Development-only validator. Do not use in production. Replace it with a
/// database-backed password-hash validator before deploying outside a lab
/// LAN - because callers depend only on <see cref="ICredentialValidator"/>
/// (Dependency Inversion Principle), that replacement is a one-file change:
/// implement the interface and change one registration line in
/// <c>Program.cs</c>.
/// </summary>
public sealed class DevelopmentCredentialValidator : ICredentialValidator
{
    private readonly IReadOnlyDictionary<string, string> _accounts;

    public DevelopmentCredentialValidator(IReadOnlyDictionary<string, string> accounts)
    {
        _accounts = new Dictionary<string, string>(accounts, StringComparer.OrdinalIgnoreCase);
    }

    public bool Validate(string username, string password) =>
        !string.IsNullOrWhiteSpace(username) &&
        _accounts.TryGetValue(username.Trim(), out var expected) &&
        string.Equals(expected, password, StringComparison.Ordinal);
}
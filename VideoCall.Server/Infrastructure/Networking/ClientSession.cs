public Guid SessionToken { get; } = Guid.NewGuid();
public string? Username { get; private set; }
public bool IsAuthenticated => Username is not null;

// ... (æåĞå ÇáÏÇáÉ ÃíÖÇğ ÊÇÈÚÉ áãåãÊß) ...

public void SetAuthenticatedUsername(string username)
{
    if (string.IsNullOrWhiteSpace(username))
        throw new ArgumentException("Username is required.", nameof(username));
    Username = username.Trim();
}
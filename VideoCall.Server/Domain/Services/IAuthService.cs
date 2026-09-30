namespace VideoCall.Server.Domain.Services;

public sealed record RegisterResult(bool Success, string? ErrorCode, string? Username)
{
    public static RegisterResult Ok(string username) => new(true, null, username);

    public static RegisterResult Fail(string errorCode) => new(false, errorCode, null);
}

public sealed record AuthenticationResult(bool Success, int UserId, string Username, string DisplayName)
{
    public static AuthenticationResult Failed { get; } = new(false, 0, string.Empty, string.Empty);
}

// المصادقة اعتمادًا على قاعدة البيانات
public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(string username, string password, string? displayName, CancellationToken ct);

    // يتحقق من اسم المستخدم وكلمة المرور فقط (لا ينشئ جلسة)
    Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken ct);

    // ينشئ سجل UserSessions ويحدّث LastLoginAt
    Task StartSessionAsync(Guid sessionId, int userId, CancellationToken ct);

    Task EndSessionAsync(Guid sessionId, CancellationToken ct);
}

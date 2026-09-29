using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Domain.Services;
using VideoCall.Shared.Messages;

namespace VideoCall.Server.Application;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUserSessionRepository _sessions;
    private readonly IPasswordHasher _hasher;

    // Hash وهمي يُستخدم عند غياب المستخدم حتى يتساوى زمن الاستجابة تقريبًا (تقليل تسريب وجود الحساب)
    private readonly string _dummyHash;

    public AuthService(IUserRepository users, IUserSessionRepository sessions, IPasswordHasher hasher)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _dummyHash = _hasher.HashPassword(Guid.NewGuid().ToString("N"));
    }

    public async Task<RegisterResult> RegisterAsync(string username, string password, string? displayName, CancellationToken ct)
    {
        var usernameError = InputValidation.ValidateUsername(username);
        if (usernameError is not null) return RegisterResult.Fail(usernameError);

        var passwordError = InputValidation.ValidatePassword(password);
        if (passwordError is not null) return RegisterResult.Fail(passwordError);

        var name = username.Trim();
        var display = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        if (display.Length > InputValidation.MaxDisplayNameLength)
            display = display[..InputValidation.MaxDisplayNameLength];

        if (await _users.FindByUsernameAsync(name, ct) is not null)
            return RegisterResult.Fail(ErrorCodes.UsernameAlreadyExists);

        var hash = _hasher.HashPassword(password);

        // الـ Unique Constraint هو الحكم النهائي عند تسجيلين متزامنين بنفس الاسم
        var userId = await _users.CreateAsync(name, hash, display, ct);
        return userId is null
            ? RegisterResult.Fail(ErrorCodes.UsernameAlreadyExists)
            : RegisterResult.Ok(name);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        var user = await _users.FindByUsernameAsync(username.Trim(), ct);
        if (user is null)
        {
            _hasher.VerifyPassword(password, _dummyHash);
            return AuthenticationResult.Failed;
        }

        if (!user.IsActive || !_hasher.VerifyPassword(password, user.PasswordHash))
            return AuthenticationResult.Failed;

        return new AuthenticationResult(true, user.UserId, user.Username, user.DisplayName);
    }

    public async Task StartSessionAsync(Guid sessionId, int userId, CancellationToken ct)
    {
        await _sessions.CreateAsync(sessionId, userId, ct);
        await _users.UpdateLastLoginAsync(userId, ct);
    }

    public Task EndSessionAsync(Guid sessionId, CancellationToken ct) =>
        _sessions.EndAsync(sessionId, ct);
}

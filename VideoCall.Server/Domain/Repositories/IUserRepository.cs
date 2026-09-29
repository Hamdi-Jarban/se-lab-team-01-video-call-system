namespace VideoCall.Server.Domain.Repositories;

// الوصول إلى جدول Users
public interface IUserRepository
{
    Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct);

    Task<UserRecord?> FindByIdAsync(int userId, CancellationToken ct);

    // يعيد null إذا كان اسم المستخدم موجودًا مسبقًا (Unique Constraint)
    Task<int?> CreateAsync(string username, string passwordHash, string displayName, CancellationToken ct);

    Task UpdateLastLoginAsync(int userId, CancellationToken ct);

    Task<IReadOnlyList<UserRecord>> FindByUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct);
}

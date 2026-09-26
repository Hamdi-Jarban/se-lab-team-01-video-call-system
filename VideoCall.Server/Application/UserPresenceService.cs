using System.Collections.Concurrent;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Repositories;

namespace VideoCall.Server.Application;

/// <summary>
/// يدير المستخدمين المتصلين ويربط كل مستخدم بجلسة الاتصال الخاصة به.
/// </summary>
public sealed class UserPresenceService : IUserPresenceRepository
{
    // قاموس آمن للاستخدام مع عدة جلسات في الوقت نفسه.
    private readonly ConcurrentDictionary<string, IClientHandler> _online =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// يضيف مستخدمًا إلى قائمة المتصلين.
    /// </summary>
    public bool TryAdd(string username, IClientHandler session)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        return _online.TryAdd(username.Trim(), session);
    }

    /// <summary>
    /// يبحث عن جلسة المستخدم.
    /// </summary>
    public bool TryGet(string username, out IClientHandler session) =>
        _online.TryGetValue(username, out session!);

    /// <summary>
    /// يتحقق من اتصال المستخدم حاليًا.
    /// </summary>
    public bool IsOnline(string username) =>
        !string.IsNullOrWhiteSpace(username) &&
        _online.ContainsKey(username.Trim());

    /// <summary>
    /// يزيل المستخدم إذا كانت الجلسة الحالية مطابقة للجلسة المتوقعة.
    /// </summary>
    public bool Remove(string username, IClientHandler expectedSession)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        if (!_online.TryGetValue(username.Trim(), out var current) ||
            !ReferenceEquals(current, expectedSession))
        {
            return false;
        }

        return _online.TryRemove(username.Trim(), out _);
    }

    /// <summary>
    /// يعيد أسماء المستخدمين المتصلين مرتبة أبجديًا.
    /// </summary>
    public IReadOnlyList<string> GetUsernames() =>
        _online.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// يعيد جلسات المستخدمين المتصلين.
    /// </summary>
    public IReadOnlyList<IClientHandler> GetSessions() =>
        _online.Values.ToArray();
}

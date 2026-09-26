using System.Collections.Concurrent;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Repositories;

namespace VideoCall.Server.Infrastructure.Repositories;

/// <summary>
/// مستودع الذاكرة المؤقتة لإدارة جلسات المستخدمين المتصلين.
/// يستخدم ConcurrentDictionary لضمان أمان العمليات في بيئة متعددة المسارات (Thread-Safe).
/// </summary>
public class InMemoryUserPresenceRepository : IUserPresenceRepository
{
    // يتجاهل حالة الأحرف في أسماء المستخدمين (A = a)
    private readonly ConcurrentDictionary<string, IClientHandler> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(string username, IClientHandler session)
    {
        if (string.IsNullOrWhiteSpace(username) || session == null)
            return false;
        return _sessions.TryAdd(username, session);
    }

    public bool TryGet(string username, out IClientHandler session)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            session = null!;
            return false;
        }

        return _sessions.TryGetValue(username, out session!);
    }

    public bool IsOnline(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return _sessions.ContainsKey(username);
    }

    public bool Remove(string username, IClientHandler expectedSession)
    {
        if (string.IsNullOrWhiteSpace(username) || expectedSession == null)
            return false;

        // إزالة الجلسة المشروطة: تمنع حذف الجلسة الجديدة إذا قام المستخدم بتسجيل الدخول مرة أخرى قبل حذف الجلسة القديمة
        var pair = new KeyValuePair<string, IClientHandler>(username, expectedSession);
        return ((ICollection<KeyValuePair<string, IClientHandler>>)_sessions).Remove(pair);
    }

    public IReadOnlyList<string> GetUsernames()
    {
        return _sessions.Keys.ToList();
    }

    public IReadOnlyList<IClientHandler> GetSessions()
    {
        return _sessions.Values.ToList();
    }
}
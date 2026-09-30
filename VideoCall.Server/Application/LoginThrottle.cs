using System.Collections.Concurrent;

namespace VideoCall.Server.Application;

// حماية بسيطة من تخمين كلمات المرور: بعد عدة محاولات فاشلة لنفس الاسم يُقفل الدخول لفترة قصيرة.
// (الحالة في الذاكرة فقط؛ تُصفَّر عند إعادة تشغيل الخادم.)
public sealed class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);

    private sealed class Entry
    {
        public int Failures;
        public DateTime LockedUntilUtc;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public bool IsLocked(string username)
    {
        if (!_entries.TryGetValue(username, out var entry)) return false;
        lock (entry) return entry.LockedUntilUtc > DateTime.UtcNow;
    }

    public void RegisterFailure(string username)
    {
        var entry = _entries.GetOrAdd(username, _ => new Entry());
        lock (entry)
        {
            if (entry.LockedUntilUtc > DateTime.UtcNow) return;

            entry.Failures++;
            if (entry.Failures >= MaxFailures)
            {
                entry.Failures = 0;
                entry.LockedUntilUtc = DateTime.UtcNow + LockDuration;
            }
        }
    }

    public void Reset(string username) => _entries.TryRemove(username, out _);
}

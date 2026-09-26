using System.Collections.Concurrent;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Repositories;

namespace VideoCall.Server.Application;

/// <summary>
/// íÏíÑ ÇáãÓÊÎÏãíä ÇáãÊÕáíä æíÑÈØ ßá ãÓÊÎÏã ÈÌáÓÉ ÇáÇÊÕÇá ÇáÎÇÕÉ Èå.
/// </summary>
public sealed class UserPresenceService : IUserPresenceRepository
{
    // ŞÇãæÓ Âãä ááÇÓÊÎÏÇã ãÚ ÚÏÉ ÌáÓÇÊ İí ÇáæŞÊ äİÓå.
    private readonly ConcurrentDictionary<string, IClientHandler> _online =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// íÖíİ ãÓÊÎÏãğÇ Åáì ŞÇÆãÉ ÇáãÊÕáíä.
    /// </summary>
    public bool TryAdd(string username, IClientHandler session)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        return _online.TryAdd(username.Trim(), session);
    }

    /// <summary>
    /// íÈÍË Úä ÌáÓÉ ÇáãÓÊÎÏã.
    /// </summary>
    public bool TryGet(string username, out IClientHandler session) =>
        _online.TryGetValue(username, out session!);

    /// <summary>
    /// íÊÍŞŞ ãä ÇÊÕÇá ÇáãÓÊÎÏã ÍÇáíğÇ.
    /// </summary>
    public bool IsOnline(string username) =>
        !string.IsNullOrWhiteSpace(username) &&
        _online.ContainsKey(username.Trim());

    /// <summary>
    /// íÒíá ÇáãÓÊÎÏã ÅĞÇ ßÇäÊ ÇáÌáÓÉ ÇáÍÇáíÉ ãØÇÈŞÉ ááÌáÓÉ ÇáãÊæŞÚÉ.
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
    /// íÚíÏ ÃÓãÇÁ ÇáãÓÊÎÏãíä ÇáãÊÕáíä ãÑÊÈÉ ÃÈÌÏíğÇ.
    /// </summary>
    public IReadOnlyList<string> GetUsernames() =>
        _online.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// íÚíÏ ÌáÓÇÊ ÇáãÓÊÎÏãíä ÇáãÊÕáíä.
    /// </summary>
    public IReadOnlyList<IClientHandler> GetSessions() =>
        _online.Values.ToArray();
}

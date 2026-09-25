using VideoCall.Server.Domain;

namespace VideoCall.Server.Domain.Repositories;

/// <summary>
/// Owns "who is currently online and which session belongs to them".
/// <para>
/// Software-engineering concepts:
/// <list type="bullet">
/// <item><b>Repository Pattern</b>: gives Application- and Api-layer code a
/// collection-like view ("add", "get", "remove", "list") over online users,
/// hiding the fact that today it is a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
/// in memory. Swapping to a distributed cache (e.g. Redis) later only means
/// writing a new class that implements this interface.</item>
/// <item><b>Dependency Inversion Principle</b>: <c>ProtocolRouter</c> and
/// <c>Api.ApiServer</c> depend on this abstraction, never on the concrete
/// <c>Application.UserPresenceService</c>.</item>
/// </list>
/// </para>
/// </summary>
public interface IUserPresenceRepository
{
    bool TryAdd(string username, IClientHandler session);

    bool TryGet(string username, out IClientHandler session);

    bool IsOnline(string username);

    /// <summary>Removes <paramref name="username"/> only if it still maps to <paramref name="expectedSession"/> (guards against a stale/replaced session removing a newer one).</summary>
    bool Remove(string username, IClientHandler expectedSession);

    IReadOnlyList<string> GetUsernames();

    IReadOnlyList<IClientHandler> GetSessions();
}

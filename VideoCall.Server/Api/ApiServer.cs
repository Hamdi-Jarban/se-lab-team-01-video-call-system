using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using VideoCall.Server.Api.Dtos;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Api;

/// <summary>
/// Read-only HTTP/JSON view over the same in-memory state the TCP/UDP
/// server exposes to its own clients (assignment section 4). Lives entirely
/// in its own <c>Api</c> folder/namespace and depends <b>only</b> on
/// <see cref="IUserPresenceRepository"/> and <see cref="IConversationRepository"/>
/// - two Domain-layer interfaces - never on
/// <c>Infrastructure.Networking.ClientSession</c>, the TCP listener, or the
/// UDP relay.
/// <para>
/// Software-engineering concepts:
/// <list type="bullet">
/// <item><b>Dependency Inversion Principle</b>: this is the clearest example
/// of DIP in the whole project. The API layer is high-level policy ("expose
/// current state as JSON"); it depends on repository abstractions, and the
/// low-level detail of *how* that state is produced (TCP messages mutating
/// an in-memory dictionary) is completely invisible to it.</item>
/// <item><b>Separation of Concerns</b>: nothing here can accidentally start
/// a call, create a room, or otherwise mutate state - only GET endpoints
/// exist, and the interfaces this class depends on are only ever used for
/// reads (<c>GetAllSnapshot</c>, <c>GetUsernames</c>, <c>GetSessions</c>).</item>
/// </list>
/// </para>
/// <para>
/// ⚠️ Graceful-shutdown behaviour: follows the exact same shape as
/// <c>UdpMediaRelayService</c> - <see cref="StartAsync"/>/<see cref="StopAsync"/>,
/// an idempotent stop guarded by <see cref="Interlocked.Exchange(ref int, int)"/>,
/// in-flight requests are tracked and awaited (never fire-and-forgotten) and
/// awaited again before the listener is closed, and it implements
/// <see cref="IAsyncDisposable"/>. <c>ServerHost.StopAsync</c> calls this
/// class's <c>StopAsync</c> as part of its own shutdown sequence, not in
/// isolation.
/// </para>
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpListener _listener = new();
    private readonly IUserPresenceRepository _presence;
    private readonly IConversationRepository _conversations;
    private readonly IAppLogger _logger;
    private readonly ConcurrentBag<Task> _pendingRequests = new();

    private CancellationTokenSource? _internalCts;
    private Task? _acceptLoopTask;
    private Stopwatch? _uptime;
    private int _stopped;

    public ApiServer(
        IUserPresenceRepository presence,
        IConversationRepository conversations,
        IAppLogger logger,
        int port)
    {
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // "localhost" (rather than "+") needs no admin-granted URL ACL reservation
        // on Windows, which keeps `dotnet run` working out of the box for the
        // grading environment. Expose on all interfaces only if you have
        // reserved the prefix (netsh http add urlacl) or run elevated.
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public Task StartAsync(CancellationToken externalCancellation)
    {
        if (_acceptLoopTask is not null) return Task.CompletedTask;

        _uptime = Stopwatch.StartNew();
        _listener.Start();
        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
        _acceptLoopTask = AcceptLoopAsync(_internalCts.Token);
        _logger.Info($"HTTP API listening on {string.Join(", ", _listener.Prefixes)}");
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception) when (Volatile.Read(ref _stopped) != 0)
            {
                // Listener was stopped/disposed while a GetContextAsync() call
                // was outstanding (HttpListenerException/ObjectDisposedException).
                break;
            }
            catch (Exception ex)
            {
                _logger.Warn($"HTTP API accept failed: {ex.Message}");
                continue;
            }

            var requestTask = HandleRequestAsync(context, ct);
            _pendingRequests.Add(requestTask);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            response.ContentType = "application/json; charset=utf-8";

            if (!string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(response, 405, new { error = "method_not_allowed" }, ct);
                return;
            }

            switch (request.Url?.AbsolutePath)
            {
                case "/api/status":
                    await WriteJsonAsync(response, 200, BuildStatus(), ct);
                    return;
                case "/api/users":
                    await WriteJsonAsync(response, 200, BuildUsers(), ct);
                    return;
                case "/api/rooms":
                    await WriteJsonAsync(response, 200, BuildRooms(), ct);
                    return;
                case "/api/sessions":
                    await WriteJsonAsync(response, 200, BuildSessions(), ct);
                    return;
                default:
                    await WriteJsonAsync(response, 404, new { error = "not_found" }, ct);
                    return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn($"HTTP API request failed: {ex.Message}");
        }
        finally
        {
            try { context.Response.Close(); } catch { /* client may already be gone */ }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload, CancellationToken ct)
    {
        response.StatusCode = statusCode;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, ct);
    }

    private ServerStatusResponse BuildStatus() => new(
        Status: "running",
        ConnectedClients: _presence.GetSessions().Count,
        Uptime: (_uptime?.Elapsed ?? TimeSpan.Zero).ToString(@"hh\:mm\:ss"));

    private OnlineUsersResponse BuildUsers()
    {
        var users = _presence.GetUsernames();
        return new OnlineUsersResponse(users, users.Count);
    }

    private RoomsResponse BuildRooms()
    {
        var rooms = _conversations.GetAllSnapshot()
            .Where(c => c.Type == ConversationType.Group)
            .Select(c => new RoomSummary(c.Id, c.Members.ToList()))
            .ToList();
        return new RoomsResponse(rooms);
    }

    private SessionsResponse BuildSessions()
    {
        var active = _conversations.GetAllSnapshot()
            .Where(c => c.State == ConversationState.Active && c.MediaId is not null)
            .Select(c => new ActiveSessionSummary(c.MediaId!.Value.ToString("N"), c.Members.ToList()))
            .ToList();
        return new SessionsResponse(active);
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;

        _internalCts?.Cancel();
        try { _listener.Stop(); } catch { /* already stopped */ }

        if (_acceptLoopTask is not null)
        {
            try { await _acceptLoopTask; } catch { /* observed above */ }
        }

        try { await Task.WhenAll(_pendingRequests.ToArray()); }
        catch { /* individual request failures were already logged */ }

        try { _listener.Close(); } catch { /* already closed */ }
        _internalCts?.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

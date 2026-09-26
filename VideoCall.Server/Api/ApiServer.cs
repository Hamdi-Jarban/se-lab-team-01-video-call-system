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
/// ط®ط§ط¯ظ… HTTP ظ„ظ„ظ‚ط±ط§ط،ط© ظپظ‚ط· ظٹط¹ط±ط¶ ط­ط§ظ„ط© ط§ظ„ط®ط§ط¯ظ… ط§ظ„ط­ط§ظ„ظٹط© ط¨طµظٹط؛ط© JSON.
///
/// ظٹط¹طھظ…ط¯ ApiServer ط¹ظ„ظ‰ ظ†ظپط³ ط§ظ„ط­ط§ظ„ط© ط§ظ„ظ…ظˆط¬ظˆط¯ط© ظپظٹ ط®ط§ط¯ظ… TCP ظˆUDPطŒ ظ„ظƒظ†ظ‡ ظ„ط§ ظٹظ…ظ„ظƒ
/// طµظ„ط§ط­ظٹط© طھط¹ط¯ظٹظ„ظ‡ط§. ظٹظ‚ط±ط£ ط§ظ„ط¨ظٹط§ظ†ط§طھ ظ…ظ† IUserPresenceRepository ظˆ
/// IConversationRepository ظپظ‚ط·طŒ ظˆظ„ط§ ظٹط±طھط¨ط· ظ…ط¨ط§ط´ط±ط© ط¨ط¬ظ„ط³ط§طھ TCP ط£ظˆ ظ…ط³طھظ…ط¹ TCP
/// ط£ظˆ ط®ط¯ظ…ط© طھظ…ط±ظٹط± ط§ظ„ظˆط³ط§ط¦ط·.
///
/// ظٹظˆظپط± ط§ظ„ط®ط§ط¯ظ… ظ†ظ‚ط§ط· ظ‚ط±ط§ط،ط© ظ„ظ…ط±ط§ظ‚ط¨ط©:
/// - ط­ط§ظ„ط© ط§ظ„ط®ط§ط¯ظ… ظˆط¹ط¯ط¯ ط§ظ„ط¹ظ…ظ„ط§ط، ط§ظ„ظ…طھطµظ„ظٹظ†.
/// - ط§ظ„ظ…ط³طھط®ط¯ظ…ظٹظ† ط§ظ„ظ…طھطµظ„ظٹظ† ط­ط§ظ„ظٹظ‹ط§.
/// - ط§ظ„ط؛ط±ظپ ط§ظ„ظ…ظˆط¬ظˆط¯ط©.
/// - ط¬ظ„ط³ط§طھ ط§ظ„ظˆط³ط§ط¦ط· ط§ظ„ظ†ط´ط·ط©.
///
/// ظٹط­ظ‚ظ‚ ظ‡ط°ط§ ط§ظ„طھطµظ…ظٹظ… ظ…ط¨ط¯ط£ ط¹ظƒط³ ط§طھط¬ط§ظ‡ ط§ظ„ط§ط¹طھظ…ط§ط¯طŒ ط¥ط° طھط¹طھظ…ط¯ ط·ط¨ظ‚ط© API ط¹ظ„ظ‰ ط¹ظ‚ظˆط¯
/// Domain ط¨ط¯ظ„ ظ…ط¹ط±ظپط© طھظپط§طµظٹظ„ طھط®ط²ظٹظ† ط§ظ„ط­ط§ظ„ط© ط£ظˆ ط·ط±ظٹظ‚ط© طھط­ط¯ظٹط«ظ‡ط§.
/// ظƒظ…ط§ ظٹط­ظ‚ظ‚ ظ…ط¨ط¯ط£ ظپطµظ„ ط§ظ„ظ…ط³ط¤ظˆظ„ظٹط§طھطŒ ظ„ط£ظ† ط¬ظ…ظٹط¹ endpoints ظ‡ظ†ط§ ظ„ظ„ظ‚ط±ط§ط،ط© ظپظ‚ط· ظˆظ„ط§
/// ظٹظ…ظƒظ†ظ‡ط§ ط¥ظ†ط´ط§ط، ط؛ط±ظپط© ط£ظˆ ط¨ط¯ط، ظ…ظƒط§ظ„ظ…ط© ط£ظˆ طھط¹ط¯ظٹظ„ ط­ط§ظ„ط© ط§ظ„ظ†ط¸ط§ظ….
///
/// طھطھظ… ط¥ط¯ط§ط±ط© ط§ظ„ط¥ظٹظ‚ط§ظپ ط¨ط·ط±ظٹظ‚ط© ظ…ظ†ط¸ظ…ط©ط› ط­ظٹط« ظٹطھظ… ط¥ظ„ط؛ط§ط، ط­ظ„ظ‚ط© ط§ظ„ط§ط³طھظ‚ط¨ط§ظ„طŒ ط«ظ… ط§ظ†طھط¸ط§ط±
/// ط§ظ„ط·ظ„ط¨ط§طھ ط§ظ„ط¬ط§ط±ظٹط©طŒ ظˆط¨ط¹ط¯ ط°ظ„ظƒ ط¥ط؛ظ„ط§ظ‚ ظ…ط³طھظ…ط¹ HTTP ظˆطھط­ط±ظٹط± ط§ظ„ظ…ظˆط§ط±ط¯.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    // ط¥ط¹ط¯ط§ط¯ط§طھ JSON ط§ظ„ظ…ظˆط­ط¯ط© ظ„ط¬ظ…ظٹط¹ ط§ط³طھط¬ط§ط¨ط§طھ HTTP.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // ظ…ط³طھظ…ط¹ HTTP ط§ظ„ظ…ط³ط¤ظˆظ„ ط¹ظ† ط§ط³طھظ‚ط¨ط§ظ„ ط§ظ„ط·ظ„ط¨ط§طھ.
    private readonly HttpListener _listener = new();

    // ظ…ط³طھظˆط¯ط¹ ط§ظ„ظ…ط³طھط®ط¯ظ…ظٹظ† ط§ظ„ظ…طھطµظ„ظٹظ†.
    private readonly IUserPresenceRepository _presence;

    // ظ…ط³طھظˆط¯ط¹ ط§ظ„ظ…ط­ط§ط¯ط«ط§طھ ظˆط§ظ„ط؛ط±ظپ.
    private readonly IConversationRepository _conversations;

    // ط®ط¯ظ…ط© طھط³ط¬ظٹظ„ ط£ط­ط¯ط§ط« ظˆط£ط®ط·ط§ط، API.
    private readonly IAppLogger _logger;

    // ط§ظ„ظ…ظ‡ط§ظ… ط§ظ„ط®ط§طµط© ط¨ط§ظ„ط·ظ„ط¨ط§طھ ط§ظ„طھظٹ ط¨ط¯ط£طھ ظˆظ„ظ… طھظ†طھظ‡ ط¨ط¹ط¯.
    // ظٹطھظ… ط§ظ†طھط¸ط§ط±ظ‡ط§ ط£ط«ظ†ط§ط، ط§ظ„ط¥ظٹظ‚ط§ظپ ط§ظ„ظ…ظ†ط¸ظ….
    private readonly ConcurrentBag<Task> _pendingRequests = new();

    // ظ…طµط¯ط± ط§ظ„ط¥ظ„ط؛ط§ط، ط§ظ„ط¯ط§ط®ظ„ظٹ ط§ظ„ظ…ط±طھط¨ط· ط¨ط¥ظ„ط؛ط§ط، ط§ظ„طھط·ط¨ظٹظ‚ ط§ظ„ط®ط§ط±ط¬ظٹ.
    private CancellationTokenSource? _internalCts;

    // ظ…ظ‡ظ…ط© ط­ظ„ظ‚ط© ط§ط³طھظ‚ط¨ط§ظ„ ط·ظ„ط¨ط§طھ HTTP.
    private Task? _acceptLoopTask;

    // ظ…ط¤ظ‚طھ ظ‚ظٹط§ط³ ظ…ط¯ط© طھط´ط؛ظٹظ„ API.
    private Stopwatch? _uptime;

    // ط¹ظ„ط§ظ…ط© ط°ط±ظٹط© طھظ…ظ†ط¹ طھظ†ظپظٹط° ط§ظ„ط¥ظٹظ‚ط§ظپ ط£ظƒط«ط± ظ…ظ† ظ…ط±ط©.
    private int _stopped;

    /// <summary>
    /// ظٹظ†ط´ط¦ ط®ط§ط¯ظ… API ظˆظٹط±ط¨ط·ظ‡ ط¨ظ…ط³طھظˆط¯ط¹ط§طھ ط§ظ„ط­ط§ظ„ط© ظˆط®ط¯ظ…ط© ط§ظ„طھط³ط¬ظٹظ„.
    /// </summary>
    /// <param name="presence">ظ…ط³طھظˆط¯ط¹ ط§ظ„ظ…ط³طھط®ط¯ظ…ظٹظ† ط§ظ„ظ…طھطµظ„ظٹظ†.</param>
    /// <param name="conversations">ظ…ط³طھظˆط¯ط¹ ط§ظ„ظ…ط­ط§ط¯ط«ط§طھ ظˆط§ظ„ط؛ط±ظپ.</param>
    /// <param name="logger">ط®ط¯ظ…ط© طھط³ط¬ظٹظ„ ط§ظ„ط£ط­ط¯ط§ط« ظˆط§ظ„ط£ط®ط·ط§ط،.</param>
    /// <param name="port">ظ…ظ†ظپط° HTTP ط§ظ„ط°ظٹ ط³ظٹط³طھظ…ط¹ ط¹ظ„ظٹظ‡ ط§ظ„ط®ط§ط¯ظ….</param>
    public ApiServer(IUserPresenceRepository presence, IConversationRepository conversations, IAppLogger logger, int port)
    {
        _presence = presence
            ?? throw new ArgumentNullException(nameof(presence));

        _conversations = conversations
            ?? throw new ArgumentNullException(nameof(conversations));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        // ط§ط³طھط®ط¯ط§ظ… localhost ظٹظ‚ظ„ظ„ ظ…طھط·ظ„ط¨ط§طھ URL ACL ط¹ظ„ظ‰ Windows.
        // ظ„ط§ ظٹطھظ… ظپطھط­ ط§ظ„ط®ط¯ظ…ط© ط¹ظ„ظ‰ ط¬ظ…ظٹط¹ ط§ظ„ظˆط§ط¬ظ‡ط§طھ ط¥ظ„ط§ ط¨ط¹ط¯ ط¥ط¹ط¯ط§ط¯ ط§ظ„طµظ„ط§ط­ظٹط§طھ
        // ط§ظ„ظ…ظ†ط§ط³ط¨ط© ظˆطھط£ظ…ظٹظ† ظ†ظ‚ط·ط© ط§ظ„ظ†ظ‡ط§ظٹط© ظپظٹ ط¨ظٹط¦ط© ط§ظ„ط¥ظ†طھط§ط¬.
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>
    /// ظٹط¨ط¯ط£ ظ…ط³طھظ…ط¹ HTTP ظˆط­ظ„ظ‚ط© ط§ط³طھظ‚ط¨ط§ظ„ ط§ظ„ط·ظ„ط¨ط§طھ.
    /// </summary>
    /// <param name="externalCancellation">
    /// ط±ظ…ط² ط§ظ„ط¥ظ„ط؛ط§ط، ط§ظ„ظ‚ط§ط¯ظ… ظ…ظ† ط¯ظˆط±ط© طھط´ط؛ظٹظ„ ط§ظ„طھط·ط¨ظٹظ‚.
    /// </param>
    public Task StartAsync(CancellationToken externalCancellation)
    {
        // ظ…ظ†ط¹ ط¨ط¯ط، ط§ظ„ط®ط¯ظ…ط© ط£ظƒط«ط± ظ…ظ† ظ…ط±ط©.
        if (_acceptLoopTask is not null)
        {
            return Task.CompletedTask;
        }

        _uptime = Stopwatch.StartNew();
        _listener.Start();

        // ط±ط¨ط· ط¯ظˆط±ط© ط­ظٹط§ط© API ط¨ط¯ظˆط±ط© ط­ظٹط§ط© ط§ظ„طھط·ط¨ظٹظ‚ ط§ظ„ط±ط¦ظٹط³ظٹط©.
        _internalCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellation);

        _acceptLoopTask = AcceptLoopAsync(_internalCts.Token);

        _logger.Info(
            $"HTTP API listening on {string.Join(", ", _listener.Prefixes)}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// ط­ظ„ظ‚ط© ط§ط³طھظ‚ط¨ط§ظ„ ط·ظ„ط¨ط§طھ HTTP ظˆطھظˆط²ظٹط¹ ظƒظ„ ط·ظ„ط¨ ط¹ظ„ظ‰ ظ…ظ‡ظ…ط© ظ…ط³طھظ‚ظ„ط©.
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                // ط§ظ†طھط¸ط§ط± ط·ظ„ط¨ ط¬ط¯ظٹط¯ ظ…ط¹ ط¯ط¹ظ… ط§ظ„ط¥ظ„ط؛ط§ط،.
                context = await _listener
                    .GetContextAsync()
                    .WaitAsync(ct);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                // ط¥ظٹظ‚ط§ظپ ط·ط¨ظٹط¹ظٹ ظ†طھظٹط¬ط© ط¥ظ„ط؛ط§ط، ط§ظ„ط®ط¯ظ…ط©.
                break;
            }
            catch (Exception)
                when (Volatile.Read(ref _stopped) != 0)
            {
                // طھظ… ط¥ط؛ظ„ط§ظ‚ ط§ظ„ظ…ط³طھظ…ط¹ ط£ط«ظ†ط§ط، ط§ظ†طھط¸ط§ط± ط·ظ„ط¨ ط¬ط¯ظٹط¯.
                break;
            }
            catch (Exception ex)
            {
                _logger.Warn(
                    $"HTTP API accept failed: {ex.Message}");
                continue;
            }

            // طھط´ط؛ظٹظ„ ظ…ط¹ط§ظ„ط¬ط© ط§ظ„ط·ظ„ط¨ ظ…ط¹ ط§ظ„ط§ط­طھظپط§ط¸ ط¨ط§ظ„ظ…ظ‡ظ…ط© ظ„ظ„ط§ظ†طھط¸ط§ط± ط£ط«ظ†ط§ط، ط§ظ„ط¥ظٹظ‚ط§ظپ.
            var requestTask = HandleRequestAsync(context, ct);
            _pendingRequests.Add(requestTask);
        }
    }

    /// <summary>
    /// ظٹط¹ط§ظ„ط¬ ط·ظ„ط¨ HTTP ظˆط§ط­ط¯ظ‹ط§ ظˆظٹط±ط³ظ„ ط§ظ„ط§ط³طھط¬ط§ط¨ط© ط§ظ„ظ…ظ†ط§ط³ط¨ط©.
    /// </summary>
    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            response.ContentType =
                "application/json; charset=utf-8";

            // API ط§ظ„ط­ط§ظ„ظٹط© ظ„ظ„ظ‚ط±ط§ط،ط© ظپظ‚ط·طŒ ظ„ط°ظ„ظƒ ظٹطھظ… ظ‚ط¨ظˆظ„ GET ظپظ‚ط·.
            if (!string.Equals(
                    request.HttpMethod,
                    "GET",
                    StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(
                    response,
                    405,
                    new { error = "method_not_allowed" },
                    ct);
                return;
            }

            // طھظˆط¬ظٹظ‡ ط§ظ„ط·ظ„ط¨ ط­ط³ط¨ ط§ظ„ظ…ط³ط§ط± ط§ظ„ظ…ط·ظ„ظˆط¨.
            switch (request.Url?.AbsolutePath)
            {
                case "/api/status":
                    await WriteJsonAsync(
                        response,
                        200,
                        BuildStatus(),
                        ct);
                    return;

                case "/api/users":
                    await WriteJsonAsync(
                        response,
                        200,
                        BuildUsers(),
                        ct);
                    return;

                case "/api/rooms":
                    await WriteJsonAsync(
                        response,
                        200,
                        BuildRooms(),
                        ct);
                    return;

                case "/api/sessions":
                    await WriteJsonAsync(
                        response,
                        200,
                        BuildSessions(),
                        ct);
                    return;

                default:
                    await WriteJsonAsync(
                        response,
                        404,
                        new { error = "not_found" },
                        ct);
                    return;
            }
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            _logger.Warn(
                $"HTTP API request failed: {ex.Message}");
        }
        finally
        {
            // ط¥ط؛ظ„ط§ظ‚ ط§ظ„ط§ط³طھط¬ط§ط¨ط© ط­طھظ‰ ط¹ظ†ط¯ ط§ظ†ظ‚ط·ط§ط¹ ط§ظ„ط¹ظ…ظٹظ„ ط£ظˆ ط­ط¯ظˆط« ط®ط·ط£.
            try
            {
                context.Response.Close();
            }
            catch
            {
                // ظ‚ط¯ ظٹظƒظˆظ† ط§ظ„ط¹ظ…ظٹظ„ ط£ط؛ظ„ظ‚ ط§ظ„ط§طھطµط§ظ„ ظ…ط³ط¨ظ‚ظ‹ط§.
            }
        }
    }

    /// <summary>
    /// ظٹط­ظˆظ„ payload ط¥ظ„ظ‰ JSON ظˆظٹط±ط³ظ„ظ‡ ظ…ط¹ ط±ظ…ط² ط§ظ„ط­ط§ظ„ط© ط§ظ„ظ…ط·ظ„ظˆط¨.
    /// </summary>
    private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload, CancellationToken ct)
    {
        response.StatusCode = statusCode;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            payload,
            JsonOptions);

        response.ContentLength64 = bytes.Length;

        await response.OutputStream.WriteAsync(bytes, ct);
    }

    /// <summary>
    /// ظٹط¨ظ†ظٹ ط§ط³طھط¬ط§ط¨ط© ط­ط§ظ„ط© ط§ظ„ط®ط§ط¯ظ… ظˆط¹ط¯ط¯ ط§ظ„ط¹ظ…ظ„ط§ط، ظˆظ…ط¯ط© ط§ظ„طھط´ط؛ظٹظ„.
    /// </summary>
    private ServerStatusResponse BuildStatus() => new("running", _presence.GetSessions().Count, (_uptime?.Elapsed ?? TimeSpan.Zero).ToString(@"hh\:mm\:ss"));

    /// <summary>
    /// ظٹط¨ظ†ظٹ ظ‚ط§ط¦ظ…ط© ط§ظ„ظ…ط³طھط®ط¯ظ…ظٹظ† ط§ظ„ظ…طھطµظ„ظٹظ† ط­ط§ظ„ظٹظ‹ط§.
    /// </summary>
    private OnlineUsersResponse BuildUsers()
    {
        var users = _presence.GetUsernames();
        return new OnlineUsersResponse(users, users.Count);
    }

    /// <summary>
    /// ظٹط¨ظ†ظٹ ظ…ظ„ط®طµ ط§ظ„ط؛ط±ظپ ط§ظ„ط¬ظ…ط§ط¹ظٹط© ط§ظ„ط­ط§ظ„ظٹط© ظپظ‚ط·.
    /// </summary>
    private RoomsResponse BuildRooms()
    {
        var rooms = _conversations
            .GetAllSnapshot()
            .Where(c => c.Type == ConversationType.Group)
            .Select(c => new RoomSummary(
                c.Id,
                c.Members.ToList()))
            .ToList();

        return new RoomsResponse(rooms);
    }

    /// <summary>
    /// ظٹط¨ظ†ظٹ ظ…ظ„ط®طµ ط§ظ„ظ…ط­ط§ط¯ط«ط§طھ ط§ظ„طھظٹ طھط¹ظ…ظ„ ظپظٹظ‡ط§ ط§ظ„ظˆط³ط§ط¦ط· ط­ط§ظ„ظٹظ‹ط§.
    /// </summary>
    private SessionsResponse BuildSessions()
    {
        var active = _conversations
            .GetAllSnapshot()
            .Where(c => c.State == ConversationState.Active
                && c.MediaId is not null)
            .Select(c => new ActiveSessionSummary(
                c.MediaId!.Value.ToString("N"),
                c.Members.ToList()))
            .ToList();

        return new SessionsResponse(active);
    }

    /// <summary>
    /// ظٹظˆظ‚ظپ API ط¨ط·ط±ظٹظ‚ط© ظ…ظ†ط¸ظ…ط© ظˆظٹظ†طھط¸ط± ط­ظ„ظ‚ط© ط§ظ„ط§ط³طھظ‚ط¨ط§ظ„ ظˆط§ظ„ط·ظ„ط¨ط§طھ ط§ظ„ط¬ط§ط±ظٹط©.
    /// </summary>
    public async Task StopAsync()
    {
        // ط¶ظ…ط§ظ† طھظ†ظپظٹط° ط§ظ„ط¥ظٹظ‚ط§ظپ ظ…ط±ط© ظˆط§ط­ط¯ط© ظپظ‚ط·.
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _internalCts?.Cancel();

        try
        {
            _listener.Stop();
        }
        catch
        {
            // ط§ظ„ظ…ط³طھظ…ط¹ ظ…طھظˆظ‚ظپ ظ…ط³ط¨ظ‚ظ‹ط§ ط£ظˆ طھظ… طھط­ط±ظٹط±ظ‡.
        }

        // ط§ظ†طھط¸ط§ط± ط§ظ†طھظ‡ط§ط، ط­ظ„ظ‚ط© ط§ظ„ط§ط³طھظ‚ط¨ط§ظ„.
        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask;
            }
            catch
            {
                // طھظ… طھط³ط¬ظٹظ„ ط£ط®ط·ط§ط، ط§ظ„ط§ط³طھظ‚ط¨ط§ظ„ ظپظٹ ط§ظ„ط­ظ„ظ‚ط© ظ†ظپط³ظ‡ط§.
            }
        }

        // ط§ظ†طھط¸ط§ط± ط¬ظ…ظٹط¹ ط·ظ„ط¨ط§طھ HTTP ط§ظ„طھظٹ ط¨ط¯ط£طھ ظ‚ط¨ظ„ ط§ظ„ط¥ظٹظ‚ط§ظپ.
        try
        {
            await Task.WhenAll(
                _pendingRequests.ToArray());
        }
        catch
        {
            // طھظ… طھط³ط¬ظٹظ„ ط£ط®ط·ط§ط، ط§ظ„ط·ظ„ط¨ط§طھ ط£ط«ظ†ط§ط، ظ…ط¹ط§ظ„ط¬طھظ‡ط§.
        }

        try
        {
            _listener.Close();
        }
        catch
        {
            // ط§ظ„ظ…ظˆط±ط¯ ظ…ط؛ظ„ظ‚ ظ…ط³ط¨ظ‚ظ‹ط§.
        }

        _internalCts?.Dispose();
    }

    /// <summary>
    /// ظٹط­ط±ط± ظ…ظˆط§ط±ط¯ ApiServer ط¹ظ†ط¯ ط§ظ†طھظ‡ط§ط، ط¯ظˆط±ط© ط­ظٹط§طھظ‡.
    /// </summary>
    public async ValueTask DisposeAsync() => await StopAsync();
}

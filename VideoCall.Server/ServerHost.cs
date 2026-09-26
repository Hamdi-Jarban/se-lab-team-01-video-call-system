using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using VideoCall.Server.Api;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Infrastructure.Media;
using VideoCall.Server.Infrastructure.Networking;
using VideoCall.Shared.Networking;

namespace VideoCall.Server;

/// <summary>
/// ط§ظ„ظ…ط¶ظٹظپ ط§ظ„ط±ط¦ظٹط³ظٹ ظ„ط®ط§ط¯ظ… VideoCall ظˆط§ظ„ظ…ط³ط¤ظˆظ„ ط¹ظ† ط¥ط¯ط§ط±ط© ط¯ظˆط±ط© ط­ظٹط§ط© ط§ظ„ط§طھطµط§ظ„ط§طھ.
///
/// ظٹظ…ط«ظ„ ظ‡ط°ط§ ط§ظ„ظ†ظˆط¹ ظ†ظ‚ط·ط© ط§ظ„طھظ†ط³ظٹظ‚ ط¨ظٹظ† ظ…ظˆط§ط±ط¯ ط§ظ„ط¨ظ†ظٹط© ط§ظ„طھط­طھظٹط© ط§ظ„ظ…ط®طھظ„ظپط©طŒ ظˆطھط´ظ…ظ„:
/// - ظ…ط³طھظ…ط¹ ط§طھطµط§ظ„ط§طھ TCP.
/// - ط¬ظ„ط³ط§طھ ط§ظ„ط¹ظ…ظ„ط§ط، ClientSession.
/// - ط®ط¯ظ…ط© طھظ…ط±ظٹط± ط§ظ„ظˆط³ط§ط¦ط· ط¹ط¨ط± UDP.
/// - ظˆط§ط¬ظ‡ط© HTTP ط§ظ„ط®ط§طµط© ط¨ط§ظ„ظ…ط±ط§ظ‚ط¨ط© ظˆط§ظ„ظ‚ط±ط§ط،ط©.
///
/// طھظ… ط¥ط¨ظ‚ط§ط، ظ…ظ†ط·ظ‚ ط§ظ„ط£ط¹ظ…ط§ظ„ ط®ط§ط±ط¬ ظ‡ط°ط§ ط§ظ„ظ†ظˆط¹ ط¹ظ…ط¯ظ‹ط§. ظ„ط§ ظٹط­طھظˆظٹ ServerHost ط¹ظ„ظ‰ ظ…ظ†ط·ظ‚
/// طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„ ط£ظˆ ط§ظ„ظ…ظƒط§ظ„ظ…ط§طھ ط£ظˆ ط§ظ„ط؛ط±ظپط› ط¥ط° طھطھظ… ظ…ط¹ط§ظ„ط¬ط© ظ‡ط°ظ‡ ط§ظ„ط¹ظ…ظ„ظٹط§طھ ط¯ط§ط®ظ„
/// ProtocolRouter ظˆط·ط¨ظ‚ط§طھ ط§ظ„طھط·ط¨ظٹظ‚ ظˆط§ظ„ظ…ط³طھظˆط¯ط¹ط§طھ.
///
/// ظٹط·ط¨ظ‚ ط§ظ„طھطµظ…ظٹظ… ظ…ط¨ط¯ط£ ط§ظ„ظ…ط³ط¤ظˆظ„ظٹط© ط§ظ„ظˆط§ط­ط¯ط© (Single Responsibility Principle)
/// ظˆظ…ط¨ط¯ط£ ط¹ظƒط³ ط§طھط¬ط§ظ‡ ط§ظ„ط§ط¹طھظ…ط§ط¯ (Dependency Inversion Principle)طŒ ط­ظٹط« ظٹط¹طھظ…ط¯
/// ServerHost ط¹ظ„ظ‰ ظˆط§ط¬ظ‡ط§طھ Domain ط¹ظ†ط¯ ط§ظ„طھط¹ط§ظ…ظ„ ظ…ط¹ ظ…ظ†ط·ظ‚ ط§ظ„طھط·ط¨ظٹظ‚.
/// </summary>
public sealed class ServerHost : IClientSessionRegistry, IAsyncDisposable
{
    // ظ…ط³طھظ…ط¹ TCP ط§ظ„ظ…ط³ط¤ظˆظ„ ط¹ظ† ط§ط³طھظ‚ط¨ط§ظ„ ط§طھطµط§ظ„ط§طھ ط§ظ„ط¹ظ…ظ„ط§ط، ط§ظ„ط¬ط¯ط¯.
    private readonly TcpListener _tcpListener;

    // ظ…ظˆط²ط¹ ط§ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ظˆط§ط±ط¯ط© ظ…ظ† ط¬ظ„ط³ط§طھ ط§ظ„ط¹ظ…ظ„ط§ط،.
    // ظٹط³طھط®ط¯ظ… ProtocolRouter ظپظٹ ط§ظ„طھط·ط¨ظٹظ‚ ط§ظ„ظپط¹ظ„ظٹ.
    private readonly IProtocolMessageDispatcher _dispatcher;

    // ظ…ط¹ط§ظ„ط¬ طھظ†ط¸ظٹظپ ظ…ظˆط§ط±ط¯ ط§ظ„ظ…ط³طھط®ط¯ظ… ط¹ظ†ط¯ ط§ظ†طھظ‡ط§ط، ط¬ظ„ط³ط© ط§ظ„ط§طھطµط§ظ„.
    private readonly IConnectionLifecycleHandler _disconnectHandler;

    // ط®ط¯ظ…ط© طھظ…ط±ظٹط± ط­ط²ظ… ط§ظ„طµظˆطھ ظˆط§ظ„ظپظٹط¯ظٹظˆ ط¹ط¨ط± UDP.
    private readonly UdpMediaRelayService _media;

    // ط®ط§ط¯ظ… HTTP ط§ظ„ط®ط§طµ ط¨ظ…ط±ط§ظ‚ط¨ط© ط­ط§ظ„ط© ط§ظ„ط®ط§ط¯ظ… ظˆظ‚ط±ط§ط،ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ ط§ظ„ط¹ط§ظ…ط©.
    private readonly ApiServer _api;

    // ظˆط§ط¬ظ‡ط© طھط³ط¬ظٹظ„ ط§ظ„ط£ط­ط¯ط§ط« ظˆط§ظ„ط£ط®ط·ط§ط، ط§ظ„طھط´ط؛ظٹظ„ظٹط©.
    private readonly IAppLogger _logger;

    // ط³ط¬ظ„ ط§ظ„ط¬ظ„ط³ط§طھ ط§ظ„ظ…ظپطھظˆط­ط© ط­ط§ظ„ظٹظ‹ط§.
    // ظٹط³طھط®ط¯ظ… ConcurrentDictionary ظ„ط£ظ† ط§ظ„ط¥ط¶ط§ظپط© ظˆط§ظ„ط¥ط²ط§ظ„ط© ظ‚ط¯ طھط­ط¯ط« ظ…ظ† ظ…ظ‡ط§ظ… ظ…طھط¹ط¯ط¯ط©.
    private readonly ConcurrentDictionary<ClientSession, byte> _sessions = new();

    // ظ…طµط¯ط± ط¥ظ„ط؛ط§ط، ط¯ط§ط®ظ„ظٹ ط®ط§طµ ط¨ط¯ظˆط±ط© ط­ظٹط§ط© ServerHost.
    private readonly CancellationTokenSource _stop = new();

    // ط¹ظ„ط§ظ…ط© ط°ط±ظٹط© طھظ…ظ†ط¹ طھظ†ظپظٹط° StopAsync ط£ظƒط«ط± ظ…ظ† ظ…ط±ط©.
    private int _stopped;

    /// <summary>
    /// ظٹظ†ط´ط¦ ظ…ط¶ظٹظپ ط§ظ„ط®ط§ط¯ظ… ظˆظٹط­ظ‚ظ† ط¬ظ…ظٹط¹ ط§ظ„ط§ط¹طھظ…ط§ط¯ظٹط§طھ ط§ظ„ظ…ط·ظ„ظˆط¨ط© ط¹ط¨ط± Constructor Injection.
    /// </summary>
    /// <param name="dispatcher">ظ…ظˆط²ط¹ ط±ط³ط§ط¦ظ„ ط¨ط±ظˆطھظˆظƒظˆظ„ TCP.</param>
    /// <param name="disconnectHandler">ظ…ط¹ط§ظ„ط¬ طھظ†ط¸ظٹظپ ط¬ظ„ط³ط© ط§ظ„ط¹ظ…ظٹظ„ ط¨ط¹ط¯ ط§ظ„ط¥ط؛ظ„ط§ظ‚.</param>
    /// <param name="media">ط®ط¯ظ…ط© طھظ…ط±ظٹط± ط§ظ„ظˆط³ط§ط¦ط· ط¹ط¨ط± UDP.</param>
    /// <param name="api">ط®ط¯ظ…ط© HTTP ط§ظ„ط®ط§طµط© ط¨ط§ظ„ظ…ط±ط§ظ‚ط¨ط©.</param>
    /// <param name="logger">ط®ط¯ظ…ط© ط§ظ„طھط³ط¬ظٹظ„ ط§ظ„ظ…ط±ظƒط²ظٹ.</param>
    /// <param name="bindAddress">ط¹ظ†ظˆط§ظ† ط§ظ„ط´ط¨ظƒط© ط§ظ„ط°ظٹ ط³ظٹط³طھظ…ط¹ ط¹ظ„ظٹظ‡ ط§ظ„ط®ط§ط¯ظ….</param>
    /// <param name="tcpPort">ظ…ظ†ظپط° ط§طھطµط§ظ„ط§طھ ط§ظ„طھط­ظƒظ… ط¹ط¨ط± TCP.</param>
    public ServerHost(
        IProtocolMessageDispatcher dispatcher,
        IConnectionLifecycleHandler disconnectHandler,
        UdpMediaRelayService media,
        ApiServer api,
        IAppLogger logger,
        IPAddress? bindAddress = null,
        int tcpPort = NetworkConfig.TcpControlPort)
    {
        _dispatcher = dispatcher
            ?? throw new ArgumentNullException(nameof(dispatcher));

        _disconnectHandler = disconnectHandler
            ?? throw new ArgumentNullException(nameof(disconnectHandler));

        _media = media
            ?? throw new ArgumentNullException(nameof(media));

        _api = api
            ?? throw new ArgumentNullException(nameof(api));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        // ط¥ط°ط§ ظ„ظ… ظٹطھظ… طھط­ط¯ظٹط¯ ط¹ظ†ظˆط§ظ†طŒ ظٹط³طھظ…ط¹ ط§ظ„ط®ط§ط¯ظ… ط¹ظ„ظ‰ ط¬ظ…ظٹط¹ ظˆط§ط¬ظ‡ط§طھ ط§ظ„ط´ط¨ظƒط©.
        _tcpListener = new TcpListener(
            bindAddress ?? IPAddress.Any,
            tcpPort);
    }

    /// <summary>
    /// ظٹط¨ط¯ط£ ط¬ظ…ظٹط¹ ط®ط¯ظ…ط§طھ ط§ظ„ط®ط§ط¯ظ… ط«ظ… ظٹط¯ط®ظ„ ظپظٹ ط­ظ„ظ‚ط© ط§ط³طھظ‚ط¨ط§ظ„ ط§طھطµط§ظ„ط§طھ TCP.
    /// </summary>
    /// <param name="externalCancellation">
    /// ط±ظ…ط² ط§ظ„ط¥ظ„ط؛ط§ط، ط§ظ„ظ‚ط§ط¯ظ… ظ…ظ† ظ†ظ‚ط·ط© طھط´ط؛ظٹظ„ ط§ظ„طھط·ط¨ظٹظ‚طŒ ظ…ط«ظ„ Ctrl+C.
    /// </param>
    public async Task RunAsync(
        CancellationToken externalCancellation = default)
    {
        // ط¯ظ…ط¬ ط¥ظ„ط؛ط§ط، ط§ظ„طھط·ط¨ظٹظ‚ ط§ظ„ط®ط§ط±ط¬ظٹ ظ…ط¹ ط¥ظ„ط؛ط§ط، ServerHost ط§ظ„ط¯ط§ط®ظ„ظٹ.
        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellation,
                _stop.Token);

        var ct = linked.Token;

        // ط¨ط¯ط، ظ…ط³طھظ…ط¹ TCP ظ‚ط¨ظ„ ط§ط³طھظ‚ط¨ط§ظ„ ط£ظٹ ط¹ظ…ظٹظ„.
        _tcpListener.Start();

        _logger.Info(
            $"TCP listening on {((IPEndPoint)_tcpListener.LocalEndpoint).Port}.");

        // ط¨ط¯ط، ط®ط¯ظ…ط© طھظ…ط±ظٹط± ط§ظ„ظˆط³ط§ط¦ط· ظˆط®ط¯ظ…ط© HTTP ظ‚ط¨ظ„ ظ‚ط¨ظˆظ„ ط¬ظ„ط³ط§طھ ط§ظ„ط¹ظ…ظ„ط§ط،.
        var mediaTask = _media.RunAsync(ct);
        await _api.StartAsync(ct);

        // ط§ظ„ط§ط­طھظپط§ط¸ ط¨ظ…ظ‡ط§ظ… ط§ظ„ط¬ظ„ط³ط§طھ ط­طھظ‰ ظٹطھظ… ط§ظ†طھط¸ط§ط±ظ‡ط§ ط¹ظ†ط¯ ط¥ظٹظ‚ط§ظپ ط§ظ„ط®ط§ط¯ظ….
        var sessionTasks = new ConcurrentBag<Task>();

        try
        {
            // ط§ط³طھظ…ط±ط§ط± ظ‚ط¨ظˆظ„ ط§ظ„ط§طھطµط§ظ„ط§طھ ط­طھظ‰ ط·ظ„ط¨ ط§ظ„ط¥ظ„ط؛ط§ط،.
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    // ط§ظ†طھط¸ط§ط± ط§طھطµط§ظ„ TCP ط¬ط¯ظٹط¯ ظ…ط¹ ط¯ط¹ظ… ط§ظ„ط¥ظ„ط؛ط§ط،.
                    client = await _tcpListener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    // ط¥ظٹظ‚ط§ظپ ط·ط¨ظٹط¹ظٹ ظ†طھظٹط¬ط© ط¥ظ„ط؛ط§ط، ط¯ظˆط±ط© ط§ظ„طھط´ط؛ظٹظ„.
                    break;
                }
                catch (ObjectDisposedException)
                    when (ct.IsCancellationRequested)
                {
                    // طھظ… ط¥ط؛ظ„ط§ظ‚ ط§ظ„ظ…ط³طھظ…ط¹ ط£ط«ظ†ط§ط، ط¹ظ…ظ„ظٹط© ط§ظ„ط¥ظٹظ‚ط§ظپ.
                    break;
                }

                // ط¥ظ†ط´ط§ط، ط¬ظ„ط³ط© ظ…ط³طھظ‚ظ„ط© ظ„ظƒظ„ ط¹ظ…ظٹظ„.
                var session = new ClientSession(
                    client,
                    _dispatcher,
                    this,
                    _logger);

                // طھط³ط¬ظٹظ„ ط§ظ„ط¬ظ„ط³ط© ظ‚ط¨ظ„ ط¨ط¯ط، ظ…ظ‡ظ…طھظ‡ط§ ظ„ط¶ظ…ط§ظ† ط¥ظ…ظƒط§ظ†ظٹط© طھظ†ط¸ظٹظپظ‡ط§ ظ„ط§ط­ظ‚ظ‹ط§.
                if (!_sessions.TryAdd(session, 0))
                {
                    await session.CloseAsync();
                    continue;
                }

                _logger.Info(
                    $"TCP client connected from {client.Client.RemoteEndPoint}.");

                // ط¨ط¯ط، ط¯ظˆط±ط© ظ‚ط±ط§ط،ط© ط§ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ط®ط§طµط© ط¨ط§ظ„ط¹ظ…ظٹظ„.
                var task = session.RunAsync(ct);
                sessionTasks.Add(task);

                // ظ…ط±ط§ظ‚ط¨ط© ط§ظ„ط¬ظ„ط³ط© ظˆطھط³ط¬ظٹظ„ ط£ظٹ ط§ط³طھط«ظ†ط§ط، ط؛ظٹط± ظ…طھظˆظ‚ط¹.
                _ = ObserveSessionAsync(task);
            }
        }
        finally
        {
            // ط¥ظٹظ‚ط§ظپ ط§ط³طھظ‚ط¨ط§ظ„ ط§ظ„ط§طھطµط§ظ„ط§طھ ط§ظ„ط¬ط¯ظٹط¯ط© ط£ظˆظ„ظ‹ط§.
            _tcpListener.Stop();

            // ط¥ظٹظ‚ط§ظپ ط§ظ„ط®ط¯ظ…ط§طھ ط§ظ„طھط§ط¨ط¹ط© ط¨ط´ظƒظ„ ظ…ظ†ط¸ظ… ظˆظ‚ط§ط¨ظ„ ظ„ظ„طھظƒط±ط§ط± ط¨ط£ظ…ط§ظ†.
            await StopAsync();

            // ط¥ط؛ظ„ط§ظ‚ ط¬ظ…ظٹط¹ ط§ظ„ط¬ظ„ط³ط§طھ ط§ظ„طھظٹ ظ…ط§ ط²ط§ظ„طھ ظ…ظپطھظˆط­ط©.
            foreach (var session in _sessions.Keys.ToArray())
            {
                try
                {
                    await session.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.Warn(
                        $"Session close failed: {ex.Message}");
                }
            }

            // ط§ظ†طھط¸ط§ط± ط§ظ†طھظ‡ط§ط، ط¬ظ…ظٹط¹ ظ…ظ‡ط§ظ… ط§ظ„ط¬ظ„ط³ط§طھ ظ‚ط¨ظ„ ط¥ظ†ظ‡ط§ط، ط§ظ„ط®ط§ط¯ظ….
            try
            {
                await Task.WhenAll(sessionTasks.ToArray());
            }
            catch
            {
                // طھظ… طھط³ط¬ظٹظ„ ط£ط®ط·ط§ط، ط§ظ„ط¬ظ„ط³ط§طھ ط¯ط§ط®ظ„ ObserveSessionAsync.
            }

            // ط§ظ†طھط¸ط§ط± ط§ظ†طھظ‡ط§ط، ط®ط¯ظ…ط© ط§ظ„ظˆط³ط§ط¦ط· ط¨ط¹ط¯ ط¥ط؛ظ„ط§ظ‚ ط¬ظ„ط³ط§طھ ط§ظ„ط¹ظ…ظ„ط§ط،.
            try
            {
                await mediaTask;
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                // ط¥ظ„ط؛ط§ط، ظ…طھظˆظ‚ط¹ ط£ط«ظ†ط§ط، ط§ظ„ط¥ط؛ظ„ط§ظ‚ ط§ظ„ظ…ظ†ط¸ظ….
            }
        }
    }

    /// <summary>
    /// ظٹط³طھظ‚ط¨ظ„ ط¥ط´ط¹ط§ط±ظ‹ط§ ط¹ظ†ط¯ ط¥ط؛ظ„ط§ظ‚ ط¬ظ„ط³ط© ط¹ظ…ظٹظ„طŒ ط«ظ… ظٹط¨ط¯ط£ طھظ†ط¸ظٹظپ ظ…ظˆط§ط±ط¯ظ‡ط§.
    /// </summary>
    public async Task OnSessionClosedAsync(IClientHandler session)
    {
        // ServerHost ظٹط­طھظپط¸ ط­ط§ظ„ظٹظ‹ط§ ط¨ط¬ظ„ط³ط§طھ ClientSession ط§ظ„ظ…ظ„ظ…ظˆط³ط©.
        // ط¥ط²ط§ظ„ط© ط§ظ„ط¬ظ„ط³ط© ظ…ط´ط±ظˆط·ط© ط¨ظ†ط¬ط§ط­ ط§ظ„ط¹ط«ظˆط± ط¹ظ„ظٹظ‡ط§ ظ„ظ…ظ†ط¹ طھظ†ظپظٹط° ط§ظ„طھظ†ط¸ظٹظپ ظ…ط±طھظٹظ†.
        if (session is not ClientSession concrete
            || !_sessions.TryRemove(concrete, out _))
        {
            return;
        }

        try
        {
            // ط¥ط²ط§ظ„ط© ط§ظ„ظ…ط³طھط®ط¯ظ… ظ…ظ† ط§ظ„ط­ط¶ظˆط± ظˆط§ظ„ظ…ط­ط§ط¯ط«ط§طھ ظˆط§ظ„ظ…ظˆط§ط±ط¯ ط§ظ„ظ…ط±طھط¨ط·ط© ط¨ظ‡.
            await _disconnectHandler.HandleDisconnectAsync(
                session,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Warn(
                $"Disconnect cleanup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// ظٹط±ط§ظ‚ط¨ ظ…ظ‡ظ…ط© ط¬ظ„ط³ط© ظˆط§ط­ط¯ط© ظˆظٹط³ط¬ظ„ ط£ظٹ ط§ط³طھط«ظ†ط§ط، ط؛ظٹط± ظ…ط¹ط§ظ„ط¬.
    /// </summary>
    private async Task ObserveSessionAsync(Task sessionTask)
    {
        try
        {
            await sessionTask;
        }
        catch (Exception ex)
        {
            _logger.Error(
                $"Session task failed: {ex.Message}");
        }
    }

    /// <summary>
    /// ظٹظ†ظپط° ط¥ظٹظ‚ط§ظپظ‹ط§ ظ…ظ†ط¸ظ…ظ‹ط§ ظ„ط¬ظ…ظٹط¹ ط§ظ„ظ…ظˆط§ط±ط¯ ط§ظ„طھظٹ ظٹط¯ظٹط±ظ‡ط§ ServerHost.
    /// </summary>
    public async Task StopAsync()
    {
        // Interlocked ظٹط¶ظ…ظ† ط£ظ† طھظ†ظپظٹط° ط§ظ„ط¥ظٹظ‚ط§ظپ ظٹطھظ… ظ…ط±ط© ظˆط§ط­ط¯ط© ظپظ‚ط·
        // ط­طھظ‰ ظ„ظˆ ط§ط³طھط¯ط¹طھظ‡ ط£ظƒط«ط± ظ…ظ† ط¬ظ‡ط© ظپظٹ ط§ظ„ظˆظ‚طھ ظ†ظپط³ظ‡.
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        // ط¥ط±ط³ط§ظ„ ط¥ط´ط§ط±ط© ط§ظ„ط¥ظ„ط؛ط§ط، ط¥ظ„ظ‰ ط­ظ„ظ‚ط© ط§ط³طھظ‚ط¨ط§ظ„ TCP ظˆط¨ظ‚ظٹط© ط§ظ„ط®ط¯ظ…ط§طھ.
        _stop.Cancel();

        // ط¥ظٹظ‚ط§ظپ ظ‚ط¨ظˆظ„ ط§طھطµط§ظ„ط§طھ TCP ط¬ط¯ظٹط¯ط©.
        _tcpListener.Stop();

        // ط¥ظٹظ‚ط§ظپ ط®ط¯ظ…ط© UDP ط«ظ… ط®ط¯ظ…ط© HTTP.
        await _media.StopAsync();
        await _api.StopAsync();
    }

    /// <summary>
    /// طھط­ط±ظٹط± ظ…ظˆط§ط±ط¯ ط§ظ„ط®ط§ط¯ظ… ط¹ظ†ط¯ ط§ظ†طھظ‡ط§ط، ظ†ط·ط§ظ‚ ط§ظ„ط§ط³طھط®ط¯ط§ظ….
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}

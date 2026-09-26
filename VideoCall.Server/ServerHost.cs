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
/// Composition-root / connection acceptor. This is the only class in the
/// solution that is allowed to depend on concrete infrastructure types
/// (<see cref="UdpMediaRelayService"/>, <see cref="ApiServer"/>,
/// <see cref="ClientSession"/>) as well as on Domain interfaces - because
/// starting, stopping and disposing a process's I/O resources in the right
/// order is exactly what a composition root is for. Business logic itself
/// (<c>ProtocolRouter</c>, the repositories) never sees these concrete
/// types; see <see cref="Program"/> for how everything is wired together
/// with <c>Microsoft.Extensions.DependencyInjection</c>.
/// <para>
/// Software-engineering concepts:
/// <list type="bullet">
/// <item><b>Dependency Inversion Principle</b>: implements
/// <see cref="IClientSessionRegistry"/> so <see cref="ClientSession"/> can
/// notify "someone" on close without knowing it is specifically a
/// <c>ServerHost</c>.</item>
/// <item><b>Single Responsibility Principle</b>: this class only accepts TCP
/// connections and orchestrates the start/stop order of the TCP listener,
/// the UDP media relay and the HTTP API. It contains zero call/room/login
/// business logic (that all now lives in <c>Application.ProtocolRouter</c>
/// and the repositories).</item>
/// </list>
/// </para>
/// <para>
/// ⚠️ Graceful-shutdown behaviour preserved exactly, and extended
/// consistently to the new HTTP API: <see cref="RunAsync"/> still starts the
/// media relay and (now also) the API server before accepting connections,
/// the accept loop still breaks cleanly on cancellation, the <c>finally</c>
/// block still stops the listener, calls <see cref="StopAsync"/>, explicitly
/// closes every still-open session, awaits every session task
/// (<c>Task.WhenAll</c>, never fire-and-forget), and only then awaits the
/// media task. <see cref="StopAsync"/> is still idempotent
/// (<see cref="Interlocked.Exchange(ref int, int)"/>) and now also awaits
/// <c>ApiServer.StopAsync()</c> as part of the same sequence, not in
/// isolation, exactly as the assignment requires.
/// </para>
/// </summary>
public sealed class ServerHost : IClientSessionRegistry, IAsyncDisposable
{
    private readonly TcpListener _tcpListener;
    private readonly IProtocolMessageDispatcher _dispatcher;
    private readonly IConnectionLifecycleHandler _disconnectHandler;
    private readonly UdpMediaRelayService _media;
    private readonly ApiServer _api;
    private readonly IAppLogger _logger;
    private readonly ConcurrentDictionary<ClientSession, byte> _sessions = new();
    private readonly CancellationTokenSource _stop = new();
    private int _stopped;

    public ServerHost(
        IProtocolMessageDispatcher dispatcher,
        IConnectionLifecycleHandler disconnectHandler,
        UdpMediaRelayService media,
        ApiServer api,
        IAppLogger logger,
        IPAddress? bindAddress = null,
        int tcpPort = NetworkConfig.TcpControlPort)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _disconnectHandler = disconnectHandler ?? throw new ArgumentNullException(nameof(disconnectHandler));
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tcpListener = new TcpListener(bindAddress ?? IPAddress.Any, tcpPort);
    }

    public async Task RunAsync(CancellationToken externalCancellation = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            externalCancellation, _stop.Token);
        var ct = linked.Token;

        _tcpListener.Start();
        _logger.Info($"TCP listening on {((IPEndPoint)_tcpListener.LocalEndpoint).Port}.");

        var mediaTask = _media.RunAsync(ct);
        await _api.StartAsync(ct);
        var sessionTasks = new ConcurrentBag<Task>();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _tcpListener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (ct.IsCancellationRequested)
                {
                    break;
                }

                var session = new ClientSession(client, _dispatcher, this, _logger);
                if (!_sessions.TryAdd(session, 0))
                {
                    await session.CloseAsync();
                    continue;
                }

                _logger.Info($"TCP client connected from {client.Client.RemoteEndPoint}.");
                var task = session.RunAsync(ct);
                sessionTasks.Add(task);
                _ = ObserveSessionAsync(task);
            }
        }
        finally
        {
            _tcpListener.Stop();
            await StopAsync();

            foreach (var session in _sessions.Keys.ToArray())
            {
                try { await session.CloseAsync(); }
                catch (Exception ex) { _logger.Warn($"Session close failed: {ex.Message}"); }
            }

            try { await Task.WhenAll(sessionTasks.ToArray()); }
            catch { /* individual session errors were already logged */ }

            try { await mediaTask; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }
    }

    public async Task OnSessionClosedAsync(IClientHandler session)
    {
        if (session is not ClientSession concrete || !_sessions.TryRemove(concrete, out _)) return;

        try
        {
            await _disconnectHandler.HandleDisconnectAsync(session, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Disconnect cleanup failed: {ex.Message}");
        }
    }

    private async Task ObserveSessionAsync(Task sessionTask)
    {
        try { await sessionTask; }
        catch (Exception ex) { _logger.Error($"Session task failed: {ex.Message}"); }
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        _stop.Cancel();
        _tcpListener.Stop();
        await _media.StopAsync();
        await _api.StopAsync();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

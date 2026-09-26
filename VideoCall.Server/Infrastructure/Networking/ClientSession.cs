using System.Net;
using System.Net.Sockets;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Networking;

namespace VideoCall.Server.Infrastructure.Networking;

/// <summary>
/// ظٹظ…ط«ظ„ ط§طھطµط§ظ„ TCP ظˆط§ط­ط¯ظ‹ط§ ط¨ظٹظ† ط§ظ„ط®ط§ط¯ظ… ظˆط¹ظ…ظٹظ„ ظˆط§ط­ط¯.
///
/// طھظ…ظ„ظƒ ظ‡ط°ظ‡ ط§ظ„ظپط¦ط© ظ…ظˆط§ط±ط¯ ط§ظ„ط§طھطµط§ظ„ ظˆطھط¯ظٹط± ط¯ظˆط±ط© ط­ظٹط§طھظ‡طŒ ط¨ظ…ط§ ظپظٹ ط°ظ„ظƒ:
/// - ظ‚ط±ط§ط،ط© ط§ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ظ…ط¤ط·ط±ط© ط¹ط¨ط± TcpMessageReaderWriter.
/// - طھظ…ط±ظٹط± ط§ظ„ط±ط³ط§ط¦ظ„ ط¥ظ„ظ‰ IProtocolMessageDispatcher.
/// - ط¥ط±ط³ط§ظ„ ط§ظ„ط±ط¯ظˆط¯ ط¥ظ„ظ‰ ط§ظ„ط¹ظ…ظٹظ„.
/// - ط±ط¨ط· ط§ظ„ط¬ظ„ط³ط© ط¨ط§ظ„ظ…ط³طھط®ط¯ظ… ط¨ط¹ط¯ ظ†ط¬ط§ط­ ط§ظ„ظ…طµط§ط¯ظ‚ط©.
/// - ط¥ط؛ظ„ط§ظ‚ ط§ظ„ط§طھطµط§ظ„ ظˆط¥ط¨ظ„ط§ط؛ ط³ط¬ظ„ ط§ظ„ط¬ظ„ط³ط§طھ.
///
/// ظ„ط§ طھط­طھظˆظٹ ClientSession ط¹ظ„ظ‰ ظ…ظ†ط·ظ‚ طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„ ط£ظˆ ط§ظ„ظ…ظƒط§ظ„ظ…ط§طھ ط£ظˆ ط§ظ„ط؛ط±ظپ.
/// ظˆط¸ظٹظپطھظ‡ط§ طھظ‚طھطµط± ط¹ظ„ظ‰ ط§ظ„ظ†ظ‚ظ„ ظˆط¯ظˆط±ط© ط­ظٹط§ط© ط§ظ„ط§طھطµط§ظ„طŒ ط¨ظٹظ†ظ…ط§ ظٹط­ط¯ط¯ ProtocolRouter
/// ظ…ط¹ظ†ظ‰ ط§ظ„ط±ط³ط§ط¦ظ„ ظˆظٹظ†ظپط° ظ…ظ†ط·ظ‚ ط§ظ„ط£ط¹ظ…ط§ظ„.
///
/// ظٹط­ظ‚ظ‚ ظ‡ط°ط§ ط§ظ„ظپطµظ„ ظ…ط¨ط¯ط£ ط§ظ„ظ…ط³ط¤ظˆظ„ظٹط© ط§ظ„ظˆط§ط­ط¯ط© (Single Responsibility Principle)
/// ظˆظ…ط¨ط¯ط£ ط¹ظƒط³ ط§طھط¬ط§ظ‡ ط§ظ„ط§ط¹طھظ…ط§ط¯ (Dependency Inversion Principle)طŒ ط¥ط° طھط¹طھظ…ط¯
/// ط§ظ„ط¬ظ„ط³ط© ط¹ظ„ظ‰ ظˆط§ط¬ظ‡ط§طھ Domain ط¨ط¯ظ„ ط§ظ„ط§ط¹طھظ…ط§ط¯ ط§ظ„ظ…ط¨ط§ط´ط± ط¹ظ„ظ‰ ServerHost ط£ظˆ ظ…ظ†ط·ظ‚ ط§ظ„ط£ط¹ظ…ط§ظ„.
///
/// طھط·ط¨ظ‚ ط§ظ„ط¬ظ„ط³ط© ط¥ط؛ظ„ط§ظ‚ظ‹ط§ ظ…ظ†ط¸ظ…ظ‹ط§ ظˆظ‚ط§ط¨ظ„ظ‹ط§ ظ„ظ„طھظƒط±ط§ط± ط¨ط£ظ…ط§ظ†ط› ط­ظٹط« ظٹظ…ظƒظ† ط§ط³طھط¯ط¹ط§ط،
/// CloseAsync ظ…ظ† ط­ظ„ظ‚ط© ط§ظ„ظ‚ط±ط§ط،ط© ط£ظˆ ظ…ظ† ServerHost ط¯ظˆظ† طھظ†ظپظٹط° ط§ظ„طھظ†ط¸ظٹظپ ظ…ط±طھظٹظ†.
/// </summary>
public sealed class ClientSession : IClientHandler, IAsyncDisposable
{
    // ط§طھطµط§ظ„ TCP ط§ظ„ط®ط§طµ ط¨ط§ظ„ط¹ظ…ظٹظ„.
    private readonly TcpClient _client;

    // ظ…ط³ط¤ظˆظ„ ط¹ظ† ظ‚ط±ط§ط،ط© ظˆظƒطھط§ط¨ط© ط§ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ظ…ط¤ط·ط±ط© ط¹ط¨ط± TCP.
    private readonly TcpMessageReaderWriter _wire;

    // ظ…ظˆط²ط¹ ط§ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ط°ظٹ ظٹظپط³ط± LoginRequest ظˆط¨ظ‚ظٹط© ط±ط³ط§ط¦ظ„ ط§ظ„ط¨ط±ظˆطھظˆظƒظˆظ„.
    private readonly IProtocolMessageDispatcher _dispatcher;

    // ط³ط¬ظ„ ط§ظ„ط¬ظ„ط³ط§طھ ط§ظ„ط°ظٹ ظٹطھظ… ط¥ط´ط¹ط§ط±ظ‡ ط¹ظ†ط¯ ط¥ط؛ظ„ط§ظ‚ ط§ظ„ط§طھطµط§ظ„.
    private readonly IClientSessionRegistry _registry;

    // ط®ط¯ظ…ط© طھط³ط¬ظٹظ„ ط§ظ„ط£ط­ط¯ط§ط« ظˆط§ظ„ط£ط®ط·ط§ط،.
    private readonly IAppLogger _logger;

    // ظ…طµط¯ط± ط¥ظ„ط؛ط§ط، ط®ط§طµ ط¨ظ‡ط°ظ‡ ط§ظ„ط¬ظ„ط³ط©.
    private readonly CancellationTokenSource _stop = new();

    // ط¹ظ„ط§ظ…ط© ط°ط±ظٹط© طھظ…ظ†ط¹ ط¥ط؛ظ„ط§ظ‚ ط§ظ„ط¬ظ„ط³ط© ط£ظƒط«ط± ظ…ظ† ظ…ط±ط©.
    private int _closed;

    /// <summary>
    /// ط±ظ…ط² ظپط±ظٹط¯ ظ„ظ„ط¬ظ„ط³ط© ظٹطھظ… ط¥ط±ط³ط§ظ„ظ‡ ط¨ط¹ط¯ ظ†ط¬ط§ط­ طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„.
    /// ظٹط³طھط®ط¯ظ… ط£ظٹط¶ظ‹ط§ ظ„ظ„طھط­ظ‚ظ‚ ظ…ظ† ط­ط²ظ… UDP ط§ظ„ط®ط§طµط© ط¨ط§ظ„ظˆط³ط§ط¦ط·.
    /// </summary>
    public Guid SessionToken { get; } = Guid.NewGuid();

    /// <summary>
    /// ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… ط§ظ„ظ…ط±طھط¨ط· ط¨ط§ظ„ط¬ظ„ط³ط© ط¨ط¹ط¯ ظ†ط¬ط§ط­ ط§ظ„ظ…طµط§ط¯ظ‚ط©.
    /// طھظƒظˆظ† ط§ظ„ظ‚ظٹظ…ط© null ظ‚ط¨ظ„ ط§ظƒطھظ…ط§ظ„ طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„.
    /// </summary>
    public string? Username { get; private set; }

    /// <summary>
    /// ظٹط­ط¯ط¯ ظ…ط§ ط¥ط°ط§ ظƒط§ظ† ط§ظ„ظ…ط³طھط®ط¯ظ… ظ‚ط¯ ط§ط¬طھط§ط² ظ…ط±ط­ظ„ط© ط§ظ„ظ…طµط§ط¯ظ‚ط©.
    /// </summary>
    public bool IsAuthenticated => Username is not null;

    /// <summary>
    /// ط¹ظ†ظˆط§ظ† ط§ظ„ط´ط¨ظƒط© ط§ظ„ط¨ط¹ظٹط¯ ط§ظ„ط®ط§طµ ط¨ط§ظ„ط¹ظ…ظٹظ„ ظ„ط£ط؛ط±ط§ط¶ ط§ظ„طھط´ط®ظٹطµ ظˆط§ظ„طھط³ط¬ظٹظ„.
    /// </summary>
    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    /// <summary>
    /// ظٹظ†ط´ط¦ ط¬ظ„ط³ط© TCP ظˆظٹط­ظ‚ظ† ظ…ظƒظˆظ†ط§طھ ط§ظ„ط§طھطµط§ظ„ ظˆط§ظ„طھظˆط²ظٹط¹ ظˆط§ظ„طھط³ط¬ظٹظ„.
    /// </summary>
    /// <param name="client">ط§طھطµط§ظ„ TCP ط§ظ„ظ…ظ‚ط¨ظˆظ„ ظ…ظ† ط§ظ„ط®ط§ط¯ظ….</param>
    /// <param name="dispatcher">ظ…ظˆط²ط¹ ط§ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ظˆط§ط±ط¯ط©.</param>
    /// <param name="registry">ط³ط¬ظ„ ط§ظ„ط¬ظ„ط³ط§طھ ط§ظ„ظ…ظپطھظˆط­ط©.</param>
    /// <param name="logger">ط®ط¯ظ…ط© طھط³ط¬ظٹظ„ ط§ظ„ط£ط­ط¯ط§ط« ظˆط§ظ„ط£ط®ط·ط§ط،.</param>
    public ClientSession(
        TcpClient client,
        IProtocolMessageDispatcher dispatcher,
        IClientSessionRegistry registry,
        IAppLogger logger)
    {
        _client = client
            ?? throw new ArgumentNullException(nameof(client));

        _dispatcher = dispatcher
            ?? throw new ArgumentNullException(nameof(dispatcher));

        _registry = registry
            ?? throw new ArgumentNullException(nameof(registry));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        // ط¥ظ†ط´ط§ط، ظ‚ط§ط±ط¦ ظˆظƒط§طھط¨ ط§ظ„ط¨ط±ظˆطھظˆظƒظˆظ„ ظپظˆظ‚ stream ط§ظ„ط®ط§طµ ط¨ط§طھطµط§ظ„ TCP.
        _wire = new TcpMessageReaderWriter(client.GetStream());

        // طھظ‚ظ„ظٹظ„ ط§ظ„طھط£ط®ظٹط± ظپظٹ ط±ط³ط§ط¦ظ„ ط§ظ„طھط­ظƒظ… ظ…ط«ظ„ LoginRequest ظˆLoginResponse.
        _client.NoDelay = true;
    }

    /// <summary>
    /// ظٹط±ط¨ط· ط§ظ„ط¬ظ„ط³ط© ط¨ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… ط¨ط¹ط¯ ظ†ط¬ط§ط­ ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ط¨ظٹط§ظ†ط§طھ ط§ظ„ط¯ط®ظˆظ„.
    /// </summary>
    /// <param name="username">ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… ط§ظ„ط°ظٹ طھظ…طھ ظ…طµط§ط¯ظ‚طھظ‡.</param>
    public void SetAuthenticatedUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Username is required.",
                nameof(username));
        }

        Username = username.Trim();
    }

    /// <summary>
    /// ظٹط¨ط¯ط£ ط­ظ„ظ‚ط© ظ‚ط±ط§ط،ط© ط§ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ظˆط§ط±ط¯ط© ظ…ظ† ط§ظ„ط¹ظ…ظٹظ„.
    ///
    /// ظƒظ„ ط±ط³ط§ظ„ط© ظٹطھظ… طھظ…ط±ظٹط±ظ‡ط§ ط¥ظ„ظ‰ ProtocolRouterطŒ ط§ظ„ط°ظٹ ظٹط­ط¯ط¯ ظ†ظˆط¹ظ‡ط§ ظˆظٹظ†ظپط°
    /// ط§ظ„ط¹ظ…ظ„ظٹط© ط§ظ„ظ…ظ†ط§ط³ط¨ط©طŒ ظ…ط«ظ„ طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„ ط£ظˆ ط¥ط¯ط§ط±ط© ط§ظ„ظ…ظƒط§ظ„ظ…ط§طھ ظˆط§ظ„ط؛ط±ظپ.
    /// </summary>
    /// <param name="serverCancellation">
    /// ط±ظ…ط² ط§ظ„ط¥ظ„ط؛ط§ط، ط§ظ„ظ‚ط§ط¯ظ… ظ…ظ† ط¯ظˆط±ط© ط­ظٹط§ط© ط§ظ„ط®ط§ط¯ظ….
    /// </param>
    public async Task RunAsync(CancellationToken serverCancellation)
    {
        // ط¯ظ…ط¬ ط¥ظ„ط؛ط§ط، ط§ظ„ط®ط§ط¯ظ… ظ…ط¹ ط¥ظ„ط؛ط§ط، ظ‡ط°ظ‡ ط§ظ„ط¬ظ„ط³ط© ط¨ط´ظƒظ„ ظ…ط³طھظ‚ظ„.
        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                serverCancellation,
                _stop.Token);

        try
        {
            while (!linked.IsCancellationRequested)
            {
                // ظ‚ط±ط§ط،ط© ط±ط³ط§ظ„ط© ظˆط§ط­ط¯ط© ظ…ظ† ط§ظ„ط¹ظ…ظٹظ„.
                var message = await _wire.ReadMessageAsync(linked.Token);

                // null طھط¹ظ†ظٹ ط£ظ† ط§ظ„ط·ط±ظپ ط§ظ„ط¢ط®ط± ط£ط؛ظ„ظ‚ ط§ظ„ط§طھطµط§ظ„.
                if (message is null)
                {
                    break;
                }

                // طھظ…ط±ظٹط± ط§ظ„ط±ط³ط§ظ„ط© ط¥ظ„ظ‰ ط·ط¨ظ‚ط© ط§ظ„طھط·ط¨ظٹظ‚ ظ„ظ…ط¹ط§ظ„ط¬طھظ‡ط§.
                await _dispatcher.DispatchAsync(
                    this,
                    message,
                    linked.Token);
            }
        }
        catch (OperationCanceledException)
            when (linked.IsCancellationRequested)
        {
            // ط¥ط؛ظ„ط§ظ‚ ط·ط¨ظٹط¹ظٹ ظ†طھظٹط¬ط© ط¥ظ„ط؛ط§ط، ط§ظ„ط®ط§ط¯ظ… ط£ظˆ ط§ظ„ط¬ظ„ط³ط©.
        }
        catch (IOException ex)
        {
            _logger.Warn(
                $"TCP connection closed for {Describe()}: {ex.Message}");
        }
        catch (SocketException ex)
        {
            _logger.Warn(
                $"TCP socket failed for {Describe()}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error(
                $"Unhandled session error for {Describe()}: {ex}");
        }
        finally
        {
            // ط¶ظ…ط§ظ† طھظ†ظپظٹط° ط§ظ„طھظ†ط¸ظٹظپ ظ…ظ‡ظ…ط§ ظƒط§ظ† ط³ط¨ط¨ ط§ظ†طھظ‡ط§ط، ط§ظ„ط­ظ„ظ‚ط©.
            await CloseAsync();
        }
    }

    /// <summary>
    /// ظٹط±ط³ظ„ ط±ط³ط§ظ„ط© ط¥ظ„ظ‰ ط§ظ„ط¹ظ…ظٹظ„ ط¹ط¨ط± ط§طھطµط§ظ„ TCP.
    /// </summary>
    /// <param name="message">ط§ظ„ط±ط³ط§ظ„ط© ط§ظ„طھظٹ ط³ظٹطھظ… ط¥ط±ط³ط§ظ„ظ‡ط§.</param>
    /// <param name="ct">ط±ظ…ط² ط§ظ„ط¥ظ„ط؛ط§ط، ط§ظ„ط®ط§طµ ط¨ط¹ظ…ظ„ظٹط© ط§ظ„ط¥ط±ط³ط§ظ„.</param>
    public async Task SendAsync(
        Message message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // ط¥ظ„ط؛ط§ط، ط§ظ„ط¥ط±ط³ط§ظ„ ط¹ظ†ط¯ ط¥ظ„ط؛ط§ط، ط§ظ„ط·ظ„ط¨ ط£ظˆ ط¥ط؛ظ„ط§ظ‚ ط§ظ„ط¬ظ„ط³ط©.
        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                _stop.Token);

        try
        {
            await _wire.WriteMessageAsync(
                message,
                linked.Token);
        }
        catch (OperationCanceledException)
            when (linked.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is IOException
                or SocketException
                or ObjectDisposedException)
        {
            _logger.Warn(
                $"Failed to send {message.Type} to {Describe()}: {ex.Message}");

            // ظپط´ظ„ ط§ظ„ط¥ط±ط³ط§ظ„ ظٹط¹ظ†ظٹ ط£ظ† ط§ظ„ط¬ظ„ط³ط© ظ„ظ… طھط¹ط¯ طµط§ظ„ط­ط© ط؛ط§ظ„ط¨ظ‹ط§.
            await CloseAsync();
            throw;
        }
    }

    /// <summary>
    /// ظٹط­ط±ط± ظ…ظˆط§ط±ط¯ ط§ظ„ط§طھطµط§ظ„ ط¹ظ†ط¯ ط§ظ†طھظ‡ط§ط، ط¯ظˆط±ط© ط­ظٹط§ط© ط§ظ„ط¬ظ„ط³ط©.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    /// <summary>
    /// ظٹط؛ظ„ظ‚ ط§ظ„ط¬ظ„ط³ط© ظ…ط±ط© ظˆط§ط­ط¯ط©طŒ ط«ظ… ظٹط¨ظ„ط؛ ط³ط¬ظ„ ط§ظ„ط¬ظ„ط³ط§طھ ظ„طھظ†ظپظٹط° ط§ظ„طھظ†ط¸ظٹظپ.
    /// </summary>
    public async Task CloseAsync()
    {
        // ظ…ظ†ط¹ طھظƒط±ط§ط± ط¥ظ„ط؛ط§ط، ط§ظ„ط¬ظ„ط³ط© ط£ظˆ طھط­ط±ظٹط± ط§ظ„ظ€ socket ط£ظˆ طھظ†ظپظٹط° cleanup.
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();

        try
        {
            // ط¥ط؛ظ„ط§ظ‚ ط§طھط¬ط§ظ‡ظٹ ط§ظ„ط§طھطµط§ظ„ ظ„ط¥ظٹظ‚ط§ظپ ط¹ظ…ظ„ظٹط§طھ ط§ظ„ظ‚ط±ط§ط،ط© ظˆط§ظ„ظƒطھط§ط¨ط©.
            _client.Client.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // ط§ظ„ط§طھطµط§ظ„ ظ‚ط¯ ظٹظƒظˆظ† ظ…ط؛ظ„ظ‚ظ‹ط§ ظ…ط³ط¨ظ‚ظ‹ط§.
        }

        _client.Dispose();

        // ط¥ط´ط¹ط§ط± ServerHost ط­طھظ‰ ظٹط²ظٹظ„ ط§ظ„ط¬ظ„ط³ط© ظˆظٹظ†ط¸ظپ ط­ط¶ظˆط± ط§ظ„ظ…ط³طھط®ط¯ظ….
        await _registry.OnSessionClosedAsync(this);
    }

    /// <summary>
    /// ظٹط¹ظٹط¯ ظˆطµظپظ‹ط§ ط¢ظ…ظ†ظ‹ط§ ظ„ظ„ط¬ظ„ط³ط© ظ„ط§ط³طھط®ط¯ط§ظ…ظ‡ ظپظٹ ط§ظ„ط³ط¬ظ„ط§طھ.
    /// </summary>
    private string Describe()
    {
        return Username
            ?? RemoteEndPoint?.ToString()
            ?? "unknown";
    }
}

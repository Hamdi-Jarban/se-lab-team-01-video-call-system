using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Networking;

namespace VideoCall.Server.Infrastructure.Media;

/// <summary>
/// ط®ط¯ظ…ط© طھظ…ط±ظٹط± ط§ظ„ظˆط³ط§ط¦ط· ط¹ط¨ط± UDP ط¨ظ†ظ…ط· SFU.
///
/// ظٹط±ط³ظ„ ظƒظ„ ط¹ظ…ظٹظ„ ط­ط²ظ… ط§ظ„طµظˆطھ ظˆط§ظ„ظپظٹط¯ظٹظˆ ط¥ظ„ظ‰ ط§ظ„ط®ط§ط¯ظ…طŒ ط«ظ… ظٹظ‚ظˆظ… ط§ظ„ط®ط§ط¯ظ… ط¨طھظ…ط±ظٹط±
/// ظƒظ„ ط­ط²ظ…ط© ط¥ظ„ظ‰ ط¨ظ‚ظٹط© ط£ط¹ط¶ط§ط، ط§ظ„ظ…ط­ط§ط¯ط«ط© ط§ظ„ظ†ط´ط·ط© ظ†ظپط³ظ‡ط§.
///
/// طھط¹طھظ…ط¯ ط§ظ„ط®ط¯ظ…ط© ط¹ظ„ظ‰ ظˆط§ط¬ظ‡ط§طھ Domain ظ„ظ„ظˆطµظˆظ„ ط¥ظ„ظ‰ ط¨ظٹط§ظ†ط§طھ ط§ظ„ظ…ط­ط§ط¯ط«ط§طھ ظˆط§ظ„ط­ط¶ظˆط±طŒ
/// ط¨ط¯ظ„ ط§ظ„ط§ط±طھط¨ط§ط· ط§ظ„ظ…ط¨ط§ط´ط± ط¨ط§ظ„طھظ†ظپظٹط°ط§طھ ط§ظ„ظپط¹ظ„ظٹط©. ظˆظ‡ط°ط§ ظٹط³ظ…ط­ ط¨ط§ط³طھط¨ط¯ط§ظ„ ظ…طµط§ط¯ط±
/// ط§ظ„ط¨ظٹط§ظ†ط§طھ ط£ظˆ ط§ط®طھط¨ط§ط± ط§ظ„ط®ط¯ظ…ط© ط¨ط§ط³طھط®ط¯ط§ظ… طھظ†ظپظٹط°ط§طھ ط¨ط¯ظٹظ„ط©.
///
/// طھظ†ظپط° ط§ظ„ط®ط¯ظ…ط© ظˆط§ط¬ظ‡طھظٹظ† ظ…ظ†ظپطµظ„طھظٹظ† ظ„ط£ط³ط¨ط§ط¨ طھطµظ…ظٹظ…ظٹط©:
/// - IMediaRelayCoordinator: ط§ظ„ط¹ظ…ظ„ظٹط§طھ ط§ظ„طھظٹ ظٹط­طھط§ط¬ظ‡ط§ ProtocolRouter ظپظ‚ط·طŒ
///   ظ…ط«ظ„ ط­ط°ظپ endpoint ط£ظˆ ظ†ط³ظٹط§ظ† ظ…ط­ط§ط¯ط«ط©.
/// - IAsyncDisposable: ط¥ط¯ط§ط±ط© ط¯ظˆط±ط© ط­ظٹط§ط© ظ…ظˆط±ط¯ UDPطŒ ظˆطھط³طھط®ط¯ظ…ظ‡ط§ ط·ط¨ظ‚ط© ط§ظ„طھط´ط؛ظٹظ„.
///
/// ظ‡ط°ط§ ط§ظ„ظپطµظ„ ط¨ظٹظ† ط§ظ„ظˆط§ط¬ظ‡ط§طھ ظٹط·ط¨ظ‚ ظ…ط¨ط¯ط£ ظپطµظ„ ط§ظ„ظˆط§ط¬ظ‡ط§طھ
/// (Interface Segregation Principle)طŒ ط¨ط­ظٹط« ظ„ط§ طھط¹طھظ…ط¯ ظƒظ„ ط·ط¨ظ‚ط© ط¥ظ„ط§ ط¹ظ„ظ‰
/// ط§ظ„ط¹ظ…ظ„ظٹط§طھ ط§ظ„طھظٹ طھط­طھط§ط¬ظ‡ط§ ظپط¹ظ„ظٹظ‹ط§.
///
/// ظٹطھظ… طھظ†ظپظٹط° ط§ظ„ط¥ظٹظ‚ط§ظپ ط¨ط·ط±ظٹظ‚ط© ط¢ظ…ظ†ط© ظˆظ‚ط§ط¨ظ„ط© ظ„ظ„طھظƒط±ط§ط±ط› ط¥ط° ظ„ط§ ظٹظ…ظƒظ† طھظ†ظپظٹط° StopAsync
/// ط£ظƒط«ط± ظ…ظ† ظ…ط±ط©طŒ ظƒظ…ط§ ظٹطھظ… ط§ظ†طھط¸ط§ط± ط¯ظˆط±ط© ط§ظ„ط§ط³طھظ‚ط¨ط§ظ„ ظ‚ط¨ظ„ طھط­ط±ظٹط± UdpClient.
/// </summary>
public sealed class UdpMediaRelayService : IMediaRelayCoordinator,IAsyncDisposable
{
    // ط§ظ„ط­ط¯ ط§ظ„ط£ط¹ظ„ظ‰ ظ„ط­ط¬ظ… ط­ط²ظ…ط© UDP ط§ظ„ظ…ظ‚ط¨ظˆظ„ط©.
    // ظٹظ…ظ†ط¹ ط§ط³طھظ‚ط¨ط§ظ„ ط­ط²ظ… ط؛ظٹط± ظ…ظ†ط·ظ‚ظٹط© ط£ظˆ ط§ط³طھظ‡ظ„ط§ظƒ ظ…ظˆط§ط±ط¯ ط؛ظٹط± ظ…طھظˆظ‚ط¹.
    private const int MaxDatagramBytes = 64 * 1024;

    // ظ‚ظ†ط§ط© UDP ط§ظ„ظ…ط³طھط®ط¯ظ…ط© ظ„ط§ط³طھظ‚ط¨ط§ظ„ ظˆطھظ…ط±ظٹط± ط­ط²ظ… ط§ظ„ظˆط³ط§ط¦ط·.
    private readonly UdpClient _udp;

    // ظ…ط³طھظˆط¯ط¹ ط§ظ„ظ…ط­ط§ط¯ط«ط§طھ ظ„ظ„طھط­ظ‚ظ‚ ظ…ظ† ط­ط§ظ„ط© ط§ظ„ظ…ظƒط§ظ„ظ…ط© ظˆط¹ط¶ظˆظٹط© ط§ظ„ظ…ط±ط³ظ„.
    private readonly IConversationRepository _conversations;

    // ظ…ط³طھظˆط¯ط¹ ط§ظ„ط­ط¶ظˆط± ظ„ظ„طھط­ظ‚ظ‚ ظ…ظ† ط£ظ† ط§ظ„ظ…ط±ط³ظ„ ظ…ط³طھط®ط¯ظ… ظ…طھطµظ„ ظپط¹ظ„ظٹظ‹ط§.
    private readonly IUserPresenceRepository _presence;

    // ط®ط¯ظ…ط© ط§ظ„طھط³ط¬ظٹظ„ ط§ظ„ظ…ط±ظƒط²ظٹ ظ„ظ„ط£ط®ط·ط§ط، ظˆط§ظ„ط£ط­ط¯ط§ط« ط§ظ„طھط´ط؛ظٹظ„ظٹط©.
    private readonly IAppLogger _logger;

    // ط¬ط¯ظˆظ„ ظ†ظ‚ط§ط· ط§ظ„ط§طھطµط§ظ„ ط§ظ„ظ…ط³ط¬ظ„ط© ظ„ظƒظ„ ظ…ط­ط§ط¯ط«ط©.
    // ط§ظ„ظ…ظپطھط§ط­ ط§ظ„ط£ظˆظ„ ظ‡ظˆ ظ…ط¹ط±ظپ ط§ظ„ظ…ط­ط§ط¯ط«ط©طŒ ظˆط§ظ„ظ…ظپطھط§ط­ ط§ظ„ط«ط§ظ†ظٹ ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ….
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, IPEndPoint>> _endpoints =
        new(StringComparer.OrdinalIgnoreCase);

    // ط¹ظ„ط§ظ…ط© ط°ط±ظٹط© طھط­ط¯ط¯ ظ…ط§ ط¥ط°ط§ ظƒط§ظ†طھ ط§ظ„ط®ط¯ظ…ط© ظ‚ط¯ ط¯ط®ظ„طھ ظ…ط±ط­ظ„ط© ط§ظ„ط¥ظٹظ‚ط§ظپ.
    private int _stopped;

    /// <summary>
    /// ظٹظ†ط´ط¦ ط®ط¯ظ…ط© طھظ…ط±ظٹط± ط§ظ„ظˆط³ط§ط¦ط· ظˆظٹط±ط¨ط·ظ‡ط§ ط¨ط§ظ„ط§ط¹طھظ…ط§ط¯ظٹط§طھ ط§ظ„ظ…ط·ظ„ظˆط¨ط©.
    /// </summary>
    /// <param name="conversations">ظ…ط³طھظˆط¯ط¹ ط§ظ„ظ…ط­ط§ط¯ط«ط§طھ.</param>
    /// <param name="presence">ظ…ط³طھظˆط¯ط¹ ط§ظ„ظ…ط³طھط®ط¯ظ…ظٹظ† ط§ظ„ظ…طھطµظ„ظٹظ†.</param>
    /// <param name="logger">ط®ط¯ظ…ط© ط§ظ„طھط³ط¬ظٹظ„.</param>
    /// <param name="port">ظ…ظ†ظپط° UDP ط§ظ„ط®ط§طµ ط¨ط§ظ„ظˆط³ط§ط¦ط·.</param>
    public UdpMediaRelayService(
        IConversationRepository conversations,
        IUserPresenceRepository presence,
        IAppLogger logger,
        int port)
    {
        _conversations = conversations
            ?? throw new ArgumentNullException(nameof(conversations));

        _presence = presence
            ?? throw new ArgumentNullException(nameof(presence));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        // ظپطھط­ ظ…ظ†ظپط° UDP ط¹ظ†ط¯ ط¥ظ†ط´ط§ط، ط§ظ„ط®ط¯ظ…ط©.
        _udp = new UdpClient(port);
    }

    /// <summary>
    /// ظٹط¨ط¯ط£ ط­ظ„ظ‚ط© ط§ط³طھظ‚ط¨ط§ظ„ ط­ط²ظ… ط§ظ„ظˆط³ط§ط¦ط· ظ…ظ† ط§ظ„ط¹ظ…ظ„ط§ط،.
    /// </summary>
    /// <param name="ct">ط±ظ…ط² ط¥ظ„ط؛ط§ط، ط¯ظˆط±ط© طھط´ط؛ظٹظ„ ط§ظ„ط®ط§ط¯ظ….</param>
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.Info("SFU media relay started.");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult received;

                try
                {
                    // ط§ظ†طھط¸ط§ط± ط­ط²ظ…ط© UDP ط¬ط¯ظٹط¯ط© ظ…ط¹ ط¯ط¹ظ… ط§ظ„ط¥ظ„ط؛ط§ط،.
                    received = await _udp.ReceiveAsync(ct);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    // ط¥ظٹظ‚ط§ظپ ط·ط¨ظٹط¹ظٹ ظ†طھظٹط¬ط© ط¥ظ„ط؛ط§ط، ط§ظ„ط®ط§ط¯ظ….
                    break;
                }
                catch (SocketException ex)
                {
                    _logger.Warn(
                        $"UDP receive failed: {ex.Message}");
                    continue;
                }

                // طھط¬ط§ظ‡ظ„ ط§ظ„ط­ط²ظ… ط§ظ„طھظٹ طھطھط¬ط§ظˆط² ط§ظ„ط­ط¯ ط§ظ„ظ…ط³ظ…ظˆط­ ط¨ظ‡.
                if (received.Buffer.Length > MaxDatagramBytes)
                {
                    continue;
                }

                await HandlePacketAsync(
                    received.Buffer,
                    received.RemoteEndPoint,
                    ct);
            }
        }
        finally
        {
            // ط¶ظ…ط§ظ† طھط­ط±ظٹط± ظ…ظˆط±ط¯ UDP ط­طھظ‰ ط¹ظ†ط¯ ط­ط¯ظˆط« ط§ط³طھط«ظ†ط§ط، ط£ظˆ ط¥ظ„ط؛ط§ط،.
            await StopAsync();
        }
    }

    /// <summary>
    /// ظٹطھط­ظ‚ظ‚ ظ…ظ† ط­ط²ظ…ط© ظˆط³ط§ط¦ط· ظˆط§ط­ط¯ط© ط«ظ… ظٹط³ط¬ظ„ endpoint ط§ظ„ظ…ط±ط³ظ„ ظˆظٹظ…ط±ط± ط§ظ„ط­ط²ظ…ط©
    /// ط¥ظ„ظ‰ ط¨ظ‚ظٹط© ط£ط¹ط¶ط§ط، ط§ظ„ظ…ط­ط§ط¯ط«ط© ط§ظ„ظ†ط´ط·ط©.
    /// </summary>
    private async Task HandlePacketAsync(
        byte[] bytes,
        IPEndPoint senderEndpoint,
        CancellationToken ct)
    {
        // ظ…ط­ط§ظˆظ„ط© ظپظƒ طھط³ظ„ط³ظ„ ط§ظ„ط­ط²ظ…ط© ظˆط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ظ…ط¹ط±ظپ ط§ظ„ظ…ط­ط§ط¯ط«ط©.
        var packet = MediaPacket.TryDeserialize(
            bytes,
            bytes.Length);

        if (packet is null || packet.CallId == Guid.Empty)
        {
            return;
        }

        // ظ„ط§ ظٹظ…ظƒظ† ظ‚ط¨ظˆظ„ ط­ط²ظ…ط© ظ„ط§ طھط­طھظˆظٹ ط¹ظ„ظ‰ ظ‡ظˆظٹط© ظ…ط±ط³ظ„.
        if (string.IsNullOrWhiteSpace(packet.SenderUsername))
        {
            return;
        }

        // ظ‚ط¨ظˆظ„ ط£ظ†ظˆط§ط¹ ط§ظ„ظˆط³ط§ط¦ط· ط§ظ„ظ…ط¯ط¹ظˆظ…ط© ظپظ‚ط·.
        if (packet.MediaType is not
            (MediaType.Audio or MediaType.Video or MediaType.Handshake))
        {
            return;
        }

        // ط§ظ„طھط£ظƒط¯ ظ…ظ† ط£ظ† ظ…ط¹ط±ظپ ط§ظ„ظˆط³ط§ط¦ط· ظ…ط±طھط¨ط· ط¨ظ…ط­ط§ط¯ط«ط© ظ†ط´ط·ط©.
        if (!_conversations.TryGetActiveConversationByMediaId(
                packet.CallId,
                out var conversation))
        {
            return;
        }

        var conversationId = conversation.Id;

        // ظ…ظ†ط¹ ظ…ط³طھط®ط¯ظ… ط؛ظٹط± ط¹ط¶ظˆ ظپظٹ ط§ظ„ظ…ط­ط§ط¯ط«ط© ظ…ظ† ط¥ط±ط³ط§ظ„ ط§ظ„ظˆط³ط§ط¦ط· ط¥ظ„ظٹظ‡ط§.
        if (!_conversations.IsMember(
                conversationId,
                packet.SenderUsername))
        {
            return;
        }

        // ط§ظ„طھط£ظƒط¯ ظ…ظ† ط£ظ† ط§ظ„ظ…ط³طھط®ط¯ظ… ظ…طھطµظ„ ط­ط§ظ„ظٹظ‹ط§.
        if (!_presence.TryGet(
                packet.SenderUsername,
                out var session))
        {
            return;
        }

        // ظ…ط·ط§ط¨ظ‚ط© SessionToken طھظ…ظ†ط¹ ط§ظ†طھط­ط§ظ„ ظ‡ظˆظٹط© ظ…ط³طھط®ط¯ظ… ظ…طھطµظ„.
        if (session.SessionToken != packet.SessionToken)
        {
            return;
        }

        // ط¥ظ†ط´ط§ط، ط³ط¬ظ„ endpoints ظ„ظ„ظ…ط­ط§ط¯ط«ط© ط¹ظ†ط¯ ط§ظ„ط­ط§ط¬ط©.
        var roomEndpoints = _endpoints.GetOrAdd(
            conversationId,
            _ => new ConcurrentDictionary<string, IPEndPoint>(
                StringComparer.OrdinalIgnoreCase));

        // طھط­ط¯ظٹط« ط¹ظ†ظˆط§ظ† ط§ظ„ط´ط¨ظƒط© ط§ظ„ط®ط§طµ ط¨ط§ظ„ظ…ط±ط³ظ„.
        // ظ‡ط°ط§ ظٹط¯ط¹ظ… طھط؛ظٹط± endpoint ط§ظ„ظ†ط§طھط¬ ط¹ظ† NAT ط£ظˆ ط¥ط¹ط§ط¯ط© ط§ظ„ط§طھطµط§ظ„.
        roomEndpoints[packet.SenderUsername] = senderEndpoint;

        // Handshake ظٹط³طھط®ط¯ظ… ظ„طھط³ط¬ظٹظ„ endpoint ظپظ‚ط· ظˆظ„ط§ ظٹطھظ… طھظ…ط±ظٹط±ظ‡ ظƒطµظˆطھ.
        if (packet.MediaType == MediaType.Handshake)
        {
            return;
        }

        // طھظ…ط±ظٹط± ط§ظ„ط­ط²ظ…ط© ط¥ظ„ظ‰ ط¬ظ…ظٹط¹ ط£ط¹ط¶ط§ط، ط§ظ„ظ…ط­ط§ط¯ط«ط© ط¨ط§ط³طھط«ظ†ط§ط، ط§ظ„ظ…ط±ط³ظ„.
        foreach (var item in roomEndpoints.ToArray())
        {
            ct.ThrowIfCancellationRequested();

            if (item.Key.Equals(
                    packet.SenderUsername,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                await _udp.SendAsync(
                    bytes,
                    bytes.Length,
                    item.Value);
            }
            catch (SocketException ex)
            {
                _logger.Warn(
                    $"UDP forward to {item.Key} failed: {ex.Message}");
            }
            catch (ObjectDisposedException)
                when (Volatile.Read(ref _stopped) != 0)
            {
                // ط§ظ„ط¥ط±ط³ط§ظ„ ط£ظˆظ‚ظپ ظ„ط£ظ† ط§ظ„ط®ط¯ظ…ط© ط¯ط®ظ„طھ ظ…ط±ط­ظ„ط© ط§ظ„ط¥ط؛ظ„ط§ظ‚.
                return;
            }
        }
    }

    /// <summary>
    /// ظٹط­ط°ظپ endpoint ط§ظ„ط®ط§طµ ط¨ظ…ط³طھط®ط¯ظ… ظ…ظ† ظ…ط­ط§ط¯ط«ط© ظ…ط­ط¯ط¯ط©.
    /// ظٹط³طھط®ط¯ظ… ط¹ظ†ط¯ ظ…ط؛ط§ط¯ط±ط© ط§ظ„ظ…ط³طھط®ط¯ظ… ط£ظˆ ط§ظ†ظ‚ط·ط§ط¹ ط¬ظ„ط³ط© ط§ظ„ط§طھطµط§ظ„.
    /// </summary>
    public void RemoveEndpoint(
        string conversationId,
        string username)
    {
        if (!_endpoints.TryGetValue(
                conversationId,
                out var members))
        {
            return;
        }

        members.TryRemove(username, out _);

        // ط­ط°ظپ ط³ط¬ظ„ ط§ظ„ظ…ط­ط§ط¯ط«ط© ط¥ط°ط§ ظ„ظ… ظٹطھط¨ظ‚ ط£ظٹ endpoint.
        if (members.IsEmpty)
        {
            _endpoints.TryRemove(
                conversationId,
                out _);
        }
    }

    /// <summary>
    /// ظٹط­ط°ظپ ط¬ظ…ظٹط¹ endpoints ط§ظ„ظ…ط±طھط¨ط·ط© ط¨ظ…ط­ط§ط¯ط«ط© ظƒط§ظ…ظ„ط©.
    /// ظٹط³طھط®ط¯ظ… ط¹ظ†ط¯ ط¥ظ†ظ‡ط§ط، ط§ظ„ظ…ظƒط§ظ„ظ…ط© ط£ظˆ ط¥ظٹظ‚ط§ظپ ط¬ظ„ط³ط© ط§ظ„ظˆط³ط§ط¦ط·.
    /// </summary>
    public void ForgetConversation(string conversationId)
    {
        _endpoints.TryRemove(
            conversationId,
            out _);
    }

    /// <summary>
    /// ظٹظˆظ‚ظپ ط®ط¯ظ…ط© UDP ظ…ط±ط© ظˆط§ط­ط¯ط© ظپظ‚ط· ظˆظٹط­ط±ط± ظ…ظˆط±ط¯ ط§ظ„ط´ط¨ظƒط©.
    /// </summary>
    public async Task StopAsync()
    {
        // ظ…ظ†ط¹ طھظ†ظپظٹط° ط§ظ„ط¥ظٹظ‚ط§ظپ ط£ظƒط«ط± ظ…ظ† ظ…ط±ط© ط¹ظ†ط¯ طھط²ط§ظ…ظ† ط¹ط¯ط© ظ…ط³ط§ط±ط§طھ.
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _udp.Dispose();

        // ط§ظ„ط­ظپط§ط¸ ط¹ظ„ظ‰ ظˆط§ط¬ظ‡ط© async ظ„طھط³ظ‡ظٹظ„ ط¯ظ…ط¬ ط§ظ„ط®ط¯ظ…ط© ظ…ط¹ ط¯ظˆط±ط© طھط´ط؛ظٹظ„ ط§ظ„ط®ط§ط¯ظ….
        await Task.CompletedTask;
    }

    /// <summary>
    /// ظٹط­ط±ط± ظ…ظˆط§ط±ط¯ ط§ظ„ط®ط¯ظ…ط© ط¹ظ†ط¯ ط§ظ†طھظ‡ط§ط، ط¯ظˆط±ط© ط­ظٹط§طھظ‡ط§.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Networking;

namespace VideoCall.Server.Infrastructure.Media;

/// <summary>
/// SFU-style UDP relay: every client sends its audio/video to this process,
/// which forwards each packet to every other member of the same active
/// conversation. Behaviourally unchanged from the original <c>MediaRelay</c>;
/// only its dependencies were switched from concrete
/// <c>ConversationManager</c>/<c>PresenceManager</c> to the
/// <see cref="IConversationRepository"/>/<see cref="IUserPresenceRepository"/>
/// Domain interfaces, and logging goes through <see cref="IAppLogger"/>.
/// <para>
/// Implements <see cref="IMediaRelayCoordinator"/> (the narrow slice of
/// behaviour <c>ProtocolRouter</c> needs) in addition to its own
/// <see cref="IAsyncDisposable"/> lifecycle, which only the composition root
/// (<c>ServerHost</c>) uses - see <see cref="IMediaRelayCoordinator"/> for
/// why those two roles are deliberately kept on separate interfaces
/// (Interface Segregation Principle).
/// </para>
/// <para>
/// ⚠️ Graceful-shutdown behaviour preserved exactly: <see cref="StopAsync"/>
/// is still idempotent (<see cref="Interlocked.Exchange(ref int, int)"/>),
/// <see cref="RunAsync"/> still awaits <see cref="StopAsync"/> from its
/// <c>finally</c> block, and disposal still just releases the
/// <see cref="UdpClient"/> - no new fire-and-forget work was introduced.
/// </para>
/// </summary>
public sealed class UdpMediaRelayService : IMediaRelayCoordinator, IAsyncDisposable
{
    private const int MaxDatagramBytes = 64 * 1024;

    private readonly UdpClient _udp;
    private readonly IConversationRepository _conversations;
    private readonly IUserPresenceRepository _presence;
    private readonly IAppLogger _logger;
    private readonly ConcurrentDictionary<string,
        ConcurrentDictionary<string, IPEndPoint>> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private int _stopped;

    public UdpMediaRelayService(
        IConversationRepository conversations,
        IUserPresenceRepository presence,
        IAppLogger logger,
        int port)
    {
        _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _udp = new UdpClient(port);
    }

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
                    received = await _udp.ReceiveAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    _logger.Warn($"UDP receive failed: {ex.Message}");
                    continue;
                }

                if (received.Buffer.Length > MaxDatagramBytes) continue;
                await HandlePacketAsync(received.Buffer, received.RemoteEndPoint, ct);
            }
        }
        finally
        {
            await StopAsync();
        }
    }

    private async Task HandlePacketAsync(
        byte[] bytes,
        IPEndPoint senderEndpoint,
        CancellationToken ct)
    {
        var packet = MediaPacket.TryDeserialize(bytes, bytes.Length);
        if (packet is null || packet.CallId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(packet.SenderUsername)) return;
        if (packet.MediaType is not (MediaType.Audio or MediaType.Video or MediaType.Handshake)) return;
        if (!_conversations.TryGetActiveConversationByMediaId(packet.CallId, out var conversation)) return;
        var conversationId = conversation.Id;
        if (!_conversations.IsMember(conversationId, packet.SenderUsername)) return;

        if (!_presence.TryGet(packet.SenderUsername, out var session)) return;
        if (session.SessionToken != packet.SessionToken) return;

        var roomEndpoints = _endpoints.GetOrAdd(
            conversationId,
            _ => new ConcurrentDictionary<string, IPEndPoint>(StringComparer.OrdinalIgnoreCase));
        roomEndpoints[packet.SenderUsername] = senderEndpoint;

        // Handshake registers the endpoint but is never forwarded as audio.
        if (packet.MediaType == MediaType.Handshake) return;

        foreach (var item in roomEndpoints.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            if (item.Key.Equals(packet.SenderUsername, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                await _udp.SendAsync(bytes, bytes.Length, item.Value);
            }
            catch (SocketException ex)
            {
                _logger.Warn($"UDP forward to {item.Key} failed: {ex.Message}");
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref _stopped) != 0)
            {
                return;
            }
        }
    }

    public void RemoveEndpoint(string conversationId, string username)
    {
        if (_endpoints.TryGetValue(conversationId, out var members))
        {
            members.TryRemove(username, out _);
            if (members.IsEmpty) _endpoints.TryRemove(conversationId, out _);
        }
    }

    public void ForgetConversation(string conversationId) =>
        _endpoints.TryRemove(conversationId, out _);

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        _udp.Dispose();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Networking;

namespace VideoCall.Server.Infrastructure.Media;

/// <summary>
/// خدمة تمرير الوسائط عبر UDP بنمط SFU.
///
/// يرسل كل عميل حزم الصوت والفيديو إلى الخادم، ثم يقوم الخادم بتمرير
/// كل حزمة إلى بقية أعضاء المحادثة النشطة نفسها.
///
/// تعتمد الخدمة على واجهات Domain للوصول إلى بيانات المحادثات والحضور،
/// بدل الارتباط المباشر بالتنفيذات الفعلية. وهذا يسمح باستبدال مصادر
/// البيانات أو اختبار الخدمة باستخدام تنفيذات بديلة.
///
/// تنفذ الخدمة واجهتين منفصلتين لأسباب تصميمية:
/// - IMediaRelayCoordinator: العمليات التي يحتاجها ProtocolRouter فقط،
///   مثل حذف endpoint أو نسيان محادثة.
/// - IAsyncDisposable: إدارة دورة حياة مورد UDP، وتستخدمها طبقة التشغيل.
///
/// هذا الفصل بين الواجهات يطبق مبدأ فصل الواجهات
/// (Interface Segregation Principle)، بحيث لا تعتمد كل طبقة إلا على
/// العمليات التي تحتاجها فعليًا.
///
/// يتم تنفيذ الإيقاف بطريقة آمنة وقابلة للتكرار؛ إذ لا يمكن تنفيذ StopAsync
/// أكثر من مرة، كما يتم انتظار دورة الاستقبال قبل تحرير UdpClient.
/// </summary>
public sealed class UdpMediaRelayService : IMediaRelayCoordinator,IAsyncDisposable
{
    // الحد الأعلى لحجم حزمة UDP المقبولة.
    // يمنع استقبال حزم غير منطقية أو استهلاك موارد غير متوقع.
    private const int MaxDatagramBytes = 64 * 1024;

    // قناة UDP المستخدمة لاستقبال وتمرير حزم الوسائط.
    private readonly UdpClient _udp;

    // مستودع المحادثات للتحقق من حالة المكالمة وعضوية المرسل.
    private readonly IConversationRepository _conversations;

    // مستودع الحضور للتحقق من أن المرسل مستخدم متصل فعليًا.
    private readonly IUserPresenceRepository _presence;

    // خدمة التسجيل المركزي للأخطاء والأحداث التشغيلية.
    private readonly IAppLogger _logger;

    // جدول نقاط الاتصال المسجلة لكل محادثة.
    // المفتاح الأول هو معرف المحادثة، والمفتاح الثاني اسم المستخدم.
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, IPEndPoint>> _endpoints =
        new(StringComparer.OrdinalIgnoreCase);

    // علامة ذرية تحدد ما إذا كانت الخدمة قد دخلت مرحلة الإيقاف.
    private int _stopped;

    /// <summary>
    /// ينشئ خدمة تمرير الوسائط ويربطها بالاعتماديات المطلوبة.
    /// </summary>
    /// <param name="conversations">مستودع المحادثات.</param>
    /// <param name="presence">مستودع المستخدمين المتصلين.</param>
    /// <param name="logger">خدمة التسجيل.</param>
    /// <param name="port">منفذ UDP الخاص بالوسائط.</param>
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

        // فتح منفذ UDP عند إنشاء الخدمة.
        _udp = new UdpClient(port);
    }

    /// <summary>
    /// يبدأ حلقة استقبال حزم الوسائط من العملاء.
    /// </summary>
    /// <param name="ct">رمز إلغاء دورة تشغيل الخادم.</param>
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
                    // انتظار حزمة UDP جديدة مع دعم الإلغاء.
                    received = await _udp.ReceiveAsync(ct);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    // إيقاف طبيعي نتيجة إلغاء الخادم.
                    break;
                }
                catch (SocketException ex)
                {
                    _logger.Warn(
                        $"UDP receive failed: {ex.Message}");
                    continue;
                }

                // تجاهل الحزم التي تتجاوز الحد المسموح به.
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
            // ضمان تحرير مورد UDP حتى عند حدوث استثناء أو إلغاء.
            await StopAsync();
        }
    }

    /// <summary>
    /// يتحقق من حزمة وسائط واحدة ثم يسجل endpoint المرسل ويمرر الحزمة
    /// إلى بقية أعضاء المحادثة النشطة.
    /// </summary>
    private async Task HandlePacketAsync(
        byte[] bytes,
        IPEndPoint senderEndpoint,
        CancellationToken ct)
    {
        // محاولة فك تسلسل الحزمة والتحقق من معرف المحادثة.
        var packet = MediaPacket.TryDeserialize(
            bytes,
            bytes.Length);

        if (packet is null || packet.CallId == Guid.Empty)
        {
            return;
        }

        // لا يمكن قبول حزمة لا تحتوي على هوية مرسل.
        if (string.IsNullOrWhiteSpace(packet.SenderUsername))
        {
            return;
        }

        // قبول أنواع الوسائط المدعومة فقط.
        if (packet.MediaType is not
            (MediaType.Audio or MediaType.Video or MediaType.Handshake))
        {
            return;
        }

        // التأكد من أن معرف الوسائط مرتبط بمحادثة نشطة.
        if (!_conversations.TryGetActiveConversationByMediaId(
                packet.CallId,
                out var conversation))
        {
            return;
        }

        var conversationId = conversation.Id;

        // منع مستخدم غير عضو في المحادثة من إرسال الوسائط إليها.
        if (!_conversations.IsMember(
                conversationId,
                packet.SenderUsername))
        {
            return;
        }

        // التأكد من أن المستخدم متصل حاليًا.
        if (!_presence.TryGet(
                packet.SenderUsername,
                out var session))
        {
            return;
        }

        // مطابقة SessionToken تمنع انتحال هوية مستخدم متصل.
        if (session.SessionToken != packet.SessionToken)
        {
            return;
        }

        // إنشاء سجل endpoints للمحادثة عند الحاجة.
        var roomEndpoints = _endpoints.GetOrAdd(
            conversationId,
            _ => new ConcurrentDictionary<string, IPEndPoint>(
                StringComparer.OrdinalIgnoreCase));

        // تحديث عنوان الشبكة الخاص بالمرسل.
        // هذا يدعم تغير endpoint الناتج عن NAT أو إعادة الاتصال.
        roomEndpoints[packet.SenderUsername] = senderEndpoint;

        // Handshake يستخدم لتسجيل endpoint فقط ولا يتم تمريره كصوت.
        if (packet.MediaType == MediaType.Handshake)
        {
            return;
        }

        // تمرير الحزمة إلى جميع أعضاء المحادثة باستثناء المرسل.
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
                // الإرسال أوقف لأن الخدمة دخلت مرحلة الإغلاق.
                return;
            }
        }
    }

    /// <summary>
    /// يحذف endpoint الخاص بمستخدم من محادثة محددة.
    /// يستخدم عند مغادرة المستخدم أو انقطاع جلسة الاتصال.
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

        // حذف سجل المحادثة إذا لم يتبق أي endpoint.
        if (members.IsEmpty)
        {
            _endpoints.TryRemove(
                conversationId,
                out _);
        }
    }

    /// <summary>
    /// يحذف جميع endpoints المرتبطة بمحادثة كاملة.
    /// يستخدم عند إنهاء المكالمة أو إيقاف جلسة الوسائط.
    /// </summary>
    public void ForgetConversation(string conversationId)
    {
        _endpoints.TryRemove(
            conversationId,
            out _);
    }

    /// <summary>
    /// يوقف خدمة UDP مرة واحدة فقط ويحرر مورد الشبكة.
    /// </summary>
    public async Task StopAsync()
    {
        // منع تنفيذ الإيقاف أكثر من مرة عند تزامن عدة مسارات.
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _udp.Dispose();

        // الحفاظ على واجهة async لتسهيل دمج الخدمة مع دورة تشغيل الخادم.
        await Task.CompletedTask;
    }

    /// <summary>
    /// يحرر موارد الخدمة عند انتهاء دورة حياتها.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}

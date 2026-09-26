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
/// المضيف الرئيسي لخادم VideoCall والمسؤول عن إدارة دورة حياة الاتصالات.
///
/// يمثل هذا النوع نقطة التنسيق بين موارد البنية التحتية المختلفة، وتشمل:
/// - مستمع اتصالات TCP.
/// - جلسات العملاء ClientSession.
/// - خدمة تمرير الوسائط عبر UDP.
/// - واجهة HTTP الخاصة بالمراقبة والقراءة.
///
/// تم إبقاء منطق الأعمال خارج هذا النوع عمدًا. لا يحتوي ServerHost على منطق
/// تسجيل الدخول أو المكالمات أو الغرف؛ إذ تتم معالجة هذه العمليات داخل
/// ProtocolRouter وطبقات التطبيق والمستودعات.
///
/// يطبق التصميم مبدأ المسؤولية الواحدة (Single Responsibility Principle)
/// ومبدأ عكس اتجاه الاعتماد (Dependency Inversion Principle)، حيث يعتمد
/// ServerHost على واجهات Domain عند التعامل مع منطق التطبيق.
/// </summary>
public sealed class ServerHost : IClientSessionRegistry, IAsyncDisposable
{
    // مستمع TCP المسؤول عن استقبال اتصالات العملاء الجدد.
    private readonly TcpListener _tcpListener;

    // موزع الرسائل الواردة من جلسات العملاء.
    // يستخدم ProtocolRouter في التطبيق الفعلي.
    private readonly IProtocolMessageDispatcher _dispatcher;

    // معالج تنظيف موارد المستخدم عند انتهاء جلسة الاتصال.
    private readonly IConnectionLifecycleHandler _disconnectHandler;

    // خدمة تمرير حزم الصوت والفيديو عبر UDP.
    private readonly UdpMediaRelayService _media;

    // خادم HTTP الخاص بمراقبة حالة الخادم وقراءة البيانات العامة.
    private readonly ApiServer _api;

    // واجهة تسجيل الأحداث والأخطاء التشغيلية.
    private readonly IAppLogger _logger;

    // سجل الجلسات المفتوحة حاليًا.
    // يستخدم ConcurrentDictionary لأن الإضافة والإزالة قد تحدث من مهام متعددة.
    private readonly ConcurrentDictionary<ClientSession, byte> _sessions = new();

    // مصدر إلغاء داخلي خاص بدورة حياة ServerHost.
    private readonly CancellationTokenSource _stop = new();

    // علامة ذرية تمنع تنفيذ StopAsync أكثر من مرة.
    private int _stopped;

    /// <summary>
    /// ينشئ مضيف الخادم ويحقن جميع الاعتماديات المطلوبة عبر Constructor Injection.
    /// </summary>
    /// <param name="dispatcher">موزع رسائل بروتوكول TCP.</param>
    /// <param name="disconnectHandler">معالج تنظيف جلسة العميل بعد الإغلاق.</param>
    /// <param name="media">خدمة تمرير الوسائط عبر UDP.</param>
    /// <param name="api">خدمة HTTP الخاصة بالمراقبة.</param>
    /// <param name="logger">خدمة التسجيل المركزي.</param>
    /// <param name="bindAddress">عنوان الشبكة الذي سيستمع عليه الخادم.</param>
    /// <param name="tcpPort">منفذ اتصالات التحكم عبر TCP.</param>
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

        // إذا لم يتم تحديد عنوان، يستمع الخادم على جميع واجهات الشبكة.
        _tcpListener = new TcpListener(
            bindAddress ?? IPAddress.Any,
            tcpPort);
    }

    /// <summary>
    /// يبدأ جميع خدمات الخادم ثم يدخل في حلقة استقبال اتصالات TCP.
    /// </summary>
    /// <param name="externalCancellation">
    /// رمز الإلغاء القادم من نقطة تشغيل التطبيق، مثل Ctrl+C.
    /// </param>
    public async Task RunAsync(
        CancellationToken externalCancellation = default)
    {
        // دمج إلغاء التطبيق الخارجي مع إلغاء ServerHost الداخلي.
        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellation,
                _stop.Token);

        var ct = linked.Token;

        // بدء مستمع TCP قبل استقبال أي عميل.
        _tcpListener.Start();

        _logger.Info(
            $"TCP listening on {((IPEndPoint)_tcpListener.LocalEndpoint).Port}.");

        // بدء خدمة تمرير الوسائط وخدمة HTTP قبل قبول جلسات العملاء.
        var mediaTask = _media.RunAsync(ct);
        await _api.StartAsync(ct);

        // الاحتفاظ بمهام الجلسات حتى يتم انتظارها عند إيقاف الخادم.
        var sessionTasks = new ConcurrentBag<Task>();

        try
        {
            // استمرار قبول الاتصالات حتى طلب الإلغاء.
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    // انتظار اتصال TCP جديد مع دعم الإلغاء.
                    client = await _tcpListener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    // إيقاف طبيعي نتيجة إلغاء دورة التشغيل.
                    break;
                }
                catch (ObjectDisposedException)
                    when (ct.IsCancellationRequested)
                {
                    // تم إغلاق المستمع أثناء عملية الإيقاف.
                    break;
                }

                // إنشاء جلسة مستقلة لكل عميل.
                var session = new ClientSession(
                    client,
                    _dispatcher,
                    this,
                    _logger);

                // تسجيل الجلسة قبل بدء مهمتها لضمان إمكانية تنظيفها لاحقًا.
                if (!_sessions.TryAdd(session, 0))
                {
                    await session.CloseAsync();
                    continue;
                }

                _logger.Info(
                    $"TCP client connected from {client.Client.RemoteEndPoint}.");

                // بدء دورة قراءة الرسائل الخاصة بالعميل.
                var task = session.RunAsync(ct);
                sessionTasks.Add(task);

                // مراقبة الجلسة وتسجيل أي استثناء غير متوقع.
                _ = ObserveSessionAsync(task);
            }
        }
        finally
        {
            // إيقاف استقبال الاتصالات الجديدة أولًا.
            _tcpListener.Stop();

            // إيقاف الخدمات التابعة بشكل منظم وقابل للتكرار بأمان.
            await StopAsync();

            // إغلاق جميع الجلسات التي ما زالت مفتوحة.
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

            // انتظار انتهاء جميع مهام الجلسات قبل إنهاء الخادم.
            try
            {
                await Task.WhenAll(sessionTasks.ToArray());
            }
            catch
            {
                // تم تسجيل أخطاء الجلسات داخل ObserveSessionAsync.
            }

            // انتظار انتهاء خدمة الوسائط بعد إغلاق جلسات العملاء.
            try
            {
                await mediaTask;
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                // إلغاء متوقع أثناء الإغلاق المنظم.
            }
        }
    }

    /// <summary>
    /// يستقبل إشعارًا عند إغلاق جلسة عميل، ثم يبدأ تنظيف مواردها.
    /// </summary>
    public async Task OnSessionClosedAsync(IClientHandler session)
    {
        // ServerHost يحتفظ حاليًا بجلسات ClientSession الملموسة.
        // إزالة الجلسة مشروطة بنجاح العثور عليها لمنع تنفيذ التنظيف مرتين.
        if (session is not ClientSession concrete
            || !_sessions.TryRemove(concrete, out _))
        {
            return;
        }

        try
        {
            // إزالة المستخدم من الحضور والمحادثات والموارد المرتبطة به.
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
    /// يراقب مهمة جلسة واحدة ويسجل أي استثناء غير معالج.
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
    /// ينفذ إيقافًا منظمًا لجميع الموارد التي يديرها ServerHost.
    /// </summary>
    public async Task StopAsync()
    {
        // Interlocked يضمن أن تنفيذ الإيقاف يتم مرة واحدة فقط
        // حتى لو استدعته أكثر من جهة في الوقت نفسه.
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        // إرسال إشارة الإلغاء إلى حلقة استقبال TCP وبقية الخدمات.
        _stop.Cancel();

        // إيقاف قبول اتصالات TCP جديدة.
        _tcpListener.Stop();

        // إيقاف خدمة UDP ثم خدمة HTTP.
        await _media.StopAsync();
        await _api.StopAsync();
    }

    /// <summary>
    /// تحرير موارد الخادم عند انتهاء نطاق الاستخدام.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}

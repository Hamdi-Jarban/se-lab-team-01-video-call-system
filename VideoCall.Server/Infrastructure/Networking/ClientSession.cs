using System.Net;
using System.Net.Sockets;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Networking;

namespace VideoCall.Server.Infrastructure.Networking;

/// <summary>
/// يمثل اتصال TCP واحدًا بين الخادم وعميل واحد.
///
/// تملك هذه الفئة موارد الاتصال وتدير دورة حياته، بما في ذلك:
/// - قراءة الرسائل المؤطرة عبر TcpMessageReaderWriter.
/// - تمرير الرسائل إلى IProtocolMessageDispatcher.
/// - إرسال الردود إلى العميل.
/// - ربط الجلسة بالمستخدم بعد نجاح المصادقة.
/// - إغلاق الاتصال وإبلاغ سجل الجلسات.
///
/// لا تحتوي ClientSession على منطق تسجيل الدخول أو المكالمات أو الغرف.
/// وظيفتها تقتصر على النقل ودورة حياة الاتصال، بينما يحدد ProtocolRouter
/// معنى الرسائل وينفذ منطق الأعمال.
///
/// يحقق هذا الفصل مبدأ المسؤولية الواحدة (Single Responsibility Principle)
/// ومبدأ عكس اتجاه الاعتماد (Dependency Inversion Principle)، إذ تعتمد
/// الجلسة على واجهات Domain بدل الاعتماد المباشر على ServerHost أو منطق الأعمال.
///
/// تطبق الجلسة إغلاقًا منظمًا وقابلًا للتكرار بأمان؛ حيث يمكن استدعاء
/// CloseAsync من حلقة القراءة أو من ServerHost دون تنفيذ التنظيف مرتين.
/// </summary>
public sealed class ClientSession : IClientHandler, IAsyncDisposable
{
    // اتصال TCP الخاص بالعميل.
    private readonly TcpClient _client;

    // مسؤول عن قراءة وكتابة الرسائل المؤطرة عبر TCP.
    private readonly TcpMessageReaderWriter _wire;

    // موزع الرسائل الذي يفسر LoginRequest وبقية رسائل البروتوكول.
    private readonly IProtocolMessageDispatcher _dispatcher;

    // سجل الجلسات الذي يتم إشعاره عند إغلاق الاتصال.
    private readonly IClientSessionRegistry _registry;

    // خدمة تسجيل الأحداث والأخطاء.
    private readonly IAppLogger _logger;

    // مصدر إلغاء خاص بهذه الجلسة.
    private readonly CancellationTokenSource _stop = new();

    // علامة ذرية تمنع إغلاق الجلسة أكثر من مرة.
    private int _closed;

    /// <summary>
    /// رمز فريد للجلسة يتم إرساله بعد نجاح تسجيل الدخول.
    /// يستخدم أيضًا للتحقق من حزم UDP الخاصة بالوسائط.
    /// </summary>
    public Guid SessionToken { get; } = Guid.NewGuid();

    /// <summary>
    /// اسم المستخدم المرتبط بالجلسة بعد نجاح المصادقة.
    /// تكون القيمة null قبل اكتمال تسجيل الدخول.
    /// </summary>
    public string? Username { get; private set; }

    /// <summary>
    /// يحدد ما إذا كان المستخدم قد اجتاز مرحلة المصادقة.
    /// </summary>
    public bool IsAuthenticated => Username is not null;

    /// <summary>
    /// عنوان الشبكة البعيد الخاص بالعميل لأغراض التشخيص والتسجيل.
    /// </summary>
    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    /// <summary>
    /// ينشئ جلسة TCP ويحقن مكونات الاتصال والتوزيع والتسجيل.
    /// </summary>
    /// <param name="client">اتصال TCP المقبول من الخادم.</param>
    /// <param name="dispatcher">موزع الرسائل الواردة.</param>
    /// <param name="registry">سجل الجلسات المفتوحة.</param>
    /// <param name="logger">خدمة تسجيل الأحداث والأخطاء.</param>
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

        // إنشاء قارئ وكاتب البروتوكول فوق stream الخاص باتصال TCP.
        _wire = new TcpMessageReaderWriter(client.GetStream());

        // تقليل التأخير في رسائل التحكم مثل LoginRequest وLoginResponse.
        _client.NoDelay = true;
    }

    /// <summary>
    /// يربط الجلسة باسم المستخدم بعد نجاح التحقق من بيانات الدخول.
    /// </summary>
    /// <param name="username">اسم المستخدم الذي تمت مصادقته.</param>
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
    /// يبدأ حلقة قراءة الرسائل الواردة من العميل.
    ///
    /// كل رسالة يتم تمريرها إلى ProtocolRouter، الذي يحدد نوعها وينفذ
    /// العملية المناسبة، مثل تسجيل الدخول أو إدارة المكالمات والغرف.
    /// </summary>
    /// <param name="serverCancellation">
    /// رمز الإلغاء القادم من دورة حياة الخادم.
    /// </param>
    public async Task RunAsync(CancellationToken serverCancellation)
    {
        // دمج إلغاء الخادم مع إلغاء هذه الجلسة بشكل مستقل.
        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                serverCancellation,
                _stop.Token);

        try
        {
            while (!linked.IsCancellationRequested)
            {
                // قراءة رسالة واحدة من العميل.
                var message = await _wire.ReadMessageAsync(linked.Token);

                // null تعني أن الطرف الآخر أغلق الاتصال.
                if (message is null)
                {
                    break;
                }

                // تمرير الرسالة إلى طبقة التطبيق لمعالجتها.
                await _dispatcher.DispatchAsync(
                    this,
                    message,
                    linked.Token);
            }
        }
        catch (OperationCanceledException)
            when (linked.IsCancellationRequested)
        {
            // إغلاق طبيعي نتيجة إلغاء الخادم أو الجلسة.
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
            // ضمان تنفيذ التنظيف مهما كان سبب انتهاء الحلقة.
            await CloseAsync();
        }
    }

    /// <summary>
    /// يرسل رسالة إلى العميل عبر اتصال TCP.
    /// </summary>
    /// <param name="message">الرسالة التي سيتم إرسالها.</param>
    /// <param name="ct">رمز الإلغاء الخاص بعملية الإرسال.</param>
    public async Task SendAsync(
        Message message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // إلغاء الإرسال عند إلغاء الطلب أو إغلاق الجلسة.
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

            // فشل الإرسال يعني أن الجلسة لم تعد صالحة غالبًا.
            await CloseAsync();
            throw;
        }
    }

    /// <summary>
    /// يحرر موارد الاتصال عند انتهاء دورة حياة الجلسة.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    /// <summary>
    /// يغلق الجلسة مرة واحدة، ثم يبلغ سجل الجلسات لتنفيذ التنظيف.
    /// </summary>
    public async Task CloseAsync()
    {
        // منع تكرار إلغاء الجلسة أو تحرير الـ socket أو تنفيذ cleanup.
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();

        try
        {
            // إغلاق اتجاهي الاتصال لإيقاف عمليات القراءة والكتابة.
            _client.Client.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // الاتصال قد يكون مغلقًا مسبقًا.
        }

        _client.Dispose();

        // إشعار ServerHost حتى يزيل الجلسة وينظف حضور المستخدم.
        await _registry.OnSessionClosedAsync(this);
    }

    /// <summary>
    /// يعيد وصفًا آمنًا للجلسة لاستخدامه في السجلات.
    /// </summary>
    private string Describe()
    {
        return Username
            ?? RemoteEndPoint?.ToString()
            ?? "unknown";
    }
}

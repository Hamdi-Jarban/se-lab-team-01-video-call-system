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
/// خادم HTTP للقراءة فقط يعرض حالة الخادم الحالية بصيغة JSON.
///
/// يعتمد ApiServer على نفس الحالة الموجودة في خادم TCP وUDP، لكنه لا يملك
/// صلاحية تعديلها. يقرأ البيانات من IUserPresenceRepository و
/// IConversationRepository فقط، ولا يرتبط مباشرة بجلسات TCP أو مستمع TCP
/// أو خدمة تمرير الوسائط.
///
/// يوفر الخادم نقاط قراءة لمراقبة:
/// - حالة الخادم وعدد العملاء المتصلين.
/// - المستخدمين المتصلين حاليًا.
/// - الغرف الموجودة.
/// - جلسات الوسائط النشطة.
///
/// يحقق هذا التصميم مبدأ عكس اتجاه الاعتماد، إذ تعتمد طبقة API على عقود
/// Domain بدل معرفة تفاصيل تخزين الحالة أو طريقة تحديثها.
/// كما يحقق مبدأ فصل المسؤوليات، لأن جميع endpoints هنا للقراءة فقط ولا
/// يمكنها إنشاء غرفة أو بدء مكالمة أو تعديل حالة النظام.
///
/// تتم إدارة الإيقاف بطريقة منظمة؛ حيث يتم إلغاء حلقة الاستقبال، ثم انتظار
/// الطلبات الجارية، وبعد ذلك إغلاق مستمع HTTP وتحرير الموارد.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    // إعدادات JSON الموحدة لجميع استجابات HTTP.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // مستمع HTTP المسؤول عن استقبال الطلبات.
    private readonly HttpListener _listener = new();

    // مستودع المستخدمين المتصلين.
    private readonly IUserPresenceRepository _presence;

    // مستودع المحادثات والغرف.
    private readonly IConversationRepository _conversations;

    // خدمة تسجيل أحداث وأخطاء API.
    private readonly IAppLogger _logger;

    // المهام الخاصة بالطلبات التي بدأت ولم تنته بعد.
    // يتم انتظارها أثناء الإيقاف المنظم.
    private readonly ConcurrentBag<Task> _pendingRequests = new();

    // مصدر الإلغاء الداخلي المرتبط بإلغاء التطبيق الخارجي.
    private CancellationTokenSource? _internalCts;

    // مهمة حلقة استقبال طلبات HTTP.
    private Task? _acceptLoopTask;

    // مؤقت قياس مدة تشغيل API.
    private Stopwatch? _uptime;

    // علامة ذرية تمنع تنفيذ الإيقاف أكثر من مرة.
    private int _stopped;

    /// <summary>
    /// ينشئ خادم API ويربطه بمستودعات الحالة وخدمة التسجيل.
    /// </summary>
    /// <param name="presence">مستودع المستخدمين المتصلين.</param>
    /// <param name="conversations">مستودع المحادثات والغرف.</param>
    /// <param name="logger">خدمة تسجيل الأحداث والأخطاء.</param>
    /// <param name="port">منفذ HTTP الذي سيستمع عليه الخادم.</param>
    public ApiServer(IUserPresenceRepository presence, IConversationRepository conversations, IAppLogger logger, int port)
    {
        _presence = presence
            ?? throw new ArgumentNullException(nameof(presence));

        _conversations = conversations
            ?? throw new ArgumentNullException(nameof(conversations));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        // استخدام localhost يقلل متطلبات URL ACL على Windows.
        // لا يتم فتح الخدمة على جميع الواجهات إلا بعد إعداد الصلاحيات
        // المناسبة وتأمين نقطة النهاية في بيئة الإنتاج.
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>
    /// يبدأ مستمع HTTP وحلقة استقبال الطلبات.
    /// </summary>
    /// <param name="externalCancellation">
    /// رمز الإلغاء القادم من دورة تشغيل التطبيق.
    /// </param>
    public Task StartAsync(CancellationToken externalCancellation)
    {
        // منع بدء الخدمة أكثر من مرة.
        if (_acceptLoopTask is not null)
        {
            return Task.CompletedTask;
        }

        _uptime = Stopwatch.StartNew();
        _listener.Start();

        // ربط دورة حياة API بدورة حياة التطبيق الرئيسية.
        _internalCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellation);

        _acceptLoopTask = AcceptLoopAsync(_internalCts.Token);

        _logger.Info(
            $"HTTP API listening on {string.Join(", ", _listener.Prefixes)}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// حلقة استقبال طلبات HTTP وتوزيع كل طلب على مهمة مستقلة.
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                // انتظار طلب جديد مع دعم الإلغاء.
                context = await _listener
                    .GetContextAsync()
                    .WaitAsync(ct);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                // إيقاف طبيعي نتيجة إلغاء الخدمة.
                break;
            }
            catch (Exception)
                when (Volatile.Read(ref _stopped) != 0)
            {
                // تم إغلاق المستمع أثناء انتظار طلب جديد.
                break;
            }
            catch (Exception ex)
            {
                _logger.Warn(
                    $"HTTP API accept failed: {ex.Message}");
                continue;
            }

            // تشغيل معالجة الطلب مع الاحتفاظ بالمهمة للانتظار أثناء الإيقاف.
            var requestTask = HandleRequestAsync(context, ct);
            _pendingRequests.Add(requestTask);
        }
    }

    /// <summary>
    /// يعالج طلب HTTP واحدًا ويرسل الاستجابة المناسبة.
    /// </summary>
    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            response.ContentType =
                "application/json; charset=utf-8";

            // API الحالية للقراءة فقط، لذلك يتم قبول GET فقط.
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

            // توجيه الطلب حسب المسار المطلوب.
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
            // إغلاق الاستجابة حتى عند انقطاع العميل أو حدوث خطأ.
            try
            {
                context.Response.Close();
            }
            catch
            {
                // قد يكون العميل أغلق الاتصال مسبقًا.
            }
        }
    }

    /// <summary>
    /// يحول payload إلى JSON ويرسله مع رمز الحالة المطلوب.
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
    /// يبني استجابة حالة الخادم وعدد العملاء ومدة التشغيل.
    /// </summary>
    private ServerStatusResponse BuildStatus() => new("running", _presence.GetSessions().Count, (_uptime?.Elapsed ?? TimeSpan.Zero).ToString(@"hh\:mm\:ss"));

    /// <summary>
    /// يبني قائمة المستخدمين المتصلين حاليًا.
    /// </summary>
    private OnlineUsersResponse BuildUsers()
    {
        var users = _presence.GetUsernames();
        return new OnlineUsersResponse(users, users.Count);
    }

    /// <summary>
    /// يبني ملخص الغرف الجماعية الحالية فقط.
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
    /// يبني ملخص المحادثات التي تعمل فيها الوسائط حاليًا.
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
    /// يوقف API بطريقة منظمة وينتظر حلقة الاستقبال والطلبات الجارية.
    /// </summary>
    public async Task StopAsync()
    {
        // ضمان تنفيذ الإيقاف مرة واحدة فقط.
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
            // المستمع متوقف مسبقًا أو تم تحريره.
        }

        // انتظار انتهاء حلقة الاستقبال.
        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask;
            }
            catch
            {
                // تم تسجيل أخطاء الاستقبال في الحلقة نفسها.
            }
        }

        // انتظار جميع طلبات HTTP التي بدأت قبل الإيقاف.
        try
        {
            await Task.WhenAll(
                _pendingRequests.ToArray());
        }
        catch
        {
            // تم تسجيل أخطاء الطلبات أثناء معالجتها.
        }

        try
        {
            _listener.Close();
        }
        catch
        {
            // المورد مغلق مسبقًا.
        }

        _internalCts?.Dispose();
    }

    /// <summary>
    /// يحرر موارد ApiServer عند انتهاء دورة حياته.
    /// </summary>
    public async ValueTask DisposeAsync() => await StopAsync();
}

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VideoCall.Server;
using VideoCall.Server.Api;
using VideoCall.Server.Application;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Infrastructure.Logging;
using VideoCall.Server.Infrastructure.Media;
using VideoCall.Server.Infrastructure.Security;
using VideoCall.Shared.Networking;

// ============================================================================
// نقطة تركيب التطبيق (Composition Root)
// ============================================================================
//
// هذا الملف هو المكان المركزي الذي تُربط فيه الواجهات (Interfaces)
// بالتنفيذات الفعلية داخل التطبيق.
//
// التزمنا هنا بمبدأ عكس اتجاه الاعتماد (Dependency Inversion Principle)،
// بحيث تعتمد طبقات التطبيق على الواجهات بدل الاعتماد المباشر على التفاصيل
// التنفيذية مثل TCP وUDP وConsole وطرق تخزين البيانات.
//
// فوائد هذا الأسلوب:
// - تسهيل استبدال أي مكوّن دون تعديل الطبقات التي تستخدمه.
// - تحسين قابلية اختبار الخدمات باستخدام بدائل وهمية (Mocks/Fakes).
// - إبقاء مسؤولية إنشاء الكائنات في مكان واحد.
// - منع انتشار عمليات new داخل طبقات التطبيق والأعمال.
//
// يتم استخدام Microsoft.Extensions.DependencyInjection لإدارة دورة حياة
// الخدمات وحقن الاعتماديات عبر Constructors.
// ============================================================================

// منفذ واجهة HTTP الخاصة بمراقبة حالة الخادم.
// ملاحظة: يجب نقل الإعدادات إلى Configuration أو Environment Variables
// عند تشغيل التطبيق في بيئة الإنتاج.
const int HttpApiPort = 8080;

// ---------------------------------------------------------------------------
// حسابات التطوير
// ---------------------------------------------------------------------------
//
// هذه الحسابات مخصصة للتطوير والاختبار المحلي فقط.
// لا يُسمح باستخدام كلمات مرور نصية ثابتة في بيئة الإنتاج.
//
// في بيئة الإنتاج يجب استبدال هذا القاموس بمصدر آمن، مثل:
// - قاعدة بيانات تحتوي على Password Hashes.
// - مزود هوية مركزي (Identity Provider).
// - Secret Manager أو خدمة أسرار مؤسسية.
// ---------------------------------------------------------------------------
var accounts = new Dictionary<string, string>(
    StringComparer.OrdinalIgnoreCase)
{
    ["hamdi"] = "1234",
    ["ali1"] = "1111",
    ["ali2"] = "2222",
    ["ali3"] = "3333"
};

// إنشاء حاوية الاعتماديات الخاصة بالتطبيق.
var services = new ServiceCollection();

// ---------------------------------------------------------------------------
// تسجيل الخدمات المشتركة
// ---------------------------------------------------------------------------

// خدمة تسجيل الأحداث والأخطاء.
// يتم تسجيلها كـ Singleton حتى تستخدم جميع مكونات الخادم نفس instance.
services.AddSingleton<IAppLogger, ConsoleAppLogger>();

// خدمة التحقق من بيانات تسجيل الدخول.
// التنفيذ الحالي مخصص للتطوير ويعتمد على الحسابات الموجودة في الذاكرة.
services.AddSingleton<ICredentialValidator>(
    _ => new DevelopmentCredentialValidator(accounts));

// مستودع الحضور (Presence Repository).
// يحتفظ بالمستخدمين المتصلين ويربط كل مستخدم بجلسة TCP الخاصة به.
services.AddSingleton<IUserPresenceRepository, UserPresenceService>();

// مستودع المحادثات والغرف.
// التنفيذ الحالي يعمل داخل الذاكرة ويحدد الحد الأقصى لأعضاء الغرفة بـ 8.
// يمكن استبداله لاحقًا بتنفيذ يعتمد على قاعدة بيانات دون تعديل المستهلكين.
services.AddSingleton<IConversationRepository>(
    _ => new ConversationService(maxGroupMembers: 8));

// ---------------------------------------------------------------------------
// خدمة تمرير الوسائط عبر UDP
// ---------------------------------------------------------------------------

// تسجيل خدمة UDP كنوع ملموس لأن ServerHost مسؤول عن دورة حياتها
// وإيقافها وتحرير مواردها عبر IAsyncDisposable.
services.AddSingleton(sp => new UdpMediaRelayService(
    sp.GetRequiredService<IConversationRepository>(),
    sp.GetRequiredService<IUserPresenceRepository>(),
    sp.GetRequiredService<IAppLogger>(),
    NetworkConfig.UdpMediaPort));

// توفير نفس instance عبر واجهة ضيقة.
// ProtocolRouter لا يحتاج إلى معرفة تفاصيل تشغيل أو إيقاف خدمة UDP؛
// لذلك يعتمد فقط على IMediaRelayCoordinator.
services.AddSingleton<IMediaRelayCoordinator>(
    sp => sp.GetRequiredService<UdpMediaRelayService>());

// ---------------------------------------------------------------------------
// موجّه رسائل البروتوكول ودورة حياة الاتصالات
// ---------------------------------------------------------------------------

// ProtocolRouter مسؤول عن تحويل الرسائل الواردة من العملاء إلى عمليات
// تسجيل الدخول والمكالمات وإدارة الغرف، ثم إرسال النتائج إلى العملاء.
services.AddSingleton<ProtocolRouter>();

// استخدام نفس instance من ProtocolRouter لتنفيذ واجهة توزيع الرسائل.
services.AddSingleton<IProtocolMessageDispatcher>(
    sp => sp.GetRequiredService<ProtocolRouter>());

// استخدام نفس instance لتنفيذ تنظيف الجلسة بعد انقطاع العميل.
// هذا يضمن إزالة المستخدم من Presence وإغلاق الحالات المرتبطة به.
services.AddSingleton<IConnectionLifecycleHandler>(
    sp => sp.GetRequiredService<ProtocolRouter>());

// ---------------------------------------------------------------------------
// واجهة HTTP للقراءة والمراقبة
// ---------------------------------------------------------------------------

// ApiServer تعرض معلومات القراءة فقط مثل:
// - حالة الخادم.
// - المستخدمين المتصلين.
// - الغرف الحالية.
// - الجلسات النشطة.
//
// لا تستخدم هذه الواجهة لتنفيذ تسجيل الدخول عبر TCP.
services.AddSingleton(sp => new ApiServer(
    sp.GetRequiredService<IUserPresenceRepository>(),
    sp.GetRequiredService<IConversationRepository>(),
    sp.GetRequiredService<IAppLogger>(),
    HttpApiPort));

// ---------------------------------------------------------------------------
// مضيف الخادم الرئيسي
// ---------------------------------------------------------------------------

// ServerHost هو المسؤول عن تنسيق موارد التشغيل الرئيسية:
// - استقبال اتصالات TCP.
// - إنشاء ClientSession لكل عميل.
// - تشغيل UDP Media Relay.
// - تشغيل HTTP API.
// - تنفيذ الإغلاق المنظم عند إيقاف التطبيق.
services.AddSingleton(sp => new ServerHost(
    sp.GetRequiredService<IProtocolMessageDispatcher>(),
    sp.GetRequiredService<IConnectionLifecycleHandler>(),
    sp.GetRequiredService<UdpMediaRelayService>(),
    sp.GetRequiredService<ApiServer>(),
    sp.GetRequiredService<IAppLogger>(),
    bindAddress: IPAddress.Any,
    tcpPort: NetworkConfig.TcpControlPort));

// ---------------------------------------------------------------------------
// بناء مزود الخدمات
// ---------------------------------------------------------------------------

// بناء حاوية الاعتماديات بعد اكتمال جميع التسجيلات.
// يتم التخلص منها تلقائيًا عند انتهاء التطبيق.
await using var provider = services.BuildServiceProvider();

// رمز الإلغاء المشترك لإيقاف جميع الخدمات بشكل منظم.
using var shutdown = new CancellationTokenSource();

// اعتراض Ctrl+C ومنع الإنهاء الفوري، ثم إرسال إشارة إيقاف
// إلى ServerHost وبقية الخدمات.
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

// الحصول على المضيف من حاوية الاعتماديات.
// using يضمن تحرير الموارد عند انتهاء دورة حياة الخادم.
await using var server = provider.GetRequiredService<ServerHost>();

Console.WriteLine("VideoCall server started. Press Ctrl+C to stop.");

// بدء دورة تشغيل الخادم وانتظار الإيقاف أو الإلغاء.
await server.RunAsync(shutdown.Token);

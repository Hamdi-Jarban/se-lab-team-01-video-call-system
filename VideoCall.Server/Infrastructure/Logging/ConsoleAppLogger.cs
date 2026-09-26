using VideoCall.Server.Domain.Logging;

namespace VideoCall.Server.Infrastructure.Logging;

/// <summary>
/// تنفيذ خدمة التسجيل الموجهة إلى نافذة Console.
///
/// يطبق هذا النوع العقد IAppLogger، ويوفر ثلاث درجات أساسية للتسجيل:
/// - Info: معلومات التشغيل والأحداث الطبيعية.
/// - Warn: تحذيرات لا توقف الخادم لكنها تحتاج إلى متابعة.
/// - Error: أخطاء تحتاج إلى تحليل أو تدخل من فريق التطوير.
///
/// تم تصميم الخدمة ككائن قابل للحقن (Injectable Service) بدل استخدام
/// Logger عام من النوع static. ويسمح ذلك بإدارة دورة حياة الخدمة من خلال
/// Composition Root، كما يسهل استبدالها بتنفيذ آخر في الاختبارات أو الإنتاج،
/// مثل خدمة تسجيل مركزية أو نظام مراقبة مؤسسي.
///
/// يتم تسجيل هذه الخدمة عادةً كـ Singleton داخل Program.cs حتى تستخدم
/// جميع مكونات الخادم نفس قناة التسجيل.
/// </summary>
public sealed class ConsoleAppLogger : IAppLogger
{
    // قفل مشترك يمنع تداخل مخرجات المهام المتوازية داخل نافذة Console.
    // هذا مهم لأن الخادم يعالج عدة جلسات واتصالات في الوقت نفسه.
    private readonly object _consoleLock = new();

    /// <summary>
    /// يسجل رسالة معلوماتية عن حدث طبيعي داخل النظام.
    /// </summary>
    /// <param name="message">نص الرسالة المراد تسجيلها.</param>
    public void Info(string message) =>
        Write("INFO", message, ConsoleColor.Gray);

    /// <summary>
    /// يسجل تحذيرًا لا يؤدي بالضرورة إلى إيقاف الخدمة.
    /// </summary>
    /// <param name="message">نص التحذير المراد تسجيله.</param>
    public void Warn(string message) =>
        Write("WARN", message, ConsoleColor.Yellow);

    /// <summary>
    /// يسجل خطأ حدث أثناء تنفيذ إحدى عمليات النظام.
    /// </summary>
    /// <param name="message">تفاصيل الخطأ المراد تسجيلها.</param>
    public void Error(string message) =>
        Write("ERROR", message, ConsoleColor.Red);

    /// <summary>
    /// يكتب رسالة موحدة إلى نافذة Console مع مستوى التسجيل واللون والوقت.
    /// </summary>
    /// <param name="level">مستوى الرسالة مثل INFO أو WARN أو ERROR.</param>
    /// <param name="message">نص الرسالة.</param>
    /// <param name="color">اللون المستخدم لتمييز مستوى الرسالة.</param>
    private void Write(
        string level,
        string message,
        ConsoleColor color)
    {
        // ضمان تنفيذ عملية الكتابة كاملة دون تداخل مع Thread آخر.
        lock (_consoleLock)
        {
            // حفظ اللون الحالي حتى لا تؤثر الرسالة على المخرجات اللاحقة.
            var previous = Console.ForegroundColor;

            Console.ForegroundColor = color;

            // استخدام تنسيق موحد يسهل قراءته والبحث عنه داخل سجلات التشغيل.
            Console.WriteLine(
                $"[{level}] {DateTime.Now:HH:mm:ss} {message}");

            // استعادة اللون السابق بعد انتهاء الكتابة.
            Console.ForegroundColor = previous;
        }
    }
}

using VideoCall.Server.Domain;

namespace VideoCall.Server.Infrastructure.Security;

/// <summary>
/// خدمة تحقق مخصصة لبيئة التطوير والاختبار فقط.
/// لا تستخدم هذه الخدمة في بيئة الإنتاج لأنها تعتمد على كلمات مرور
/// محفوظة كنص مباشر داخل الذاكرة.
///
/// عند الانتقال إلى الإنتاج، يجب استبدالها بتنفيذ يعتمد على قاعدة بيانات
/// وتخزين كلمات المرور باستخدام Hash آمن. وبما أن بقية النظام يعتمد على
/// ICredentialValidator، فإن استبدال آلية التحقق لا يتطلب تعديل منطق التطبيق.
/// </summary>
public sealed class DevelopmentCredentialValidator : ICredentialValidator
{
    // يحتوي على حسابات التطوير المسموح باستخدامها أثناء تشغيل الخادم.
    private readonly IReadOnlyDictionary<string, string> _accounts;

    /// <summary>
    /// ينشئ خدمة التحقق باستخدام حسابات التطوير.
    /// </summary>
    /// <param name="accounts">قاموس أسماء المستخدمين وكلمات المرور.</param>
    public DevelopmentCredentialValidator(
        IReadOnlyDictionary<string, string> accounts)
    {
        // نسخ البيانات مع تجاهل حالة الأحرف في أسماء المستخدمين.
        _accounts = new Dictionary<string, string>(
            accounts,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// يتحقق من تطابق اسم المستخدم وكلمة المرور.
    /// </summary>
    /// <param name="username">اسم المستخدم.</param>
    /// <param name="password">كلمة المرور.</param>
    /// <returns>
    /// true إذا كانت بيانات الدخول صحيحة، وإلا false.
    /// </returns>
    public bool Validate(
        string username,
        string password) =>
        !string.IsNullOrWhiteSpace(username)
        && _accounts.TryGetValue(
            username.Trim(),
            out var expectedPassword)
        && string.Equals(
            expectedPassword,
            password,
            StringComparison.Ordinal);
}

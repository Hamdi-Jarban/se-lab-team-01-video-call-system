using VideoCall.Server.Domain;

namespace VideoCall.Server.Domain.Repositories;

/// <summary>
/// يدير المستخدمين المتصلين والجلسات المرتبطة بهم.
/// </summary>
public interface IUserPresenceRepository
{
    /// <summary>
    /// يضيف مستخدمًا إلى قائمة المتصلين.
    /// </summary>
    /// <param name="username">اسم المستخدم.</param>
    /// <param name="session">جلسة المستخدم.</param>
    /// <returns>
    /// true عند نجاح الإضافة، وfalse إذا كان المستخدم موجودًا مسبقًا.
    /// </returns>
    bool TryAdd(string username, IClientHandler session);

    /// <summary>
    /// يبحث عن جلسة مستخدم متصل.
    /// </summary>
    /// <param name="username">اسم المستخدم.</param>
    /// <param name="session">الجلسة المرتبطة بالمستخدم.</param>
    /// <returns>true إذا تم العثور على المستخدم.</returns>
    bool TryGet(
        string username,
        out IClientHandler session);

    /// <summary>
    /// يتحقق من اتصال المستخدم حاليًا.
    /// </summary>
    /// <param name="username">اسم المستخدم.</param>
    /// <returns>true إذا كان المستخدم متصلًا.</returns>
    bool IsOnline(string username);

    /// <summary>
    /// يزيل المستخدم من قائمة المتصلين إذا كانت الجلسة الحالية
    /// هي نفسها الجلسة المتوقعة.
    /// </summary>
    /// <param name="username">اسم المستخدم.</param>
    /// <param name="expectedSession">الجلسة المراد التحقق منها.</param>
    /// <returns>true إذا تمت الإزالة بنجاح.</returns>
    bool Remove(
        string username,
        IClientHandler expectedSession);

    /// <summary>
    /// يعيد أسماء المستخدمين المتصلين حاليًا.
    /// </summary>
    IReadOnlyList<string> GetUsernames();

    /// <summary>
    /// يعيد جلسات المستخدمين المتصلين حاليًا.
    /// </summary>
    IReadOnlyList<IClientHandler> GetSessions();
}

namespace VideoCall.Server.Domain;

/// <summary>
/// مسؤول عن التحقق من صحة اسم المستخدم وكلمة المرور.
/// </summary>
public interface ICredentialValidator
{
    /// <summary>
    /// يتحقق من بيانات دخول المستخدم.
    /// </summary>
    /// <param name="username">اسم المستخدم.</param>
    /// <param name="password">كلمة المرور.</param>
    /// <returns>
    /// true إذا كانت البيانات صحيحة، وإلا false.
    /// </returns>
    bool Validate(string username, string password);
}

namespace VideoCall.Shared.Models;

// يمثل حساب مستخدم مسجل في النظام
public class User
{
    // اسم المستخدم
    public string Username { get; init; } = string.Empty;

    // كلمة مرور المستخدم
    public string Password { get; init; } = string.Empty;
}

// يمثل مستخدمًا متصلًا حاليًا بالنظام
public class OnlineUser
{
    // اسم المستخدم المتصل
    public string Username { get; init; } = string.Empty;

    // المعرّف الفريد لجلسة اتصال المستخدم
    public Guid SessionId { get; init; } = Guid.NewGuid();
}
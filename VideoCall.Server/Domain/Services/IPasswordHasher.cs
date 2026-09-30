namespace VideoCall.Server.Domain.Services;

// تجزئة كلمات المرور والتحقق منها (لا تُخزَّن كلمة المرور الأصلية أبدًا)
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string storedHash);
}

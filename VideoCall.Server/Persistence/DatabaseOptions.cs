namespace VideoCall.Server.Persistence;

// إعدادات قاعدة البيانات (تُقرأ من appsettings.json أو متغيرات البيئة، لا توجد قيم ثابتة داخل الكود)
public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "VideoCallDb";

    public string ConnectionString { get; }

    public DatabaseOptions(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                $"Connection string '{ConnectionStringName}' is missing. Set ConnectionStrings:{ConnectionStringName} in appsettings.json.",
                nameof(connectionString));
        }

        ConnectionString = connectionString;
    }
}

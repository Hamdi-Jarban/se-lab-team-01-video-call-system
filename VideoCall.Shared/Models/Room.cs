namespace VideoCall.Shared.Models;

// يمثل غرفة داخل النظام
public class Room
{
    // المعرّف الفريد للغرفة
    public string RoomId { get; init; } = string.Empty;

    // اسم المستخدم الذي أنشأ الغرفة
    public string Host { get; init; } = string.Empty;

    // قائمة المستخدمين الموجودين داخل الغرفة
    public HashSet<string> Members { get; init; } = new();

    // وقت إنشاء الغرفة بالتوقيت العالمي UTC
    public DateTime CreationTimeUtc { get; init; } = DateTime.UtcNow;
}
// استدعاء مكتبة JSON للتعامل مع تحويل الكائنات إلى JSON والعكس
using System.Text.Json;

// تحديد مساحة الأسماء التي ينتمي إليها هذا الكلاس
namespace VideoCall.Shared.Messages;

// تعريف كلاس Message، وهو المسؤول عن تمثيل الرسالة المتبادلة بين الطرفين
public sealed class Message 
{ 
    // تحديد نوع الرسالة
    public MessageType Type { get; init; } 

    // تخزين محتوى الرسالة بصيغة JSON
    public string Payload { get; init; } = string.Empty; 
 
    // دالة ثابتة لإنشاء رسالة من نوع معين وتحويل محتواها إلى JSON
    public static Message Create<T>(MessageType type, T payload) 
    { 
        // التأكد من أن محتوى الرسالة ليس فارغًا
        ArgumentNullException.ThrowIfNull(payload); 

        // إنشاء رسالة جديدة وتحديد نوعها وتحويل محتواها إلى JSON
        return new Message 
        { 
            Type = type, 
            Payload = JsonSerializer.Serialize(payload) 
        }; 
    } 
 
    // دالة لقراءة محتوى الرسالة وتحويل JSON إلى النوع المطلوب
    public T? ReadPayload<T>() 
    { 
        // إذا كان محتوى الرسالة فارغًا أو يحتوي على مسافات فقط، يتم إرجاع القيمة الافتراضية
        if (string.IsNullOrWhiteSpace(Payload)) return default; 

        try 
        { 
            // تحويل محتوى JSON إلى الكائن من النوع المطلوب
            return JsonSerializer.Deserialize<T>(Payload); 
        } 
        catch (JsonException) 
        { 
            // في حالة حدوث خطأ أثناء قراءة JSON يتم إرجاع القيمة الافتراضية
            return default; 
        } 
    } 
}


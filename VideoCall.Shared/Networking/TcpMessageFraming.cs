using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using VideoCall.Shared.Messages;

namespace VideoCall.Shared.Networking;

// مسؤول عن قراءة وإرسال الرسائل عبر اتصال TCP
public sealed class TcpMessageReaderWriter
{
    // عدد البايتات المستخدمة لتخزين طول الرسالة
    private const int LengthPrefixBytes = 4;

    // الحد الأقصى المسموح به لحجم الرسالة
    private const int MaxMessageSizeBytes = 10 * 1024 * 1024;

    // إعدادات تحويل الرسائل إلى JSON والعكس
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // مسار الشبكة المستخدم لإرسال واستقبال البيانات
    private readonly NetworkStream _stream;

    // قفل يمنع إرسال أكثر من رسالة في نفس الوقت
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // إنشاء كائن الاتصال باستخدام NetworkStream
    public TcpMessageReaderWriter(NetworkStream stream)
    {
        // التأكد من أن مسار الشبكة ليس فارغًا
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    // قراءة رسالة كاملة من اتصال TCP
    public async Task<Message?> ReadMessageAsync(CancellationToken ct)
    {
        // قراءة الجزء الذي يحتوي على طول الرسالة
        var prefix = await ReadExactAsync(LengthPrefixBytes, ct);

        // إذا لم تصل بيانات فهذا يعني أن الاتصال انتهى
        if (prefix is null) return null;

        // استخراج طول الرسالة من البيانات المرسلة
        var length = BinaryPrimitives.ReadInt32BigEndian(prefix);

        // التحقق من أن حجم الرسالة ضمن الحدود المسموحة
        if (length <= 0 || length > MaxMessageSizeBytes)
            throw new InvalidDataException($"Invalid message length: {length}.");

        // قراءة محتوى الرسالة حسب الطول المحدد
        var payload = await ReadExactAsync(length, ct);

        // التأكد من أن الاتصال لم ينقطع أثناء قراءة الرسالة
        if (payload is null)
            throw new EndOfStreamException("Connection closed in the middle of a message.");

        try
        {
            // تحويل بيانات JSON إلى كائن Message
            var message = JsonSerializer.Deserialize<Message>(payload, JsonOptions);

            // التأكد من أن الرسالة التي تم تحويلها ليست فارغة
            return message ?? throw new InvalidDataException("Message is null.");
        }
        catch (JsonException ex)
        {
            // التعامل مع الخطأ الناتج عن JSON غير صالح
            throw new InvalidDataException("Invalid JSON message.", ex);
        }
    }

    // إرسال رسالة عبر اتصال TCP
    public async Task WriteMessageAsync(Message message, CancellationToken ct)
    {
        // التأكد من أن الرسالة ليست فارغة
        ArgumentNullException.ThrowIfNull(message);

        // تحويل الرسالة إلى بيانات JSON بصيغة UTF-8
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        // التحقق من أن حجم الرسالة ضمن الحدود المسموحة
        if (payload.Length == 0 || payload.Length > MaxMessageSizeBytes)
            throw new InvalidDataException("Message size is outside the allowed range.");

        // إنشاء الجزء الذي سيحمل طول الرسالة
        var prefix = new byte[LengthPrefixBytes];

        // كتابة طول الرسالة باستخدام Big Endian
        BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);

        // انتظار الحصول على قفل الكتابة قبل إرسال الرسالة
        await _writeLock.WaitAsync(ct);

        try
        {
            // إرسال طول الرسالة أولًا
            await _stream.WriteAsync(prefix, ct);

            // ثم إرسال محتوى الرسالة
            await _stream.WriteAsync(payload, ct);

            // التأكد من إرسال البيانات الموجودة في الذاكرة المؤقتة
            await _stream.FlushAsync(ct);
        }
        finally
        {
            // تحرير قفل الكتابة بعد انتهاء عملية الإرسال
            _writeLock.Release();
        }
    }

    // قراءة عدد محدد من البايتات بشكل كامل من الاتصال
    private async Task<byte[]?> ReadExactAsync(int count, CancellationToken ct)
    {
        // إنشاء مصفوفة لتخزين البيانات المقروءة
        var buffer = new byte[count];

        // تحديد موضع الكتابة الحالي داخل المصفوفة
        var offset = 0;

        // الاستمرار في القراءة حتى يتم الحصول على العدد المطلوب من البايتات
        while (offset < count)
        {
            // قراءة البيانات من الاتصال
            var read = await _stream.ReadAsync(
                buffer.AsMemory(offset, count - offset), ct);

            // إذا لم تصل أي بيانات فهذا يعني أن الاتصال أغلق
            if (read == 0)
            {
                // إذا لم تتم قراءة أي جزء من الرسالة، يتم إرجاع null
                if (offset == 0) return null;

                // إذا انقطع الاتصال أثناء الرسالة يتم إطلاق استثناء
                throw new EndOfStreamException(
                    "Connection closed in the middle of a frame.");
            }

            // تحديث موضع الكتابة بعدد البايتات التي تمت قراءتها
            offset += read;
        }

        // إرجاع البيانات التي تمت قراءتها بالكامل
        return buffer;
    }
}
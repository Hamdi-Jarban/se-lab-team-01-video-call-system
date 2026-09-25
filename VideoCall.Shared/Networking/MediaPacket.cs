using System.Text;

namespace VideoCall.Shared.Networking;

// تحديد نوع البيانات التي يمكن أن تحملها حزمة الوسائط
public enum MediaType : byte
{
    // بيانات صوتية
    Audio = 1,

    // بيانات فيديو
    Video = 2,

    // بيانات المصافحة وبدء الاتصال
    Handshake = 3
}

// يمثل حزمة بيانات الوسائط التي يتم إرسالها عبر الشبكة
public class MediaPacket
{
    // الحجم الأساسي لرأس الحزمة قبل إضافة اسم المرسل
    public const int BaseHeaderSize = 16 + 16 + 1 + 4 + 8 + 2 + 2 + 2 + 4;

    // الحد الأقصى الآمن لحجم بيانات UDP داخل الحزمة
    public const int MaxSafeUdpPayload = 1200;

    // رمز جلسة الاتصال
    public Guid SessionToken { get; init; }

    // المعرّف الخاص بالمكالمة
    public Guid CallId { get; init; }

    // اسم المستخدم الذي أرسل الحزمة
    public string SenderUsername { get; init; } = string.Empty;

    // نوع بيانات الوسائط الموجودة في الحزمة
    public MediaType MediaType { get; init; }

    // الرقم التسلسلي للحزمة
    public uint SequenceNumber { get; init; }

    // الوقت المرتبط بالحزمة بوحدة Ticks
    public long TimestampTicks { get; init; }

    // رقم الجزء الحالي من الحزمة المجزأة
    public ushort FragmentIndex { get; init; }

    // العدد الإجمالي لأجزاء الحزمة
    public ushort FragmentCount { get; init; }

    // البيانات الفعلية الموجودة داخل الحزمة
    public byte[] Payload { get; init; } = Array.Empty<byte>();

    // تحويل الحزمة إلى مصفوفة من البايتات لإرسالها عبر الشبكة
    public byte[] Serialize()
    {
        // تحويل اسم المستخدم إلى بايتات باستخدام UTF-8
        var usernameBytes = Encoding.UTF8.GetBytes(SenderUsername ?? string.Empty);

        // التأكد من أن اسم المستخدم لا يتجاوز الحد المسموح
        if (usernameBytes.Length > ushort.MaxValue)
            throw new InvalidDataException("Sender username is too long.");

        // التأكد من أن حجم البيانات لا يتجاوز الحجم الآمن لـ UDP
        if (Payload.Length > MaxSafeUdpPayload)
            throw new InvalidDataException("Payload exceeds the safe UDP size.");

        // التحقق من صحة معلومات تجزئة الحزمة
        if (FragmentCount == 0 || FragmentIndex >= FragmentCount)
            throw new InvalidDataException("Invalid fragment metadata.");

        // حساب الحجم الإجمالي لرأس الحزمة مع اسم المستخدم
        var totalHeaderSize = BaseHeaderSize + usernameBytes.Length;

        // إنشاء المصفوفة التي ستحتوي على الرأس والبيانات
        var buffer = new byte[totalHeaderSize + Payload.Length];

        // تحديد الموضع الحالي داخل المصفوفة
        int offset = 0;

        // كتابة رمز الجلسة داخل الحزمة
        WriteGuid(buffer, ref offset, SessionToken);

        // كتابة معرّف المكالمة داخل الحزمة
        WriteGuid(buffer, ref offset, CallId);

        // كتابة نوع الوسائط
        buffer[offset++] = (byte)MediaType;

        // كتابة الرقم التسلسلي بصيغة Big Endian
        WriteUInt32BE(buffer, ref offset, SequenceNumber);

        // كتابة الوقت المرتبط بالحزمة
        WriteInt64BE(buffer, ref offset, TimestampTicks);

        // كتابة رقم الجزء الحالي
        WriteUInt16BE(buffer, ref offset, FragmentIndex);

        // كتابة عدد الأجزاء الكلي
        WriteUInt16BE(buffer, ref offset, FragmentCount);

        // كتابة طول اسم المستخدم
        WriteUInt16BE(buffer, ref offset, (ushort)usernameBytes.Length);

        // نسخ اسم المستخدم إلى الحزمة
        Buffer.BlockCopy(usernameBytes, 0, buffer, offset, usernameBytes.Length);

        // تحديث موضع الكتابة بعد اسم المستخدم
        offset += usernameBytes.Length;

        // كتابة طول بيانات Payload
        WriteInt32BE(buffer, ref offset, Payload.Length);

        // نسخ بيانات Payload إلى الحزمة
        Buffer.BlockCopy(Payload, 0, buffer, offset, Payload.Length);

        // إرجاع الحزمة بعد تحويلها إلى مصفوفة بايتات
        return buffer;
    }

    // محاولة تحويل مصفوفة البايتات المستلمة إلى حزمة وسائط
    public static MediaPacket? TryDeserialize(byte[] data, int length)
    {
        // التأكد من أن البيانات تحتوي على الحد الأدنى من حجم الرأس
        if (length < BaseHeaderSize)
        {
            return null;
        }

        // تحديد موضع القراءة الحالي
        int offset = 0;

        // قراءة رمز الجلسة
        var sessionToken = ReadGuid(data, ref offset);

        // قراءة معرّف المكالمة
        var callId = ReadGuid(data, ref offset);

        // قراءة نوع الوسائط
        var mediaType = (MediaType)data[offset++];

        // التحقق من أن نوع الوسائط معروف
        if (mediaType is not (MediaType.Audio or MediaType.Video or MediaType.Handshake))
            return null;

        // قراءة الرقم التسلسلي
        var seq = ReadUInt32BE(data, ref offset);

        // قراءة الوقت المرتبط بالحزمة
        var ts = ReadInt64BE(data, ref offset);

        // قراءة رقم الجزء الحالي
        var fragIndex = ReadUInt16BE(data, ref offset);

        // قراءة العدد الإجمالي للأجزاء
        var fragCount = ReadUInt16BE(data, ref offset);

        // قراءة طول اسم المستخدم
        var usernameLen = ReadUInt16BE(data, ref offset);

        // التأكد من وجود اسم المستخدم كاملًا داخل البيانات
        if (offset + usernameLen > length)
            return null;

        // تحويل اسم المستخدم من UTF-8 إلى نص
        var senderUsername = Encoding.UTF8.GetString(data, offset, usernameLen);

        // تحديث موضع القراءة بعد اسم المستخدم
        offset += usernameLen;

        // التأكد من وجود أربعة بايتات على الأقل لطول Payload
        if (offset + 4 > length)
            return null;

        // قراءة طول بيانات Payload
        var payloadLength = ReadInt32BE(data, ref offset);

        // التحقق من صحة حجم Payload ومعلومات التجزئة
        if (payloadLength < 0 ||
            payloadLength > MaxSafeUdpPayload ||
            offset + payloadLength > length ||
            fragCount == 0 ||
            fragIndex >= fragCount)
        {
            return null;
        }

        // إنشاء مصفوفة لتخزين بيانات Payload
        var payload = new byte[payloadLength];

        // نسخ بيانات Payload من الحزمة المستلمة
        Buffer.BlockCopy(data, offset, payload, 0, payloadLength);

        // إنشاء MediaPacket باستخدام البيانات التي تمت قراءتها
        return new MediaPacket
        {
            SessionToken = sessionToken,
            CallId = callId,
            SenderUsername = senderUsername,
            MediaType = mediaType,
            SequenceNumber = seq,
            TimestampTicks = ts,
            FragmentIndex = fragIndex,
            FragmentCount = fragCount,
            Payload = payload
        };
    }

    // كتابة Guid داخل مصفوفة البايتات
    private static void WriteGuid(byte[] buffer, ref int offset, Guid value)
    {
        value.TryWriteBytes(buffer.AsSpan(offset, 16));
        offset += 16;
    }

    // قراءة Guid من مصفوفة البايتات
    private static Guid ReadGuid(byte[] buffer, ref int offset)
    {
        var g = new Guid(buffer.AsSpan(offset, 16));
        offset += 16;
        return g;
    }

    // كتابة قيمة UInt32 بصيغة Big Endian
    private static void WriteUInt32BE(byte[] buffer, ref int offset, uint value)
    {
        buffer[offset++] = (byte)(value >> 24);
        buffer[offset++] = (byte)(value >> 16);
        buffer[offset++] = (byte)(value >> 8);
        buffer[offset++] = (byte)value;
    }

    // قراءة قيمة UInt32 بصيغة Big Endian
    private static uint ReadUInt32BE(byte[] buffer, ref int offset)
    {
        uint value = (uint)((buffer[offset] << 24) |
                            (buffer[offset + 1] << 16) |
                            (buffer[offset + 2] << 8) |
                            buffer[offset + 3]);

        offset += 4;
        return value;
    }

    // كتابة قيمة Int32 بصيغة Big Endian
    private static void WriteInt32BE(byte[] buffer, ref int offset, int value) =>
        WriteUInt32BE(buffer, ref offset, unchecked((uint)value));

    // قراءة قيمة Int32 بصيغة Big Endian
    private static int ReadInt32BE(byte[] buffer, ref int offset) =>
        unchecked((int)ReadUInt32BE(buffer, ref offset));

    // كتابة قيمة Int64 بصيغة Big Endian
    private static void WriteInt64BE(byte[] buffer, ref int offset, long value)
    {
        for (int i = 7; i >= 0; i--)
        {
            buffer[offset++] = (byte)(value >> (i * 8));
        }
    }

    // قراءة قيمة Int64 بصيغة Big Endian
    private static long ReadInt64BE(byte[] buffer, ref int offset)
    {
        long value = 0;

        for (int i = 0; i < 8; i++)
        {
            value = (value << 8) | buffer[offset++];
        }

        return value;
    }

    // كتابة قيمة UInt16 بصيغة Big Endian
    private static void WriteUInt16BE(byte[] buffer, ref int offset, ushort value)
    {
        buffer[offset++] = (byte)(value >> 8);
        buffer[offset++] = (byte)value;
    }

    // قراءة قيمة UInt16 بصيغة Big Endian
    private static ushort ReadUInt16BE(byte[] buffer, ref int offset)
    {
        ushort value = (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
        offset += 2;
        return value;
    }
}
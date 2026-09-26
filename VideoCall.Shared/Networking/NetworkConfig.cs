// تحديد المساحة الاسمية الخاصة بإعدادات الاتصال الشبكي المشتركة
namespace VideoCall.Shared.Networking;

// فئة ثابتة تحتوي على إعدادات الشبكة المستخدمة في المشروع
public static class NetworkConfig
{
    // منفذ TCP المستخدم لاتصالات التحكم وإرسال الرسائل بين العميل والخادم
    public const int TcpControlPort = 5000;

    // منفذ UDP المستخدم لإرسال بيانات الوسائط مثل الصوت والفيديو
    public const int UdpMediaPort = 5001;
}
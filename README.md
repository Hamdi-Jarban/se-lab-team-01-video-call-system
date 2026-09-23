<div align="center">

# 🎥 VideoCallSystem

**Real-Time Audio & Video Communication System — Client/Server Architecture**

نظام سطح مكتب متكامل للتواصل الصوتي والمرئي المباشر عبر الشبكات المحلية والمباشرة دون الحاجة لخدمات سحابية خارجية.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=.net&logoColor=white)]()
[![C#](https://img.shields.io/badge/C%23-WPF-239120?style=for-the-badge&logo=c-sharp&logoColor=white)]()
[![Status](https://img.shields.io/badge/Status-Educational%20Project-blue?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-Academic-lightgrey?style=for-the-badge)]()

</div>

---

## 📌 نظرة عامة

يقدم **VideoCallSystem** حلًا برمجياً متكاملاً لسطح المكتب يُتيح للمستخدمين تسجيل الدخول، واستعراض المتواجدين لحظياً، وبدء مكالمات صوتية ومرئية بجودة عالية.

يعتمد النظام على بنية **Client/Server** حيث يتم فصل مهام التحكم والوسائط كالتالي:
* **بروتوكول TCP:** معالجة الإشارات، الاتصال، إدارات الجلسات، طلبات المكالمات، وتحديث حالة التواجد (حيث تكون الدقة والموثوقية أولوية).
* **بروتوكول UDP:** نقل بث الصوت والفيديو المباشر (حيث تكون السرعة وقيم الـ Latency المنخفضة هي الأهم).

---

## ✨ المزايا الأساسية

| الميزة | الوصف |
| :--- | :--- |
| 🔐 **تسجيل الدخول** | الدخول للنظام وإدارة التواجد باسم مستخدم معرّف |
| 👥 **المستخدمون المتصلون** | القائمة التفاعلية المحدثة لحظياً للمستخدمين النشطين |
| 📞 **إشارات المكالمات** | إرسال وبث طلبات الاتصال مع خيارات القبول أو الرفض |
| 🎥 **مكالمات الفيديو** | بث مرئي مستمر بمنخفض التأخير (Low Latency) |
| 🎙️ **المعالجة الصوتية** | بث صوتي مباشر مع تقنية إلغاء الصدى الصوتي (AEC) |
| 🎛️ **التحكم بالميديا** | إمكانية تشغيل/إيقاف الكاميرا وكتم/تفعيل الميكروفون أثناء المكالمة |
| ❌ **إدارة الجلسات** | إمكانية إنهاء المكالمة فوراً من أي طرف |

---

## 🏗️ البنية المعمارية

```text
                     ┌──────────────────────┐
                     │        SERVER        │
                     │  Connection Manager  │
                     │  User Management     │
                     │  Call Signaling      │
                     └──────────┬───────────┘
                                │
                  ┌─────────────┴─────────────┐
                  ▼                           ▼
        ┌─────────────────┐         ┌─────────────────┐
        │    CLIENT 1     │◄───────►│    CLIENT 2     │
        │  WPF Interface  │  TCP /  │  WPF Interface  │
        │  Camera / Mic   │   UDP   │  Camera / Mic   │
        └─────────────────┘         └─────────────────┘
```

---

## 🛠️ التقنيات المستخدمة

* **اللغة والمنصة:** C# / .NET 8
* **واجهة المستخدم:** WPF (Windows Presentation Foundation)
* **بروتوكولات الشبكة:** TCP/IP للتحكم والإشارة | UDP لنقل بث الصوت والفيديو
* **معالجة الصوت:** NAudio
* **معالجة الفيديو والرؤية الحاسوبية:** OpenCvSharp
* **إدارة الإصدارات:** Git & GitHub

---

## 📂 هيكل المشروع

```text
VideoCallSystem/
├── VideoCallSystem.sln            # ملف الحل الرئيسي
├── VideoCall.Server/              # منطق الخادم، إدارة الاتصالات والمستخدمين
├── VideoCall.Client/              # واجهة WPF، معالجة الصوت والفيديو
├── VideoCall.Shared/              # النماذج والرسائل المشتركة بين Client وServer
├── docs/
│   └── SRS.md                     # وثيقة متطلبات النظام
├── AI_Log.md                      # سجل توثيق استخدام أدوات الذكاء الاصطناعي
├── README.md                      # التوثيق الرئيسي
└── .gitignore                     # استثناء ملفات البناء المؤقتة
```

---

## ▶️ التشغيل والإعداد

### المتطلبات الأساسية
* نظام تشغيل **Windows 10/11**
* **.NET 8 SDK** / Desktop Runtime
* كاميرا وميكروفون يعملان بشكل صحيح
* اتصال شبكي فعال بين الأجهزة (LAN أو Localhub)

### خطوات التشغيل
1. **تشغيل الخادم (Server):**
   ```bash
   dotnet run --project VideoCall.Server
   ```
2. **تشغيل العميل (Client):**
   ```bash
   dotnet run --project VideoCall.Client
   ```
> **ملاحظة:** يمكن تشغيل عدة نسخ من الـ Client على أجهزة مختلفة أو على نفس الجهاز لاختبار المكالمات المتبادلة.

---

## 📚 وثائق المشروع

* 📄 [وثيقة متطلبات البرمجيات — SRS.md](docs/SRS.md)
* 🤖 [سجل استخدام الذكاء الاصطناعي — AI_Log.md](AI_Log.md)

---

## 👥 فريق العمل

* **حمدي عبدالله ناصر جربان**
* **هشام زيد ثابت الأديب**
* **ياسر محمد السري**

---

## 🔄 منهجية سير العمل (Workflow)

نتبع دورة عمل **Git/GitHub Flow** قياسية لضمان جودة الكود والعمل الجماعي:

$$ \text{Requirement} \longrightarrow \text{Issue} \longrightarrow \text{Branch} \longrightarrow \text{Commit} \longrightarrow \text{Push} \longrightarrow \text{Pull Request} \longrightarrow \text{Review} \longrightarrow \text{Merge} $$

* تبدأ كل مهمة بفتح **Issue** مخصصة مرتبطة بوثيقة المتطلبات `docs/SRS.md`.
* يتم التطوير على فرع مستقل بصيغة: `type/issue-number-short-description`.
* لا يُدمج أي كود إلى الفرع الرئيسي إلا عبر **Pull Request (PR)** وبعد مراجعة معتمدة من باقي أفراد الفريق.
* تدار وتتابع حالات المهام عبر **GitHub Project Board**:
  `Backlog ──> Ready ──> In Progress ──> In Review ──> Done`

---

## 🔒 ملاحظة أمنية

> **تنبيه:** هذا المشروع صُمم لأغراض تعليمية وأكاديمية. لا تتضمن النسخة الحالية طبقات تشفير للاتصالات (Encryption) أو نظام مصادقة آمن (Secure Authentication)، وهي خارج نطاق الإصدار الحالي.

---

<div align="center">
  <sub>مشروع أكاديمي تعليمي — تم التطوير بواسطة فريق العمل</sub>
</div>
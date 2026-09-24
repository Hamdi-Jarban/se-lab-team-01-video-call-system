# AI Usage Log

## Entry 01

### Date
2026-09-23

### Student
حمدي

### Tool
Claude (Anthropic)

### Purpose
صياغة مسودة أولية لوثيقة SRS-Mini، وIssues، وUser Stories، وAcceptance Criteria، وEdge Cases، وREADME.md لمشروع VideoCallSystem.

### Prompt Summary
طلبنا من الأداة إعداد مسودة SRS-Mini وIssues وREADME بناءً على الوصف الفعلي للمشروع.

### AI Suggestions
- ثمانية متطلبات وظيفية (FR-01 إلى FR-08) مبنية على ميزات المشروع الفعلية.
- خمسة متطلبات غير وظيفية (NFR-01 إلى NFR-05).
- سبع Issues كاملة.
- قالب README.md مختصر.

### Accepted Suggestions
- جميع المتطلبات الوظيفية وغير الوظيفية.
- Issues أرقام 1 إلى 7.
- قالب README.md.

### Rejected Suggestions
لا يوجد رفض مباشر.

### Reason for Rejection
لا ينطبق.

### Human Review
راجع الطالب المخرجات وتأكد من توافق الوصف مع المشروع.

---

## Entry 02

### Date
2026-09-24

### Student
هشام زيد الأديب

### Tool
Gemini (Google)

### Purpose
إنجاز وتطبيق المهمة الأولى (Issue #1: تسجيل الدخول باستخدام اسم المستخدم).

### Prompt Summary
- طلب إضافة تعليقات عربية احترافية لكود LoginViewModel.cs.
- طلب تنقية وتصفية ملفات الرسائل المشتركة لتشمل أكواد المصادقة والأخطاء الخاصة بالمهمة فقط.

### AI Suggestions
- توثيق كود LoginViewModel.cs بتعليقات عربية.
- حصر محتويات MessageType.cs و Payloads.cs في نطاق المصادقة لضمان Clean PR.

### Accepted Suggestions
- قبول التعليقات المضافة.
- قبول هيكلية الملفات المشتركة المخصصة للمهمة #1.

### Rejected Suggestions
لا يوجد رفض.

### Reason for Rejection
لا ينطبق.

### Human Review
قام الطالب باختبار بناء المشروع وتشغيل نافذة تسجيل الدخول محلياً والتأكد من عمل الواجهة بشكل سليم قبل الرفع.

---

# AI Usage — FR-03 using manus

1. تم استخدام الذكاء الاصطناعي لتحليل المشروع المرجعي وتحديد الملفات والأجزاء المتعلقة بإرسال طلب المكالمة فقط.
2. ساعد الذكاء الاصطناعي في اقتراح كود CallRequest، وحالة انتظار الرد، ومنع الطلبات المتكررة، ومعالجة الخطأ والمهلة.
3. ساعد أيضاً في تصحيح واجهة XAML وشرح أخطاء الأنواع، مثل الفرق بين ErrorPayload و CallErrorPayload.
4. تمت مراجعة الاقتراحات وتعديلها لتتوافق مع بنية المشروع ونطاق FR-03، ولم يعتمد أي كود دون مراجعة.

**الاسم:** حمدي جربان
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
طلبنا من الأداة إعداد مسودة SRS-Mini وIssues وREADME بناءً على الوصف الفعلي للمشروع (README الأصلي وبنية الكود الموجودة)، مع الالتزام بقالب المحاضرة الأولى لمقرر هندسة البرمجيات.

### AI Suggestions
- ثمانية متطلبات وظيفية (FR-01 إلى FR-08) مبنية على ميزات المشروع الفعلية (تسجيل دخول، قائمة مستخدمين، طلب مكالمة، قبول/رفض، فيديو/صوت، تحكم بالكاميرا والميكروفون، إنهاء المكالمة).
- خمسة متطلبات غير وظيفية (NFR-01 إلى NFR-05) تتعلق باستخدام TCP/UDP وزمن الاستجابة والاستقرار عند الانقطاع.
- سبع Issues كاملة (User Story + Acceptance Criteria + Edge Cases + Definition of Done).
- قالب README.md مختصر حسب صيغة المحاضرة.

### Accepted Suggestions
- جميع المتطلبات الوظيفية وغير الوظيفية بعد التأكد من ارتباطها بالكود الفعلي للمشروع.
- Issues أرقام 1 إلى 7 كأساس لتقسيم العمل على الفريق.
- قالب README.md.

### Rejected Suggestions
لا يوجد رفض مباشر؛ الاقتراحات كانت مبنية على وصف المشروع الحقيقي الذي زودت به الأداة.

### Reason for Rejection
لا ينطبق.

### Human Review
راجع الطالب المخرجات وتأكد من توافقها مع بنية المشروع الفعلية (VideoCall.Client / VideoCall.Server / VideoCall.Shared) قبل اعتمادها في المستودع. أسماء أعضاء الفريق في README.md لم تُملأ بعد وتحتاج مراجعة بشرية قبل الرفع.

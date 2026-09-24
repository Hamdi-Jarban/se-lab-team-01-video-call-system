using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Application;

// Issue #3:
// يعتمد هذا الكلاس على Conversation وConversationState
// من المشروع المشترك لإدارة طلبات المكالمات الخاصة.
public sealed class ConversationService : IConversationRepository
{
    // Issue #3:
    // يستخدم القفل لمنع إنشاء طلبين متزامنين في نفس الوقت.
    private readonly object _gate = new();

    // Issue #3:
    // تخزين المحادثات الخاصة النشطة وطلبات المكالمات قيد الانتظار.
    private readonly Dictionary<string, Conversation> _conversations =new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Issue #3:
    /// التحقق مما إذا كان المستخدم لديه طلب مكالمة
    /// أو محادثة خاصة نشطة.
    /// </summary>
    /// <param name="username">اسم المستخدم المطلوب التحقق منه.</param>
    /// <returns>
    /// true إذا كان المستخدم مشغولاً،
    /// وfalse إذا كان يستطيع استقبال طلب جديد.
    /// </returns>
    public bool IsUserBusy(string username)
    {
        // Issue #3:
        // الاسم الفارغ لا يمثل مستخدماً صالحاً، لذلك لا يعتبر مشغولاً.
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        // Issue #3:
        // حماية قراءة قائمة المحادثات أثناء وصول طلبات متزامنة.
        lock (_gate)
        {
            return _conversations.Values.Any(conversation =>
                // Created تعني أن الطلب ما زال ينتظر رد المستخدم.
                // Active تعني أن المكالمة بدأت بالفعل.
                (conversation.State is
                    ConversationState.Created or
                    ConversationState.Active) &&

                // التحقق من وجود المستخدم ضمن أعضاء المحادثة.
                conversation.Members.Contains(username));
        }
    }

    /// <summary>
    /// Issue #3:
    /// إنشاء محادثة خاصة جديدة عند إرسال طلب مكالمة.
    /// تبدأ المحادثة بالحالة Created حتى يرد المستخدم المستهدف.
    /// </summary>
    /// <param name="conversationId">المعرّف الفريد للمكالمة.</param>
    /// <param name="caller">اسم المستخدم الذي أرسل الطلب.</param>
    /// <param name="callee">اسم المستخدم المستهدف.</param>
    /// <param name="conversation">المحادثة التي تم إنشاؤها.</param>
    /// <returns>نتيجة عملية إنشاء المحادثة.</returns>
    public ConversationOperation CreatePrivate(string conversationId,string caller,string callee,out Conversation? conversation)
    {
        conversation = null;
        // Issue #3:
        // التحقق من البيانات الأساسية قبل إنشاء المحادثة.
        if (string.IsNullOrWhiteSpace(conversationId) ||
            string.IsNullOrWhiteSpace(caller) ||
            string.IsNullOrWhiteSpace(callee) ||
            caller.Equals(
                callee,
                StringComparison.OrdinalIgnoreCase))
        {
            // منع الاتصال بالنفس أو إنشاء محادثة ببيانات ناقصة.
            return ConversationOperation.InvalidType;
        }

        // Issue #3:
        // تنفيذ الفحص والإنشاء داخل قفل واحد لمنع
        // تجاوز فحص الانشغال عند وصول طلبين في الوقت نفسه.
        lock (_gate)
        {
            // Issue #3:
            // منع إرسال طلب جديد إذا كان المرسل أو المستهدف
            // لديه طلب مكالمة أو محادثة نشطة.
            if (IsUserBusyUnsafe(caller) ||
                IsUserBusyUnsafe(callee))
            {
                return ConversationOperation.Busy;
            }

            // Issue #3:
            // التأكد من عدم استخدام نفس معرّف المحادثة مسبقاً.
            if (_conversations.ContainsKey(conversationId))
            {
                return ConversationOperation.AlreadyExists;
            }

            // Issue #3:
            // إنشاء محادثة خاصة بحالة Created.
            // هذه الحالة تعني أن الطلب أُرسل ولم تتم الموافقة عليه بعد.
            var item = new Conversation
            {
                Id = conversationId,
                Type = ConversationType.Private,
                Host = caller,
                State = ConversationState.Created
            };

            // Issue #3:
            // إضافة المرسل والمستخدم المستهدف إلى المحادثة.
            item.Members.Add(caller);
            item.Members.Add(callee);

            // Issue #3:
            // حفظ المحادثة حتى يتم اعتبار الطرفين مشغولين
            // ومنع إرسال طلب آخر قبل انتهاء الطلب الحالي.
            _conversations.Add(item.Id, item);

            // إعادة المحادثة التي تم إنشاؤها إلى المستدعي.
            conversation = item;

            return ConversationOperation.Success;
        }
    }

    /// <summary>
    /// Issue #3:
    /// فحص داخلي لانشغال المستخدم أثناء وجود القفل.
    /// لا يستخدم lock داخلياً لأنه يُستدعى من داخل lock آخر.
    /// </summary>
    private bool IsUserBusyUnsafe(string username)
    {
        return _conversations.Values.Any(conversation =>
            // Created: طلب مكالمة ينتظر الرد.
            // Active: مكالمة جارية.
            (conversation.State is
                ConversationState.Created or
                ConversationState.Active) &&

            // التحقق من أن المستخدم مشارك في المحادثة.
            conversation.Members.Contains(username));
    }
}

using Microsoft.VisualBasic;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Domain.Repositories;

public interface IConversationRepository
{
    // Issue #3:
    // يعتمد عليه ProtocolRouter لمنع طلبات المكالمات المتزامنة.
    bool IsUserBusy(string username);

    // Issue #3:
    // إنشاء محادثة بحالة Created عند إرسال الطلب.
    ConversationOperation CreatePrivate(
        string conversationId,
        string caller,
        string callee,
        out Conversation? conversation);
}

public enum ConversationOperation
{
    Success,
    AlreadyExists,
    NotFound,
    InvalidType,
    Busy
}

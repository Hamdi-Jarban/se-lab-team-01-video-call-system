using Microsoft.VisualBasic;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Application;

public sealed class ConversationService : IConversationRepository
{
    private readonly object _gate = new();

    private readonly Dictionary<string, Conversation> _conversations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Issue #3:
    /// التحقق من وجود طلب أو مكالمة نشطة للمستخدم.
    /// Created تعني أن الطلب ينتظر الرد.
    /// Active تعني أن المكالمة بدأت.
    /// </summary>
    public bool IsUserBusy(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        lock (_gate)
        {
            return _conversations.Values.Any(conversation =>
                conversation.State is
                    ConversationState.Created or
                    ConversationState.Active &&
                conversation.Members.Contains(username));
        }
    }

    /// <summary>
    /// Issue #3:
    /// إنشاء محادثة خاصة بحالة Created.
    /// </summary>
    public ConversationOperation CreatePrivate(
        string conversationId,
        string caller,
        string callee,
        out Conversation? conversation)
    {
        conversation = null;

        if (string.IsNullOrWhiteSpace(conversationId) ||
            string.IsNullOrWhiteSpace(caller) ||
            string.IsNullOrWhiteSpace(callee) ||
            caller.Equals(
                callee,
                StringComparison.OrdinalIgnoreCase))
        {
            return ConversationOperation.InvalidType;
        }

        lock (_gate)
        {
            // Issue #3:
            // منع الطلبات المتزامنة أو الاتصال بمستخدم مشغول.
            if (IsUserBusyUnsafe(caller) ||
                IsUserBusyUnsafe(callee))
            {
                return ConversationOperation.Busy;
            }

            if (_conversations.ContainsKey(conversationId))
            {
                return ConversationOperation.AlreadyExists;
            }

            var item = new Conversation
            {
                Id = conversationId,
                Type = ConversationType.Private,
                Host = caller,
                State = ConversationState.Created
            };

            item.Members.Add(caller);
            item.Members.Add(callee);

            _conversations.Add(item.Id, item);

            conversation = item;

            return ConversationOperation.Success;
        }
    }

    private bool IsUserBusyUnsafe(string username)
    {
        return _conversations.Values.Any(conversation =>
            conversation.State is
                ConversationState.Created or
                ConversationState.Active &&
            conversation.Members.Contains(username));
    }
}

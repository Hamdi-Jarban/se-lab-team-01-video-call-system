namespace VideoCall.Server.Domain.Repositories;

// الوصول إلى جدولي Conversations و ConversationMembers.
// ملاحظة: IConversationRepository الموجود مسبقًا يمثل الغرف/المكالمات الحية في الذاكرة (Runtime)،
// لذلك سُمّيت هذه الواجهة IChatConversationRepository لتفادي الخلط بين المفهومين.
public interface IChatConversationRepository
{
    // يعيد المحادثة الخاصة الموجودة أو ينشئها (بدون تكرار حتى مع الطلبات المتزامنة)
    Task<(int ConversationId, bool Created)> GetOrCreatePrivateAsync(int userA, int userB, CancellationToken ct);

    // ينشئ المجموعة وأعضاءها داخل Transaction واحدة
    Task<int> CreateGroupAsync(string name, int creatorId, IReadOnlyCollection<int> memberIds, CancellationToken ct);

    Task<ChatConversationRecord?> GetAsync(int conversationId, CancellationToken ct);

    Task<bool> IsActiveMemberAsync(int conversationId, int userId, CancellationToken ct);

    Task<IReadOnlyList<ChatMemberRecord>> GetActiveMembersAsync(int conversationId, CancellationToken ct);

    // يعيد معرّفات المستخدمين الذين أُضيفوا فعليًا (يتجاهل الأعضاء الحاليين)
    Task<IReadOnlyList<int>> AddMembersAsync(int conversationId, IReadOnlyCollection<int> userIds, CancellationToken ct);

    Task<bool> RemoveMemberAsync(int conversationId, int userId, CancellationToken ct);

    Task<IReadOnlyList<ChatConversationRecord>> GetForUserAsync(int userId, CancellationToken ct);

    // أعضاء جميع محادثات المستخدم في استعلام واحد
    Task<IReadOnlyList<ChatMemberRecord>> GetMembersForUserConversationsAsync(int userId, CancellationToken ct);
}

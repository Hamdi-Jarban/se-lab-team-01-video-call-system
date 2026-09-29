using VideoCall.Shared.Models;

namespace VideoCall.Server.Domain.Repositories;

// سجل مستخدم كما هو مخزن في قاعدة البيانات
public sealed record UserRecord(
    int UserId,
    string Username,
    string PasswordHash,
    string DisplayName,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool IsActive);

// سجل محادثة (خاصة أو جماعية)
public sealed record ChatConversationRecord(
    int ConversationId,
    ConversationType Type,
    string? Name,
    int CreatedByUserId,
    string CreatedByUsername,
    DateTime CreatedAt);

// عضو نشط في محادثة
public sealed record ChatMemberRecord(
    int ConversationId,
    int UserId,
    string Username,
    string DisplayName);

// رسالة محفوظة مع بيانات المرسل
public sealed record ChatMessageRecord(
    long MessageId,
    int ConversationId,
    int SenderId,
    string SenderUsername,
    string SenderDisplayName,
    string Content,
    DateTime SentAt,
    DateTime? EditedAt = null);

// نوع المكالمة في سجل المكالمات
public enum CallType : byte
{
    Private = 0,
    Room = 1
}

// حالة المكالمة في سجل المكالمات
public enum CallStatus : byte
{
    Ringing = 0,
    Connected = 1,
    Rejected = 2,
    Ended = 3,
    TimedOut = 4,
    Interrupted = 5
}

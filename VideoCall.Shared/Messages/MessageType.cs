namespace VideoCall.Shared.Messages;

/// íÍÏÏ ÌãíÚ ÃäæÇÚ ÇáÑÓÇÆá ÇáãÊÈÇÏáÉ Èíä ÇáÚãíá æÇáÎÇÏã ÚÈÑ ÇáÔÈßÉ (TCP).
/// ßá äæÚ íÑÇİŞå ßÇÆä (Payload) íÍãá ÇáÈíÇäÇÊ ÇáÎÇÕÉ Èå.
public enum MessageType
{
    // === ŞÓã ÇáãÕÇÏŞÉ æÊÓÌíá ÇáÏÎæá (ÇáĞí äÚãá Úáíå ÍÇáíÇğ) ===
    LoginRequest,
    LoginResponse,
    // === ÍÇáÉ ÇáãÓÊÎÏãíä (ÊÍÏíË ŞÇÆãÉ ÇáãÊÕáíä) ===
    OnlineUsersUpdate,
    // === ÅÏÇÑÉ ÇáãßÇáãÇÊ (ØáÈ¡ ŞÈæá¡ ÑİÖ¡ ÅäåÇÁ) ===
    CallRequest,
    CallAccepted,
    CallRejected,
    CallEnded,
    CallTimedOut,
    CallError,
    // === ÅÏÇÑÉ ÇáÛÑİ ÇáÌãÇÚíÉ æÇáÏÚæÇÊ ===
    CreateRoomRequest,
    AddUserToRoomRequest,
    JoinRoomRequest,
    LeaveRoomRequest,
    RoomUpdate,
    RoomError,
    StartRoomMedia,
    StopRoomMedia,
    RoomMediaStarted,
    RoomMediaStopped,
    RoomInvite,
    RoomInviteAccepted,
    RoomInviteRejected,
    // === ÇáÃÎØÇÁ æÇáÑÓÇÆá ÇáÚÇãÉ ááäÙÇã ===
    Error,
    Disconnect,
    StopConversationMedia,
    StartConversationMedia
}

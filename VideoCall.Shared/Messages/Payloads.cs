namespace VideoCall.Shared.Messages;

//  «·„’«œﬁ… Ê ”ÃÌ· «·œŒÊ· (Auth) 

public record LoginRequestPayload(string Username, string Password);

public record LoginResponsePayload(bool Success, string? ErrorCode, string? Username, Guid? SessionToken);

//  Õ«·… «·„” Œœ„Ì‰ (Presence) 

public record OnlineUsersUpdatePayload(List<string> Usernames);

//  ≈œ«—… «·„ﬂ«·„«  (Call signaling) 

public record CallRequestPayload(Guid CallId, string Caller, string Callee);

public record CallAcceptedPayload(Guid CallId, string Caller, string Callee);

public record CallRejectedPayload(Guid CallId, string Caller, string Callee);

public record CallEndedPayload(Guid CallId, string EndedBy);

public record CallTimedOutPayload(Guid CallId);

public record CallErrorPayload(string ErrorCode, string Message);

//  «·€—› «·Ã„«⁄Ì… (Rooms) 

public record CreateRoomRequestPayload(string RoomId);

public record AddUserToRoomRequestPayload(string RoomId, string Username);

public record JoinRoomRequestPayload(string RoomId);

public record LeaveRoomRequestPayload(string RoomId);

public record RoomUpdatePayload(string RoomId, string Host, List<string> Members);

public record RoomErrorPayload(string ErrorCode, string Message);

//  Ê”«∆ÿ «·€—› «·Ã„«⁄Ì… (Group media) 

public record StartRoomMediaPayload(string RoomId, Guid MediaId);

public record StopRoomMediaPayload(string RoomId, Guid MediaId);

public record RoomMediaPayload(string RoomId, Guid MediaId);

public record RoomInvitePayload(Guid InviteId, string RoomId, string Host, string Invitee);
public record RoomInviteAcceptedPayload(Guid InviteId, string RoomId, string Invitee);
public record RoomInviteRejectedPayload(Guid InviteId, string RoomId, string Invitee);

//  ⁄«„ (Generic) 

public record ErrorPayload(string ErrorCode, string Message);

/// —„Ê“ «·√Œÿ«¡ «·ﬁÌ«”Ì… «·„‘ —ﬂ… »Ì‰ «·Œ«œ„ Ê«·⁄„Ì·.
/// Ì⁄ „œ ⁄·ÌÂ« «·⁄„Ì· · —Ã„… «·—„“ ≈·Ï —”«·… ‰’Ì… ⁄—»Ì… ›Ì «·Ê«ÃÂ…°
/// „„« Ì€‰Ì «·Œ«œ„ ⁄‰ «· ⁄«„· „⁄ «·‰’Ê’ «·„ —Ã„….
public static class ErrorCodes
{
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AlreadyLoggedIn = "ALREADY_LOGGED_IN";
    public const string TargetOffline = "TARGET_OFFLINE";
    public const string TargetBusy = "TARGET_BUSY";
    public const string CallNotFound = "CALL_NOT_FOUND";
    public const string NotYourCall = "NOT_YOUR_CALL";
    public const string InvalidCallState = "INVALID_CALL_STATE";
    public const string RoomAlreadyExists = "ROOM_ALREADY_EXISTS";
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string RoomFull = "ROOM_FULL";
    public const string MediaAlreadyStarted = "MEDIA_ALREADY_STARTED";
    public const string MediaNotStarted = "MEDIA_NOT_STARTED";
    public const string NotRoomMember = "NOT_ROOM_MEMBER";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string ServerUnavailable = "SERVER_UNAVAILABLE";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}
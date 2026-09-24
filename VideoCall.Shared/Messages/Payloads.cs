using System;

namespace VideoCall.Shared.Messages;

// === Issue #1: «·„’«œﬁ… Ê ”ÃÌ· «·œŒÊ· (Â‘«„) ===
public record LoginRequestPayload(string Username, string Password);
public record LoginResponsePayload(bool Success, string? ErrorCode, string? Username, Guid? SessionToken);

// === Issue #3: ÿ·» „ﬂ«·„… Œ«’… («·“„·«¡) ===
public record CallRequestPayload(Guid CallId, string Caller, string Callee);
public record CallTimedOutPayload(Guid CallId);
public record CallErrorPayload(string ErrorCode, string Message);

// === ⁄«„ (Generic) ===
public record ErrorPayload(string ErrorCode, string Message);

/// <summary>
/// —„Ê“ «·√Œÿ«¡ «·ﬁÌ«”Ì… «·„‘ —ﬂ… („œ„Ã… ··„Â„… 1 Ê«·„Â„… 3).
/// </summary>
public static class ErrorCodes
{
    // √Œÿ«¡  ”ÃÌ· «·œŒÊ· (Issue #1)
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AlreadyLoggedIn = "ALREADY_LOGGED_IN";
    public const string UserNotFound = "USER_NOT_FOUND";

    // √Œÿ«¡ ÿ·» «·„ﬂ«·„… (Issue #3)
    public const string TargetOffline = "TARGET_OFFLINE";
    public const string TargetBusy = "TARGET_BUSY";
    public const string CallNotFound = "CALL_NOT_FOUND";
    public const string InvalidCallState = "INVALID_CALL_STATE";

    // √Œÿ«¡ ⁄«„… („‘ —ﬂ…)
    public const string ServerUnavailable = "SERVER_UNAVAILABLE";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}
using System;

namespace VideoCall.Shared.Messages;

// Issue #3 Ì⁄ „œ ⁄·Ï MessageType ÊMessage.Create ·≈—”«· ÿ·» «·„ﬂ«·„… ⁄»— TCP.

/// <summary>
/// »Ì«‰«  ÿ·» „ﬂ«·„… Œ«’… „‰ „” Œœ„ ≈·Ï „” Œœ„ ¬Œ—.
/// </summary>
public record CallRequestPayload(
    Guid CallId,
    string Caller,
    string Callee);

/// <summary>
/// »Ì«‰«  «‰ Â«¡ „Â·… ÿ·» «·„ﬂ«·„….
/// </summary>
public record CallTimedOutPayload(Guid CallId);

/// <summary>
/// »Ì«‰«  «·Œÿ√ «·‰« Ã ⁄‰ ÿ·» «·„ﬂ«·„….
/// </summary>
public record CallErrorPayload(
    string ErrorCode,
    string Message);

/// <summary>
/// —„Ê“ «·√Œÿ«¡ «· Ì ÌÕ «ÃÂ« Issue #3.
/// </summary>
public static class ErrorCodes
{
    public const string TargetOffline = "TARGET_OFFLINE";
    public const string TargetBusy = "TARGET_BUSY";
    public const string CallNotFound = "CALL_NOT_FOUND";
    public const string InvalidCallState = "INVALID_CALL_STATE";
    public const string ServerUnavailable = "SERVER_UNAVAILABLE";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}

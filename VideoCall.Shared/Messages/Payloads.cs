using System;
using System.Collections.Generic;

namespace VideoCall.Shared.Messages;

// === Issue #1: Authentication and login ===
public record LoginRequestPayload(string Username, string Password);
public record LoginResponsePayload(bool Success, string? ErrorCode, string? Username, Guid? SessionToken);

// === Issue #2: Display list of online users ===
// Contains the list of usernames currently online.
// The server sends this payload to clients whenever the online users list changes.
public record OnlineUsersUpdatePayload(List<string> Usernames);

// === Issue #3: Private call request ===
public record CallRequestPayload(Guid CallId, string Caller, string Callee);
public record CallTimedOutPayload(Guid CallId);
public record CallErrorPayload(string ErrorCode, string Message);

// === Generic ===
public record ErrorPayload(string ErrorCode, string Message);

/// <summary>
/// Standard shared error codes (used by Issue #1 and Issue #3).
/// </summary>
public static class ErrorCodes
{
    // Login errors (Issue #1)
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AlreadyLoggedIn = "ALREADY_LOGGED_IN";
    public const string UserNotFound = "USER_NOT_FOUND";

    // Call request errors (Issue #3)
    public const string TargetOffline = "TARGET_OFFLINE";
    public const string TargetBusy = "TARGET_BUSY";
    public const string CallNotFound = "CALL_NOT_FOUND";
    public const string InvalidCallState = "INVALID_CALL_STATE";

    // General errors (Shared)
    public const string ServerUnavailable = "SERVER_UNAVAILABLE";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}
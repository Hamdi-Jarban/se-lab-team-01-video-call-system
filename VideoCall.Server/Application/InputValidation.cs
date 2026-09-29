using System.Text.RegularExpressions;
using VideoCall.Shared.Messages;

namespace VideoCall.Server.Application;

// تحقق مركزي من مدخلات المستخدم (يُستخدم قبل أي وصول لقاعدة البيانات)
public static class InputValidation
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 32;
    public const int MinPasswordLength = 6;
    public const int MaxPasswordLength = 128;
    public const int MaxDisplayNameLength = 100;
    public const int MaxGroupNameLength = 100;
    public const int MaxMessageLength = 4000;
    public const int MaxGroupMembers = 100;

    // أحرف وأرقام (بما فيها العربية) و _ . -
    private static readonly Regex UsernamePattern =
        new(@"^[\p{L}\p{N}_.\-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return ErrorCodes.InvalidUsername;
        var trimmed = username.Trim();
        if (trimmed.Length < MinUsernameLength || trimmed.Length > MaxUsernameLength) return ErrorCodes.InvalidUsername;
        return UsernamePattern.IsMatch(trimmed) ? null : ErrorCodes.InvalidUsername;
    }

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password)) return ErrorCodes.WeakPassword;
        return password.Length is < MinPasswordLength or > MaxPasswordLength ? ErrorCodes.WeakPassword : null;
    }

    public static string? ValidateMessageContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return ErrorCodes.InvalidMessage;
        return content.Trim().Length > MaxMessageLength ? ErrorCodes.InvalidMessage : null;
    }

    public static string? ValidateGroupName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ErrorCodes.InvalidRequest;
        return name.Trim().Length > MaxGroupNameLength ? ErrorCodes.InvalidRequest : null;
    }
}

using VideoCall.Server.Domain.Logging;

namespace VideoCall.Server.Infrastructure.Logging;

/// <summary>
/// Console-based <see cref="IAppLogger"/> implementation. Output format and
/// coloring are unchanged from the original static <c>Logger</c> class; the
/// only difference is that this is now an ordinary, injectable instance
/// instead of a global static, so the composition root decides its lifetime
/// (registered as a singleton in <c>Program.cs</c>) instead of every
/// consumer reaching for a static field.
/// </summary>
public sealed class ConsoleAppLogger : IAppLogger
{
    private readonly object _consoleLock = new();

    public void Info(string message) => Write("INFO", message, ConsoleColor.Gray);

    public void Warn(string message) => Write("WARN", message, ConsoleColor.Yellow);

    public void Error(string message) => Write("ERROR", message, ConsoleColor.Red);

    private void Write(string level, string message, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{level}] {DateTime.Now:HH:mm:ss} {message}");
            Console.ForegroundColor = previous;
        }
    }
}

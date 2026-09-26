namespace VideoCall.Server.Domain.Logging;

/// <summary>
/// Abstraction over server-side logging.
/// <para>
/// Software-engineering concept: <b>Dependency Inversion Principle (DIP)</b>.
/// The original project used a <c>static class Logger</c>, which is a hidden,
/// global dependency that every class silently reaches out to. That makes
/// unit testing hard (you cannot substitute a fake logger) and couples every
/// consumer to one concrete implementation (Console output). By depending on
/// this interface instead, high-level classes (ClientSession, the media
/// relay, the server host) no longer know or care *how* logging happens -
/// only Infrastructure.Logging.ConsoleAppLogger knows that detail.
/// </para>
/// </summary>
public interface IAppLogger
{
    void Info(string message);

    void Warn(string message);

    void Error(string message);
}

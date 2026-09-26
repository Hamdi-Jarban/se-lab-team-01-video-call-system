using VideoCall.Server.Domain.Logging;

namespace VideoCall.Server.Infrastructure.Logging;

/// <summary>
/// ÊהÝםÐ ÎÏדÉ ÇבÊÓÌםב ÇבדזÌוÉ Åבל הÇÝÐÉ Console.
///
/// םØÈÞ וÐÇ ÇבהזÚ ÇבÚÞÏ IAppLogger¡ זםזÝÑ ËבÇË ÏÑÌÇÊ ÃÓÇÓםÉ בבÊÓÌםב:
/// - Info: דÚבזדÇÊ ÇבÊÔÛםב זÇבÃÍÏÇË ÇבØÈםÚםÉ.
/// - Warn: ÊÍÐםÑÇÊ בÇ ÊזÞÝ ÇבÎÇÏד ב‗הוÇ ÊÍÊÇÌ Åבל דÊÇÈÚÉ.
/// - Error: ÃÎØÇÁ ÊÍÊÇÌ Åבל ÊÍבםב Ãז ÊÏÎב דה ÝÑםÞ ÇבÊØזםÑ.
///
/// Êד ÊÕדםד ÇבÎÏדÉ ‗‗ÇÆה ÞÇÈב בבÍÞה (Injectable Service) ÈÏב ÇÓÊÎÏÇד
/// Logger ÚÇד דה ÇבהזÚ static. זםÓדÍ Ðב‗ ÈÅÏÇÑÉ ÏזÑÉ ÍםÇÉ ÇבÎÏדÉ דה ÎבÇב
/// Composition Root¡ ‗דÇ םÓוב ÇÓÊÈÏÇבוÇ ÈÊהÝםÐ ÂÎÑ Ýם ÇבÇÎÊÈÇÑÇÊ Ãז ÇבÅהÊÇÌ¡
/// דËב ÎÏדÉ ÊÓÌםב דÑ‗ÒםÉ Ãז הÙÇד דÑÇÞÈÉ דÄÓÓם.
///
/// םÊד ÊÓÌםב וÐו ÇבÎÏדÉ ÚÇÏÉנ ‗Ü Singleton ÏÇÎב Program.cs ÍÊל ÊÓÊÎÏד
/// ÌדםÚ ד‗זהÇÊ ÇבÎÇÏד הÝÓ ÞהÇÉ ÇבÊÓÌםב.
/// </summary>
public sealed class ConsoleAppLogger : IAppLogger
{
    // ÞÝב דÔÊÑ‗ םדהÚ ÊÏÇÎב דÎÑÌÇÊ ÇבדוÇד ÇבדÊזÇÒםÉ ÏÇÎב הÇÝÐÉ Console.
    // וÐÇ דוד בÃה ÇבÎÇÏד םÚÇבÌ ÚÏÉ ÌבÓÇÊ זÇÊÕÇבÇÊ Ýם ÇבזÞÊ הÝÓו.
    private readonly object _consoleLock = new();

    /// <summary>
    /// םÓÌב ÑÓÇבÉ דÚבזדÇÊםÉ Úה ÍÏË ØÈםÚם ÏÇÎב ÇבהÙÇד.
    /// </summary>
    /// <param name="message">הÕ ÇבÑÓÇבÉ ÇבדÑÇÏ ÊÓÌםבוÇ.</param>
    public void Info(string message) =>
        Write("INFO", message, ConsoleColor.Gray);

    /// <summary>
    /// םÓÌב ÊÍÐםÑנÇ בÇ םÄÏם ÈÇבÖÑזÑÉ Åבל ÅםÞÇÝ ÇבÎÏדÉ.
    /// </summary>
    /// <param name="message">הÕ ÇבÊÍÐםÑ ÇבדÑÇÏ ÊÓÌםבו.</param>
    public void Warn(string message) =>
        Write("WARN", message, ConsoleColor.Yellow);

    /// <summary>
    /// םÓÌב ÎØÃ ÍÏË ÃËהÇÁ ÊהÝםÐ ÅÍÏל ÚדבםÇÊ ÇבהÙÇד.
    /// </summary>
    /// <param name="message">ÊÝÇÕםב ÇבÎØÃ ÇבדÑÇÏ ÊÓÌםבוÇ.</param>
    public void Error(string message) =>
        Write("ERROR", message, ConsoleColor.Red);

    /// <summary>
    /// ם‗ÊÈ ÑÓÇבÉ דזÍÏÉ Åבל הÇÝÐÉ Console דÚ דÓÊזל ÇבÊÓÌםב זÇבבזה זÇבזÞÊ.
    /// </summary>
    /// <param name="level">דÓÊזל ÇבÑÓÇבÉ דËב INFO Ãז WARN Ãז ERROR.</param>
    /// <param name="message">הÕ ÇבÑÓÇבÉ.</param>
    /// <param name="color">Çבבזה ÇבדÓÊÎÏד בÊדםםÒ דÓÊזל ÇבÑÓÇבÉ.</param>
    private void Write(
        string level,
        string message,
        ConsoleColor color)
    {
        // ÖדÇה ÊהÝםÐ ÚדבםÉ Çב‗ÊÇÈÉ ‗ÇדבÉ Ïזה ÊÏÇÎב דÚ Thread ÂÎÑ.
        lock (_consoleLock)
        {
            // ÍÝÙ Çבבזה ÇבÍÇבם ÍÊל בÇ ÊÄËÑ ÇבÑÓÇבÉ Úבל ÇבדÎÑÌÇÊ ÇבבÇÍÞÉ.
            var previous = Console.ForegroundColor;

            Console.ForegroundColor = color;

            // ÇÓÊÎÏÇד ÊהÓםÞ דזÍÏ םÓוב ÞÑÇÁÊו זÇבÈÍË Úהו ÏÇÎב ÓÌבÇÊ ÇבÊÔÛםב.
            Console.WriteLine(
                $"[{level}] {DateTime.Now:HH:mm:ss} {message}");

            // ÇÓÊÚÇÏÉ Çבבזה ÇבÓÇÈÞ ÈÚÏ ÇהÊוÇÁ Çב‗ÊÇÈÉ.
            Console.ForegroundColor = previous;
        }
    }
}

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VideoCall.Server;
using VideoCall.Server.Api;
using VideoCall.Server.Application;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Domain.Services;
using VideoCall.Server.Infrastructure.Persistence;
using VideoCall.Server.Persistence;
using Microsoft.Extensions.Configuration;
using VideoCall.Server.Infrastructure.Logging;
using VideoCall.Server.Infrastructure.Media;
using VideoCall.Server.Infrastructure.Security;
using VideoCall.Shared.Networking;

const int HttpApiPort = 8080;


var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).AddEnvironmentVariables().Build();

var databaseOptions = new DatabaseOptions(configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty);

// ≈‰‘«¡ Õ«ÊÌ… «·«⁄ „«œÌ«  «·Œ«’… »«· ÿ»Ìﬁ.
var services = new ServiceCollection();

services.AddSingleton<IAppLogger, ConsoleAppLogger>();

services.AddSingleton(databaseOptions);
services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
services.AddSingleton<DatabaseInitializer>();
services.AddSingleton<IUserRepository, SqlUserRepository>();
services.AddSingleton<IUserSessionRepository, SqlUserSessionRepository>();
services.AddSingleton<IChatConversationRepository, SqlChatConversationRepository>();
services.AddSingleton<IMessageRepository, SqlMessageRepository>();
services.AddSingleton<ICallRepository, SqlCallRepository>();

services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
services.AddSingleton<LoginThrottle>();
services.AddSingleton<IAuthService, AuthService>();
services.AddSingleton<IChatService, ChatService>();
services.AddSingleton<ICallHistoryRecorder, CallHistoryRecorder>();
services.AddSingleton<ChatProtocolHandler>();

// „” Êœ⁄ «·Õ÷Ê— (Presence Repository).
// ÌÕ ›Ÿ »«·„” Œœ„Ì‰ «·„ ’·Ì‰ ÊÌ—»ÿ ﬂ· „” Œœ„ »Ã·”… TCP «·Œ«’… »Â.
services.AddSingleton<IUserPresenceRepository, UserPresenceService>();

services.AddSingleton<IConversationRepository>(_ => new ConversationService(maxGroupMembers: 8));

// ---------------------------------------------------------------------------
// Œœ„…  „—Ì— «·Ê”«∆ÿ ⁄»— UDP
// ---------------------------------------------------------------------------
services.AddSingleton(sp => new UdpMediaRelayService(sp.GetRequiredService<IConversationRepository>(),sp.GetRequiredService<IUserPresenceRepository>(),sp.GetRequiredService<IAppLogger>(),NetworkConfig.UdpMediaPort));

services.AddSingleton<IMediaRelayCoordinator>(sp => sp.GetRequiredService<UdpMediaRelayService>());

services.AddSingleton<ProtocolRouter>();

// «” Œœ«„ ‰›” instance „‰ ProtocolRouter · ‰›Ì– Ê«ÃÂ…  Ê“Ì⁄ «·—”«∆·.
services.AddSingleton<IProtocolMessageDispatcher>(sp => sp.GetRequiredService<ProtocolRouter>());

services.AddSingleton<IConnectionLifecycleHandler>(sp => sp.GetRequiredService<ProtocolRouter>());

// ---------------------------------------------------------------------------
// Ê«ÃÂ… HTTP ··ﬁ—«¡… Ê«·„—«ﬁ»…
// ---------------------------------------------------------------------------

// ApiServer  ⁄—÷ „⁄·Ê„«  «·ﬁ—«¡… ›ﬁÿ „À·:
// - Õ«·… «·Œ«œ„.
// - «·„” Œœ„Ì‰ «·„ ’·Ì‰.
// - «·€—› «·Õ«·Ì….
// - «·Ã·”«  «·‰‘ÿ….
//
// ·«  ” Œœ„ Â–Â «·Ê«ÃÂ… · ‰›Ì–  ”ÃÌ· «·œŒÊ· ⁄»— TCP.
services.AddSingleton(sp => new ApiServer(sp.GetRequiredService<IUserPresenceRepository>(),sp.GetRequiredService<IConversationRepository>(),sp.GetRequiredService<IAppLogger>(),HttpApiPort));


services.AddSingleton(sp => new ServerHost(sp.GetRequiredService<IProtocolMessageDispatcher>(),sp.GetRequiredService<IConnectionLifecycleHandler>(),sp.GetRequiredService<UdpMediaRelayService>(),sp.GetRequiredService<ApiServer>(),sp.GetRequiredService<IAppLogger>(),bindAddress: IPAddress.Any,tcpPort: NetworkConfig.TcpControlPort));

// ---------------------------------------------------------------------------
// »‰«¡ „“Êœ «·Œœ„« 
// ---------------------------------------------------------------------------

// »‰«¡ Õ«ÊÌ… «·«⁄ „«œÌ«  »⁄œ «ﬂ „«· Ã„Ì⁄ «· ”ÃÌ·« .
// Ì „ «· Œ·’ „‰Â«  ·ﬁ«∆Ì« ⁄‰œ «‰ Â«¡ «· ÿ»Ìﬁ.
await using var provider = services.BuildServiceProvider();

// —„“ «·≈·€«¡ «·„‘ —ﬂ ·≈Ìﬁ«› Ã„Ì⁄ «·Œœ„«  »‘ﬂ· „‰Ÿ„.
using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

try
{
    await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync(shutdown.Token);

    await provider.GetRequiredService<IUserSessionRepository>().EndAllActiveAsync(shutdown.Token);
    await provider.GetRequiredService<ICallRepository>().MarkStaleAsInterruptedAsync(shutdown.Token);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    provider.GetRequiredService<IAppLogger>().Error("Database initialization failed. Make sure SQL Server Express LocalDB is installed " +$"(run: sqllocaldb info) and check ConnectionStrings:{DatabaseOptions.ConnectionStringName} in appsettings.json. Details: {ex.Message}");
    return;
}

await using var server = provider.GetRequiredService<ServerHost>();

Console.WriteLine("VideoCall server started. Press Ctrl+C to stop.");

await server.RunAsync(shutdown.Token);

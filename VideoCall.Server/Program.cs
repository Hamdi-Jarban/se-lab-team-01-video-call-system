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


var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var databaseOptions = new DatabaseOptions(
    configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty);

// ÅäÔÇÁ ÍÇæíÉ ÇáÇÚÊãÇÏíÇÊ ÇáÎÇÕÉ ÈÇáÊØÈíŞ.
var services = new ServiceCollection();

// ---------------------------------------------------------------------------
// ÊÓÌíá ÇáÎÏãÇÊ ÇáãÔÊÑßÉ
// ---------------------------------------------------------------------------

// ÎÏãÉ ÊÓÌíá ÇáÃÍÏÇË æÇáÃÎØÇÁ.
// íÊã ÊÓÌíáåÇ ßÜ Singleton ÍÊì ÊÓÊÎÏã ÌãíÚ ãßæäÇÊ ÇáÎÇÏã äİÓ instance.
services.AddSingleton<IAppLogger, ConsoleAppLogger>();

services.AddSingleton(databaseOptions);
services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
services.AddSingleton<DatabaseInitializer>();
services.AddSingleton<IUserRepository, SqlUserRepository>();
services.AddSingleton<IUserSessionRepository, SqlUserSessionRepository>();
services.AddSingleton<IChatConversationRepository, SqlChatConversationRepository>();
services.AddSingleton<IMessageRepository, SqlMessageRepository>();
services.AddSingleton<ICallRepository, SqlCallRepository>();

// ---------- Ø§Ù„Ø®Ø¯Ù…Ø§Øª ----------
services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
services.AddSingleton<LoginThrottle>();
services.AddSingleton<IAuthService, AuthService>();
services.AddSingleton<IChatService, ChatService>();
services.AddSingleton<ICallHistoryRecorder, CallHistoryRecorder>();
services.AddSingleton<ChatProtocolHandler>();

// ãÓÊæÏÚ ÇáÍÖæÑ (Presence Repository).
// íÍÊİÙ ÈÇáãÓÊÎÏãíä ÇáãÊÕáíä æíÑÈØ ßá ãÓÊÎÏã ÈÌáÓÉ TCP ÇáÎÇÕÉ Èå.
services.AddSingleton<IUserPresenceRepository, UserPresenceService>();

// ãÓÊæÏÚ ÇáãÍÇÏËÇÊ æÇáÛÑİ.
// ÇáÊäİíĞ ÇáÍÇáí íÚãá ÏÇÎá ÇáĞÇßÑÉ æíÍÏÏ ÇáÍÏ ÇáÃŞÕì áÃÚÖÇÁ ÇáÛÑİÉ ÈÜ 8.
// íãßä ÇÓÊÈÏÇáå áÇÍŞğÇ ÈÊäİíĞ íÚÊãÏ Úáì ŞÇÚÏÉ ÈíÇäÇÊ Ïæä ÊÚÏíá ÇáãÓÊåáßíä.
services.AddSingleton<IConversationRepository>(
    _ => new ConversationService(maxGroupMembers: 8));

// ---------------------------------------------------------------------------
// ÎÏãÉ ÊãÑíÑ ÇáæÓÇÆØ ÚÈÑ UDP
// ---------------------------------------------------------------------------

// ÊÓÌíá ÎÏãÉ UDP ßäæÚ ãáãæÓ áÃä ServerHost ãÓÄæá Úä ÏæÑÉ ÍíÇÊåÇ
// æÅíŞÇİåÇ æÊÍÑíÑ ãæÇÑÏåÇ ÚÈÑ IAsyncDisposable.
services.AddSingleton(sp => new UdpMediaRelayService(
    sp.GetRequiredService<IConversationRepository>(),
    sp.GetRequiredService<IUserPresenceRepository>(),
    sp.GetRequiredService<IAppLogger>(),
    NetworkConfig.UdpMediaPort));

// ÊæİíÑ äİÓ instance ÚÈÑ æÇÌåÉ ÖíŞÉ.
// ProtocolRouter áÇ íÍÊÇÌ Åáì ãÚÑİÉ ÊİÇÕíá ÊÔÛíá Ãæ ÅíŞÇİ ÎÏãÉ UDPº
// áĞáß íÚÊãÏ İŞØ Úáì IMediaRelayCoordinator.
services.AddSingleton<IMediaRelayCoordinator>(
    sp => sp.GetRequiredService<UdpMediaRelayService>());

// ---------------------------------------------------------------------------
// ãæÌøå ÑÓÇÆá ÇáÈÑæÊæßæá æÏæÑÉ ÍíÇÉ ÇáÇÊÕÇáÇÊ
// ---------------------------------------------------------------------------

// ProtocolRouter ãÓÄæá Úä ÊÍæíá ÇáÑÓÇÆá ÇáæÇÑÏÉ ãä ÇáÚãáÇÁ Åáì ÚãáíÇÊ
// ÊÓÌíá ÇáÏÎæá æÇáãßÇáãÇÊ æÅÏÇÑÉ ÇáÛÑİ¡ Ëã ÅÑÓÇá ÇáäÊÇÆÌ Åáì ÇáÚãáÇÁ.
services.AddSingleton<ProtocolRouter>();

// ÇÓÊÎÏÇã äİÓ instance ãä ProtocolRouter áÊäİíĞ æÇÌåÉ ÊæÒíÚ ÇáÑÓÇÆá.
services.AddSingleton<IProtocolMessageDispatcher>(
    sp => sp.GetRequiredService<ProtocolRouter>());

// ÇÓÊÎÏÇã äİÓ instance áÊäİíĞ ÊäÙíİ ÇáÌáÓÉ ÈÚÏ ÇäŞØÇÚ ÇáÚãíá.
// åĞÇ íÖãä ÅÒÇáÉ ÇáãÓÊÎÏã ãä Presence æÅÛáÇŞ ÇáÍÇáÇÊ ÇáãÑÊÈØÉ Èå.
services.AddSingleton<IConnectionLifecycleHandler>(
    sp => sp.GetRequiredService<ProtocolRouter>());

// ---------------------------------------------------------------------------
// æÇÌåÉ HTTP ááŞÑÇÁÉ æÇáãÑÇŞÈÉ
// ---------------------------------------------------------------------------

// ApiServer ÊÚÑÖ ãÚáæãÇÊ ÇáŞÑÇÁÉ İŞØ ãËá:
// - ÍÇáÉ ÇáÎÇÏã.
// - ÇáãÓÊÎÏãíä ÇáãÊÕáíä.
// - ÇáÛÑİ ÇáÍÇáíÉ.
// - ÇáÌáÓÇÊ ÇáäÔØÉ.
//
// áÇ ÊÓÊÎÏã åĞå ÇáæÇÌåÉ áÊäİíĞ ÊÓÌíá ÇáÏÎæá ÚÈÑ TCP.
services.AddSingleton(sp => new ApiServer(
    sp.GetRequiredService<IUserPresenceRepository>(),
    sp.GetRequiredService<IConversationRepository>(),
    sp.GetRequiredService<IAppLogger>(),
    HttpApiPort));

// ---------------------------------------------------------------------------
// ãÖíİ ÇáÎÇÏã ÇáÑÆíÓí
// ---------------------------------------------------------------------------

// ServerHost åæ ÇáãÓÄæá Úä ÊäÓíŞ ãæÇÑÏ ÇáÊÔÛíá ÇáÑÆíÓíÉ:
// - ÇÓÊŞÈÇá ÇÊÕÇáÇÊ TCP.
// - ÅäÔÇÁ ClientSession áßá Úãíá.
// - ÊÔÛíá UDP Media Relay.
// - ÊÔÛíá HTTP API.
// - ÊäİíĞ ÇáÅÛáÇŞ ÇáãäÙã ÚäÏ ÅíŞÇİ ÇáÊØÈíŞ.
services.AddSingleton(sp => new ServerHost(
    sp.GetRequiredService<IProtocolMessageDispatcher>(),
    sp.GetRequiredService<IConnectionLifecycleHandler>(),
    sp.GetRequiredService<UdpMediaRelayService>(),
    sp.GetRequiredService<ApiServer>(),
    sp.GetRequiredService<IAppLogger>(),
    bindAddress: IPAddress.Any,
    tcpPort: NetworkConfig.TcpControlPort));

// ---------------------------------------------------------------------------
// ÈäÇÁ ãÒæÏ ÇáÎÏãÇÊ
// ---------------------------------------------------------------------------

// ÈäÇÁ ÍÇæíÉ ÇáÇÚÊãÇÏíÇÊ ÈÚÏ ÇßÊãÇá ÌãíÚ ÇáÊÓÌíáÇÊ.
// íÊã ÇáÊÎáÕ ãäåÇ ÊáŞÇÆíğÇ ÚäÏ ÇäÊåÇÁ ÇáÊØÈíŞ.
await using var provider = services.BuildServiceProvider();

// ÑãÒ ÇáÅáÛÇÁ ÇáãÔÊÑß áÅíŞÇİ ÌãíÚ ÇáÎÏãÇÊ ÈÔßá ãäÙã.
using var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

try
{
    await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync(shutdown.Token);

    // Ø¬Ù„Ø³Ø§Øª/Ù…ÙƒØ§Ù„Ù…Ø§Øª Ø¨Ù‚ÙŠØª Ù…ÙØªÙˆØ­Ø© Ø¨Ø³Ø¨Ø¨ Ø¥ÙŠÙ‚Ø§Ù Ù…ÙØ§Ø¬Ø¦ Ø³Ø§Ø¨Ù‚ ØªÙØºÙ„Ù‚ Ø§Ù„Ø¢Ù† (Ø§Ù„Ø­Ø§Ù„Ø© Ø§Ù„Ø­ÙŠØ© Ù„Ø§ ØªÙØ³ØªØ¹Ø§Ø¯ Ø¨Ø¹Ø¯ Ø¥Ø¹Ø§Ø¯Ø© Ø§Ù„ØªØ´ØºÙŠÙ„)
    await provider.GetRequiredService<IUserSessionRepository>().EndAllActiveAsync(shutdown.Token);
    await provider.GetRequiredService<ICallRepository>().MarkStaleAsInterruptedAsync(shutdown.Token);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    provider.GetRequiredService<IAppLogger>().Error(
        "Database initialization failed. Make sure SQL Server Express LocalDB is installed " +
        $"(run: sqllocaldb info) and check ConnectionStrings:{DatabaseOptions.ConnectionStringName} in appsettings.json. Details: {ex.Message}");
    return;
}

await using var server = provider.GetRequiredService<ServerHost>();

Console.WriteLine("VideoCall server started. Press Ctrl+C to stop.");

// ÈÏÁ ÏæÑÉ ÊÔÛíá ÇáÎÇÏã æÇäÊÙÇÑ ÇáÅíŞÇİ Ãæ ÇáÅáÛÇÁ.
await server.RunAsync(shutdown.Token);

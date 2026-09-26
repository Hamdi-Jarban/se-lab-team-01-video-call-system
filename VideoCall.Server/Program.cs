using System.Net;
using VideoCall.Server;
using VideoCall.Server.Api;
using VideoCall.Server.Application;
using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Infrastructure.Logging;
using VideoCall.Server.Infrastructure.Media;
using VideoCall.Server.Infrastructure.Security;
using VideoCall.Shared.Networking;

// ============================================================================
// äŞØÉ ÊÑßíÈ ÇáÊØÈíŞ (Composition Root)
// ============================================================================
//
// åĞÇ Çáãáİ åæ ÇáãßÇä ÇáãÑßÒí ÇáĞí ÊõÑÈØ İíå ÇáæÇÌåÇÊ (Interfaces)
// ÈÇáÊäİíĞÇÊ ÇáİÚáíÉ ÏÇÎá ÇáÊØÈíŞ.
//
// ÇáÊÒãäÇ åäÇ ÈãÈÏÃ ÚßÓ ÇÊÌÇå ÇáÇÚÊãÇÏ (Dependency Inversion Principle)¡
// ÈÍíË ÊÚÊãÏ ØÈŞÇÊ ÇáÊØÈíŞ Úáì ÇáæÇÌåÇÊ ÈÏá ÇáÇÚÊãÇÏ ÇáãÈÇÔÑ Úáì ÇáÊİÇÕíá
// ÇáÊäİíĞíÉ ãËá TCP æUDP æConsole æØÑŞ ÊÎÒíä ÇáÈíÇäÇÊ.
//
// İæÇÆÏ åĞÇ ÇáÃÓáæÈ:
// - ÊÓåíá ÇÓÊÈÏÇá Ãí ãßæøä Ïæä ÊÚÏíá ÇáØÈŞÇÊ ÇáÊí ÊÓÊÎÏãå.
// - ÊÍÓíä ŞÇÈáíÉ ÇÎÊÈÇÑ ÇáÎÏãÇÊ ÈÇÓÊÎÏÇã ÈÏÇÆá æåãíÉ (Mocks/Fakes).
// - ÅÈŞÇÁ ãÓÄæáíÉ ÅäÔÇÁ ÇáßÇÆäÇÊ İí ãßÇä æÇÍÏ.
// - ãäÚ ÇäÊÔÇÑ ÚãáíÇÊ new ÏÇÎá ØÈŞÇÊ ÇáÊØÈíŞ æÇáÃÚãÇá.
//
// íÊã ÇÓÊÎÏÇã Microsoft.Extensions.DependencyInjection áÅÏÇÑÉ ÏæÑÉ ÍíÇÉ
// ÇáÎÏãÇÊ æÍŞä ÇáÇÚÊãÇÏíÇÊ ÚÈÑ Constructors.
// ============================================================================

// ãäİĞ æÇÌåÉ HTTP ÇáÎÇÕÉ ÈãÑÇŞÈÉ ÍÇáÉ ÇáÎÇÏã.
// ãáÇÍÙÉ: íÌÈ äŞá ÇáÅÚÏÇÏÇÊ Åáì Configuration Ãæ Environment Variables
// ÚäÏ ÊÔÛíá ÇáÊØÈíŞ İí ÈíÆÉ ÇáÅäÊÇÌ.
const int HttpApiPort = 8080;

// ---------------------------------------------------------------------------
// ÍÓÇÈÇÊ ÇáÊØæíÑ
// ---------------------------------------------------------------------------
//
// åĞå ÇáÍÓÇÈÇÊ ãÎÕÕÉ ááÊØæíÑ æÇáÇÎÊÈÇÑ ÇáãÍáí İŞØ.
// áÇ íõÓãÍ ÈÇÓÊÎÏÇã ßáãÇÊ ãÑæÑ äÕíÉ ËÇÈÊÉ İí ÈíÆÉ ÇáÅäÊÇÌ.
//
// İí ÈíÆÉ ÇáÅäÊÇÌ íÌÈ ÇÓÊÈÏÇá åĞÇ ÇáŞÇãæÓ ÈãÕÏÑ Âãä¡ ãËá:
// - ŞÇÚÏÉ ÈíÇäÇÊ ÊÍÊæí Úáì Password Hashes.
// - ãÒæÏ åæíÉ ãÑßÒí (Identity Provider).
// - Secret Manager Ãæ ÎÏãÉ ÃÓÑÇÑ ãÄÓÓíÉ.
// ---------------------------------------------------------------------------
var accounts = new Dictionary<string, string>(
    StringComparer.OrdinalIgnoreCase)
{
    ["hamdi"] = "1234",
    ["ali1"] = "1111",
    ["ali2"] = "2222",
    ["ali3"] = "3333"
};

// ÅäÔÇÁ ÍÇæíÉ ÇáÇÚÊãÇÏíÇÊ ÇáÎÇÕÉ ÈÇáÊØÈíŞ.
var services = new ServiceCollection();

// ---------------------------------------------------------------------------
// ÊÓÌíá ÇáÎÏãÇÊ ÇáãÔÊÑßÉ
// ---------------------------------------------------------------------------

// ÎÏãÉ ÊÓÌíá ÇáÃÍÏÇË æÇáÃÎØÇÁ.
// íÊã ÊÓÌíáåÇ ßÜ Singleton ÍÊì ÊÓÊÎÏã ÌãíÚ ãßæäÇÊ ÇáÎÇÏã äİÓ instance.
services.AddSingleton<IAppLogger, ConsoleAppLogger>();

// ÎÏãÉ ÇáÊÍŞŞ ãä ÈíÇäÇÊ ÊÓÌíá ÇáÏÎæá.
// ÇáÊäİíĞ ÇáÍÇáí ãÎÕÕ ááÊØæíÑ æíÚÊãÏ Úáì ÇáÍÓÇÈÇÊ ÇáãæÌæÏÉ İí ÇáĞÇßÑÉ.
services.AddSingleton<ICredentialValidator>(
    _ => new DevelopmentCredentialValidator(accounts));

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

// ÇÚÊÑÇÖ Ctrl+C æãäÚ ÇáÅäåÇÁ ÇáİæÑí¡ Ëã ÅÑÓÇá ÅÔÇÑÉ ÅíŞÇİ
// Åáì ServerHost æÈŞíÉ ÇáÎÏãÇÊ.
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

// ÇáÍÕæá Úáì ÇáãÖíİ ãä ÍÇæíÉ ÇáÇÚÊãÇÏíÇÊ.
// using íÖãä ÊÍÑíÑ ÇáãæÇÑÏ ÚäÏ ÇäÊåÇÁ ÏæÑÉ ÍíÇÉ ÇáÎÇÏã.
await using var server = provider.GetRequiredService<ServerHost>();

Console.WriteLine("VideoCall server started. Press Ctrl+C to stop.");

// ÈÏÁ ÏæÑÉ ÊÔÛíá ÇáÎÇÏã æÇäÊÙÇÑ ÇáÅíŞÇİ Ãæ ÇáÅáÛÇÁ.
await server.RunAsync(shutdown.Token);

using System.Windows;
using VideoCall.Client.Services;
using VideoCall.Client.Views;

namespace VideoCall.Client;

/// äŞØÉ ÇáÈÏÇíÉ áÊØÈíŞ ÇáÚãíá (Composition Root). 
/// åĞÇ åæ ÇáãßÇä ÇáæÍíÏ ÇáãÓãæÍ İíå ÈÅäÔÇÁ ßÇÆä NetworkClient ãÈÇÔÑÉ (new NetworkClient).
/// íÊã ÊãÑíÑ åĞÇ ÇáßÇÆä ááæÇÌåÇÊ (ViewModels) ÚÈÑ æÇÌåÉ INetworkClient áÊÍŞíŞ ãÈÏÃ Dependency Inversion.
public partial class App : Application
{
    private NetworkClient? _network;

    // ÊõÓÊÏÚì åĞå ÇáÏÇáÉ ÊáŞÇÆíÇğ ÚäÏ ÊÔÛíá ÇáÊØÈíŞ
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // ÊåíÆÉ ÇáÇÊÕÇá ÇáÔÈßí æİÊÍ äÇİĞÉ ÊÓÌíá ÇáÏÎæá ßÃæá ÔÇÔÉ
        _network = new NetworkClient();
        var login = new LoginWindow(_network);
        MainWindow = login;
        login.Show();
    }

    // ÏÇáÉ ááÇäÊŞÇá Åáì ÇáÔÇÔÉ ÇáÑÆíÓíÉ (ÔÇÔÉ ÇáÛÑİ) ÈÚÏ äÌÇÍ ÊÓÌíá ÇáÏÎæá
    public void ShowMainWindow()
    {
        if (_network is null) return;
        var main = new MainWindow(_network);
        MainWindow = main;
        main.Show();
    }

    // ÊõÓÊÏÚì åĞå ÇáÏÇáÉ ÚäÏ ÅÛáÇŞ ÇáÊØÈíŞ áÊäÙíİ ÇáãæÇÑÏ
    protected override void OnExit(ExitEventArgs e)
    {
        // ÅÛáÇŞ ÌãíÚ ÇáäæÇİĞ ÇáİÑÚíÉ áÖãÇä ÅíŞÇİ ÇáßÇãíÑÇ¡ ÇáãíßÑæİæä¡ æÎÏãÇÊ UDP
        foreach (var window in Windows.OfType<Window>().ToArray())
        {
            try { window.Close(); } catch { }
        }
        
        // ÅÛáÇŞ ÇÊÕÇá TCP æÊÍÑíÑ ãæÇÑÏå
        _network?.Dispose();
        base.OnExit(e);
    }
}
using System.Windows;
using VideoCall.Client.Services;
using VideoCall.Client.ViewModels;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.Views;

/// <summary>
/// ÇáßæÏ ÇáÎáİí áäÇİĞÉ ÊÓÌíá ÇáÏÎæá: ãÓÄæá Úä ÑÈØ ÇáæÇÌåÉ ÈäãæĞÌ ÇáÚÑÖ (ViewModel) æÇáÊÚÇãá ãÚ ÇáÃÍÏÇË.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private readonly INetworkClient _network;

    public LoginWindow(INetworkClient network)
    {
        InitializeComponent();
        _network = network;
        
        // ÊåíÆÉ äãæĞÌ ÇáÚÑÖ æÑÈØå ÈÓíÇŞ ÇáÈíÇäÇÊ (DataContext) æÇáÇÔÊÑÇß İí ÍÏË ÇáäÌÇÍ
        _viewModel = new LoginViewModel(network);
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        DataContext = _viewModel;
    }

    // ÊãÑíÑ ßáãÉ ÇáãÑæÑ Åáì äãæĞÌ ÇáÚÑÖ Ëã ãÓÍåÇ İæÑÇğ ãä ÇáæÇÌåÉ áÃÓÈÇÈ ÃãäíÉ
    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginAsync(PasswordBox.Password);
        PasswordBox.Clear();
    }

    // ãÚÇáÌ ÇáÍÏË ÇáãÑÈæØ ÈÒÑ ÇáÏÎæá İí ãáİ XAML
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginAsync(PasswordBox.Password);
        PasswordBox.Clear();
    }

    // ÇáÇäÊŞÇá Åáì ÇáäÇİĞÉ ÇáÑÆíÓíÉ ááÊØÈíŞ æÅÛáÇŞ äÇİĞÉ ÇáÏÎæá ÇáÍÇáíÉ ÚäÏ äÌÇÍ ÇáÚãáíÉ
    private void OnLoginSucceeded()
    {
        var main = new MainWindow(_network);
        Application.Current.MainWindow = main;
        main.Show();
        Close();
    }

    // ÊäÙíİ ÇáãæÇÑÏ æÅáÛÇÁ ÇáÇÑÊÈÇØ ÈÇáÃÍÏÇË ÚäÏ ÅÛáÇŞ ÇáäÇİĞÉ áÊÌäÈ ÊÓÑÈ ÇáĞÇßÑÉ (Memory Leak)
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.LoginSucceeded -= OnLoginSucceeded;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
using System.Windows;
using VideoCall.Client.Services;
using VideoCall.Client.ViewModels;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private readonly INetworkClient _network;

    public LoginWindow(INetworkClient network)
    {
        InitializeComponent();
        _network = network;

        //  ÂÌ∆… «·‹ ViewModel Ê—»ÿ «·»Ì«‰«  »«·Ê«ÃÂ…
        _viewModel = new LoginViewModel(network);
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        DataContext = _viewModel;
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginAsync(PasswordBox.Password);
        PasswordBox.Clear();
    }

    // “—  ”ÃÌ· «·œŒÊ·
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginAsync(PasswordBox.Password);
        PasswordBox.Clear(); // „”Õ ﬂ·„… «·„—Ê— »⁄œ «·≈—”«·
    }

    // «·«‰ ﬁ«· ··‘«‘… «·—∆Ì”Ì… »⁄œ ‰Ã«Õ «·œŒÊ·
    private void OnLoginSucceeded()
    {
        var main = new MainWindow(_network);
        Application.Current.MainWindow = main;
        main.Show();
        Close();
    }

    //  ‰ŸÌ› «·√Õœ«À ⁄‰œ ≈€·«ﬁ «·‰«›–…
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.LoginSucceeded -= OnLoginSucceeded;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
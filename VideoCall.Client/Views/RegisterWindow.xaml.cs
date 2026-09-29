using System.Windows;
using VideoCall.Client.Contracts;
using VideoCall.Client.ViewModels;

namespace VideoCall.Client.Views;

public partial class RegisterWindow : Window
{
    private readonly RegisterViewModel _viewModel;

    // اسم المستخدم الذي تم تسجيله بنجاح (null إذا أُغلقت النافذة بدون تسجيل)
    public string? RegisteredUsername { get; private set; }

    public RegisterWindow(INetworkClient network, string serverAddress)
    {
        InitializeComponent();
        _viewModel = new RegisterViewModel(network, serverAddress);
        _viewModel.Registered += OnRegistered;
        DataContext = _viewModel;
    }

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RegisterAsync(PasswordBox.Password, ConfirmBox.Password);
        PasswordBox.Clear();
        ConfirmBox.Clear();
    }

    private void OnRegistered(string username)
    {
        RegisteredUsername = username;
        DialogResult = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Registered -= OnRegistered;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}

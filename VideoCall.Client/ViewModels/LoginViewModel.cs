using System.Windows;
using VideoCall.Client.Services;
using VideoCall.Shared.Messages;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.ViewModels;

public sealed class LoginViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkClient _network;
    private string _serverAddress = "127.0.0.1";
    private string _username = string.Empty;
    private string _status = string.Empty;
    private bool _busy;

    public string ServerAddress
    {
        get => _serverAddress;
        set => SetField(ref _serverAddress, value);
    }
    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
    }
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }
    public bool IsBusy
    {
        get => _busy;
        private set => SetField(ref _busy, value);
    }

    public event Action? LoginSucceeded;

    public LoginViewModel(INetworkClient network)
    {
        _network = network;
        _network.LoginResponseReceived += OnLoginResponse;
    }

    public async Task LoginAsync(string password)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(ServerAddress) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(password))
        {
            Status = "ÃÏÎá ÚäæÇä ÇáÎÇÏã æÇÓã ÇáãÓÊÎÏã æßáãÉ ÇáãÑæÑ.";
            return;
        }

        IsBusy = true;
        Status = "ÌÇÑí ÇáÇÊÕÇá ÈÇáÎÇÏã...";
        if (!_network.IsConnected)
        {
            var connected = await _network.ConnectAsync(ServerAddress.Trim());
            if (!connected)
            {
                IsBusy = false;
                Status = "ÊÚÐÑ ÇáÇÊÕÇá ÈÇáÎÇÏã.";
                return;
            }
        }

        await _network.LoginAsync(Username.Trim(), password);
    }

    private void OnLoginResponse(LoginResponsePayload response)
    {
        OnUi(() =>
        {
            IsBusy = false;
            if (response.Success)
            {
                Status = string.Empty;
                LoginSucceeded?.Invoke();
                return;
            }

            Status = response.ErrorCode switch
            {
                ErrorCodes.InvalidCredentials => "ÈíÇäÇÊ ÇáÏÎæá ÛíÑ ÕÍíÍÉ.",
                ErrorCodes.AlreadyLoggedIn => "ÇáãÓÊÎÏã ãÓÌá ÇáÏÎæá ãÓÈÞðÇ.",
                _ => "ÝÔá ÊÓÌíá ÇáÏÎæá."
            };
        });
    }

    public void Dispose() => _network.LoginResponseReceived -= OnLoginResponse;

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}

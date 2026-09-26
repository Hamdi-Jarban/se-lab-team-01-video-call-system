using System.Windows;
using VideoCall.Client.Services;
using VideoCall.Shared.Messages;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.ViewModels;

/// <summary>
/// ≈œ«—… Õ«·… Ê«ÃÂ…  ”ÃÌ· «·œŒÊ· Ê«·« ’«· »Œœ„«  «·‘»ﬂ….
/// </summary>
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

    /// <summary>
    /// «· Õﬁﬁ „‰ «·ÕﬁÊ·° ≈‰‘«¡ «·« ’«· »«·Œ«œ„ ≈‰ ·“„ «·√„—° Ê≈—”«· »Ì«‰«  «·«⁄ „«œ.
    /// </summary>
    public async Task LoginAsync(string password)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(ServerAddress) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(password))
        {
            Status = "√œŒ· ⁄‰Ê«‰ «·Œ«œ„ Ê«”„ «·„” Œœ„ Êﬂ·„… «·„—Ê—.";
            return;
        }

        IsBusy = true;
        Status = "Ã«—Ì «·« ’«· »«·Œ«œ„...";
        if (!_network.IsConnected)
        {
            var connected = await _network.ConnectAsync(ServerAddress.Trim());
            if (!connected)
            {
                IsBusy = false;
                Status = " ⁄–— «·« ’«· »«·Œ«œ„.";
                return;
            }
        }

        await _network.LoginAsync(Username.Trim(), password);
    }

    /// <summary>
    /// „⁄«·Ã… —œ «·Œ«œ„ Ê ÕœÌÀ Õ«·… Ê«ÃÂ… «·„” Œœ„ »‰«¡ ⁄·Ï «·‰ ÌÃ….
    /// </summary>
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
                ErrorCodes.InvalidCredentials => "»Ì«‰«  «·œŒÊ· €Ì— ’ÕÌÕ….",
                ErrorCodes.AlreadyLoggedIn => "«·„” Œœ„ „”Ã· «·œŒÊ· „”»ﬁ«.",
                _ => "›‘·  ”ÃÌ· «·œŒÊ·."
            };
        });
    }

    public void Dispose() => _network.LoginResponseReceived -= OnLoginResponse;

    /// <summary>
    /// ÷„«‰  ‰›Ì– «·⁄„·Ì«  ⁄·Ï ŒÌÿ Ê«ÃÂ… «·„” Œœ„ (UI Dispatcher).
    /// </summary>
    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
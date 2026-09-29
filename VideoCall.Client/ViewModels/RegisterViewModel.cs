using System.Windows;
using VideoCall.Client.Contracts;
using VideoCall.Shared.Messages;

namespace VideoCall.Client.ViewModels;

// إنشاء حساب جديد (يُخزَّن في SQL Server؛ كلمة المرور تُرسل للخادم ليجزّئها ولا تُحفظ على العميل)
public sealed class RegisterViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkClient _network;
    private string _serverAddress;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string _status = string.Empty;
    private bool _busy;

    public RegisterViewModel(INetworkClient network, string serverAddress)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _serverAddress = string.IsNullOrWhiteSpace(serverAddress) ? "127.0.0.1" : serverAddress;
        _network.RegisterResponseReceived += OnRegisterResponse;
    }

    public string ServerAddress { get => _serverAddress; set => SetField(ref _serverAddress, value); }

    public string Username { get => _username; set => SetField(ref _username, value); }

    public string DisplayName { get => _displayName; set => SetField(ref _displayName, value); }

    public string Status { get => _status; private set => SetField(ref _status, value); }

    public bool IsBusy { get => _busy; private set => SetField(ref _busy, value); }

    // يُطلق باسم المستخدم المسجَّل عند النجاح
    public event Action<string>? Registered;

    public async Task RegisterAsync(string password, string confirmPassword)
    {
        if (IsBusy) return;

        var name = Username.Trim();
        if (name.Length < 3 || name.Length > 32)
        {
            Status = ErrorTexts.ForCode(ErrorCodes.InvalidUsername);
            return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            Status = ErrorTexts.ForCode(ErrorCodes.WeakPassword);
            return;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            Status = "كلمتا المرور غير متطابقتين.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ServerAddress))
        {
            Status = "أدخل عنوان الخادم.";
            return;
        }

        IsBusy = true;
        Status = "جارٍ إنشاء الحساب...";

        if (!_network.IsConnected && !await _network.ConnectAsync(ServerAddress.Trim()))
        {
            IsBusy = false;
            Status = "تعذر الاتصال بالخادم.";
            return;
        }

        await _network.RegisterAsync(name, password, string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim());
    }

    private void OnRegisterResponse(RegisterResponsePayload response)
    {
        OnUi(() =>
        {
            IsBusy = false;
            if (response.Success)
            {
                Status = string.Empty;
                Registered?.Invoke(response.Username ?? Username.Trim());
                return;
            }

            Status = ErrorTexts.ForCode(response.ErrorCode);
        });
    }

    public void Dispose() => _network.RegisterResponseReceived -= OnRegisterResponse;

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}

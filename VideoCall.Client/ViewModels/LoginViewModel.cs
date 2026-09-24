using System.Windows;
using VideoCall.Client.Services;
using VideoCall.Shared.Messages;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.ViewModels;

/// ‰„Ê–Ã «·⁄—÷ (ViewModel) «·Œ«’ »‰«›–…  ”ÃÌ· «·œŒÊ·.
/// Ì ⁄«„· „⁄ „œŒ·«  «·„” Œœ„° ÊÌ Õﬂ„ »ÿ·» «·« ’«· ⁄»— «·‘»ﬂ… Ê≈œ«—… Õ«·… «·‘«‘….
public sealed class LoginViewModel : ViewModelBase, IDisposable
{
    // Œœ„«  Ê”«∆ÿ «·« ’«· Ê«·ÕﬁÊ· «·Œ«’… »Õ«·… «·‰„Ê–Ã
    private readonly INetworkClient _network;
    private string _serverAddress = "127.0.0.1";
    private string _username = string.Empty;
    private string _status = string.Empty;
    private bool _busy;

    // ⁄‰Ê«‰ «·Œ«œ„ «·„—«œ «·« ’«· »Â
    public string ServerAddress
    {
        get => _serverAddress;
        set => SetField(ref _serverAddress, value);
    }

    // «”„ «·„” Œœ„ «·„œŒ·
    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
    }

    // ‰’ Õ«·… «·« ’«· √Ê —”«∆· «·Œÿ√ «·„⁄—Ê÷… ··„” Œœ„
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    // „ƒ‘— Ì»Ì‰ „« ≈–« ﬂ«‰  Â‰«ﬂ ⁄„·Ì… « ’«· Ã«—Ì… ·„‰⁄  ﬂ—«— «·÷€ÿ
    public bool IsBusy
    {
        get => _busy;
        private set => SetField(ref _busy, value);
    }

    // ÕœÀ Ì „ ≈ÿ·«ﬁÂ ⁄‰œ ‰Ã«Õ ⁄„·Ì…  ”ÃÌ· «·œŒÊ· ··«‰ ﬁ«· ··‘«‘… «· «·Ì…
    public event Action? LoginSucceeded;
    /// „‰‘∆ «·ﬂ·«”: ÌÕﬁ‰ Œœ„… «·‘»ﬂ… ÊÌ‘ —ﬂ ›Ì «” ﬁ»«· —œÊœ «·Œ«œ„.
    public LoginViewModel(INetworkClient network)
    {
        _network = network;
        // «·«‘ —«ﬂ ›Ì ÕœÀ «” ﬁ»«· —œ  ”ÃÌ· «·œŒÊ· „‰ «·Œ«œ„
        _network.LoginResponseReceived += OnLoginResponse;
    }
    /// ≈—”«· ÿ·»  ”ÃÌ· «·œŒÊ· »‘ﬂ· €Ì— „ “«„‰ (Async).
    public async Task LoginAsync(string password)
    {
        // „‰⁄  ‰›Ì– «·ÿ·» ≈–« ﬂ«‰  Â‰«ﬂ ⁄„·Ì… Ã«—Ì… »«·›⁄·
        if (IsBusy)
            return;

        // «· Õﬁﬁ „‰ «ﬂ „«· ﬂ«›… «·»Ì«‰«  «·„ÿ·Ê»… ﬁ»· »œ¡ «·« ’«·
        if (string.IsNullOrWhiteSpace(ServerAddress) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(password))
        {
            Status = "√œŒ· ⁄‰Ê«‰ «·Œ«œ„ Ê«”„ «·„” Œœ„ Êﬂ·„… «·„—Ê—.";
            return;
        }

        IsBusy = true;
        Status = "Ã«—Ì «·« ’«· »«·Œ«œ„...";

        // «· √ﬂœ „‰ ÊÃÊœ « ’«· ›⁄·Ì »«·Œ«œ„ √Ê ≈‰‘«∆Â
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

        // ≈—”«· »Ì«‰«  «·„” Œœ„ Êﬂ·„… «·„—Ê— ⁄»— «·‘»ﬂ…
        await _network.LoginAsync(Username.Trim(), password);
    }
    /// „⁄«·Ã ÕœÀ «” ·«„ —œ  ”ÃÌ· «·œŒÊ· „‰ «·Œ«œ„.
    private void OnLoginResponse(LoginResponsePayload response)
    {
        // ÷„«‰  ‰›Ì– «· ⁄œÌ·«  ⁄·Ï Ê«ÃÂ… «·„” Œœ„ œ«Œ· „”·ﬂ «·‹ UI «·Œ«’ »‹ WPF
        OnUi(() =>
        {
            IsBusy = false;

            // ›Ì Õ«· ‰Ã«Õ «·œŒÊ·: ≈⁄«œ… ÷»ÿ «·Õ«·«  Ê≈ÿ·«ﬁ ÕœÀ «·‰Ã«Õ
            if (response.Success)
            {
                Status = string.Empty;
                LoginSucceeded?.Invoke();
                return;
            }

            // ›Ì Õ«· «·›‘·:  ÕœÌœ ”»» «·Œÿ√ Ê⁄—÷ «·—”«·… «·„‰«”»…
            Status = response.ErrorCode switch
            {
                ErrorCodes.InvalidCredentials => "»Ì«‰«  «·œŒÊ· €Ì— ’ÕÌÕ….",
                ErrorCodes.AlreadyLoggedIn => "«·„” Œœ„ „”Ã· «·œŒÊ· „”»ﬁ«.",
                _ => "›‘·  ”ÃÌ· «·œŒÊ·."
            };
        });
    }
    /// ≈·€«¡ «·«‘ —«ﬂ ›Ì «·√Õœ«À ⁄‰œ «· Œ·’ „‰ «·ﬂ«∆‰ ·„‰⁄ «· ”—Ì» ›Ì «·–«ﬂ—… (Memory Leaks).
    public void Dispose() => _network.LoginResponseReceived -= OnLoginResponse;
    /// œ«·… „”«⁄œ… ·÷„«‰  ‰›Ì– «·⁄„·Ì«  «·»—„ÃÌ… ›Ì «·„”·ﬂ «·—∆Ì”Ì ··Ê«ÃÂ… (UI Thread).
    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}
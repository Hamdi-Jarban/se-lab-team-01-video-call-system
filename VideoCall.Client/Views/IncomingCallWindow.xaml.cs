using System.Windows;
using System.Threading;

namespace VideoCall.Client.Views;

public partial class IncomingCallWindow : Window
{
    // ãÊÛíÑ ÊÊÈÚ áÖãÇä ÇÊÎÇĞ ŞÑÇÑ æÇÍÏ İŞØ (ŞÈæá Ãæ ÑİÖ) æÊÌäÈ ÇáäŞÑ ÇáãÒÏæÌ
    private int _handled;

    public event Action? Accepted;
    public event Action? Rejected;

    public IncomingCallWindow(string caller)
    {
        InitializeComponent();
        CallerText.Text = $"{caller} íÊÕá Èß";
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        // ÇÓÊÎÏÇã Interlocked áÖãÇä ÇáÃãÇä (Thread-Safe) æÚÏã ÅØáÇŞ ÇáÍÏË ÃßËÑ ãä ãÑÉ
        if (Interlocked.Exchange(ref _handled, 1) != 0)
            return;
        Accepted?.Invoke();
    }

    private void Reject_Click(object sender, RoutedEventArgs e)
    {
        // ÅÍÈÇØ ÇáÚãáíÉ ÅĞÇ Êã ÇáäŞÑ ãÓÈŞÇğ Úáì Ãí ãä ÇáÒÑíä
        if (Interlocked.Exchange(ref _handled, 1) != 0)
            return;
        Rejected?.Invoke();
    }
}
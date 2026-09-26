using System.Windows;
using VideoCall.Shared.Messages;
using VideoCall.Client.Services;
using VideoCall.Client.Contracts;

namespace VideoCall.Client.Views;

// ÇáßæÏ ÇáÎáİí áäÇİĞÉ ÇáÏÚæÇÊ¡ íÏíÑ ÇÓÊÌÇÈÉ ÇáãÓÊÎÏã ÇáÓÑíÚÉ ááØáÈÇÊ ÇáæÇÑÏÉ (ŞÈæá Ãæ ÑİÖ)
public partial class RoomInviteWindow : Window
{
    private readonly INetworkClient _network;
    private readonly RoomInvitePayload _invite;

    // ãÊÛíÑ ááÊÍßã İí ÍÇáÉ ÇáäŞÑ æãäÚ ÇáÇÓÊÏÚÇÁ ÇáãÒÏæÌ (Double-click) ÈÔßá Âãä (Thread-safe)
    private int _handled;

    public RoomInviteWindow(INetworkClient network, RoomInvitePayload invite)
    {
        InitializeComponent();
        _network = network;
        _invite = invite;

        // ÊãÑíÑ ÊİÇÕíá ÇáÏÚæÉ (ÇÓã ÇáãÖíİ æÑŞã ÇáÛÑİÉ) áÚÑÖåÇ ãÈÇÔÑÉ İí ÇáæÇÌåÉ
        InviteText.Text = $"ÏÚÇß {invite.Host} ááÇäÖãÇã Åáì ÇáÛÑİÉ: {invite.RoomId}";
    }

    private async void Accept_Click(object sender, RoutedEventArgs e)
    {
        // ÇÓÊÎÏÇã Interlocked áÖãÇä ÊäİíĞ ÅÌÑÇÁ ÇáŞÈæá ãÑÉ æÇÍÏÉ İŞØ æÊÌÇåá Ãí äŞÑÇÊ ãßÑÑÉ
        if (Interlocked.Exchange(ref _handled, 1) != 0)
            return;
        try
        {
            await _network.AcceptRoomInviteAsync(_invite);

            // ÅÛáÇŞ ÇáäÇİĞÉ ÈäÌÇÍ æÅÑÌÇÚ ÇáäÊíÌÉ ááäÇİĞÉ ÇáÃÓÇÓíÉ
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ÊÚĞÑ ŞÈæá ÇáÏÚæÉ: {ex.Message}", "ÎØÃ", MessageBoxButton.OK, MessageBoxImage.Error);

            // ÅÚÇÏÉ ÊÚííä ÇáÍÇáÉ İí ÍÇá ÇáİÔá áíÊãßä ÇáãÓÊÎÏã ãä ÇáãÍÇæáÉ ãÌÏÏÇğ
            Interlocked.Exchange(ref _handled, 0);
        }
    }

    private async void Reject_Click(object sender, RoutedEventArgs e)
    {
        // ãäÚ ÊäİíĞ ÅÌÑÇÁ ÇáÑİÖ ÃßËÑ ãä ãÑÉ
        if (Interlocked.Exchange(ref _handled, 1) != 0)
            return;
        try
        {
            await _network.RejectRoomInviteAsync(_invite);
        }
        finally
        {
            // ÖãÇä ÅÛáÇŞ ÇáäÇİĞÉ ÓæÇÁ äÌÍ ÅÑÓÇá ØáÈ ÇáÑİÖ ÚÈÑ ÇáÔÈßÉ Ãã áÇ
            DialogResult = false;
        }
    }
}
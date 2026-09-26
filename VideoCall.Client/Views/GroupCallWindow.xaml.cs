using System.Windows;
using VideoCall.Client.ViewModels;

namespace VideoCall.Client.Views;

public partial class GroupCallWindow : Window
{
    private readonly GroupCallViewModel _viewModel;

    public GroupCallWindow(GroupCallViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        // —»ÿ Ê«ÃÂ… «·„” Œœ„ »‰„Ê–Ã «·⁄—÷ (Data Binding)
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        //  Õ—Ì— „Ê«—œ «·ﬂ«„Ì—« Ê«·‘»ﬂ… ›Ê— ≈€·«ﬁ «·‰«›–… ·„‰⁄  ”—» «·–«ﬂ—…
        _viewModel.Dispose();

        base.OnClosed(e);
    }
}
using System.Windows;
using System.Windows.Controls;
using VideoCall.Client.ViewModels;

namespace VideoCall.Client.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        // Issue #3 Ì⁄ „œ ⁄·Ï MainViewModel ·≈œ«—… Õ«·… «·ÿ·».
        _viewModel = viewModel;

        DataContext = _viewModel;
    }

    /// <summary>
    /// Issue #3:  „—Ì— «·„” Œœ„ «·„Õœœ ≈·Ï ViewModel.
    /// </summary>
    private async void Call_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button { Tag: string target })
        {
            await _viewModel.RequestPrivateCallAsync(target);
        }
    }
}

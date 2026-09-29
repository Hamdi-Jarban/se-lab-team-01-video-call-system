using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VideoCall.Client.ViewModels;

namespace VideoCall.Client.Views;

// نافذة المحادثات. الـ ViewModel مملوك للنافذة الرئيسية (يبقى يستقبل الرسائل حتى لو أُغلقت هذه النافذة)
public partial class ChatWindow : Window
{
    private readonly ChatViewModel _viewModel;

    public ChatWindow(ChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        _viewModel.ScrollToEndRequested += OnScrollToEndRequested;
    }

    // القائمة (تعديل/حذف) تظهر فقط على رسائلي أنا
    private void Bubble_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ChatMessageItemViewModel { IsMine: true } })
            e.Handled = true;
    }

    private void EditMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChatMessageItemViewModel message })
            _viewModel.BeginEdit(message);
    }

    private async void DeleteMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ChatMessageItemViewModel message }) return;

        var answer = MessageBox.Show(this, "حذف هذه الرسالة لدى جميع الأعضاء؟", "تأكيد الحذف",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
            await _viewModel.DeleteMessageAsync(message);
    }

    private void OnScrollToEndRequested()
    {
        // ننتظر اكتمال التخطيط قبل التمرير إلى آخر رسالة
        Dispatcher.BeginInvoke(new Action(() => MessagesScroll.ScrollToEnd()), DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ScrollToEndRequested -= OnScrollToEndRequested;
        base.OnClosed(e);
    }
}

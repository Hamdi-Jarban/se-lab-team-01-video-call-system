using System.Windows.Media.Imaging;

namespace VideoCall.Client.ViewModels;

/// íÍÊİÙ ÈÍÇáÉ ÇáãÔÇÑß (ÇÓã ÇáãÓÊÎÏã¡ ÏİŞ ÇáİíÏíæ ÇáãÈÇÔÑ¡ ÍÇáÉ ÇáßÇãíÑÇ æÇáãÇíßÑæİæä)
/// æíæİÑ ÑÈØ ÈíÇäÇÊ (Data Binding) ÓáÓ ãÚ æÇÌåÉ ÇáãÓÊÎÏã İí WPF.
/// </summary>
public sealed class RemoteParticipantViewModel : ViewModelBase
{
    private BitmapSource? _video;
    private bool _isCameraOn = true;
    private bool _isMuted;

    /// ÇÓã ÇáãÓÊÎÏã ÇáÎÇÕ ÈÇáãÔÇÑß ÇáÈÚíÏ.
    public string Username { get; }

    /// ÅØÇÑ ÇáİíÏíæ ÇáÍÇáí ÇáÎÇÕ ÈÇáãÔÇÑß¡ íÊã ÊÍÏíËå ÊáŞÇÆíÇğ æÊäÈíå æÇÌåÉ ÇáãÓÊÎÏã áÅÚÇÏÉ ÇáÑÓã ÚÈÑ SetField.
    public BitmapSource? Video { get => _video; set => SetField(ref _video, value); }

    /// ÍÇáÉ ÊÔÛíá ÇáßÇãíÑÇ ááãÔÇÑß ÇáÈÚíÏ (ãİÚáÉ/ãÚØáÉ).
    public bool IsCameraOn { get => _isCameraOn; set => SetField(ref _isCameraOn, value); }

    /// ÍÇáÉ ßÊã ÇáÕæÊ ááãÔÇÑß ÇáÈÚíÏ (ãßÊæã/ãİÚá).
    public bool IsMuted { get => _isMuted; set => SetField(ref _isMuted, value); }

    /// ÊåíÆÉ äãæĞÌ ÇáÚÑÖ æÊÚííä ÇÓã ÇáãÔÇÑß ÚäÏ ÇäÖãÇãå ááÛÑİÉ.
    public RemoteParticipantViewModel(string username) => Username = username;
}
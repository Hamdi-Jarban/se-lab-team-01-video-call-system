using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VideoCall.Client.Views;

/// <summary>
/// İÆÇÊ ãÓÇÚÏÉ (Converters) ÊõÓÊÎÏã İí XAML áÊÍæíá ÇáÈíÇäÇÊ ãä ÇáÜ ViewModel 
/// Åáì ÕíÛ ÊİåãåÇ æÇÌåÉ ÇáãÓÊÎÏã (ãËá ÊÍæíá ÇáŞíã ÇáãäØŞíÉ Åáì ãÑÆí/ãÎİí).
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && flag ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility == Visibility.Visible;
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag;
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && !flag ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// ãÍæáÇÊ ãÎÕÕÉ áÊÍÏíË äÕæÕ ÃÒÑÇÑ ÇáÊÍßã İí ÇáãßÇáãÉ ÏíäÇãíßíÇğ ÈÇááÛÉ ÇáÚÑÈíÉ
public sealed class MuteLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "ÅáÛÇÁ ÇáßÊã" : "ßÊã ÇáÕæÊ";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CameraLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "ÅíŞÇİ ÇáßÇãíÑÇ" : "ÊÔÛíá ÇáßÇãíÑÇ";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
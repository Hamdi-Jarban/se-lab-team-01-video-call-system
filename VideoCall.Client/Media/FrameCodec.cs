using System.IO;
using System.Windows.Media.Imaging;

namespace VideoCall.Client.Media;

// İÆÉ ãÓÄæáÉ Úä ÚãáíÇÊ ÇáÊÑãíÒ æÇáİß (Codec) ááÕæÑ æÇáÅØÇÑÇÊ ÇáãÚÑæÖÉ İí ÇáæÇÌåÉ
public static class FrameCodec
{
    // ÊÍæíá ãÕİæİÉ ÇáÈÇíÊÇÊ ÇáãÔİÑÉ Åáì ßÇÆä BitmapSource ÕÇáÍ ááÚÑÖ ÇáãÈÇÔÑ İí æÇÌåÉ WPF
    public static BitmapSource BytesToBitmapSource(byte[] encodedBytes)
    {
        if (encodedBytes is null || encodedBytes.Length == 0)
            throw new ArgumentException("Image bytes are empty.", nameof(encodedBytes));

        using var stream = new MemoryStream(encodedBytes, writable: false);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        // ÊÌãíÏ ÇáÅØÇÑ (Freeze) áÊÌÇæÒ ŞíæÏ ÇáÎíæØ (Cross-thread) æÇáÓãÇÍ ÈÚÑÖå ãÈÇÔÑÉ İí æÇÌåÉ ÇáãÓÊÎÏã WPF
        frame.Freeze();
        return frame;
    }
}
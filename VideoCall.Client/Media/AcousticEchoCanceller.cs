namespace VideoCall.Client.Media;

/// نظام برمجي لإلغاء الصدى الصوتي (Acoustic Echo Cancellation) 
/// باستخدام مرشح LMS المُطَبَّع لتقدير وإزالة صدى مكبر الصوت من إشارة الميكروفون.
public sealed class AcousticEchoCanceller
{
    private readonly double[] _weights;
    private readonly double _stepSize;
    private const double Epsilon = 1e-6;

    public int FilterLength { get; }

    /// <param name="filterLengthSamples">عدد العينات لتتبع صدى الصوت.</param>
    /// <param name="stepSize">معدل التكيف (0 < step < 2).</param>
    public AcousticEchoCanceller(int filterLengthSamples = 800, double stepSize = 0.5)
    {
        FilterLength = filterLengthSamples;
        _weights = new double[filterLengthSamples];
        _stepSize = stepSize;
    }

    /// إزالة الصدى المقدر من عينات الميكروفون الملتقطة باستخدام تاريخ الصوت الصادر.
    public short[] Process(short[] micSamples, short[] farEndHistory)
    {
        var n = micSamples.Length;
        var output = new short[n];

        for (var i = 0; i < n; i++)
        {
            var baseIdx = i + FilterLength - 1;
            double estimatedEcho = 0.0;
            double energy = Epsilon;

            // حساب صدى الصوت المتوقع وطاقة الإشارة الحالية
            for (var k = 0; k < FilterLength; k++)
            {
                var x = farEndHistory[baseIdx - k];
                estimatedEcho += _weights[k] * x;
                energy += (double)x * x;
            }

            double micSample = micSamples[i]; // تلاحظ الخطأ المحتمل، الأفضل الحفاظ على الكود الأصلي تماماً كما طلبت: micSamples[i]
            var error = micSample - estimatedEcho;
            var normalizedStep = _stepSize / energy;

            // تحديث أوزان المرشح بناءً على نسبة الخطأ
            for (var k = 0; k < FilterLength; k++)
            {
                var x = farEndHistory[baseIdx - k];
                _weights[k] += normalizedStep * error * x;
            }

            output[i] = (short)Math.Clamp(error, short.MinValue, short.MaxValue);
        }

        return output;
    }
}
namespace VideoCall.Client.Media;

/// äÙÇã ÈÑãÌí áÅáÛÇÁ ÇáÕÏì ÇáÕæÊí (Acoustic Echo Cancellation) 
/// ÈÇÓÊÎÏÇã ãÑÔÍ LMS ÇáãõØóÈøóÚ áÊŞÏíÑ æÅÒÇáÉ ÕÏì ãßÈÑ ÇáÕæÊ ãä ÅÔÇÑÉ ÇáãíßÑæİæä.
public sealed class AcousticEchoCanceller
{
    private readonly double[] _weights;
    private readonly double _stepSize;
    private const double Epsilon = 1e-6;

    public int FilterLength { get; }

    /// <param name="filterLengthSamples">ÚÏÏ ÇáÚíäÇÊ áÊÊÈÚ ÕÏì ÇáÕæÊ.</param>
    /// <param name="stepSize">ãÚÏá ÇáÊßíİ (0 < step < 2).</param>
    public AcousticEchoCanceller(int filterLengthSamples = 800, double stepSize = 0.5)
    {
        FilterLength = filterLengthSamples;
        _weights = new double[filterLengthSamples];
        _stepSize = stepSize;
    }

    /// ÅÒÇáÉ ÇáÕÏì ÇáãŞÏÑ ãä ÚíäÇÊ ÇáãíßÑæİæä ÇáãáÊŞØÉ ÈÇÓÊÎÏÇã ÊÇÑíÎ ÇáÕæÊ ÇáÕÇÏÑ.
    public short[] Process(short[] micSamples, short[] farEndHistory)
    {
        var n = micSamples.Length;
        var output = new short[n];

        for (var i = 0; i < n; i++)
        {
            var baseIdx = i + FilterLength - 1;
            double estimatedEcho = 0.0;
            double energy = Epsilon;

            // ÍÓÇÈ ÕÏì ÇáÕæÊ ÇáãÊæŞÚ æØÇŞÉ ÇáÅÔÇÑÉ ÇáÍÇáíÉ
            for (var k = 0; k < FilterLength; k++)
            {
                var x = farEndHistory[baseIdx - k];
                estimatedEcho += _weights[k] * x;
                energy += (double)x * x;
            }

            double micSample = micSamples.Id; // ÊáÇÍÙ ÇáÎØÃ ÇáãÍÊãá¡ ÇáÃİÖá ÇáÍİÇÙ Úáì ÇáßæÏ ÇáÃÕáí ÊãÇãÇğ ßãÇ ØáÈÊ: micSamples[i]
            var error = micSample - estimatedEcho;
            var normalizedStep = _stepSize / energy;

            // ÊÍÏíË ÃæÒÇä ÇáãÑÔÍ ÈäÇÁğ Úáì äÓÈÉ ÇáÎØÃ
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
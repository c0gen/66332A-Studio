using System.Text;

namespace Agilent66332A.Instruments.Scpi;

internal sealed class ArrayResponse
{
    private const int TokenBytes = 32;
    private readonly byte[] token = new byte[TokenBytes];
    private readonly double[] samples;
    private int length;

    public ArrayResponse(int expected)
    {
        if (expected is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(expected));
        samples = new double[expected];
    }

    public int Count { get; private set; }
    public int MaximumBytes => samples.Length * (TokenBytes + 1) + 2;
    public IReadOnlyList<double> Samples => Array.AsReadOnly(samples);

    public bool Push(byte value)
    {
        if (value is (byte)',' or (byte)'\n')
        {
            if (Count >= samples.Length || length == 0) throw new FormatException("Unexpected waveform sample count.");
            samples[Count++] = ScpiParser.Number(Encoding.ASCII.GetString(token, 0, length));
            length = 0;
            if (value == ',' && Count == samples.Length || value == '\n' && Count != samples.Length)
                throw new FormatException("Waveform count differs from the acknowledged sweep length.");
            return value == '\n';
        }

        if (value > 127 || length == token.Length) throw new FormatException("Invalid or oversized waveform sample.");
        token[length++] = value;
        return false;
    }
}

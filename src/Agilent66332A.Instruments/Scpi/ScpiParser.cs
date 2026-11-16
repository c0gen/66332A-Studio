using System.Globalization;
using System.Text;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Instruments.Scpi;

public static class ScpiParser
{
    public static double Number(string text)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            throw new FormatException($"Invalid finite SCPI number: {text}");
        return value;
    }
    public static bool Boolean(string text) => text.Trim() switch { "0" => false, "1" => true, _ => throw new FormatException("Expected SCPI 0 or 1.") };
    public static int Register(string text)
    {
        double value = Number(text);
        if (value < 0 || value > 65535 || value != Math.Truncate(value)) throw new FormatException("Invalid 16-bit status register.");
        return (int)value;
    }
    public static InstrumentIdentity Identity(string text)
    {
        string[] parts = text.Trim().Split(',');
        if (parts.Length != 4 || parts[1].Trim() != "66332A" ||
            !new[] { "HEWLETT-PACKARD", "HP", "AGILENT TECHNOLOGIES", "AGILENT", "KEYSIGHT TECHNOLOGIES", "KEYSIGHT" }.Contains(parts[0].Trim(), StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException("Only an identified HP/Agilent/Keysight 66332A is supported.");
        return new(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim(), text);
    }
    public static (int Code, string Message) Error(string text)
    {
        int comma = text.IndexOf(',');
        if (comma < 1 || !int.TryParse(text.AsSpan(0, comma), CultureInfo.InvariantCulture, out int code)) throw new FormatException("Malformed SCPI error.");
        string message = text[(comma + 1)..].Trim();
        if (message.Length < 2 || message[0] != '"' || message[^1] != '"') throw new FormatException("Expected quoted SCPI error text.");
        return (code, message[1..^1].Replace("\"\"", "\""));
    }
    public static string Format(double value) => double.IsFinite(value) ? value.ToString("G17", CultureInfo.InvariantCulture) : throw new ArgumentException("SCPI values must be finite.");
}

public sealed class LineFramer
{
    private readonly List<byte> pending = [];
    public string? Push(byte value)
    {
        if (value == 10)
        {
            string text = Encoding.ASCII.GetString(pending.ToArray()).TrimEnd('\r');
            pending.Clear();
            return text;
        }
        if (value > 127 || pending.Count >= 4096) throw new FormatException("Non-ASCII or oversized SCPI response.");
        pending.Add(value);
        return null;
    }
    public void Clear() => pending.Clear();
}

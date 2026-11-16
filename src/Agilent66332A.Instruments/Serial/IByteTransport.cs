using Agilent66332A.Core.Models;

namespace Agilent66332A.Instruments.Serial;

public interface IByteTransport : IDisposable
{
    bool IsOpen { get; }
    void Open(ConnectionProfile profile);
    void Write(ReadOnlySpan<byte> bytes);
    // Read must return or throw TimeoutException within one bounded slice.
    int Read(Span<byte> buffer);
    void Close();
    void SendBreak(CancellationToken token);
    void DiscardInput();
}

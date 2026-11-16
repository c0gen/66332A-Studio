namespace Agilent66332A.Core.Models;

public enum OutputBandwidth { Normal, Fast, Unknown }
public enum DigitalPortFunction { InhibitFault, DigitalIo }
public enum InhibitMode { Off, Latching, Live }
public enum FaultIndicatorSource { Off, Questionable, Operation, StandardEvent, RequestService }
public enum RelayPolarity { Normal, Reverse }
public enum PanelDisplayMode { Normal, Text }
public enum PanelControlMode { Local, Remote, RemoteLockout }

public sealed record DigitalPortSettings(DigitalPortFunction Function, InhibitMode Inhibit,
    bool FaultIndicatorEnabled, FaultIndicatorSource FaultSource);
public sealed record DigitalPortReading(int RawValue, DateTimeOffset ObservedUtc)
{
    public bool Output0 => (RawValue & 1) != 0;
    public bool Output1 => (RawValue & 2) != 0;
    public bool Pin3High => (RawValue & 4) != 0;
}
public sealed record RelaySettings(bool Closed, RelayPolarity Polarity);
public sealed record PanelDisplaySettings(bool Enabled, PanelDisplayMode Mode, string Text);
public sealed record InstrumentSetupSnapshot(InstrumentIdentity Identity, string ScpiVersion,
    IReadOnlyList<string> Options, OutputBandwidth OutputBandwidth, DigitalPortSettings Port,
    DigitalPortReading? DigitalReading, RelaySettings? Relay, PanelDisplaySettings Display, DateTimeOffset ObservedUtc);

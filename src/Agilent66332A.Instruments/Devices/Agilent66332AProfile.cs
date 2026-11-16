using Agilent66332A.Core.Models;

namespace Agilent66332A.Instruments.Devices;

public static class Agilent66332AProfile
{
    // Programming guide table 3-1: CV bit 8, CC+ bit 10, CC- bit 11.
    public static InstrumentStatus Status(int operation, int questionable, DateTimeOffset now)
    {
        var faults = new List<string>();
        if ((questionable & 1) != 0) faults.Add("OVP");
        if ((questionable & 2) != 0) faults.Add("OCP");
        if ((questionable & 4) != 0) faults.Add("Fuse");
        if ((questionable & 16) != 0) faults.Add("Overtemperature");
        if ((questionable & 512) != 0) faults.Add("Remote inhibit");
        if ((questionable & 1024) != 0) faults.Add("Unregulated");
        int known = 1 | 2 | 4 | 16 | 512 | 1024 | 16384;
        if ((questionable & ~known) != 0) faults.Add($"Questionable 0x{questionable & ~known:X}");
        string mode = operation switch
        {
            _ when (operation & 1024) != 0 => "CC+ (source)",
            _ when (operation & 2048) != 0 => "CC− (sink)",
            _ when (operation & 256) != 0 => "CV",
            _ => "Unregulated / off"
        };
        return new(mode, operation, questionable, string.Join(", ", faults), now);
    }
}

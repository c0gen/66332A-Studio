using System.Runtime.InteropServices;
using System.IO;
using System.Security;
using System.Text;
using Agilent66332A.Core.Models;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Agilent66332A.Instruments.Serial;

namespace Agilent66332A.Desktop.Features.Connection;

public sealed record SerialPortOption(string PortName, string DisplayName, SerialAdapterIdentity? Adapter = null);

internal static class WindowsSerialPortDiscovery
{
    private static readonly Guid ComPortInterface = new("86E0D1E0-8089-11D0-9CE4-08003E301F73");
    private static readonly Guid PortsSetupClass = new("4D36E978-E325-11CE-BFC1-08002BE10318");
    private const uint Present = 0x02, DeviceInterface = 0x10;
    private const uint FriendlyName = 0x0C, DeviceDescription = 0, DriverProperty = 0x09;
    private const uint GlobalScope = 1, DeviceKey = 1, DriverKey = 2, QueryValue = 1;

    public static SerialPortOption[] GetPorts()
    {
        string[] ports = SerialPortTransport.GetPortNames();
        if (ports.Length == 0) return [];

        var devices = new Dictionary<string, SerialPortOption>(StringComparer.OrdinalIgnoreCase);
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadDevices(ComPortInterface, Present | DeviceInterface, devices, ambiguous);
        // Some legacy drivers register in the Ports setup class without exposing a COM interface.
        ReadDevices(PortsSetupClass, Present, devices, ambiguous);
        return ports.Select(port =>
        {
            var option = devices.GetValueOrDefault(port) ?? new(port, port);
            return ambiguous.Contains(port) ? option with { Adapter = null } : option;
        }).ToArray();
    }

    private static void ReadDevices(Guid deviceClass, uint flags, Dictionary<string, SerialPortOption> ports,
        HashSet<string> ambiguous)
    {
        using var devices = SetupDiGetClassDevsW(in deviceClass, 0, 0, flags);
        if (devices.IsInvalid) return;

        var device = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
        var buffer = new byte[1024];
        for (uint index = 0; SetupDiEnumDeviceInfo(devices, index, ref device); index++)
        {
            using var keyHandle = SetupDiOpenDevRegKey(devices, ref device, GlobalScope, 0, DeviceKey, QueryValue);
            if (keyHandle.IsInvalid) continue;

            using var key = RegistryKey.FromHandle(keyHandle);
            if (key.GetValue("PortName") is not string port) continue;

            string? name = ReadName(devices, ref device, FriendlyName, buffer)
                ?? ReadName(devices, ref device, DeviceDescription, buffer);
            var adapter = ReadAdapter(devices, ref device, buffer);
            string suffix = $"({port})";
            string label = name is null ? port : name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name : $"{name} {suffix}";
            if (ports.TryGetValue(port, out var previous))
            {
                if (adapter is not null && previous.Adapter is not null && !adapter.Matches(previous.Adapter))
                    ambiguous.Add(port);
                if (previous.Adapter is not null) adapter = previous.Adapter;
                if (previous.DisplayName != port) label = previous.DisplayName;
            }
            ports[port] = new(port, label, adapter);
        }
    }

    private static SerialAdapterIdentity? ReadAdapter(DeviceInfoSet devices, ref DeviceInfo device, byte[] buffer)
    {
        string? driverKey = ReadName(devices, ref device, DriverProperty, buffer);
        if (driverKey is null) return null;
        var instance = new char[256];
        if (!SetupDiGetDeviceInstanceIdW(devices, ref device, instance, (uint)instance.Length, out _)) return null;
        int length = Array.IndexOf(instance, '\0');
        if (length <= 0) return null;

        using var keyHandle = SetupDiOpenDevRegKey(devices, ref device, GlobalScope, 0, DriverKey, QueryValue);
        if (keyHandle.IsInvalid) return null;
        try
        {
            using var key = RegistryKey.FromHandle(keyHandle);
            if (key.GetValue("InfPath") is not string inf || key.GetValue("DriverVersion") is not string version) return null;
            var result = new SerialAdapterIdentity(new string(instance, 0, length), driverKey, inf.Trim(), version.Trim());
            return result.IsComplete ? result : null;
        }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException) { return null; }
    }

    private static string? ReadName(DeviceInfoSet devices, ref DeviceInfo device, uint property, byte[] buffer)
    {
        if (!SetupDiGetDeviceRegistryPropertyW(devices, ref device, property, 0, buffer, (uint)buffer.Length, out uint size) || size > buffer.Length)
            return null;

        string name = Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0').Trim();
        return name.Length == 0 ? null : name;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfo
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DevInst;
        public nint Reserved;
    }

    private sealed class DeviceInfoSet() : SafeHandleZeroOrMinusOneIsInvalid(true)
    {
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }

    [DllImport("setupapi.dll", ExactSpelling = true)]
    private static extern DeviceInfoSet SetupDiGetClassDevsW(in Guid classGuid, nint enumerator, nint parent, uint flags);

    [DllImport("setupapi.dll", ExactSpelling = true)]
    private static extern bool SetupDiEnumDeviceInfo(DeviceInfoSet devices, uint index, ref DeviceInfo device);

    [DllImport("setupapi.dll", ExactSpelling = true)]
    private static extern SafeRegistryHandle SetupDiOpenDevRegKey(DeviceInfoSet devices, ref DeviceInfo device,
        uint scope, uint hardwareProfile, uint keyType, uint access);

    [DllImport("setupapi.dll", ExactSpelling = true)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(DeviceInfoSet devices, ref DeviceInfo device,
        uint property, nint propertyType, [Out] byte[] buffer, uint bufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool SetupDiGetDeviceInstanceIdW(DeviceInfoSet devices, ref DeviceInfo device,
        [Out] char[] instanceId, uint instanceIdSize, out uint requiredSize);

    [DllImport("setupapi.dll", ExactSpelling = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(nint devices);
}

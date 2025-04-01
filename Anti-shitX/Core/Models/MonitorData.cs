using System;
using System.Collections.Generic;
using System.IO;

namespace AntiCheatClient.Core.Models
{
    public class MonitorData
    {
        public string ActivisionId { get; set; }
        public int ChannelId { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime ClientStartTime { get; set; }
        public string PcStartTime { get; set; }
        public bool IsGameRunning { get; set; }
        public List<ProcessInfo> Processes { get; set; } = new List<ProcessInfo>();
        public List<DeviceInfo> UsbDevices { get; set; } = new List<DeviceInfo>();
        public HardwareInfo HardwareInfo { get; set; } = new HardwareInfo();
        public SystemInfo SystemInfo { get; set; } = new SystemInfo();
        public List<string> GameConfigHashes { get; set; } = new List<string>();
        public List<NetworkConnection> NetworkConnections { get; set; } = new List<NetworkConnection>();
        public List<DriverInfo> LoadedDrivers { get; set; } = new List<DriverInfo>();
        public List<ServiceInfo> BackgroundServices { get; set; } = new List<ServiceInfo>();
        public Dictionary<string, object> MemoryInjections { get; set; } = new Dictionary<string, object>();
    }
}
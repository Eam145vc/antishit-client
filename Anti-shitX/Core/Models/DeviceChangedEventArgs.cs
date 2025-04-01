using System;
using AntiCheatClient.Core.Models;

namespace AntiCheatClient.DetectionEngine
{
    public class DeviceChangedEventArgs : EventArgs
    {
        public DeviceInfo Device { get; set; }
        public bool IsConnected { get; set; }
    }
}
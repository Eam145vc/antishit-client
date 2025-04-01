using System;

namespace AntiCheatClient.Core.Models
{
    public class SystemInfo
    {
        public string WindowsVersion { get; set; }
        public string DirectXVersion { get; set; }
        public string GpuDriverVersion { get; set; }
        public string ScreenResolution { get; set; }
        public string WindowsUsername { get; set; }
        public string ComputerName { get; set; }
        public string WindowsInstallDate { get; set; }
        public string LastBootTime { get; set; }
        public string FirmwareType { get; set; }
        public string LanguageSettings { get; set; }
        public string TimeZone { get; set; }
        public string FrameworkVersion { get; set; }
    }
}
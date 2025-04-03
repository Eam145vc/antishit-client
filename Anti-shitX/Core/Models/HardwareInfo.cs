namespace AntiCheatClient.Core.Models
{
    public class HardwareInfo
    {
        public string Cpu { get; set; }
        public string Gpu { get; set; }
        public string GpuDriverVersion { get; set; }  // Propiedad añadida
        public string Ram { get; set; }
        public string Motherboard { get; set; }
        public string Storage { get; set; }
        public string NetworkAdapters { get; set; }
        public string AudioDevices { get; set; }
        public string BiosVersion { get; set; }
        public string HardwareId { get; set; }
    }
}
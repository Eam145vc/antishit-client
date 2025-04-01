namespace AntiCheatClient.Core.Models
{
    public class DeviceInfo
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public string ConnectionStatus { get; set; }
        public string DeviceClass { get; set; }
        public string ClassGuid { get; set; }
        public string Driver { get; set; }
        public string HardwareId { get; set; }
        public string LocationInfo { get; set; }

        // Categorización para el dashboard
        public string TrustLevel { get; set; } = "Unknown"; // Trusted, Unknown, External
    }
}
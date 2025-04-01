namespace AntiCheatClient.Core.Models
{
    public class DriverInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string PathName { get; set; }
        public string Version { get; set; }
        public bool IsSigned { get; set; }
        public string SignatureInfo { get; set; }
        public string StartType { get; set; }
        public string State { get; set; }
    }
}
namespace AntiCheatClient.Core.Models
{
    public class ProcessInfo
    {
        public string Name { get; set; }
        public int Pid { get; set; }
        public string FilePath { get; set; }
        public string FileHash { get; set; }
        public string CommandLine { get; set; }
        public string FileVersion { get; set; }
        public bool IsSigned { get; set; }
        public string SignatureInfo { get; set; }
        public long MemoryUsage { get; set; }
        public string StartTime { get; set; }
    }
}
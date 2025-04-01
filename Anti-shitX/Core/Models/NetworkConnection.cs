namespace AntiCheatClient.Core.Models
{
    public class NetworkConnection
    {
        public string LocalAddress { get; set; }
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public string Protocol { get; set; }
        public string State { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
    }
}
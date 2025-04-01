using System;

namespace AntiCheatClient.Core.Models
{
    public class Player
    {
        public string ActivisionId { get; set; }
        public int ChannelId { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public bool IsGameRunning { get; set; }
        public DateTime ClientStartTime { get; set; }
        public string PcStartTime { get; set; }
    }
}
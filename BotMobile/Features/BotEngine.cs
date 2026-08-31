using System.Collections.Generic;

namespace BotMobile.Features;

/// <summary>Data dibagi antar fitur saat run (UID list dari menu UID, dsb).</summary>
public static class BotData
{
    public class RunContext
    {
        public List<string> Uids { get; set; } = new();
        public List<string> Links { get; set; } = new();
        public List<string> GroupThreads { get; set; } = new(); // thread group yang dibuat di run sama (port target_added_member_groups)
    }

    public static RunContext Context { get; } = new();
}

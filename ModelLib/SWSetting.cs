using System.ComponentModel;

namespace View3D
{
    public static class SWSetting
    {
        public static Memory Memory = new Memory();
    }

    public class Memory
    {
        public uint UsedLimit_64bit { get; private set; }
        public uint UsedLimit_32bit { get; private set; } 
        public uint RemainMin { get; private set; }
        public ulong LimitPercent { get; private set; }
        
        public Memory()
        {
            UsedLimit_64bit = 5120;     // 5GB
            UsedLimit_32bit = 1536;     // 1.5GB
            RemainMin = 100;            // 100MB
            LimitPercent = 30;
        }
    }
}

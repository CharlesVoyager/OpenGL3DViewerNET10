using System;
using System.Diagnostics;

namespace View3D.Utils
{
    public static class RamTools
    {
        public static bool IsRamSizeValid()
        {
            bool valid = true;

#if NET10_0_OR_GREATER
            var gcInfo = GC.GetGCMemoryInfo();
            ulong totalRam = (ulong)gcInfo.TotalAvailableMemoryBytes / 1024 / 1024;  // MB
            ulong committedRam = (ulong)gcInfo.TotalCommittedBytes / 1024 / 1024;  // MB
            ulong availRam = totalRam - committedRam;
#else   // .NET Framework 4.8
            ulong availRam = new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory / 1024 / 1024;    // Unit: MB
#endif

            if (availRam < SWSetting.Memory.RemainMin || getCurMemoryUsed() >= SWSetting.Memory.UsedLimit_64bit)
            {
                valid = false;
            }
            return valid;
        }

        public static uint getCurMemoryUsed()
        {
            Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            uint totalBytesOfMemoryUsed = Convert.ToUInt16(currentProcess.WorkingSet64 / 1024 / 1024);
            return totalBytesOfMemoryUsed;
        }
    }
}

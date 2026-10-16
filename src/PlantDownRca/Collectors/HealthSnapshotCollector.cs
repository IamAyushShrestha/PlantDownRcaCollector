using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using PlantDownRca.Models;

namespace PlantDownRca.Collectors
{
    /// <summary>
    /// Current-state snapshot at run time: disk space, named service statuses, OS, boot time.
    /// The report labels this clearly as NOW, not "at incident time" - but a disk still at 100%
    /// or a service still stopped is usually a confession.
    /// </summary>
    public static class HealthSnapshotCollector
    {
        public static HealthSnapshot Collect(string serverName, List<string> servicesToCheck)
        {
            var snap = new HealthSnapshot
            {
                ServerName = serverName,
                OsVersion = Environment.OSVersion.ToString(),
                CollectedAt = DateTime.Now
            };

            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                        continue;

                    double totalGb = drive.TotalSize / 1073741824.0;
                    double freeGb = drive.AvailableFreeSpace / 1073741824.0;
                    double pctFree = totalGb > 0 ? (freeGb / totalGb) * 100.0 : 0;
                    string flag = pctFree < 5 ? "  <-- CRITICAL: under 5% free" : (pctFree < 10 ? "  <-- low" : "");
                    snap.DriveLines.Add(string.Format("{0}  {1:F1} GB free of {2:F1} GB ({3:F1}% free){4}",
                        drive.Name, freeGb, totalGb, pctFree, flag));
                }
                catch (Exception ex)
                {
                    snap.Notes.Add("Drive check failed: " + ex.Message);
                }
            }

            foreach (var name in servicesToCheck ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                try
                {
                    using (var sc = new ServiceController(name))
                    {
                        snap.ServiceLines.Add(name + ": " + sc.Status);
                    }
                }
                catch (Exception)
                {
                    snap.ServiceLines.Add(name + ": not found on this server");
                }
            }

            try
            {
                // TickCount wraps every ~49 days, so this is a rough boot estimate - labelled as such.
                var boot = DateTime.Now.AddMilliseconds(-Environment.TickCount);
                snap.Notes.Add("Approximate last boot (from uptime): " + boot.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch { }

            return snap;
        }
    }
}

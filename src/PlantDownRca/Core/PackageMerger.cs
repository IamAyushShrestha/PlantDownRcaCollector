using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using PlantDownRca.Models;

namespace PlantDownRca.Core
{
    /// <summary>
    /// Multi-server workflow:
    ///   1. Copy the exe folder to EACH involved server (SQL node that was active, app server, Kepware box).
    ///   2. Run it on each with the same time window. Each run drops a folder containing entries.xml.
    ///   3. Copy those folders anywhere (your laptop is fine) and run merge mode:
    ///      PlantDownRca.exe --merge "C:\RCA\run-sql" "C:\RCA\run-app" --out "C:\RCA\merged"
    ///   4. Merge mode reads every entries.xml, rebuilds ONE timeline/report across servers.
    /// No server ever needs to reach another server - which is why this works across VPN/plant boundaries.
    /// </summary>
    public static class PackageMerger
    {
        public static RunResult Merge(IEnumerable<string> packageFolders)
        {
            var merged = new RunResult
            {
                RanAt = DateTime.Now,
                WindowFrom = DateTime.MaxValue,
                WindowTo = DateTime.MinValue
            };

            var plantNames = new List<string>();
            var servers = new List<string>();
            var snapshots = new List<string>();

            foreach (var folder in packageFolders)
            {
                string xml = Path.Combine(folder, "entries.xml");
                if (!File.Exists(xml))
                    throw new FileNotFoundException("No entries.xml in package folder: " + folder, xml);

                RunPackage pkg;
                var serializer = new XmlSerializer(typeof(RunPackage));
                using (var stream = new FileStream(xml, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    pkg = (RunPackage)serializer.Deserialize(stream);
                }

                if (pkg == null)
                    continue;

                merged.Entries.AddRange(pkg.Entries);
                merged.Collectors.AddRange(pkg.Collectors);

                if (!string.IsNullOrEmpty(pkg.PlantName) && !plantNames.Contains(pkg.PlantName))
                    plantNames.Add(pkg.PlantName);
                if (!string.IsNullOrEmpty(pkg.ServerName) && !servers.Contains(pkg.ServerName))
                    servers.Add(pkg.ServerName);

                if (pkg.WindowFrom < merged.WindowFrom) merged.WindowFrom = pkg.WindowFrom;
                if (pkg.WindowTo > merged.WindowTo) merged.WindowTo = pkg.WindowTo;

                if (pkg.Snapshot != null)
                {
                    // Keep the first snapshot as primary and note the others in the report collection list.
                    if (merged.Snapshot == null)
                        merged.Snapshot = pkg.Snapshot;
                    else
                        snapshots.Add(pkg.Snapshot.ServerName + " snapshot collected " +
                                        pkg.Snapshot.CollectedAt.ToString("yyyy-MM-dd HH:mm:ss") +
                                        " (" + pkg.Snapshot.DriveLines.Count + " drives, " +
                                        pkg.Snapshot.ServiceLines.Count + " services - see its own run folder)");
                }
            }

            if (merged.WindowFrom == DateTime.MaxValue) merged.WindowFrom = DateTime.Now.AddHours(-2);
            if (merged.WindowTo == DateTime.MinValue) merged.WindowTo = DateTime.Now;

            merged.PlantName = plantNames.Count > 0 ? plantNames[0] : "";
            merged.ServerName = "MERGED: " + string.Join(", ", servers.ToArray());
            merged.ServerRole = "Merged";

            foreach (var note in snapshots)
            {
                merged.Collectors.Add(new CollectorResult("Merged package snapshot")
                {
                    Success = true,
                    Notes = new List<string> { note }
                });
            }

            return merged;
        }
    }
}

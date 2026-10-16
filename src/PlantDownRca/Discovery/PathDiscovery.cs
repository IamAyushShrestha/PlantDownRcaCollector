using System;
using System.Collections.Generic;
using System.IO;

namespace PlantDownRca.Discovery
{
    /// <summary>
    /// File discovery helpers. Application log folders are configured explicitly (Configurator tab);
    /// only SQL Server ERRORLOG and Kepware log folders are auto-detected from standard install
    /// locations. File scans are depth-limited and file-time filtered so a run never crawls a whole
    /// drive of history.
    /// </summary>
    public static class PathDiscovery
    {
        /// <summary>Files worth reading: modified at/after (window start - lookback), newest first.</summary>
        public static List<string> FindRecentFiles(IEnumerable<string> folders, string[] patterns,
            DateTime windowFrom, TimeSpan lookback, List<string> notes)
        {
            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime cutoff = windowFrom - lookback;

            foreach (var folder in folders ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                if (!Directory.Exists(folder))
                {
                    if (notes != null) notes.Add("Folder not found (skipped): " + folder);
                    continue;
                }

                foreach (var pattern in patterns)
                {
                    string[] found;
                    try
                    {
                        found = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
                    }
                    catch (Exception ex)
                    {
                        if (notes != null) notes.Add("Could not scan " + folder + ": " + ex.Message);
                        continue;
                    }

                    foreach (var file in found)
                    {
                        try
                        {
                            var info = new FileInfo(file);
                            if (info.LastWriteTime >= cutoff && seen.Add(file))
                                files.Add(file);
                        }
                        catch
                        {
                            // Unreadable file metadata: skip the file, never fail the run.
                        }
                    }
                }
            }

            files.Sort(delegate(string a, string b)
            {
                return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
            });
            return files;
        }

        /// <summary>SQL: ...\Microsoft SQL Server\MSSQLxx.INSTANCE\MSSQL\Log on any drive,
        /// plus any folder literally named "Log" sitting next to a MSSQL folder.</summary>
        public static List<string> DiscoverSqlLogFolders()
        {
            var results = new List<string>();
            foreach (var drive in FixedDrives())
            {
                string pf = Path.Combine(drive, "Program Files", "Microsoft SQL Server");
                if (Directory.Exists(pf))
                {
                    try
                    {
                        foreach (var instDir in Directory.GetDirectories(pf, "MSSQL*"))
                        {
                            foreach (var sub in Directory.GetDirectories(instDir))
                            {
                                string log = Path.Combine(sub, "MSSQL", "Log");
                                if (Directory.Exists(log))
                                    results.Add(log);
                                string log2 = Path.Combine(sub, "Log");
                                if (Directory.Exists(log2))
                                    results.Add(log2);
                            }
                        }
                    }
                    catch { /* discovery must never fail the run */ }
                }
            }
            return Dedup(results);
        }

        /// <summary>Kepware: standard install locations (Program Files / ProgramData).</summary>
        public static List<string> DiscoverKepwareFolders()
        {
            var results = new List<string>();
            foreach (var drive in FixedDrives())
            {
                var roots = new[]
                {
                    Path.Combine(drive, "Program Files (x86)", "Kepware"),
                    Path.Combine(drive, "Program Files", "Kepware"),
                    Path.Combine(drive, "ProgramData", "Kepware")
                };
                foreach (var root in roots)
                {
                    if (Directory.Exists(root))
                        results.Add(root);
                }
            }
            return Dedup(results);
        }

        private static IEnumerable<string> FixedDrives()
        {
            var drives = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType == DriveType.Fixed && d.IsReady)
                        drives.Add(d.RootDirectory.FullName);
                }
                catch { }
            }
            return drives;
        }

        private static List<string> Dedup(List<string> items)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var output = new List<string>();
            foreach (var item in items)
            {
                if (seen.Add(item))
                    output.Add(item);
            }
            return output;
        }
    }
}

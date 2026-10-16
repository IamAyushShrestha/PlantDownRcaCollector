using System;
using System.Collections.Generic;
using System.IO;
using PlantDownRca.Collectors;
using PlantDownRca.Config;
using PlantDownRca.Models;

namespace PlantDownRca.Core
{
    /// <summary>Orchestrates one run: resolve folders (config or discovery), run every collector,
    /// collect the health snapshot. UI and CLI both call this - no logic lives in the form.</summary>
    public static class RunEngine
    {
        public static RunResult Run(AppConfig cfg, DateTime from, DateTime to, Action<string> progress)
        {
            string server = Environment.MachineName;
            var run = new RunResult
            {
                PlantName = cfg.PlantName ?? "",
                ServerName = server,
                ServerRole = cfg.ServerRole ?? "",
                WindowFrom = from,
                WindowTo = to,
                RanAt = DateTime.Now
            };

            // ---- Resolve folders: config wins, discovery fills empty ones ----
            var appFolders = new List<string>(cfg.AppLogFolders ?? new List<string>());
            var sqlFolders = new List<string>(cfg.SqlLogFolders ?? new List<string>());
            var kepFolders = new List<string>(cfg.KepwareLogFolders ?? new List<string>());

            if (cfg.DiscoveryEnabled)
            {
                if (appFolders.Count == 0)
                {
                    // Application logs are never auto-guessed (a machine has too many unrelated "logs"
                    // folders). Add the exact folders on the Configurator tab.
                    Note(progress, "No application log folders configured - add them on the Configurator tab.");
                }
                if (sqlFolders.Count == 0)
                {
                    sqlFolders = Discovery.PathDiscovery.DiscoverSqlLogFolders();
                    Note(progress, "Discovery: SQL log folders -> " + JoinOrNone(sqlFolders));
                }
                if (kepFolders.Count == 0)
                {
                    kepFolders = Discovery.PathDiscovery.DiscoverKepwareFolders();
                    Note(progress, "Discovery: Kepware folders -> " + JoinOrNone(kepFolders));
                }
            }

            // ---- Collectors (each isolated: one failing never kills the run) ----
            RunCollector(run, CollectorFactory.AppLogs(appFolders, server, from, to, cfg.IncludeWarnings), progress);
            RunCollector(run, CollectorFactory.SqlErrorLogs(sqlFolders, server, from, to, cfg.IncludeWarnings), progress);
            RunCollector(run, CollectorFactory.KepwareLogs(kepFolders, server, from, to, cfg.IncludeWarnings), progress);

            var eventCollector = new EventLogCollector(cfg.EventLogChannels, server, from, to, cfg.IncludeWarnings);
            RunCollector(run, eventCollector, progress);

            // ---- SQL Query Collectors (parameterized queries with @From/@To) ----
            // Only runs when EnableSqlQueries=true in config (portable default: OFF)
            if (cfg.EnableSqlQueries)
            {
                if (cfg.SqlQueryConnections == null || cfg.SqlQueryConnections.Count == 0)
                {
                    Note(progress, "SQL query collection enabled but no SqlQueryConnections configured");
                }
                else if (cfg.SqlQueries == null || cfg.SqlQueries.Count == 0)
                {
                    Note(progress, "SQL query collection enabled but no SqlQueries configured");
                }
                else
                {
                    var connMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in cfg.SqlQueryConnections)
                    {
                        if (!string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.ConnectionString))
                            connMap[c.Name] = c.ConnectionString;
                    }
                    Note(progress, "Found " + connMap.Count + " SQL connections and " + cfg.SqlQueries.Count + " queries");

                    foreach (var q in cfg.SqlQueries)
                    {
                        if (string.IsNullOrWhiteSpace(q.Name) || string.IsNullOrWhiteSpace(q.Sql))
                        {
                            Note(progress, "Skipping query: Name or Sql is empty");
                            continue;
                        }

                        if (connMap.TryGetValue(q.Connection, out var cs))
                        {
                            RunCollector(run, CollectorFactory.SqlQuery(cs, q, server, from, to), progress);
                        }
                        else
                        {
                            Note(progress, "SQL Query '" + q.Name + "': connection '" + q.Connection + "' not found. Available: " + string.Join(", ", connMap.Keys));
                        }
                    }
                }
            }
            else
            {
                Note(progress, "SQL query collection is DISABLED (see EnableSqlQueries in config)");
            }

            Note(progress, "Collecting health snapshot...");
            run.Snapshot = HealthSnapshotCollector.Collect(server, cfg.ServicesToCheck);
            run.Collectors.Add(new CollectorResult("Health snapshot")
            {
                Success = true,
                EntriesMatched = 0,
                Notes = new List<string> { "State at run time (see report)" }
            });

            return run;
        }

        private static void RunCollector(RunResult run, ICollector collector, Action<string> progress)
        {
            try
            {
                Note(progress, "Running: " + collector.Result.Name + "...");
                var entries = collector.Collect();
                run.Entries.AddRange(entries);
                run.Collectors.Add(collector.Result);
                Note(progress, "  -> " + entries.Count + " entries");
            }
            catch (Exception ex)
            {
                collector.Result.Success = false;
                collector.Result.Notes.Add("Collector failed: " + ex.Message);
                run.Collectors.Add(collector.Result);
                Note(progress, "  -> FAILED: " + ex.Message);
            }
        }

        private static void Note(Action<string> progress, string text)
        {
            if (progress != null)
                progress(text);
        }

        private static string JoinOrNone(List<string> items)
        {
            return items == null || items.Count == 0 ? "(none found)" : string.Join("; ", items.ToArray());
        }

        /// <summary>Default output folder next to the exe: RCA-yyyyMMdd-HHmmss. Never writes outside it.</summary>
        public static string DefaultOutputFolder()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            return Path.Combine(baseDir, "RCA-" + stamp);
        }
    }
}

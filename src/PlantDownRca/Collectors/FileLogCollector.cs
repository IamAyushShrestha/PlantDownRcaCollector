using System;
using System.Collections.Generic;
using System.IO;
using PlantDownRca.Config;
using PlantDownRca.Models;
using PlantDownRca.Parsing;

namespace PlantDownRca.Collectors
{
    public interface ICollector
    {
        CollectorResult Result { get; }
        List<LogEntry> Collect();
    }

    /// <summary>Generic text-log collector used for app logs, SQL ERRORLOG files, and Kepware logs.
    /// The source-specific collectors below are thin configurations of this one.</summary>
    public class FileLogCollector : ICollector
    {
        private readonly string _name;
        private readonly string _source;
        private readonly List<string> _folders;
        private readonly string[] _patterns;
        private readonly string _serverName;
        private readonly DateTime _from;
        private readonly DateTime _to;
        private readonly bool _includeWarnings;
        private readonly bool _isSqlLog;

        public CollectorResult Result { get; private set; }

        public FileLogCollector(string name, string source, List<string> folders, string[] patterns,
            string serverName, DateTime from, DateTime to, bool includeWarnings, bool isSqlLog)
        {
            _name = name;
            _source = source;
            _folders = folders ?? new List<string>();
            _patterns = patterns;
            _serverName = serverName;
            _from = from;
            _to = to;
            _includeWarnings = includeWarnings;
            _isSqlLog = isSqlLog;
            Result = new CollectorResult(name);
        }

        public List<LogEntry> Collect()
        {
            var entries = new List<LogEntry>();
            var notes = new List<string>();

            // Files written within 2 days before the window can still hold in-window lines
            // (rolling logs); the parser filters by entry timestamp, not file time.
            var files = Discovery.PathDiscovery.FindRecentFiles(
                _folders, _patterns, _from, TimeSpan.FromDays(2), notes);

            Result.Notes.AddRange(notes);
            if (files.Count == 0)
            {
                Result.Notes.Add("No candidate files found in: " + string.Join("; ", _folders.ToArray()));
                Result.Success = true; // "found nothing" is a result, not a failure
                Result.EntriesMatched = 0;
                return entries;
            }

            foreach (var file in files)
            {
                try
                {
                    var parser = new LogFileParser(_source, _serverName, _from, _to, _includeWarnings, _isSqlLog);
                    var found = parser.ParseFile(file);
                    entries.AddRange(found);
                    Result.FilesRead++;
                    Result.LinesScanned += parser.LinesScanned;
                }
                catch (IOException ex)
                {
                    Result.Notes.Add("Could not read " + file + ": " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Result.Notes.Add("Access denied " + file + ": " + ex.Message + " (try running as Administrator)");
                }
                catch (Exception ex)
                {
                    Result.Notes.Add("Error reading " + file + ": " + ex.Message);
                }
            }

            Result.Success = true;
            Result.EntriesMatched = entries.Count;
            return entries;
        }
    }

    public static class CollectorFactory
    {
        public static FileLogCollector AppLogs(List<string> folders, string serverName,
            DateTime from, DateTime to, bool includeWarnings)
        {
            return new FileLogCollector("Application logs (text files)", "AppLog", folders,
                new[] { "*.log", "*.txt" }, serverName, from, to, includeWarnings, false);
        }

        public static FileLogCollector SqlErrorLogs(List<string> folders, string serverName,
            DateTime from, DateTime to, bool includeWarnings)
        {
            // ERRORLOG, ERRORLOG.1 ... and SQLAGENT.OUT have no normal extension - match by name.
            return new FileLogCollector("SQL Server ERRORLOG", "SQL", folders,
                new[] { "ERRORLOG*", "SQLAGENT*" }, serverName, from, to, includeWarnings, true);
        }

        public static FileLogCollector KepwareLogs(List<string> folders, string serverName,
            DateTime from, DateTime to, bool includeWarnings)
        {
            return new FileLogCollector("Kepware logs", "Kepware", folders,
                new[] { "*.log", "*.txt" }, serverName, from, to, includeWarnings, false);
        }

        public static SqlQueryCollector SqlQuery(string connectionString, Config.SqlQueryDef query,
            string serverName, DateTime from, DateTime to)
        {
            return new SqlQueryCollector("SQL Query: " + query.Name, connectionString, query,
                serverName, from, to);
        }
    }
}

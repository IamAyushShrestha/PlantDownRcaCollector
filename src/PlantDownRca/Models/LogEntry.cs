using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace PlantDownRca.Models
{
    public enum EntryLevel
    {
        Unknown = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    /// <summary>One normalized log/event entry from any source. Xml-serializable so run
    /// packages from different servers can be merged later by PackageMerger.</summary>
    public class LogEntry
    {
        public string ServerName { get; set; }
        public string Source { get; set; }          // e.g. "AppLog", "SQL ERRORLOG", "EventLog:System", "Kepware"
        public bool HasTimestamp { get; set; }
        public DateTime Timestamp { get; set; }     // valid only when HasTimestamp = true; server local time
        public EntryLevel Level { get; set; }
        public string Code { get; set; }            // Event ID / error number when known, else ""
        public string Message { get; set; }
        public string RawText { get; set; }
        public string OriginFile { get; set; }

        [XmlIgnore]
        public string LevelText
        {
            get { return Level.ToString(); }
        }

        public LogEntry()
        {
            ServerName = "";
            Source = "";
            Code = "";
            Message = "";
            RawText = "";
            OriginFile = "";
        }
    }

    /// <summary>Same error repeated N times, collapsed into one row.</summary>
    public class GroupedError
    {
        public string Source { get; set; }
        public EntryLevel Level { get; set; }
        public string Fingerprint { get; set; }
        public int Count { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string SampleMessage { get; set; }
        public string SampleRaw { get; set; }
        public List<string> Tags { get; set; }

        public GroupedError()
        {
            Tags = new List<string>();
            SampleMessage = "";
            SampleRaw = "";
            Fingerprint = "";
            Source = "";
        }
    }

    public class CollectorResult
    {
        public string Name { get; set; }
        public bool Success { get; set; }
        public int FilesRead { get; set; }
        public long LinesScanned { get; set; }
        public int EntriesMatched { get; set; }
        public List<string> Notes { get; set; }

        public CollectorResult()
        {
            Name = "";
            Notes = new List<string>();
        }

        public CollectorResult(string name) : this()
        {
            Name = name;
        }
    }

    public class HealthSnapshot
    {
        public string ServerName { get; set; }
        public string OsVersion { get; set; }
        public DateTime CollectedAt { get; set; }
        public List<string> DriveLines { get; set; }
        public List<string> ServiceLines { get; set; }
        public List<string> Notes { get; set; }

        public HealthSnapshot()
        {
            ServerName = "";
            OsVersion = "";
            DriveLines = new List<string>();
            ServiceLines = new List<string>();
            Notes = new List<string>();
        }
    }

    /// <summary>Everything one run produced, before report writing.</summary>
    public class RunResult
    {
        public string PlantName { get; set; }
        public string ServerName { get; set; }
        public string ServerRole { get; set; }
        public DateTime WindowFrom { get; set; }
        public DateTime WindowTo { get; set; }
        public DateTime RanAt { get; set; }
        public List<LogEntry> Entries { get; set; }
        public List<CollectorResult> Collectors { get; set; }
        public HealthSnapshot Snapshot { get; set; }

        public RunResult()
        {
            PlantName = "";
            ServerName = "";
            ServerRole = "";
            Entries = new List<LogEntry>();
            Collectors = new List<CollectorResult>();
        }
    }

    /// <summary>Machine-readable package written into every run folder (entries.xml).
    /// Copy the run folders off each server, then use merge mode to build one unified timeline.</summary>
    public class RunPackage
    {
        public string PlantName { get; set; }
        public string ServerName { get; set; }
        public string ServerRole { get; set; }
        public DateTime WindowFrom { get; set; }
        public DateTime WindowTo { get; set; }
        public List<LogEntry> Entries { get; set; }
        public List<CollectorResult> Collectors { get; set; }
        public HealthSnapshot Snapshot { get; set; }

        public RunPackage()
        {
            PlantName = "";
            ServerName = "";
            ServerRole = "";
            Entries = new List<LogEntry>();
            Collectors = new List<CollectorResult>();
        }
    }
}

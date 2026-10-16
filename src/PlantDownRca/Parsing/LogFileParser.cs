using System;
using System.Collections.Generic;
using System.IO;
using PlantDownRca.Models;

namespace PlantDownRca.Parsing
{
    /// <summary>
    /// Parses one text log file into normalized entries for a time window.
    /// Rules that matter on prod:
    ///  - Files are opened with FileShare.ReadWrite: the writing service still has them open.
    ///  - Multi-line entries (stack traces) stay attached to the entry that started them.
    ///  - Lines before the first timestamp, or with an unparseable timestamp, are never dropped
    ///    silently losing evidence: they attach to the previous entry, or become an untimed entry.
    ///  - Only Error/Critical (+ Warning when configured) entries are kept. Info flood stays out.
    /// </summary>
    public class LogFileParser
    {
        private readonly string _source;
        private readonly string _serverName;
        private readonly DateTime _from;
        private readonly DateTime _to;
        private readonly bool _includeWarnings;
        private readonly bool _isSqlLog;

        private static readonly System.Text.RegularExpressions.Regex SqlInterestingRx =
            new System.Text.RegularExpressions.Regex(
                @"failover|availability (group|replica)|recovery|not available|quorum|brought online|went offline",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        public long LinesScanned { get; private set; }

        public LogFileParser(string source, string serverName, DateTime from, DateTime to,
            bool includeWarnings, bool isSqlLog)
        {
            _source = source;
            _serverName = serverName;
            _from = from;
            _to = to;
            _includeWarnings = includeWarnings;
            _isSqlLog = isSqlLog;
        }

        public List<LogEntry> ParseFile(string path)
        {
            var results = new List<LogEntry>();
            LogEntry current = null;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    LinesScanned++;

                    DateTime ts;
                    string remainder;
                    if (TimestampParser.TryParseLineStart(line, out ts, out remainder))
                    {
                        var newLevel = _isSqlLog ? LevelDetector.DetectSql(line) : LevelDetector.Detect(line);

                        // SQL Server writes an error and its explanation as two separate timestamped
                        // entries a fraction of a second apart (e.g. "Error: 9002" then "The transaction
                        // log ... is full"). Merge an unexplained follow-up line into the error it explains.
                        if (_isSqlLog && current != null && current.Level >= EntryLevel.Error
                            && newLevel == EntryLevel.Unknown
                            && ts >= current.Timestamp && (ts - current.Timestamp).TotalSeconds <= 2)
                        {
                            current.RawText += Environment.NewLine + line;
                            current.Message += " " + remainder.Trim();
                            continue;
                        }

                        FlushCurrent(current, results);
                        current = new LogEntry
                        {
                            ServerName = _serverName,
                            Source = _source,
                            HasTimestamp = true,
                            Timestamp = ts,
                            Message = remainder.Trim(),
                            RawText = line,
                            OriginFile = path,
                            Level = newLevel
                        };

                        // Failover / availability-group state lines carry no error keyword but are
                        // first-class RCA evidence in a cluster - keep them at Warning level.
                        if (_isSqlLog && current.Level < EntryLevel.Warning && SqlInterestingRx.IsMatch(line))
                            current.Level = EntryLevel.Warning;
                    }
                    else if (current != null)
                    {
                        // Continuation line (stack trace, wrapped message): keep with its entry.
                        current.RawText += Environment.NewLine + line;
                        if (current.Level == EntryLevel.Unknown || current.Level == EntryLevel.Info)
                        {
                            var lvl = _isSqlLog ? LevelDetector.DetectSql(line) : LevelDetector.Detect(line);
                            if (lvl > current.Level)
                                current.Level = lvl;
                        }
                    }
                    else
                    {
                        // No timestamp seen yet (file header etc.). Keep as untimed if it looks like an error.
                        var lvl = LevelDetector.Detect(line);
                        if (lvl >= EntryLevel.Error)
                        {
                            current = new LogEntry
                            {
                                ServerName = _serverName,
                                Source = _source,
                                HasTimestamp = false,
                                Timestamp = DateTime.MinValue,
                                Message = line.Trim(),
                                RawText = line,
                                OriginFile = path,
                                Level = lvl
                            };
                        }
                    }
                }
            }

            FlushCurrent(current, results);
            return results;
        }

        private void FlushCurrent(LogEntry entry, List<LogEntry> results)
        {
            if (entry == null)
                return;

            // Keep errors and criticals; warnings only when configured.
            bool keepLevel = entry.Level >= EntryLevel.Error
                             || (_includeWarnings && entry.Level == EntryLevel.Warning);
            if (!keepLevel)
                return;

            if (entry.HasTimestamp)
            {
                if (entry.Timestamp < _from || entry.Timestamp > _to)
                    return;
            }

            if (string.IsNullOrEmpty(entry.Message))
                entry.Message = entry.RawText;

            results.Add(entry);
        }
    }
}

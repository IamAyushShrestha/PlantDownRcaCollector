using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using PlantDownRca.Models;

namespace PlantDownRca.Collectors
{
    /// <summary>
    /// Windows Event Logs (Application, System, FailoverClustering, ...) read with an
    /// XPath time filter, so a huge System log is never loaded whole.
    /// Needs the log's read permission - on prod, run the exe as Administrator.
    /// </summary>
    public class EventLogCollector : ICollector
    {
        private readonly List<string> _channels;
        private readonly string _serverName;
        private readonly DateTime _from;
        private readonly DateTime _to;
        private readonly bool _includeWarnings;

        public CollectorResult Result { get; private set; }

        public EventLogCollector(List<string> channels, string serverName,
            DateTime from, DateTime to, bool includeWarnings)
        {
            _channels = channels ?? new List<string>();
            _serverName = serverName;
            _from = from;
            _to = to;
            _includeWarnings = includeWarnings;
            Result = new CollectorResult("Windows Event Logs");
        }

        public List<LogEntry> Collect()
        {
            var entries = new List<LogEntry>();
            string fromUtc = _from.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            string toUtc = _to.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

            // Levels: 1 Critical, 2 Error, 3 Warning
            string levelFilter = _includeWarnings ? "(Level=1 or Level=2 or Level=3)" : "(Level=1 or Level=2)";
            string xpath = string.Format(
                "*[System[TimeCreated[@SystemTime>='{0}' and @SystemTime<='{1}'] and {2}]]",
                fromUtc, toUtc, levelFilter);

            foreach (var channel in _channels)
            {
                try
                {
                    var query = new EventLogQuery(channel, PathType.LogName, xpath) { ReverseDirection = false };
                    using (var reader = new EventLogReader(query))
                    {
                        EventRecord record;
                        while ((record = reader.ReadEvent()) != null)
                        {
                            using (record)
                            {
                                string message = SafeFormat(record);
                                entries.Add(new LogEntry
                                {
                                    ServerName = _serverName,
                                    Source = "EventLog:" + channel,
                                    HasTimestamp = record.TimeCreated.HasValue,
                                    Timestamp = record.TimeCreated ?? DateTime.MinValue,
                                    Level = MapLevel(record.Level),
                                    Code = record.Id.ToString(CultureInfo.InvariantCulture),
                                    Message = FirstLine(message),
                                    RawText = message,
                                    OriginFile = channel + " (Provider: " + (record.ProviderName ?? "") + ")"
                                });
                            }
                            Result.LinesScanned++;
                        }
                    }
                }
                catch (EventLogNotFoundException)
                {
                    Result.Notes.Add("Channel not present on this server (skipped): " + channel);
                }
                catch (UnauthorizedAccessException)
                {
                    Result.Notes.Add("Access denied reading channel " + channel + " - run as Administrator.");
                }
                catch (Exception ex)
                {
                    Result.Notes.Add("Channel " + channel + " failed: " + ex.Message);
                }
            }

            Result.Success = true;
            Result.EntriesMatched = entries.Count;
            return entries;
        }

        private static string SafeFormat(EventRecord record)
        {
            try
            {
                string text = record.FormatDescription();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
            catch { /* some records have no localizable description - fall through */ }

            try
            {
                var parts = new List<string>();
                if (record.Properties != null)
                {
                    foreach (var p in record.Properties)
                    {
                        if (p.Value != null)
                            parts.Add(p.Value.ToString());
                    }
                }
                if (parts.Count > 0)
                    return string.Join(" | ", parts.ToArray());
            }
            catch { }

            return "(no description available) Provider=" + (record.ProviderName ?? "")
                   + " EventId=" + record.Id;
        }

        private static EntryLevel MapLevel(byte? level)
        {
            if (!level.HasValue) return EntryLevel.Unknown;
            switch (level.Value)
            {
                case 1: return EntryLevel.Critical;
                case 2: return EntryLevel.Error;
                case 3: return EntryLevel.Warning;
                case 4: return EntryLevel.Info;
                default: return EntryLevel.Unknown;
            }
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int idx = text.IndexOf('\n');
            string first = idx >= 0 ? text.Substring(0, idx) : text;
            return first.Trim();
        }
    }
}

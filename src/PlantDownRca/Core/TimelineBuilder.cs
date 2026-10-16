using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PlantDownRca.Models;

namespace PlantDownRca.Core
{
    public static class TimelineBuilder
    {
        /// <summary>Merges entries from every collector/server into one chronological timeline.
        /// WARNING stamped in the report when entries come from more than one server:
        /// ordering across servers is only as good as their clock sync (NTP).</summary>
        public static List<LogEntry> BuildMergedTimeline(IEnumerable<LogEntry> entries)
        {
            var timed = new List<LogEntry>();
            foreach (var e in entries)
            {
                if (e.HasTimestamp)
                    timed.Add(e);
            }
            timed.Sort(delegate(LogEntry a, LogEntry b)
            {
                int c = a.Timestamp.CompareTo(b.Timestamp);
                if (c != 0) return c;
                c = b.Level.CompareTo(a.Level); // same second: worse level first
                if (c != 0) return c;
                return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
            });
            return timed;
        }

        public static List<LogEntry> Untimed(IEnumerable<LogEntry> entries)
        {
            var list = new List<LogEntry>();
            foreach (var e in entries)
            {
                if (!e.HasTimestamp)
                    list.Add(e);
            }
            return list;
        }

        public static List<GroupedError> Group(IEnumerable<LogEntry> entries)
        {
            var map = new Dictionary<string, GroupedError>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                if (e.Level < EntryLevel.Warning && e.Level != EntryLevel.Critical)
                    continue;

                string fingerprint = Fingerprint(e.Message);
                string key = e.Source + "|" + fingerprint;

                GroupedError group;
                if (!map.TryGetValue(key, out group))
                {
                    group = new GroupedError
                    {
                        Source = e.Source,
                        Level = e.Level,
                        Fingerprint = fingerprint,
                        SampleMessage = e.Message,
                        SampleRaw = e.RawText,
                        FirstSeen = e.HasTimestamp ? e.Timestamp : DateTime.MaxValue,
                        LastSeen = e.HasTimestamp ? e.Timestamp : DateTime.MinValue
                    };
                    map[key] = group;
                }

                group.Count++;
                if (e.Level > group.Level)
                    group.Level = e.Level;
                if (e.HasTimestamp)
                {
                    if (e.Timestamp < group.FirstSeen) group.FirstSeen = e.Timestamp;
                    if (e.Timestamp > group.LastSeen) group.LastSeen = e.Timestamp;
                }
            }

            var groups = new List<GroupedError>(map.Values);
            foreach (var g in groups)
            {
                if (g.FirstSeen == DateTime.MaxValue) g.FirstSeen = DateTime.MinValue;
                PatternTagger.Apply(g);
            }

            groups.Sort(delegate(GroupedError a, GroupedError b)
            {
                int c = b.Count.CompareTo(a.Count);
                if (c != 0) return c;
                return a.FirstSeen.CompareTo(b.FirstSeen);
            });
            return groups;
        }

        private static readonly Regex DigitsRx = new Regex(@"\d+", RegexOptions.Compiled);
        private static readonly Regex GuidRx = new Regex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
        private static readonly Regex HexRx = new Regex(@"\b0x[0-9a-fA-F]+\b", RegexOptions.Compiled);
        private static readonly Regex SpaceRx = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>Numbers, GUIDs and hex become placeholders so "Timeout after 30s for device 12"
        /// and "Timeout after 45s for device 19" count as the same repeated error.</summary>
        public static string Fingerprint(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "";
            string s = GuidRx.Replace(message, "{guid}");
            s = HexRx.Replace(s, "{hex}");
            s = DigitsRx.Replace(s, "#");
            s = SpaceRx.Replace(s, " ").Trim();
            if (s.Length > 220)
                s = s.Substring(0, 220);
            return s;
        }
    }

    /// <summary>Known shapes from real plant incidents. Rules are simple on purpose: they tag,
    /// they never declare a root cause. Add a rule after every RCA - that list IS your experience, codified.</summary>
    public static class PatternTagger
    {
        private class Rule
        {
            public string Tag;
            public Regex Rx;
            public Rule(string tag, string pattern)
            {
                Tag = tag;
                Rx = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }

        private static readonly Rule[] Rules = new[]
        {
            new Rule("DISK_FULL", @"disk.*full|not enough space|operating system error 112|no space left"),
            new Rule("SQL_FAILOVER_OR_AG_MOVE", @"failover|availability group.*(role|state)|is now the primary|clustered instance.*(online|offline)"),
            new Rule("SQL_DEADLOCK", @"deadlock|chosen as the deadlock victim|error:\s*1205"),
            new Rule("SQL_IO_PROBLEM", @"i/o.*(taking longer|error)|operating system error (995|1117)|flush.*failed"),
            new Rule("KEPWARE_PLC_COMMS_LOSS", @"device.*not responding|unable to connect|read.*failed|tag.*quality.*bad|channel.*failed"),
            new Rule("SERVICE_CRASHED", @"terminated unexpectedly|service.*has stopped|7031|7034|7036"),
            new Rule("CLUSTER_RESOURCE_FAILED", @"1069|cluster resource.*failed|resource.*offline"),
            new Rule("MSMQ_PROBLEM", @"message queuing|msmq|negative acknowledgment"),
            new Rule("DB_CONNECTION_LOST", @"connection.*(lost|broken|closed)|transport-level error|named pipes provider.*error: 40|login timeout"),
            new Rule("OUT_OF_MEMORY", @"out of memory|outofmemoryexception|insufficient.*memory|1455")
        };

        public static void Apply(GroupedError group)
        {
            string text = (group.SampleMessage ?? "") + " " + (group.Fingerprint ?? "");
            foreach (var rule in Rules)
            {
                if (rule.Rx.IsMatch(text) && !group.Tags.Contains(rule.Tag))
                    group.Tags.Add(rule.Tag);
            }
        }
    }
}

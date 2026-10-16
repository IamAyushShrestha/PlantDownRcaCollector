using System;
using System.Globalization;
using System.Text.RegularExpressions;
using PlantDownRca.Models;

namespace PlantDownRca.Parsing
{
    /// <summary>
    /// Finds a timestamp at (or near) the start of a log line, across the formats your stack produces.
    /// Adaptive by design: plants/apps differ, so this tries a list of patterns instead of assuming one.
    ///   SQL ERRORLOG : 2026-10-06 14:31:07.45 spid52 ...
    ///   log4net/NLog : 2026-10-06 14:31:07,451 [12] ERROR ...
    ///   US style     : 10/6/2026 2:31:07 PM ...
    ///   ISO with T   : 2026-10-06T14:31:07.451 ...
    ///   Event files  : 06-Oct-2026 14:31:07 ...
    /// </summary>
    public static class TimestampParser
    {
        private static readonly Regex[] Patterns = new[]
        {
            // 2026-10-06 14:31:07.451  /  2026-10-06T14:31:07,451  /  seconds only
            new Regex(@"^(?<ts>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}([.,]\d{1,7})?)", RegexOptions.Compiled),
            // 10/6/2026 2:31:07 PM  /  10/06/2026 14:31:07
            new Regex(@"^(?<ts>\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}:\d{2}(\s*[AP]M)?([.,]\d{1,7})?)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // 06-Oct-2026 14:31:07
            new Regex(@"^(?<ts>\d{1,2}-[A-Za-z]{3}-\d{4}\s+\d{2}:\d{2}:\d{2}([.,]\d{1,7})?)", RegexOptions.Compiled),
            // [2026-10-06 14:31:07] wrapped in brackets
            new Regex(@"^\[(?<ts>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}([.,]\d{1,7})?)\]", RegexOptions.Compiled)
        };

        private static readonly string[] ExactFormats = new[]
        {
            "yyyy-MM-dd HH:mm:ss.fffffff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss.ff",
            "yyyy-MM-dd HH:mm:ss.f",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fffffff",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "M/d/yyyy h:mm:ss tt",
            "M/d/yyyy hh:mm:ss tt",
            "M/d/yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "d-MMM-yyyy HH:mm:ss",
            "dd-MMM-yyyy HH:mm:ss"
        };

        /// <summary>Returns true and splits the line into timestamp + remainder.</summary>
        public static bool TryParseLineStart(string line, out DateTime timestamp, out string remainder)
        {
            timestamp = DateTime.MinValue;
            remainder = line ?? "";

            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.TrimStart();

            foreach (var pattern in Patterns)
            {
                var match = pattern.Match(trimmed);
                if (!match.Success)
                    continue;

                string raw = match.Groups["ts"].Value.Replace(',', '.');
                DateTime parsed;
                if (DateTime.TryParseExact(raw, ExactFormats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out parsed)
                    || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                    || DateTime.TryParse(raw, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out parsed))
                {
                    timestamp = parsed;
                    remainder = trimmed.Substring(match.Length).TrimStart(' ', '\t', '-', ':', '|', ']');
                    return true;
                }
            }

            return false;
        }
    }

    public static class LevelDetector
    {
        private static readonly Regex ErrorRx =
            new Regex(@"\b(FATAL|CRITICAL|ERROR|EXCEPTION|SEV(ERITY)?\s*[:=]?\s*(1[6-9]|2[0-5])\b|FAILED|FAILURE)\b|NOT RESPONDING|UNABLE TO CONNECT|TIMED OUT|TIMEOUT",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex WarnRx =
            new Regex(@"\b(WARN|WARNING)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex InfoRx =
            new Regex(@"\b(INFO|INFORMATION|DEBUG|TRACE|VERBOSE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static EntryLevel Detect(string text)
        {
            if (string.IsNullOrEmpty(text))
                return EntryLevel.Unknown;

            if (text.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0)
                return EntryLevel.Error;

            if (ErrorRx.IsMatch(text))
                return EntryLevel.Error;

            if (WarnRx.IsMatch(text))
                return EntryLevel.Warning;

            if (InfoRx.IsMatch(text))
                return EntryLevel.Info;

            return EntryLevel.Unknown;
        }

        /// <summary>SQL ERRORLOG style: lines with "Error:" or a severity are errors even without keywords above.</summary>
        public static EntryLevel DetectSql(string text)
        {
            if (string.IsNullOrEmpty(text))
                return EntryLevel.Unknown;

            if (Regex.IsMatch(text, @"\bError:\s*\d+", RegexOptions.IgnoreCase))
                return EntryLevel.Error;

            return Detect(text);
        }
    }
}

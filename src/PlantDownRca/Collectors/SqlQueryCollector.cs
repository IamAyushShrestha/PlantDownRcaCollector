using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;
using PlantDownRca.Config;
using PlantDownRca.Models;

namespace PlantDownRca.Collectors
{
    /// <summary>
    /// Runs parameterized SQL queries against a SQL Server database to collect
    /// timestamped error/processing rows for the incident window.
    /// Each query must accept @From and @To DateTime parameters.
    /// </summary>
    public class SqlQueryCollector : ICollector
    {
        private readonly string _name;
        private readonly string _connectionString;
        private readonly SqlQueryDef _query;
        private readonly string _serverName;
        private readonly DateTime _from;
        private readonly DateTime _to;

        public CollectorResult Result { get; private set; }

        public SqlQueryCollector(string name, string connectionString, SqlQueryDef query,
            string serverName, DateTime from, DateTime to)
        {
            _name = name;
            _connectionString = connectionString;
            _query = query;
            _serverName = serverName;
            _from = from;
            _to = to;
            Result = new CollectorResult(name);
        }

        public List<LogEntry> Collect()
        {
            var entries = new List<LogEntry>();

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                Result.Notes.Add("Connection string is empty");
                Result.Success = false;
                return entries;
            }

            if (string.IsNullOrWhiteSpace(_query.Sql))
            {
                Result.Notes.Add("Query SQL is empty");
                Result.Success = false;
                return entries;
            }

            if (string.IsNullOrWhiteSpace(_query.TimestampColumn))
            {
                Result.Notes.Add("TimestampColumn not specified");
                Result.Success = false;
                return entries;
            }

            if (string.IsNullOrWhiteSpace(_query.MessageColumn))
            {
                Result.Notes.Add("MessageColumn not specified");
                Result.Success = false;
                return entries;
            }

            // Safety net: refuse anything that is not a single read-only SELECT, so a
            // mis-typed or pasted query can never write to the database.
            string reason;
            if (!IsReadOnlySelect(_query.Sql, out reason))
            {
                Result.Notes.Add("Query rejected (read-only guard): " + reason);
                Result.Success = false;
                return entries;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(_query.Sql, conn))
                    {
                        cmd.CommandTimeout = 30;
                        cmd.Parameters.Add("@From", SqlDbType.DateTime).Value = _from;
                        cmd.Parameters.Add("@To", SqlDbType.DateTime).Value = _to;

                        using (var reader = cmd.ExecuteReader())
                        {
                            int rowCount = 0;
                            while (reader.Read())
                            {
                                rowCount++;
                                var entry = MapRow(reader);
                                if (entry != null)
                                    entries.Add(entry);
                            }
                            Result.LinesScanned = rowCount;
                        }
                    }
                }

                Result.Success = true;
                Result.EntriesMatched = entries.Count;
            }
            catch (SqlException ex)
            {
                Result.Success = false;
                Result.Notes.Add("SQL error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Result.Success = false;
                Result.Notes.Add("Query failed: " + ex.Message);
            }

            return entries;
        }

        /// <summary>True only for a single read-only SELECT (or a CTE that starts with WITH).
        /// Comments and string literals are removed first, so a word like "DELETE" inside a
        /// literal or a comment does not trip the check. This is a safety net so a mis-typed
        /// or pasted query can never write to the database.</summary>
        internal static bool IsReadOnlySelect(string sql, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(sql))
            {
                reason = "the query is empty";
                return false;
            }

            string s = StripCommentsAndLiterals(sql);
            string trimmed = s.TrimStart();

            if (!StartsWithWord(trimmed, "SELECT") && !StartsWithWord(trimmed, "WITH"))
            {
                reason = "only a single read-only SELECT is allowed (the query must start with SELECT or WITH)";
                return false;
            }

            // No statement chaining: a ';' may only be the last non-space character.
            int semi = s.IndexOf(';');
            if (semi >= 0 && s.Substring(semi + 1).Trim().Length > 0)
            {
                reason = "only one statement is allowed (remove the text after ';')";
                return false;
            }

            var verb = BannedVerb.Match(s);
            if (verb.Success)
            {
                reason = "only read-only SELECT is allowed; '" + verb.Value.ToUpperInvariant() + "' is not permitted";
                return false;
            }

            if (BannedProc.IsMatch(s))
            {
                reason = "only read-only SELECT is allowed; stored procedures (sp_/xp_) are not permitted";
                return false;
            }

            return true;
        }

        private static readonly Regex BannedVerb = new Regex(
            @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|MERGE|GRANT|REVOKE|BACKUP|RESTORE|DBCC|INTO)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BannedProc = new Regex(
            @"\b(sp_|xp_)\w+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool StartsWithWord(string text, string word)
        {
            if (text.Length < word.Length)
                return false;
            if (!text.Substring(0, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase))
                return false;
            return text.Length == word.Length || !char.IsLetterOrDigit(text[word.Length]) && text[word.Length] != '_';
        }

        /// <summary>Removes -- line comments, /* */ block comments and '...' string literals
        /// (with '' escapes), so keyword scanning only sees real SQL tokens.</summary>
        private static string StripCommentsAndLiterals(string sql)
        {
            var sb = new StringBuilder(sql.Length);
            int i = 0, n = sql.Length;
            while (i < n)
            {
                char c = sql[i];
                if (c == '-' && i + 1 < n && sql[i + 1] == '-')
                {
                    while (i < n && sql[i] != '\n') i++;
                }
                else if (c == '/' && i + 1 < n && sql[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                    i = Math.Min(i + 2, n);
                }
                else if (c == '\'')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n)
                    {
                        if (sql[i] == '\'' && i + 1 < n && sql[i + 1] == '\'') { i += 2; continue; }
                        if (sql[i] == '\'') { i++; break; }
                        i++;
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        private LogEntry MapRow(IDataReader reader)
        {
            try
            {
                // Timestamp (required)
                DateTime ts;
                var tsVal = reader[_query.TimestampColumn];
                if (tsVal == null || tsVal == DBNull.Value)
                    return null;

                if (!DateTime.TryParse(tsVal.ToString(), out ts))
                    return null;

                // Message (required)
                string message = "";
                var msgVal = reader[_query.MessageColumn];
                if (msgVal != null && msgVal != DBNull.Value)
                    message = msgVal.ToString();

                // Level (optional)
                EntryLevel level = EntryLevel.Info;
                if (!string.IsNullOrWhiteSpace(_query.LevelColumn))
                {
                    var lvlVal = reader[_query.LevelColumn];
                    if (lvlVal != null && lvlVal != DBNull.Value)
                        level = MapLevel(lvlVal.ToString());
                }

                // Code (optional)
                string code = "";
                if (!string.IsNullOrWhiteSpace(_query.CodeColumn))
                {
                    var codeVal = reader[_query.CodeColumn];
                    if (codeVal != null && codeVal != DBNull.Value)
                        code = codeVal.ToString();
                }

                // Build raw text with all columns for context
                var rawParts = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = reader[i];
                    if (val != null && val != DBNull.Value)
                        rawParts.Add(reader.GetName(i) + "=" + val);
                }
                string rawText = string.Join(" | ", rawParts);

                return new LogEntry
                {
                    ServerName = _serverName,
                    Source = _name,
                    HasTimestamp = true,
                    Timestamp = ts,
                    Level = level,
                    Code = code,
                    Message = message,
                    RawText = rawText,
                    OriginFile = "SQL Query: " + _query.Name
                };
            }
            catch
            {
                return null;
            }
        }

        private static EntryLevel MapLevel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return EntryLevel.Info;

            string t = text.Trim().ToLowerInvariant();
            if (t == "critical" || t == "fatal" || t == "1" || t == "0")
                return EntryLevel.Critical;
            if (t == "error" || t == "err" || t == "2")
                return EntryLevel.Error;
            if (t == "warning" || t == "warn" || t == "3")
                return EntryLevel.Warning;
            if (t == "info" || t == "information" || t == "4")
                return EntryLevel.Info;
            return EntryLevel.Unknown;
        }
    }
}
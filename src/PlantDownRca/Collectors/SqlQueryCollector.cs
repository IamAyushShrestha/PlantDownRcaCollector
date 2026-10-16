using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
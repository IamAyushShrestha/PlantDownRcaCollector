using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace PlantDownRca.Config
{
    /// <summary>
    /// Per-server settings, in plant.config.xml next to the exe (XML on purpose: no extra
    /// libraries needed on .NET Framework, and any dev can edit it in Notepad).
    /// Keep values empty where auto-discovery should figure it out.
    /// </summary>
    public class AppConfig
    {
        public string PlantName { get; set; }

        /// <summary>App, Sql, Kepware, or Other - shown on the report so merged packages stay readable.</summary>
        public string ServerRole { get; set; }

        /// <summary>When true, empty folder settings are auto-discovered (a "Logs" folder holding log
        /// files, the SQL Server Log folder, and Kepware's default install log folders). Product-agnostic:
        /// no product or category names are hardcoded. Set AppLogFolders explicitly for tight control.</summary>
        public bool DiscoveryEnabled { get; set; }

        public bool IncludeWarnings { get; set; }

        /// <summary>When true, runs parameterized SQL queries defined in SqlQueries (procedure errors, agent errors, message processing).
        /// Default FALSE — portable across plants without SQL config. Enable only when SqlQueryConnections + SqlQueries are configured.</summary>
        public bool EnableSqlQueries { get; set; }

        /// <summary>Folders with your application text logs, searched recursively for *.log/*.txt.
        /// Point this at a logs root (e.g. C:\PlantLogs) and every subfolder is picked up automatically.
        /// Kept generic on purpose - add exactly the folders you need from the Configurator tab.</summary>
        [XmlArray("AppLogFolders")]
        [XmlArrayItem("Folder")]
        public List<string> AppLogFolders { get; set; }

        /// <summary>Folders containing SQL Server ERRORLOG / SQLAGENT.OUT files.
        /// Empty = discovery looks under Program Files\Microsoft SQL Server\MSSQL*\...\MSSQL\Log on every drive.</summary>
        [XmlArray("SqlLogFolders")]
        [XmlArrayItem("Folder")]
        public List<string> SqlLogFolders { get; set; }

        /// <summary>Folders containing Kepware (KEPServerEX) log files from a standard install.
        /// Empty = discovery looks under Program Files (x86)\Kepware, Program Files\Kepware, ProgramData\Kepware.</summary>
        [XmlArray("KepwareLogFolders")]
        [XmlArrayItem("Folder")]
        public List<string> KepwareLogFolders { get; set; }

        [XmlArray("EventLogChannels")]
        [XmlArrayItem("Channel")]
        public List<string> EventLogChannels { get; set; }

        [XmlArray("ServicesToCheck")]
        [XmlArrayItem("Service")]
        public List<string> ServicesToCheck { get; set; }

        /// <summary>Named SQL connection strings referenced by queries.</summary>
        [XmlArray("SqlQueryConnections")]
        [XmlArrayItem("Connection")]
        public List<SqlQueryConnection> SqlQueryConnections { get; set; }

        /// <summary>Parameterized SQL queries that return timestamped rows for the incident window.
        /// Each query MUST use @From and @To parameters for the time window.</summary>
        [XmlArray("SqlQueries")]
        [XmlArrayItem("Query")]
        public List<SqlQueryDef> SqlQueries { get; set; }

        public AppConfig()
        {
            PlantName = "";
            ServerRole = "App";
            DiscoveryEnabled = true;
            IncludeWarnings = true;
            AppLogFolders = new List<string>();
            SqlLogFolders = new List<string>();
            KepwareLogFolders = new List<string>();
            // Left EMPTY on purpose: XmlSerializer fills collection properties by calling Add() on the
            // existing list, so seeding defaults here would duplicate them on every load. Defaults are
            // applied once, only when there is no config file yet - see SeedDefaults().
            EventLogChannels = new List<string>();
            ServicesToCheck = new List<string>();
            SqlQueryConnections = new List<SqlQueryConnection>();
            SqlQueries = new List<SqlQueryDef>();
            EnableSqlQueries = false;
        }

        /// <summary>Standard channels/services used when no config file exists yet.</summary>
        private void SeedDefaults()
        {
            if (EventLogChannels == null || EventLogChannels.Count == 0)
                EventLogChannels = new List<string>
                {
                    "Application",
                    "System",
                    "Microsoft-Windows-FailoverClustering/Operational"
                };
            if (ServicesToCheck == null || ServicesToCheck.Count == 0)
                ServicesToCheck = new List<string>
                {
                    "MSSQLSERVER",
                    "SQLSERVERAGENT",
                    "MSMQ"
                };
        }

        private static AppConfig Defaults()
        {
            var cfg = new AppConfig();
            cfg.SeedDefaults();
            return cfg;
        }

        public static AppConfig Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Defaults();

            var serializer = new XmlSerializer(typeof(AppConfig));

            // Harden the XML read: no DTDs and no external resolver, so a tampered config
            // file cannot pull in external entities (XXE) or blow up memory (billion laughs).
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = XmlReader.Create(stream, settings))
            {
                var cfg = (AppConfig)serializer.Deserialize(reader);
                return cfg ?? Defaults();
            }
        }

        public void Save(string path)
        {
            var serializer = new XmlSerializer(typeof(AppConfig));
            using (var writer = new StreamWriter(path))
            {
                serializer.Serialize(writer, this);
            }
        }

        /// <summary>True when the same example/default file can be used: discovery fills the gaps.</summary>
        public static string DefaultConfigPathNextToExe()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plant.config.xml");
        }

        /// <summary>
        /// First run: if plant.config.xml does not exist yet but the shipped example does,
        /// copy the example to plant.config.xml so there is always a real config file to look
        /// at and edit. Returns a short status message for the UI, or null when nothing happened.
        /// </summary>
        public static string CreateFromExampleIfMissing()
        {
            string path = DefaultConfigPathNextToExe();
            if (File.Exists(path))
                return null;

            string example = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plant.config.example.xml");
            if (!File.Exists(example))
                return null;

            File.Copy(example, path);
            return "Created plant.config.xml from the example - edit below, then Save configuration.";
        }
    }

    /// <summary>Named connection string for SQL queries.</summary>
    public class SqlQueryConnection
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string ConnectionString { get; set; }

        public SqlQueryConnection()
        {
            Name = "";
            ConnectionString = "";
        }
    }

    /// <summary>One parameterized query that returns rows within a time window.
    /// The query MUST accept @From and @To parameters (DateTime).</summary>
    public class SqlQueryDef
    {
        [XmlAttribute]
        public string Connection { get; set; }          // References SqlQueryConnection.Name
        [XmlAttribute]
        public string Name { get; set; }                // Display name in report
        [XmlElement]
        public string Sql { get; set; }                 // Query with @From/@To parameters
        [XmlAttribute]
        public string TimestampColumn { get; set; }     // Column holding the event datetime
        [XmlAttribute]
        public string MessageColumn { get; set; }       // Column for message text
        [XmlAttribute]
        public string LevelColumn { get; set; }         // Optional: severity column (maps to EntryLevel)
        [XmlAttribute]
        public string CodeColumn { get; set; }          // Optional: error code / event ID column

        public SqlQueryDef()
        {
            Connection = "";
            Name = "";
            Sql = "";
            TimestampColumn = "";
            MessageColumn = "";
            LevelColumn = "";
            CodeColumn = "";
        }
    }
}

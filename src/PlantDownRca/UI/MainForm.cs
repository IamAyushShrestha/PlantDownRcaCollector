using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PlantDownRca.Config;
using PlantDownRca.Core;
using PlantDownRca.Report;

namespace PlantDownRca.UI
{
    /// <summary>Modern, professional WinForms UI with improved visual design, typography, and layout.
    /// Built in code (no designer file) for portability. Design system: Navy/Slate/Sky Blue corporate colors.</summary>
    public class MainForm : Form
    {
        // Design System Colors
        private static readonly Color ColorPrimary = Color.FromArgb(15, 23, 42);      // #0F172A
        private static readonly Color ColorAccent = Color.FromArgb(3, 105, 161);      // #0369A1
        private static readonly Color ColorBackground = Color.FromArgb(248, 250, 252); // #F8FAFC
        private static readonly Color ColorCard = Color.White;
        private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240);    // #E2E8F0
        private static readonly Color ColorText = Color.FromArgb(2, 6, 23);           // #020617
        private static readonly Color ColorMuted = Color.FromArgb(226, 232, 240);     // #E8ECF1
        private static readonly Color ColorDestructive = Color.FromArgb(220, 38, 38);  // #DC2626
        private static readonly Color ColorSuccess = Color.FromArgb(22, 163, 74);     // #16A34A

        private readonly DateTimePicker _fromPicker;
        private readonly DateTimePicker _toPicker;
        private readonly TextBox _configBox;
        private readonly TextBox _outBox;
        private readonly ListBox _progress;
        private readonly Button _runButton;
        private readonly Button _openReportButton;
        private string _lastReportPath;

        // Configurator tab controls
        private TextBox _plantBox;
        private TextBox _roleBox;
        private CheckBox _discoveryCheck;
        private CheckBox _warningsCheck;
        private CheckBox _cfgEnableSql;
        private ListBox _appFoldersList;
        private TextBox _channelsBox;
        private TextBox _servicesBox;
        // Database connection: separate simple fields; the connection string is built for the user.
        // There is NOTHING connection-string related to type: IP + credentials + pick the database
        // from a list that fills itself ("Load databases…").
        private TextBox _sqlServerBox;
        private ComboBox _sqlDbBox;          // database names; filled by "Load databases…", or type one
        private Button _sqlDbsButton;
        private TextBox _sqlUserBox;
        private TextBox _sqlPwdBox;
        private CheckBox _sqlShowPwd;
        private CheckBox _sqlWinAuth;
        private Label _sqlConnPreview;       // read-only info line — the string builds itself
        private Label _sqlTestStatus;
        private Button _sqlTestButton;
        private ToolTip _tips;
        private FlowLayoutPanel _sqlQueriesPanel;
        private readonly List<SqlQueryRow> _sqlQueryRows = new List<SqlQueryRow>();
        private string _sqlConnName = "Default";
        private int _sqlRowCounter;
        private Label _configStatus;

        /// <summary>One stacked SQL query editor (its own controls) inside the Configurator.</summary>
        private sealed class SqlQueryRow
        {
            public Panel Panel;
            public TextBox Name;
            public TextBox Ts;
            public TextBox Msg;
            public TextBox Lvl;
            public TextBox Code;
            public TextBox Sql;
        }

        public MainForm()
        {
            Text = "Plant Down RCA Collector";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ColorBackground;
            ForeColor = ColorText;
            MinimumSize = new Size(760, 560);
            Icon = LoadAppIcon();

            // One shared tooltip component for every hint in the app (hover any field for guidance).
            _tips = new ToolTip
            {
                AutoPopDelay = 30000,
                InitialDelay = 350,
                ReshowDelay = 150,
                ShowAlways = true
            };

            // Main container panel
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBackground,
                Padding = new Padding(20)
            };

            // ============ HEADER SECTION ============
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = ColorPrimary,
                Padding = new Padding(20),
                Margin = new Padding(0, 0, 0, 20)
            };

            var logoBox = new PictureBox
            {
                Location = new Point(0, 8),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = LoadLogo()
            };

            var titleLabel = new Label
            {
                Text = "Plant Down RCA Collector",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(78, 12),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Collect logs and build incident reports. Read-only, changes nothing on the server.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(80, 46),
                MaximumSize = new Size(760, 40),
                AutoSize = true
            };

            // Owner credit — visible in the app itself.
            var ownerLabel = new Label
            {
                Text = "PlantDownRca v0.2  -  owned by Ayush Shrestha",
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(170, 170, 170),
                Location = new Point(80, 74),
                AutoSize = true
            };

            headerPanel.Controls.Add(logoBox);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subtitleLabel);
            headerPanel.Controls.Add(ownerLabel);

            // ============ INPUT SECTION ============
            var inputPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 240,
                BackColor = ColorCard,
                Padding = new Padding(20),
                Margin = new Padding(0, 0, 0, 15),
                BorderStyle = BorderStyle.FixedSingle
            };
            inputPanel.BackColor = ColorCard;

            // Section title
            var inputTitleLabel = new Label
            {
                Text = "Incident Window & Configuration",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(0, 0),
                AutoSize = true
            };

            // Time range group
            var timeGroupLabel = new Label
            {
                Text = "Time Window (server local time)",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(0, 32),
                AutoSize = true
            };

            var fromLabel = new Label { Text = "From:", Location = new Point(0, 60), Width = 50, AutoSize = true };
            _fromPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",
                Value = DateTime.Now.AddHours(-2),
                Location = new Point(60, 56),
                Width = 200
            };

            var toLabel = new Label { Text = "To:", Location = new Point(280, 60), Width = 35, AutoSize = true };
            _toPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm:ss",
                Value = DateTime.Now,
                Location = new Point(330, 56),
                Width = 200
            };

            var quick1h = new Button { Text = "Last 1h", Location = new Point(550, 54), Width = 75, Height = 26, BackColor = ColorAccent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            quick1h.Click += delegate { _toPicker.Value = DateTime.Now; _fromPicker.Value = DateTime.Now.AddHours(-1); };

            var quick4h = new Button { Text = "Last 4h", Location = new Point(635, 54), Width = 75, Height = 26, BackColor = ColorAccent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            quick4h.Click += delegate { _toPicker.Value = DateTime.Now; _fromPicker.Value = DateTime.Now.AddHours(-4); };

            Tip(_fromPicker, "Start of the incident window (this server's local time).\nEverything older than this is skipped, except rolling log files written up to 2 days before it.");
            Tip(_toPicker, "End of the incident window (this server's local time).");
            Tip(quick1h, "Set the window to the last 1 hour.");
            Tip(quick4h, "Set the window to the last 4 hours.");

            // Config & Output
            var cfgLabel = new Label { Text = "Config file:", Location = new Point(0, 100), Width = 70, AutoSize = true };
            _configBox = new TextBox
            {
                Text = AppConfig.DefaultConfigPathNextToExe(),
                Location = new Point(70, 96),
                Width = 620
            };
            Tip(_configBox, "plant.config.xml — the settings file that sits next to PlantDownRca.exe.\n"
                + "Nothing here yet? Open the Configurator tab, set things up and press \"Save configuration\";\n"
                + "the file is created for you. Safe to edit in Notepad too.");

            var outLabel = new Label { Text = "Output folder:", Location = new Point(0, 135), Width = 70, AutoSize = true };
            _outBox = new TextBox
            {
                Text = RunEngine.DefaultOutputFolder(),
                Location = new Point(70, 131),
                Width = 620
            };
            Tip(_outBox, "Where the report is written. A folder named RCA-yyyyMMdd-HHmmss is created inside it,\n"
                + "holding Report.html, timeline.csv, summary.txt and entries.xml.");

            var sqlHintLabel = new Label
            {
                Text = "SQL query collection is configured on the Configurator tab (optional).",
                Location = new Point(0, 172),
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray,
                Font = new Font("Segoe UI", 8)
            };

            inputPanel.Controls.Add(inputTitleLabel);
            inputPanel.Controls.Add(timeGroupLabel);
            inputPanel.Controls.Add(fromLabel);
            inputPanel.Controls.Add(_fromPicker);
            inputPanel.Controls.Add(toLabel);
            inputPanel.Controls.Add(_toPicker);
            inputPanel.Controls.Add(quick1h);
            inputPanel.Controls.Add(quick4h);
            inputPanel.Controls.Add(cfgLabel);
            inputPanel.Controls.Add(_configBox);
            inputPanel.Controls.Add(outLabel);
            inputPanel.Controls.Add(_outBox);
            inputPanel.Controls.Add(sqlHintLabel);

            // ============ ACTION SECTION ============
            var actionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = ColorBackground,
                Padding = new Padding(0, 10, 0, 0)
            };

            _runButton = new Button
            {
                Text = "▶ Collect & Build Report",
                Location = new Point(0, 12),
                Width = 200,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = ColorAccent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _runButton.Click += RunButtonOnClick;
            Tip(_runButton, "Reads the configured log folders, Event Logs and (optionally) SQL queries for the\n"
                + "selected time window, then writes the report. Read-only: nothing on the server is changed.\n"
                + "Tip: run as Administrator so Windows Event Logs can be read.");

            _openReportButton = new Button
            {
                Text = "📄 Open Report",
                Location = new Point(210, 12),
                Width = 140,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = ColorSuccess,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _openReportButton.Click += delegate
            {
                if (!string.IsNullOrEmpty(_lastReportPath) && File.Exists(_lastReportPath))
                    Process.Start(_lastReportPath);
            };

            actionPanel.Controls.Add(_runButton);
            actionPanel.Controls.Add(_openReportButton);

            // ============ PROGRESS SECTION ============
            var progressLabelPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = ColorBackground
            };

            var progressLabel = new Label
            {
                Text = "Execution Log",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(0, 5),
                AutoSize = true
            };

            progressLabelPanel.Controls.Add(progressLabel);

            _progress = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = ColorCard,
                ForeColor = ColorText,
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle
            };

            // ============ NOTES/FOOTER SECTION ============
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = ColorMuted,
                Padding = new Padding(15),
                Margin = new Padding(0, 15, 0, 0)
            };

            var noteLabel = new Label
            {
                Text = "💡 Tip: Run as Administrator to read Event Logs. For multi-server reports, copy the output folders to one location and use --merge mode.",
                Location = new Point(0, 0),
                AutoSize = false,
                Width = 850,
                Height = 50,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            footerPanel.Controls.Add(noteLabel);

            // ============ ASSEMBLE LAYOUT ============
            mainPanel.Controls.Add(_progress);
            mainPanel.Controls.Add(progressLabelPanel);
            mainPanel.Controls.Add(actionPanel);
            mainPanel.Controls.Add(inputPanel);
            mainPanel.Controls.Add(headerPanel);
            mainPanel.Controls.Add(footerPanel);

            // ============ TABS: Run + Configurator ============
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9) };

            var runTab = new TabPage("Run") { BackColor = ColorBackground, Padding = new Padding(0) };
            runTab.Controls.Add(mainPanel);

            var configTab = new TabPage("Configurator") { BackColor = ColorBackground };
            BuildConfigurator(configTab);

            var helpTab = new TabPage("Help & Guide") { BackColor = ColorBackground };
            BuildHelp(helpTab);

            tabs.TabPages.Add(runTab);
            tabs.TabPages.Add(configTab);
            tabs.TabPages.Add(helpTab);
            Controls.Add(tabs);

            // First run: make sure there is a real plant.config.xml (copied from the example),
            // so the Configurator always loads from / saves to an existing file.
            string bootstrapMsg = null;
            try { bootstrapMsg = AppConfig.CreateFromExampleIfMissing(); }
            catch (Exception ex) { bootstrapMsg = "Could not create plant.config.xml: " + ex.Message; }

            LoadConfigIntoForm();

            if (bootstrapMsg != null)
            {
                bool bad = bootstrapMsg.StartsWith("Could not");
                _configStatus.ForeColor = bad ? ColorDestructive : ColorSuccess;
                _configStatus.Text = bootstrapMsg;
            }
        }

        private void RunButtonOnClick(object sender, EventArgs e)
        {
            _runButton.Enabled = false;
            _openReportButton.Enabled = false;
            _progress.Items.Clear();

            try
            {
                var cfg = AppConfig.Load(_configBox.Text);
                Log("═══════════════════════════════════════════════════════════");
                Log("Plant Down RCA — Incident Collection Started");
                Log("═══════════════════════════════════════════════════════════");
                Log($"Plant: {cfg.PlantName} | Server: {Environment.MachineName} | Role: {cfg.ServerRole}");
                if (cfg.EnableSqlQueries)
                    Log("⚙️  SQL query collection: ENABLED");
                else
                    Log("⚙️  SQL query collection: DISABLED");

                var run = RunEngine.Run(cfg, _fromPicker.Value, _toPicker.Value, Log);
                Log("");
                Log("📊 Building reports...");
                ReportWriters.WriteAll(run, _outBox.Text);

                _lastReportPath = Path.Combine(_outBox.Text, "Report.html");
                Log("═══════════════════════════════════════════════════════════");
                Log($"✓ SUCCESS — Collected {run.Entries.Count} entries");
                Log($"📁 Output folder: {_outBox.Text}");
                Log($"📄 Main report: Report.html");
                Log($"📋 Additional: timeline.csv, summary.txt, entries.xml");
                Log("═══════════════════════════════════════════════════════════");
                _openReportButton.Enabled = true;
            }
            catch (Exception ex)
            {
                Log("═══════════════════════════════════════════════════════════");
                Log("❌ FAILED — Exception occurred");
                Log("═══════════════════════════════════════════════════════════");
                Log($"Error: {ex.Message}");
                Log("═══════════════════════════════════════════════════════════");
                MessageBox.Show(ex.Message, "Run failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _runButton.Enabled = true;
            }
        }

        private void Log(string text)
        {
            _progress.Items.Add(text);
            _progress.TopIndex = _progress.Items.Count - 1;
            Application.DoEvents();
        }

        /// <summary>Attaches a hover tooltip to a control (no-op before the ToolTip exists).</summary>
        private void Tip(Control control, string text)
        {
            if (_tips != null && control != null)
                _tips.SetToolTip(control, text);
        }

        // ===================== CONFIGURATOR TAB =====================
        // Explicit path entry for application logs (any product/plant — nothing is hardcoded) plus an
        // optional SQL query. Everything is written back to plant.config.xml. SQL Server ERRORLOG and
        // Kepware log locations are located automatically from their default install paths.

        private void BuildConfigurator(TabPage page)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ColorBackground,
                Padding = new Padding(24)
            };
            page.Controls.Add(p);

            int y = 0;

            p.Controls.Add(new Label
            {
                Text = "Log Source Configuration",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 34;

            p.Controls.Add(new Label
            {
                Text = "Add the folders that hold your application logs (any product — nothing is hardcoded, and "
                     + "nothing is guessed). Subfolders are searched automatically. Saved to the config file shown "
                     + "on the Run tab. SQL Server (ERRORLOG) and Kepware logs are found automatically from their "
                     + "default install locations, so you do not need to add those.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.Gray,
                Location = new Point(0, y),
                MaximumSize = new Size(800, 56),
                AutoSize = true
            });
            y += 56;

            // Plant / role
            p.Controls.Add(new Label { Text = "Plant name:", Location = new Point(0, y + 3), AutoSize = true });
            _plantBox = new TextBox { Location = new Point(110, y), Width = 280 };
            Tip(_plantBox, "Free text shown in the report header, e.g. \"Plant 3 — Packaging\".\nOnly used as a label; nothing is looked up from it.");
            p.Controls.Add(_plantBox);
            p.Controls.Add(new Label { Text = "Server role:", Location = new Point(420, y + 3), AutoSize = true });
            _roleBox = new TextBox { Location = new Point(520, y), Width = 200 };
            Tip(_roleBox, "What this server is: App, Sql, Kepware or Other.\nShown on the report so merged packages from several servers stay readable.");
            p.Controls.Add(_roleBox);
            y += 40;

            _discoveryCheck = new CheckBox
            {
                Text = "Auto-detect SQL Server ERRORLOG and Kepware log folders (no paths needed)",
                Location = new Point(0, y),
                Width = 700,
                Checked = true
            };
            Tip(_discoveryCheck, "On: the tool finds SQL Server's Log folder (…\\MSSQL\\Log) and the standard Kepware "
                + "folders on its own.\nOff: only the folders you list yourself are read.");
            p.Controls.Add(_discoveryCheck);
            y += 28;

            _warningsCheck = new CheckBox
            {
                Text = "Include warnings (not only errors)",
                Location = new Point(0, y),
                Width = 700,
                Checked = true
            };
            Tip(_warningsCheck, "Off = keep only Error/Critical entries.\nOn = warnings are collected too (recommended: warnings just before the outage are usually the clue).");
            p.Controls.Add(_warningsCheck);
            y += 28;

            _appFoldersList = BuildFolderEditor(p, "Application log folders (your *.log / *.txt files)", ref y);

            // Channels and services are CHOOSABLE, not hand-typed: the dropdowns below the boxes are
            // filled from this machine (every registered Event Log channel, every installed service).
            _channelsBox = BuildLinesEditor(p, "Event log channels (one per line)", ref y,
                "Windows Event Log channels to read. Pick one from the dropdown below the box and press "
                + "\"Add\" — the list contains every channel registered on this machine (Application, System, "
                + "Microsoft-Windows-FailoverClustering/Operational, …). You can also type a name directly; "
                + "delete a line to drop a channel.\n"
                + "Run as Administrator or the read will return nothing.",
                KnownEventLogChannels, "channels registered on this machine — pick and press Add");
            _servicesBox = BuildLinesEditor(p, "Services to check (one per line)", ref y,
                "Windows services whose running state is recorded in the health snapshot. Pick one from the "
                + "dropdown below the box and press \"Add\" — the list contains every service installed on this "
                + "machine (MSSQLSERVER, SQLSERVERAGENT, MSMQ, …). The value is the service's internal name "
                + "(services.msc → Properties), not its display name. Delete a line to drop a service.",
                InstalledServiceNames, "services installed on this machine — pick and press Add");

            // ---------- Optional SQL queries (run during collection) ----------
            p.Controls.Add(new Label
            {
                Text = "SQL queries (optional)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 24;

            p.Controls.Add(new Label
            {
                Text = "WHAT THIS IS: a query is a question you ask the database — \"give me the errors between these two "
                     + "times\". The tool runs it read-only (SELECT only) and puts the rows on the same timeline as the logs.\n"
                     + "YOU NEED 3 THINGS: (1) the SQL text, (2) the name of the column that holds the date/time, "
                     + "(3) the name of the column that holds the message. The level and code columns are optional.\n"
                     + "DON'T KNOW THE COLUMN NAMES? Press \"Show columns of a table…\" inside the query box and pick from "
                     + "the list. Leave the Server box above empty to skip SQL entirely.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(0, y),
                MaximumSize = new Size(800, 70),
                AutoSize = true
            });
            y += 74;

            _cfgEnableSql = new CheckBox
            {
                Text = "Enable SQL query collection",
                Location = new Point(0, y),
                Width = 620
            };
            Tip(_cfgEnableSql, "Turns on the read-only SQL queries listed below during a run.\n"
                + "Leave it off if you only want file logs, Event Logs and the health snapshot.");
            p.Controls.Add(_cfgEnableSql);
            y += 28;

            // ---------- Connection: simple boxes only — nothing connection-string related to type ----------
            p.Controls.Add(new Label { Text = "Server (IP or host):", Location = new Point(0, y + 3), AutoSize = true });
            _sqlServerBox = new TextBox { Location = new Point(150, y), Width = 170 };
            Tip(_sqlServerBox, "IP address or host name of the SQL Server, e.g. 10.0.0.5 or sql-node-01.\n"
                + "Leave empty to skip SQL collection entirely.");
            p.Controls.Add(_sqlServerBox);

            p.Controls.Add(new Label { Text = "Database:", Location = new Point(340, y + 3), AutoSize = true });
            _sqlDbBox = new ComboBox
            {
                Location = new Point(415, y),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDown, // pick from the list, or type a name if you know it
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            Tip(_sqlDbBox, "Choose the database the plant application runs on.\n"
                + "Press \"Load databases…\" to fill this list from the server, or type the name if you know it.");
            p.Controls.Add(_sqlDbBox);

            _sqlDbsButton = new Button
            {
                Text = "Load databases…",
                Location = new Point(575, y - 2),
                Width = 120,
                Height = 27,
                FlatStyle = FlatStyle.Flat
            };
            _sqlDbsButton.Click += LoadDatabasesOnClick;
            Tip(_sqlDbsButton, "Connects with the settings above and lists every online database on that server,\n"
                + "so you can just pick one from the dropdown. Read-only: nothing is changed.");
            p.Controls.Add(_sqlDbsButton);
            y += 30;

            p.Controls.Add(new Label { Text = "User name:", Location = new Point(0, y + 3), AutoSize = true });
            _sqlUserBox = new TextBox { Location = new Point(150, y), Width = 170 };
            p.Controls.Add(_sqlUserBox);

            p.Controls.Add(new Label { Text = "Password:", Location = new Point(340, y + 3), AutoSize = true });
            _sqlPwdBox = new TextBox
            {
                Location = new Point(415, y),
                Width = 160,
                UseSystemPasswordChar = true
            };
            p.Controls.Add(_sqlPwdBox);
            _sqlShowPwd = new CheckBox
            {
                Text = "Show",
                Location = new Point(585, y + 2),
                Width = 70,
                AutoSize = true
            };
            _sqlShowPwd.CheckedChanged += delegate { _sqlPwdBox.UseSystemPasswordChar = !_sqlShowPwd.Checked; };
            p.Controls.Add(_sqlShowPwd);
            y += 30;

            _sqlWinAuth = new CheckBox
            {
                Text = "Windows authentication (Trusted_Connection) — ignore the user name and password above",
                Location = new Point(0, y),
                Width = 700
            };
            Tip(_sqlWinAuth, "Use this when the account you are logged in with already has access to SQL Server.\n"
                + "User name and password stay empty and Trusted_Connection=True is written for you.");
            p.Controls.Add(_sqlWinAuth);
            y += 30;

            p.Controls.Add(new Label
            {
                Text = "Connection string (written for you automatically — nothing to type here):",
                Location = new Point(0, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = ColorText
            });
            y += 22;

            // Information line, NOT an input: it only shows what the boxes above produce.
            _sqlConnPreview = new Label
            {
                Location = new Point(0, y),
                AutoSize = true,
                Font = new Font("Consolas", 9),
                ForeColor = Color.Gray
            };
            Tip(_sqlConnPreview, "This line builds itself from the boxes above — there is nothing to edit here.\n"
                + "Example of a filled-in connection:\n"
                + "  Server=10.0.0.5;Database=MyDatabase;User Id=report_user;Password=********;\n"
                + "The real password is stored only in the config file on this server, never in reports.");
            p.Controls.Add(_sqlConnPreview);
            y += 28;

            _sqlTestButton = new Button
            {
                Text = "Test connection",
                Location = new Point(0, y),
                Width = 140,
                Height = 30,
                FlatStyle = FlatStyle.Flat
            };
            _sqlTestButton.Click += TestConnectionOnClick;
            Tip(_sqlTestButton, "Opens a short-lived connection with the settings above and reports success or the\n"
                + "exact error. Nothing is queried or changed on the database.");
            p.Controls.Add(_sqlTestButton);

            _sqlTestStatus = new Label
            {
                Location = new Point(150, y + 6),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            p.Controls.Add(_sqlTestStatus);
            y += 40;

            // Live preview: rebuild the connection string whenever any field changes.
            EventHandler refreshConn = delegate { RefreshConnPreview(); };
            _sqlServerBox.TextChanged += refreshConn;
            _sqlDbBox.TextChanged += refreshConn;
            _sqlUserBox.TextChanged += refreshConn;
            _sqlPwdBox.TextChanged += refreshConn;
            _sqlWinAuth.CheckedChanged += refreshConn;
            RefreshConnPreview();

            // One stacked editor per query; "+ Add another SQL query" appends below.
            _sqlQueriesPanel = new FlowLayoutPanel
            {
                Location = new Point(0, y),
                Width = 800,
                Height = 360,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = ColorBackground
            };
            p.Controls.Add(_sqlQueriesPanel);
            y += 370;

            var addSqlButton = new Button
            {
                Text = "+ Add another SQL query",
                Location = new Point(0, y),
                Width = 200,
                Height = 30,
                FlatStyle = FlatStyle.Flat
            };
            addSqlButton.Click += delegate { AddSqlQueryRow("", "", "", "", "", ""); };
            Tip(addSqlButton, "Adds a second (third, …) query editor below. Each query needs @From and @To in its SQL.");
            p.Controls.Add(addSqlButton);

            var exampleSqlButton = new Button
            {
                Text = "Add an example query",
                Location = new Point(210, y),
                Width = 180,
                Height = 30,
                FlatStyle = FlatStyle.Flat
            };
            exampleSqlButton.Click += delegate { AddExampleQueryRow(); };
            Tip(exampleSqlButton, "Adds a filled-in example so you can see the shape of a working query.\n"
                + "Replace the table and column names with yours — use \"Show columns of a table…\" to find them.");
            p.Controls.Add(exampleSqlButton);
            y += 42;

            // Save / Reload
            var saveButton = new Button
            {
                Text = "Save configuration",
                Location = new Point(0, y),
                Width = 180,
                Height = 34,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = ColorAccent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            saveButton.Click += SaveConfigOnClick;
            Tip(saveButton, "Writes everything on this tab to plant.config.xml (next to the exe).\nThe file is created on the first save. Your password is stored only there, on this server.");
            p.Controls.Add(saveButton);

            var reloadButton = new Button
            {
                Text = "Reload from file",
                Location = new Point(190, y),
                Width = 140,
                Height = 34,
                FlatStyle = FlatStyle.Flat
            };
            reloadButton.Click += delegate { LoadConfigIntoForm(); };
            Tip(reloadButton, "Discards unsaved changes and re-reads plant.config.xml.");
            p.Controls.Add(reloadButton);

            _configStatus = new Label
            {
                Location = new Point(345, y + 8),
                AutoSize = true,
                MaximumSize = new Size(400, 40),
                ForeColor = ColorSuccess
            };
            p.Controls.Add(_configStatus);
        }

        // ===================== HELP & GUIDE TAB =====================
        // Plain-language guide inside the app: what to click, which files are read,
        // and how the connection is configured. Built from labels so it scales with text.

        private void BuildHelp(TabPage page)
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ColorBackground,
                Padding = new Padding(24)
            };
            page.Controls.Add(scroll);

            var inner = new Panel { Location = new Point(24, 24), Width = 870, Height = 10 };
            inner.AutoSize = true;
            scroll.Controls.Add(inner);

            int y = 0;
            y = HelpHeading(inner, "How to use this tool", y, 15);
            y = HelpBody(inner,
                "Plant Down RCA Collector gathers logs from one server for a chosen time window and writes a single "
              + "report. It is read-only: it never changes anything on the server. Copy the folder to each server "
              + "involved, run it there once per server, then merge the results (see section 6).", y);
            y += 6;

            y = HelpHeading(inner, "1. Quick start", y);
            y = HelpBody(inner,
                "1.  Open the Configurator tab (plant.config.xml is created automatically from the example "
              + "on the first run — no file editing needed).\n"
              + "2.  Add your application log folders (Configurator tab → \"Add folder…\"). Event-log channels "
              + "and services are chosen from the dropdowns under their boxes (every channel/service on this "
              + "machine is listed — pick and press \"Add\"). Fill the database boxes if you want SQL queries, "
              + "then press \"Save configuration\". The status line confirms exactly what was saved.\n"
              + "3.  Back on the Run tab, set the incident window (From / To, or the Last 1h / Last 4h buttons).\n"
              + "4.  Press \"Collect & Build Report\" and wait for the log to finish.\n"
              + "5.  Press \"Open Report\" — Report.html opens in your browser.", y);
            y += 6;

            y = HelpHeading(inner, "2. Which files does the \"Application log folders\" setting read?", y);
            y = HelpBody(inner,
                "•  File types: every  *.log  and  *.txt  file inside the folders you add, including all "
              + "subfolders (recursive). No folder is guessed for you — only what you add here is read.\n"
              + "•  Rolling logs: files written up to 2 days before the window start are still opened, because a "
              + "file created earlier can contain lines from inside the window. Entries are then filtered by the "
              + "timestamp written in the line, not by the file date.\n"
              + "•  Locked files are fine: files in use by another program can still be read.\n"
              + "•  Unreadable lines are never dropped: a line whose timestamp cannot be parsed is kept and shown "
              + "in the report's \"Untimed\" section.\n"
              + "•  SQL Server logs (ERRORLOG, ERRORLOG.1…, SQLAGENT.OUT) and Kepware logs are found automatically "
              + "from their standard install locations — you do not add those folders.\n"
              + "•  Windows Event Logs use the channels listed on the Configurator tab (run as Administrator to "
              + "read them).", y);
            y += 6;

            y = HelpHeading(inner, "3. Database connection — no connection string to write", y);
            y = HelpBody(inner,
                "On the Configurator tab, in \"Database connection (optional)\", the whole setup is boxes:\n"
              + "1.  Server (IP or host)  e.g. 10.0.0.5   or   sql-node-01\n"
              + "2.  User name            e.g. report_user      (or tick \"Windows authentication\")\n"
              + "3.  Password             (hidden by default; tick \"Show\" to check it)\n"
              + "4.  Press \"Load databases…\"  -  the Database list fills itself with every database\n"
              + "    on that server; just pick the one the plant application uses. If the list cannot\n"
              + "    be loaded, type the database name into the box instead.\n\n"
              + "The grey line below the boxes shows the connection string the tool builds for you\n"
              + "(it is information only — there is nothing to edit; the password shows as ********).\n\n"
              + "Press \"Test connection\" to confirm the settings before a real run. The password is saved only "
              + "inside plant.config.xml on this server and is never written into any report. Queries are read-only "
              + "SELECT statements; leave the Server empty to skip SQL entirely.\n\n"
              + "You do not need any SQL query to keep the connection: press \"Save configuration\" after filling "
              + "the boxes and the connection is stored (the status line tells you what was written). "
              + "Non-technical setup: IP + user + password, Load databases, pick one, Save — done.", y);
            y += 6;

            y = HelpHeading(inner, "4. SQL queries", y);
            y = HelpBody(inner,
                "The query box explains itself: every field has a plain-language label, an example next to it and a "
              + "tooltip. In short — a query is a question you ask the database (\"give me the errors between these "
              + "two times\"). You need the SQL text plus the names of two columns: the one with the date/time and the "
              + "one with the message (level and code columns are optional extras).\n"
              + "Do not know the column names? Press \"Show columns of a table…\" inside the query box, type the table "
              + "name, press Load columns and pick from the list — it fills the field for you.\n"
              + "Press \"Add an example query\" to see a complete working query you can adapt.\n"
              + "Every query must use @From and @To — they are replaced with the incident window. Only SELECT is "
              + "allowed; nothing is inserted, updated or deleted.", y);
            y += 6;

            y = HelpHeading(inner, "5. What you get", y);
            y = HelpBody(inner,
                "A folder named RCA-yyyyMMdd-HHmmss inside the output folder:\n"
              + "    Report.html    open this one — filterable timeline, grouped issues, collector notes\n"
              + "    timeline.csv   every entry, one row each (opens in Excel)\n"
              + "    summary.txt    plain-text overview of the window\n"
              + "    entries.xml    full raw entries, used by the merge mode", y);
            y += 6;

            y = HelpHeading(inner, "6. Reports from more than one server", y);
            y = HelpBody(inner,
                "Copy each server's RCA-… folder to one machine, then merge them (command prompt):\n"
              + "    PlantDownRca.exe --merge \"C:\\RCA\\run-from-sql\" \"C:\\RCA\\run-from-app\" --out \"C:\\RCA\\merged\"\n"
              + "Cross-server ordering is only as good as the servers' clock sync (NTP); the merged report says so too.", y);
            y += 6;

            y = HelpHeading(inner, "7. Troubleshooting", y);
            y = HelpBody(inner,
                "•  Event Logs empty → run PlantDownRca.exe as Administrator (right-click → Run as administrator).\n"
              + "•  \"No candidate files found\" → the folder path is wrong, the folder is empty, or nothing was "
              + "written in the window; check the path on the Configurator tab.\n"
              + "•  Nothing after a certain time → check the From/To window (server local time).\n"
              + "•  SQL test fails → check IP/host, database name and credentials; firewalls must allow the port.\n"
              + "•  Times look shifted → the source logs in UTC; note it in the config.\n"
              + "•  The config file must sit next to PlantDownRca.exe (the Run tab shows its full path).", y);
            y += 10;

            y = HelpHeading(inner, "Command line (no window)", y);
            y = HelpBody(inner,
                "PlantDownRca.exe --from \"2026-10-06 14:00\" --to \"2026-10-06 15:00\" "
              + "--config plant.config.xml --out C:\\Temp\\RCA", y);
            y += 14;

            y = HelpBody(inner,
                "PlantDownRca - portable plant-down RCA collector. Owned by Ayush Shrestha.", y);

            inner.Height = y + 40;
        }

        /// <summary>Adds a section heading; returns the next free Y position.</summary>
        private static int HelpHeading(Panel parent, string text, int y, float size = 11)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                ForeColor = ColorAccent,
                Location = new Point(0, y),
                AutoSize = true
            };
            parent.Controls.Add(label);
            return y + label.Height + 6;
        }

        /// <summary>Adds a body paragraph (word-wrapped to the panel width); returns the next free Y.</summary>
        private static int HelpBody(Panel parent, string text, int y)
        {
            var font = new Font("Segoe UI", 9);
            var bounds = TextRenderer.MeasureText(text, font, new Size(840, 0), TextFormatFlags.WordBreak);
            var label = new Label
            {
                Text = text,
                Font = font,
                ForeColor = Color.FromArgb(45, 55, 70),
                Location = new Point(0, y),
                AutoSize = false,
                Width = 850,
                Height = bounds.Height + 4
            };
            parent.Controls.Add(label);
            return y + label.Height + 6;
        }

        private ListBox BuildFolderEditor(Panel parent, string title, ref int y)
        {
            parent.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 22;

            var list = new ListBox
            {
                Location = new Point(0, y),
                Width = 700,
                Height = 72,
                HorizontalScrollbar = true
            };
            Tip(list, "Folders added here are searched for  *.log  and  *.txt  files, including every subfolder.\n"
                + "Only files modified from 2 days before the window start are opened (rolling logs), and each line "
                + "is filtered by the timestamp inside it.\n"
                + "Nothing is guessed: only the folders in this list are read.");
            parent.Controls.Add(list);

            var addButton = new Button
            {
                Text = "Add folder...",
                Location = new Point(710, y),
                Width = 120,
                Height = 30
            };
            Tip(addButton, "Pick a folder that holds your application's .log / .txt files.\n"
                + "Add the root log folder — subfolders are included automatically.");
            addButton.Click += delegate
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = title + " — every *.log and *.txt file inside (and below) the chosen folder is read.";
                    if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                        list.Items.Add(dialog.SelectedPath);
                }
            };
            parent.Controls.Add(addButton);

            var removeButton = new Button
            {
                Text = "Remove",
                Location = new Point(710, y + 36),
                Width = 120,
                Height = 30
            };
            Tip(removeButton, "Remove the selected folder from the list (nothing on disk is deleted).");
            removeButton.Click += delegate
            {
                if (list.SelectedIndex >= 0)
                    list.Items.RemoveAt(list.SelectedIndex);
            };
            parent.Controls.Add(removeButton);

            y += 80;

            var hint = new Label
            {
                Text = "Reads *.log and *.txt in these folders and all subfolders. SQL Server and Kepware log "
                     + "folders are found automatically — you do not add them here. Details on the Help & Guide tab.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(0, y),
                AutoSize = true,
                MaximumSize = new Size(780, 30)
            };
            parent.Controls.Add(hint);
            y += 34;
            return list;
        }

        private TextBox BuildLinesEditor(Panel parent, string title, ref int y, string tipText = null,
            Func<List<string>> itemSource = null, string pickHint = null)
        {
            parent.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 20;

            var box = new TextBox
            {
                Multiline = true,
                Location = new Point(0, y),
                Width = 700,
                Height = 64,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9)
            };
            if (tipText != null)
                Tip(box, tipText);
            parent.Controls.Add(box);
            y += 68;

            // Optional picker: a dropdown of real names on this machine + an Add button, so the user
            // chooses instead of typing. The box above stays editable (delete a line to remove one).
            if (itemSource != null)
            {
                var combo = new ComboBox
                {
                    Location = new Point(0, y),
                    Width = 555,
                    DropDownStyle = ComboBoxStyle.DropDown,
                    AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                    AutoCompleteSource = AutoCompleteSource.ListItems
                };
                if (pickHint != null)
                    Tip(combo, pickHint + "\nType a few letters to filter. You can also type a name that is "
                        + "not in the list (e.g. from another machine).");
                try
                {
                    foreach (var item in itemSource())
                        combo.Items.Add(item);
                }
                catch { /* enumeration is best-effort; the box stays typeable */ }

                var add = new Button
                {
                    Text = "Add",
                    Location = new Point(565, y - 2),
                    Width = 90,
                    Height = 27,
                    FlatStyle = FlatStyle.Flat
                };
                var capturedBox = box;
                var capturedCombo = combo;
                add.Click += delegate { AddLineFromCombo(capturedBox, capturedCombo); };
                Tip(add, "Appends the chosen value to the list above (no duplicates).");

                parent.Controls.Add(combo);
                parent.Controls.Add(add);
                y += 34;
            }

            return box;
        }

        /// <summary>Appends the combo's current value to the lines box, skipping duplicates.</summary>
        private static void AddLineFromCombo(TextBox box, ComboBox combo)
        {
            string value = (combo.Text ?? "").Trim();
            if (value.Length == 0)
                return;

            var lines = new List<string>();
            foreach (var raw in box.Text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0)
                    lines.Add(line);
            }
            foreach (var existing in lines)
            {
                if (existing.Equals(value, StringComparison.OrdinalIgnoreCase))
                    return; // already listed
            }

            lines.Add(value);
            box.Text = string.Join("\r\n", lines.ToArray());
            combo.Text = "";
        }

        /// <summary>Every Event Log channel registered on this machine.
        /// Best source: "wevtutil el" (lists all 1000+ channels). Falls back to the registry,
        /// then to the common ones, so the dropdown is never empty.</summary>
        private static List<string> KnownEventLogChannels()
        {
            var names = new List<string>();

            // 1) wevtutil el — the authoritative list on every Windows install.
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wevtutil.exe",
                    Arguments = "el",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    if (!proc.WaitForExit(10000))
                        proc.Kill();
                    foreach (var line in output.Replace("\r", "").Split('\n'))
                    {
                        string name = line.Trim().Trim('\uFEFF'); // strip stray BOM
                        if (name.Length > 0)
                            names.Add(name);
                    }
                }
            }
            catch { names.Clear(); }

            // 2) Registry fallback (present on some Windows versions).
            if (names.Count == 0)
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Channels"))
                    {
                        if (key != null)
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                if (!string.IsNullOrWhiteSpace(name))
                                    names.Add(name);
                            }
                        }
                    }
                }
                catch { /* best-effort */ }
            }

            // 3) Last resort: the well-known channels.
            if (names.Count == 0)
            {
                names.AddRange(new[]
                {
                    "Application", "System", "Security", "Setup",
                    "Microsoft-Windows-FailoverClustering/Operational",
                    "Microsoft-Windows-Cluster-AutoFailover/Operational"
                });
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>Every Windows service installed on this machine (internal names).</summary>
        private static List<string> InstalledServiceNames()
        {
            var names = new List<string>();
            try
            {
                foreach (var controller in System.ServiceProcess.ServiceController.GetServices())
                {
                    try { names.Add(controller.ServiceName); }
                    finally { controller.Dispose(); }
                }
            }
            catch { /* best-effort */ }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        // Adds one stacked SQL query editor (with its own Remove button) to the queries panel.
        // Everything is explained IN PLACE: plain-language labels, an example next to each box and a
        // "Show columns of a table…" helper — no trip to the Help tab needed.
        private void AddSqlQueryRow(string name, string ts, string msg, string lvl, string code, string sql)
        {
            _sqlRowCounter++;
            var row = new SqlQueryRow
            {
                Panel = new Panel
                {
                    Width = 800,
                    Height = 350,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.White,
                    Margin = new Padding(0, 0, 0, 8)
                }
            };

            row.Panel.Controls.Add(new Label
            {
                Text = "Query " + _sqlRowCounter,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = ColorText,
                Location = new Point(10, 12),
                AutoSize = true
            });

            var remove = new Button
            {
                Text = "Remove",
                Location = new Point(698, 8),
                Width = 90,
                Height = 26,
                FlatStyle = FlatStyle.Flat
            };
            remove.Click += delegate
            {
                _sqlQueryRows.Remove(row);
                _sqlQueriesPanel.Controls.Remove(row.Panel);
                row.Panel.Dispose();
            };
            row.Panel.Controls.Add(remove);

            // 1) A name for the report (optional).
            row.Name = AddLabeledBox(row.Panel, "Name for this query (optional):", 10, 44, 370, name,
                "e.g. ProcedureErrors",
                "Just a label for you and for the report, e.g. \"ProcedureErrors\".\n"
                + "Leave it empty and the query is numbered for you.");

            // Helper: list the real column names of a table on the configured database.
            var showColumns = new Button
            {
                Text = "Show columns of a table…",
                Location = new Point(560, 64),
                Width = 230,
                Height = 27,
                FlatStyle = FlatStyle.Flat
            };
            showColumns.Click += delegate { ShowColumnPicker(row); };
            Tip(showColumns, "Lists the columns of a table on the database you configured, so you can pick the\n"
                + "right names instead of guessing. Read-only — nothing is changed.");
            row.Panel.Controls.Add(showColumns);
            row.Panel.Controls.Add(new Label
            {
                Text = "Don't know the column names? Use this.",
                Location = new Point(560, 94),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8)
            });

            // 2) The two columns that are actually required.
            row.Ts = AddLabeledBox(row.Panel, "Which column has the date/time?", 10, 120, 180, ts,
                "e.g. LogTime",
                "The column in your SELECT that holds the date/time of the event.\n"
                + "This is what puts the row in the right place on the timeline.\n"
                + "Example: if your query selects ErrorDate, type ErrorDate here.");
            row.Msg = AddLabeledBox(row.Panel, "Which column has the message?", 400, 120, 180, msg,
                "e.g. Message",
                "The column that holds the text you want to read in the report.\n"
                + "Example: if your query selects ErrorMessage, type ErrorMessage here.");

            // 3) The two optional columns.
            row.Lvl = AddLabeledBox(row.Panel, "Which column says Error/Warning? (optional)", 10, 176, 180, lvl,
                "e.g. Severity",
                "Optional. The column that says how serious it is (Error / Warning / Info).\n"
                + "Leave empty if your table has no such column.");
            row.Code = AddLabeledBox(row.Panel, "Which column has the error number? (optional)", 400, 176, 180, code,
                "e.g. ErrorCode",
                "Optional. The column with the error number or event id.\n"
                + "It is shown in brackets before the message, e.g. [9002].");

            // 4) The SQL itself.
            row.Panel.Controls.Add(new Label
            {
                Text = "The SQL to run (read-only SELECT — it must use @From and @To for the time window):",
                Location = new Point(10, 232),
                AutoSize = true
            });
            row.Sql = new TextBox
            {
                Multiline = true,
                Location = new Point(10, 254),
                Width = 778,
                Height = 82,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                Text = sql ?? ""
            };
            Tip(row.Sql, "The SELECT statement the tool runs. It must filter by the incident window using @From and @To:\n"
                + "  WHERE ErrorDate >= @From AND ErrorDate <= @To\n"
                + "Both are replaced with the times you set on the Run tab. Only SELECT is allowed — nothing is written\n"
                + "to the database. The column names you typed above must match the SELECT (aliases count).");
            row.Panel.Controls.Add(row.Sql);

            _sqlQueryRows.Add(row);
            _sqlQueriesPanel.Controls.Add(row.Panel);
        }

        /// <summary>Adds a query row pre-filled with a working example, so the shape is obvious.</summary>
        private void AddExampleQueryRow()
        {
            AddSqlQueryRow(
                "Example",
                "LogTime",
                "Message",
                "Level",
                "Code",
                "SELECT TOP 1000\r\n"
              + "       ErrorDate    AS LogTime,\r\n"
              + "       ErrorMessage AS Message,\r\n"
              + "       Severity     AS Level,\r\n"
              + "       ErrorCode    AS Code\r\n"
              + "FROM   dbo.ErrorLog\r\n"
              + "WHERE  ErrorDate >= @From AND ErrorDate <= @To\r\n"
              + "ORDER BY ErrorDate");
        }

        /// <summary>Opens the column picker for one query row and fills the chosen field.</summary>
        private void ShowColumnPicker(SqlQueryRow row)
        {
            string conn = BuildConnectionString();
            if (conn.Length == 0)
            {
                _sqlTestStatus.ForeColor = ColorDestructive;
                _sqlTestStatus.Text = "Fill in the Server and Database boxes above first, then use Show columns.";
                return;
            }

            using (var dialog = new ColumnPickerDialog(conn, ""))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                switch (dialog.TargetField)
                {
                    case "Message column": row.Msg.Text = dialog.ChosenColumn; break;
                    case "Level column": row.Lvl.Text = dialog.ChosenColumn; break;
                    case "Code column": row.Code.Text = dialog.ChosenColumn; break;
                    default: row.Ts.Text = dialog.ChosenColumn; break;
                }
            }
        }

        /// <summary>Small dialog that lists the columns of a table on the configured database,
        /// so the user picks the right names instead of guessing them.</summary>
        private sealed class ColumnPickerDialog : Form
        {
            private readonly string _connectionString;
            private readonly TextBox _tableBox;
            private readonly ListBox _list;
            private readonly ComboBox _target;
            private readonly Label _status;
            private readonly List<string> _columnNames = new List<string>();

            public string ChosenColumn { get; private set; }
            public string TargetField { get { return (_target.SelectedItem as string) ?? "Timestamp column"; } }

            public ColumnPickerDialog(string connectionString, string suggestedTable)
            {
                _connectionString = connectionString ?? "";
                ChosenColumn = "";

                Text = "Columns in a table";
                Width = 560;
                Height = 480;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = Color.White;
                Font = new Font("Segoe UI", 9);

                Controls.Add(new Label
                {
                    Text = "1. Type the table name (ask your DBA if you are unsure), then press Load columns.",
                    Location = new Point(16, 14),
                    AutoSize = true
                });
                _tableBox = new TextBox { Location = new Point(16, 38), Width = 330, Text = suggestedTable ?? "" };
                Controls.Add(_tableBox);

                var load = new Button
                {
                    Text = "Load columns",
                    Location = new Point(356, 36),
                    Width = 120,
                    Height = 27,
                    FlatStyle = FlatStyle.Flat
                };
                load.Click += LoadColumnsOnClick;
                Controls.Add(load);

                Controls.Add(new Label
                {
                    Text = "2. Pick the column that holds the value:",
                    Location = new Point(16, 76),
                    AutoSize = true
                });
                _list = new ListBox
                {
                    Location = new Point(16, 98),
                    Width = 510,
                    Height = 200,
                    Font = new Font("Consolas", 9)
                };
                _list.DoubleClick += delegate { InsertOnClick(null, null); };
                Controls.Add(_list);

                Controls.Add(new Label { Text = "3. Put it into:", Location = new Point(16, 312), AutoSize = true });
                _target = new ComboBox
                {
                    Location = new Point(110, 309),
                    Width = 200,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                _target.Items.AddRange(new object[]
                {
                    "Timestamp column", "Message column", "Level column", "Code column"
                });
                _target.SelectedIndex = 0;
                Controls.Add(_target);

                var insert = new Button
                {
                    Text = "Insert",
                    Location = new Point(330, 307),
                    Width = 100,
                    Height = 30,
                    FlatStyle = FlatStyle.Flat
                };
                insert.Click += InsertOnClick;
                Controls.Add(insert);

                var close = new Button
                {
                    Text = "Close",
                    Location = new Point(440, 307),
                    Width = 86,
                    Height = 30,
                    FlatStyle = FlatStyle.Flat
                };
                close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                Controls.Add(close);

                _status = new Label
                {
                    Location = new Point(16, 352),
                    AutoSize = true,
                    ForeColor = Color.Gray,
                    MaximumSize = new Size(510, 70)
                };
                Controls.Add(_status);
            }

            private void LoadColumnsOnClick(object sender, EventArgs e)
            {
                string table = (_tableBox.Text ?? "").Trim();
                if (table.Length == 0)
                {
                    _status.ForeColor = ColorDestructive;
                    _status.Text = "Type the table name first.";
                    return;
                }
                if (_connectionString.Length == 0)
                {
                    _status.ForeColor = ColorDestructive;
                    _status.Text = "Fill in the Server and Database boxes on the Configurator tab first.";
                    return;
                }

                string schema = null;
                string name = table;
                int dot = table.IndexOf('.');
                if (dot > 0)
                {
                    schema = table.Substring(0, dot).Trim();
                    name = table.Substring(dot + 1).Trim();
                }

                _status.ForeColor = Color.Gray;
                _status.Text = "Reading columns…";
                Application.DoEvents();

                try
                {
                    _columnNames.Clear();
                    _list.Items.Clear();

                    using (var cn = new System.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        cn.Open();
                        string sql = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t"
                                   + (schema != null ? " AND TABLE_SCHEMA = @s" : "")
                                   + " ORDER BY ORDINAL_POSITION";
                        using (var cmd = new System.Data.SqlClient.SqlCommand(sql, cn))
                        {
                            cmd.Parameters.AddWithValue("@t", name);
                            if (schema != null)
                                cmd.Parameters.AddWithValue("@s", schema);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string column = reader.GetString(0);
                                    string type = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                    _columnNames.Add(column);
                                    _list.Items.Add(column.PadRight(28) + "(" + type + ")");
                                }
                            }
                        }
                    }

                    _status.ForeColor = _columnNames.Count > 0 ? ColorSuccess : ColorDestructive;
                    _status.Text = _columnNames.Count > 0
                        ? "✓ " + _columnNames.Count + " columns — pick one and press Insert (or double-click it)."
                        : "No table named \"" + table + "\" was found in this database.";
                }
                catch (Exception ex)
                {
                    _status.ForeColor = ColorDestructive;
                    string message = ex.Message ?? "";
                    if (message.Length > 160)
                        message = message.Substring(0, 157) + "…";
                    _status.Text = "✗ " + message;
                }
            }

            private void InsertOnClick(object sender, EventArgs e)
            {
                if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _columnNames.Count)
                {
                    _status.ForeColor = ColorDestructive;
                    _status.Text = "Pick a column from the list first.";
                    return;
                }

                ChosenColumn = _columnNames[_list.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        /// <summary>Label ABOVE the box (long plain-language labels need their own line), with an
        /// optional grey example next to the box. Tooltips go on BOTH the label and the box, so
        /// hovering the words explains the field too.</summary>
        private TextBox AddLabeledBox(Panel parent, string label, int x, int y, int width,
            string value, string hint = null, string tipText = null)
        {
            var caption = new Label { Text = label, Location = new Point(x, y), AutoSize = true };
            if (tipText != null)
                Tip(caption, tipText);
            parent.Controls.Add(caption);

            var box = new TextBox { Location = new Point(x, y + 20), Width = width, Text = value ?? "" };
            if (tipText != null)
                Tip(box, tipText);
            parent.Controls.Add(box);

            if (hint != null)
            {
                parent.Controls.Add(new Label
                {
                    Text = hint,
                    Location = new Point(x + width + 8, y + 23),
                    AutoSize = true,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 8)
                });
            }

            return box;
        }

        // ================= CONNECTION STRING BUILDER =================
        // The user never has to write a connection string: they type the server (IP or host),
        // user name and password into boxes, pick the database from the list that fills itself
        // ("Load databases…"), and the string is assembled here and saved to plant.config.xml.

        /// <summary>Reads an existing connection string back into the simple fields.
        /// Accepts any common spelling: Server/Addr/Data Source, Database/Initial Catalog,
        /// User Id/UID/User, Password/Pwd, Trusted_Connection/Integrated Security.</summary>
        private void ParseConnectionStringIntoFields(string connectionString)
        {
            string server = "", db = "", user = "", pwd = "";
            bool winAuth = false;

            foreach (var raw in (connectionString ?? "").Split(';'))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                if (eq < 0) continue;
                string key = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();

                if (key.Equals("Server", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Addr", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Address", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Network Address", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Data Source", StringComparison.OrdinalIgnoreCase))
                    server = value;
                else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
                    db = value;
                else if (key.Equals("User Id", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("UID", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("User", StringComparison.OrdinalIgnoreCase))
                    user = value;
                else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Pwd", StringComparison.OrdinalIgnoreCase))
                    pwd = value;
                else if (key.Equals("Trusted_Connection", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Integrated Security", StringComparison.OrdinalIgnoreCase))
                    winAuth = value.Equals("true", StringComparison.OrdinalIgnoreCase)
                          || value.Equals("sspi", StringComparison.OrdinalIgnoreCase)
                          || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                          || value == "1";
            }

            _sqlServerBox.Text = server;
            _sqlDbBox.Text = db;
            _sqlUserBox.Text = user;
            _sqlPwdBox.Text = pwd;
            _sqlWinAuth.Checked = winAuth;
        }

        /// <summary>Assembles the connection string from the simple fields.
        /// Returns "" when it is incomplete enough that SQL collection should be skipped.</summary>
        private string BuildConnectionString()
        {
            return BuildConnectionString(null);
        }

        /// <summary>Same as BuildConnectionString(), but with the database forced to the given value
        /// (used to connect to "master" when listing the databases on the server).</summary>
        private string BuildConnectionString(string databaseOverride)
        {
            string server = (_sqlServerBox.Text ?? "").Trim();
            string db = databaseOverride ?? (_sqlDbBox.Text ?? "").Trim();
            if (server.Length == 0 || db.Length == 0)
                return "";

            if (_sqlWinAuth.Checked)
                return "Server=" + server + ";Database=" + db + ";Trusted_Connection=True;";

            var parts = new List<string>
            {
                "Server=" + server,
                "Database=" + db
            };

            string user = (_sqlUserBox.Text ?? "").Trim();
            if (user.Length > 0)
                parts.Add("User Id=" + user);

            string pwd = _sqlPwdBox.Text ?? "";
            if (user.Length > 0 && pwd.Length > 0)
                parts.Add("Password=" + pwd);

            return string.Join(";", parts.ToArray()) + ";";
        }

        /// <summary>Connection string with the password replaced by asterisks, for the on-screen preview.</summary>
        private static string MaskPassword(string connectionString)
        {
            var kept = new List<string>();
            foreach (var raw in connectionString.Split(';'))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                string key = (eq >= 0 ? part.Substring(0, eq) : part).Trim();
                if ((key.Equals("Password", StringComparison.OrdinalIgnoreCase)
                     || key.Equals("Pwd", StringComparison.OrdinalIgnoreCase)) && eq >= 0)
                    part = key + "=********";
                kept.Add(part);
            }
            return string.Join(";", kept.ToArray()) + ";";
        }

        /// <summary>Keeps the auto-built info line in sync with whatever the user is typing.</summary>
        private void RefreshConnPreview()
        {
            if (_sqlConnPreview == null)
                return;
            string conn = BuildConnectionString();
            if (conn.Length == 0)
            {
                _sqlConnPreview.ForeColor = Color.Gray;
                _sqlConnPreview.Text = (_sqlServerBox.Text ?? "").Trim().Length == 0
                    ? "(not set yet — enter the server IP; SQL is skipped when it stays empty)"
                    : "(not set yet — pick a database below, or type its name)";
            }
            else
            {
                _sqlConnPreview.ForeColor = ColorSuccess;
                _sqlConnPreview.Text = MaskPassword(conn);
            }
        }

        /// <summary>Connects (read-only) and fills the database dropdown with every online database
        /// on that server, so the user just picks one instead of typing a name.</summary>
        private void LoadDatabasesOnClick(object sender, EventArgs e)
        {
            string server = (_sqlServerBox.Text ?? "").Trim();
            if (server.Length == 0)
            {
                _sqlTestStatus.ForeColor = ColorDestructive;
                _sqlTestStatus.Text = "Enter the server IP (or host) first, then press Load databases.";
                return;
            }
            if (!_sqlWinAuth.Checked && string.IsNullOrWhiteSpace(_sqlUserBox.Text))
            {
                _sqlTestStatus.ForeColor = ColorDestructive;
                _sqlTestStatus.Text = "Enter the user name first (or tick Windows authentication).";
                return;
            }

            _sqlDbsButton.Enabled = false;
            _sqlTestStatus.ForeColor = Color.Gray;
            _sqlTestStatus.Text = "Connecting to " + server + " and listing databases…";
            Application.DoEvents();

            try
            {
                var names = new List<string>();
                using (var cn = new System.Data.SqlClient.SqlConnection(BuildConnectionString("master")))
                {
                    cn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT name FROM sys.databases WHERE state = 0 ORDER BY name", cn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            names.Add(reader.GetString(0));
                    }
                }

                string previous = (_sqlDbBox.Text ?? "").Trim();
                _sqlDbBox.BeginUpdate();
                _sqlDbBox.Items.Clear();
                foreach (var name in names)
                    _sqlDbBox.Items.Add(name);
                _sqlDbBox.EndUpdate();
                if (previous.Length > 0)
                    _sqlDbBox.Text = previous; // keep the current choice when refreshing the list

                _sqlTestStatus.ForeColor = ColorSuccess;
                _sqlTestStatus.Text = names.Count > 0
                    ? "✓ " + names.Count + " databases found — choose one from the Database list."
                    : "✓ Connected, but the account sees no databases.";
            }
            catch (Exception ex)
            {
                _sqlTestStatus.ForeColor = ColorDestructive;
                string msg = ex.Message ?? "";
                if (msg.Length > 110)
                    msg = msg.Substring(0, 107) + "…";
                _sqlTestStatus.Text = "✗ Could not list databases: " + msg;
            }
            finally
            {
                _sqlDbsButton.Enabled = true;
            }
        }

        /// <summary>Opens (and immediately closes) a connection with the current settings.
        /// Read-only: nothing is queried or changed on the database.</summary>
        private void TestConnectionOnClick(object sender, EventArgs e)
        {
            string conn = BuildConnectionString();
            if (conn.Length == 0)
            {
                _sqlTestStatus.ForeColor = ColorDestructive;
                _sqlTestStatus.Text = "Enter the Server and the Database name first.";
                return;
            }

            _sqlTestButton.Enabled = false;
            _sqlTestStatus.ForeColor = Color.Gray;
            _sqlTestStatus.Text = "Connecting…";
            Application.DoEvents();

            try
            {
                using (var cn = new System.Data.SqlClient.SqlConnection(conn))
                {
                    cn.Open();
                }
                _sqlTestStatus.ForeColor = ColorSuccess;
                _sqlTestStatus.Text = "✓ Connected — SQL queries will run with these settings.";
            }
            catch (Exception ex)
            {
                _sqlTestStatus.ForeColor = ColorDestructive;
                _sqlTestStatus.Text = "✗ " + ex.Message;
            }
            finally
            {
                _sqlTestButton.Enabled = true;
            }
        }

        private void LoadConfigIntoForm()
        {
            try
            {
                var cfg = AppConfig.Load(_configBox.Text);
                _plantBox.Text = cfg.PlantName ?? "";
                _roleBox.Text = cfg.ServerRole ?? "";
                _discoveryCheck.Checked = cfg.DiscoveryEnabled;
                _warningsCheck.Checked = cfg.IncludeWarnings;
                _cfgEnableSql.Checked = cfg.EnableSqlQueries;

                FillList(_appFoldersList, cfg.AppLogFolders);
                _channelsBox.Text = string.Join("\r\n", (cfg.EventLogChannels ?? new List<string>()).ToArray());
                _servicesBox.Text = string.Join("\r\n", (cfg.ServicesToCheck ?? new List<string>()).ToArray());

                // SQL: one shared connection (entered as server/database/user/password, never as a raw
                // connection string) plus one editor row per query.
                var conn = (cfg.SqlQueryConnections != null && cfg.SqlQueryConnections.Count > 0)
                    ? cfg.SqlQueryConnections[0] : null;
                _sqlConnName = (conn != null && !string.IsNullOrWhiteSpace(conn.Name)) ? conn.Name : "Default";
                ParseConnectionStringIntoFields(conn != null ? conn.ConnectionString : "");
                RefreshConnPreview();
                _sqlTestStatus.ForeColor = Color.Gray;
                _sqlTestStatus.Text = "";

                foreach (var old in _sqlQueryRows.ToArray())
                {
                    _sqlQueriesPanel.Controls.Remove(old.Panel);
                    old.Panel.Dispose();
                }
                _sqlQueryRows.Clear();
                _sqlRowCounter = 0;

                var queries = cfg.SqlQueries;
                if (queries != null && queries.Count > 0)
                {
                    foreach (var q in queries)
                        AddSqlQueryRow(q.Name, q.TimestampColumn, q.MessageColumn, q.LevelColumn, q.CodeColumn, q.Sql);
                }
                else
                {
                    AddSqlQueryRow("", "", "", "", "", ""); // start with one blank editor
                }

                _configStatus.ForeColor = Color.Gray;
                _configStatus.Text = "Loaded from " + _configBox.Text;
            }
            catch (Exception ex)
            {
                _configStatus.ForeColor = ColorDestructive;
                _configStatus.Text = "Could not load config: " + ex.Message;
            }
        }

        private void SaveConfigOnClick(object sender, EventArgs e)
        {
            try
            {
                // Start from the existing file so SQL queries / connections and any other
                // settings we don't edit here are preserved.
                var cfg = AppConfig.Load(_configBox.Text);
                cfg.PlantName = _plantBox.Text.Trim();
                cfg.ServerRole = _roleBox.Text.Trim();
                cfg.DiscoveryEnabled = _discoveryCheck.Checked;
                cfg.IncludeWarnings = _warningsCheck.Checked;
                cfg.AppLogFolders = ReadList(_appFoldersList);
                cfg.EventLogChannels = ReadLines(_channelsBox);
                cfg.ServicesToCheck = ReadLines(_servicesBox);

                // SQL: keep existing SqlLogFolders / KepwareLogFolders untouched (no UI input for those —
                // they are auto-discovered). The connection is assembled from the simple fields.
                string fullConn = BuildConnectionString();

                // The connection and the queries are saved INDEPENDENTLY. (They used to be saved
                // only together, which silently threw away a connection typed in without a query —
                // exactly what happens when a non-technical user sets up the database part only.)
                if (fullConn.Length > 0)
                {
                    cfg.SqlQueryConnections = new List<SqlQueryConnection>
                    {
                        new SqlQueryConnection { Name = _sqlConnName, ConnectionString = fullConn }
                    };
                }
                else
                {
                    cfg.SqlQueryConnections = new List<SqlQueryConnection>();
                }

                var queries = new List<SqlQueryDef>();
                foreach (var row in _sqlQueryRows)
                {
                    string sql = row.Sql.Text.Trim();
                    if (sql.Length == 0)
                        continue; // skip empty editor rows
                    string name = row.Name.Text.Trim();
                    if (name.Length == 0)
                        name = "Query " + (queries.Count + 1);
                    queries.Add(new SqlQueryDef
                    {
                        Connection = _sqlConnName,
                        Name = name,
                        Sql = sql,
                        TimestampColumn = row.Ts.Text.Trim(),
                        MessageColumn = row.Msg.Text.Trim(),
                        LevelColumn = row.Lvl.Text.Trim(),
                        CodeColumn = row.Code.Text.Trim()
                    });
                }

                cfg.SqlQueries = queries;
                // Follows the checkbox, but can never be "on" without a connection to use.
                cfg.EnableSqlQueries = _cfgEnableSql.Checked && fullConn.Length > 0;

                cfg.Save(_configBox.Text);

                // Say exactly what landed in the file — a non-technical user should not have to
                // open the XML to know the save worked.
                string what;
                if (fullConn.Length == 0)
                    what = "saved (no database connection)";
                else if (!cfg.EnableSqlQueries)
                    what = "saved - connection stored, SQL queries OFF";
                else
                    what = string.Format("saved - SQL ON to {0}/{1}, {2} quer{3}",
                        _sqlServerBox.Text.Trim(), _sqlDbBox.Text.Trim(),
                        queries.Count, queries.Count == 1 ? "y" : "ies");
                _configStatus.ForeColor = ColorSuccess;
                _configStatus.Text = what;
            }
            catch (Exception ex)
            {
                _configStatus.ForeColor = ColorDestructive;
                _configStatus.Text = "Save failed: " + ex.Message;
            }
        }

        private static void FillList(ListBox list, List<string> items)
        {
            list.Items.Clear();
            if (items == null)
                return;
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    list.Items.Add(item);
            }
        }

        private static List<string> ReadList(ListBox list)
        {
            var items = new List<string>();
            foreach (var item in list.Items)
            {
                var text = (item ?? "").ToString().Trim();
                if (text.Length > 0)
                    items.Add(text);
            }
            return items;
        }

        private static List<string> ReadLines(TextBox box)
        {
            var items = new List<string>();
            foreach (var raw in box.Text.Replace("\r", "").Split('\n'))
            {
                var text = raw.Trim();
                if (text.Length > 0)
                    items.Add(text);
            }
            return items;
        }

        // Application icon (window/taskbar) — embedded multi-size ICO produced from Icon.png.
        private static Icon LoadAppIcon()
        {
            try
            {
                using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("PlantDownRca.app.ico"))
                {
                    if (stream == null)
                        return null;
                    return new Icon(stream, SystemInformation.SmallIconSize);
                }
            }
            catch
            {
                return null;
            }
        }

        // In-app header logo — embedded PNG (Icon.png).
        private static Image LoadLogo()
        {
            try
            {
                using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("PlantDownRca.Resources.Logo.png"))
                {
                    if (stream == null)
                        return null;
                    return Image.FromStream(stream);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
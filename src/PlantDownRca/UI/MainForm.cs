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
        private TextBox _sqlServerBox;
        private TextBox _sqlDbBox;
        private TextBox _sqlUserBox;
        private TextBox _sqlPwdBox;
        private CheckBox _sqlShowPwd;
        private CheckBox _sqlWinAuth;
        private TextBox _sqlConnPreview;
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

            headerPanel.Controls.Add(logoBox);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subtitleLabel);

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

            LoadConfigIntoForm();
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
                MaximumSize = new Size(860, 56),
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
                Width = 620,
                Checked = true
            };
            Tip(_warningsCheck, "Off = keep only Error/Critical entries.\nOn = warnings are collected too (recommended: warnings just before the outage are usually the clue).");
            p.Controls.Add(_warningsCheck);
            y += 28;

            _appFoldersList = BuildFolderEditor(p, "Application log folders (your *.log / *.txt files)", ref y);

            _channelsBox = BuildLinesEditor(p, "Event log channels (one per line)", ref y,
                "Windows Event Log channels to read, e.g.:\n  Application\n  System\n  Microsoft-Windows-FailoverClustering/Operational\n"
                + "Run as Administrator or the read will return nothing.");
            _servicesBox = BuildLinesEditor(p, "Services to check (one per line)", ref y,
                "Windows service names whose running state is recorded in the health snapshot, e.g.:\n  MSSQLSERVER\n  SQLSERVERAGENT\n"
                + "The name is the service's internal name (services.msc → Properties), not its display name.");

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
                Text = "Run one or more read-only SELECTs during collection. Use @From and @To in each query — they are "
                     + "replaced with the incident window. You do not need to know the connection-string syntax: fill in "
                     + "the boxes below and it is written for you. Leave Server empty to skip SQL entirely.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(0, y),
                MaximumSize = new Size(860, 44),
                AutoSize = true
            });
            y += 44;

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

            // ---------- Connection: four simple fields, the string is built for you ----------
            p.Controls.Add(new Label { Text = "Server (IP or host):", Location = new Point(0, y + 3), AutoSize = true });
            _sqlServerBox = new TextBox { Location = new Point(150, y), Width = 170 };
            p.Controls.Add(_sqlServerBox);

            p.Controls.Add(new Label { Text = "Database:", Location = new Point(340, y + 3), AutoSize = true });
            _sqlDbBox = new TextBox { Location = new Point(415, y), Width = 200 };
            p.Controls.Add(_sqlDbBox);
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
                Text = "Connection string (built automatically — password is never shown here):",
                Location = new Point(0, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = ColorText
            });
            y += 22;

            _sqlConnPreview = new TextBox
            {
                Location = new Point(0, y),
                Width = 700,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9)
            };
            Tip(_sqlConnPreview, "This is exactly what gets saved to plant.config.xml.\n"
                + "Example of a filled-in connection:\n"
                + "  Server=10.0.0.5;Database=MyDatabase;User Id=report_user;Password=********;\n"
                + "or, with Windows authentication:\n"
                + "  Server=10.0.0.5;Database=MyDatabase;Trusted_Connection=True;\n"
                + "The real password is stored only in the config file on this server, never in reports.");
            p.Controls.Add(_sqlConnPreview);
            y += 30;

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
                Width = 840,
                Height = 300,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = ColorBackground
            };
            p.Controls.Add(_sqlQueriesPanel);
            y += 310;

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
                "1.  Open the Configurator tab and press \"Save configuration\" if this is the first run "
              + "(this creates plant.config.xml next to the exe).\n"
              + "2.  Add your application log folders (Configurator tab → \"Add folder…\").\n"
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
                "On the Configurator tab, in \"Database connection (optional)\", simply fill in:\n"
              + "    Server (IP or host)   e.g. 10.0.0.5   or   sql-node-01\n"
              + "    Database              e.g. MyDatabase\n"
              + "    User name             e.g. report_user\n"
              + "    Password              (hidden by default; tick \"Show\" to check it)\n"
              + "or tick \"Windows authentication\" to use the account you are logged in with.\n\n"
              + "The connection string is built and previewed for you:\n"
              + "    " + ConnExample + "\n"
              + "Press \"Test connection\" to confirm the settings before a real run. The password is saved only "
              + "inside plant.config.xml on this server and is never written into any report. Queries are read-only "
              + "SELECT statements; leave the Server empty to skip SQL entirely.", y);
            y += 6;

            y = HelpHeading(inner, "4. SQL queries", y);
            y = HelpBody(inner,
                "Each query must use @From and @To — they are replaced with the incident window. Tell the tool which "
              + "column holds the timestamp and which holds the message (the optional level/code columns improve "
              + "grouping). Queries that do not contain SELECT are not expected here; nothing is inserted, updated "
              + "or deleted. Press \"+ Add another SQL query\" for more than one.", y);
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
                MaximumSize = new Size(840, 30)
            };
            parent.Controls.Add(hint);
            y += 34;
            return list;
        }

        private TextBox BuildLinesEditor(Panel parent, string title, ref int y, string tipText = null)
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
            y += 74;
            return box;
        }

        // Adds one stacked SQL query editor (with its own Remove button) to the queries panel.
        private void AddSqlQueryRow(string name, string ts, string msg, string lvl, string code, string sql)
        {
            _sqlRowCounter++;
            var row = new SqlQueryRow
            {
                Panel = new Panel
                {
                    Width = 800,
                    Height = 250,
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

            row.Name = AddLabeledBox(row.Panel, "Query name:", 10, 120, 44, 250, name);
            row.Ts = AddLabeledBox(row.Panel, "Timestamp column:", 400, 530, 44, 160, ts);
            row.Msg = AddLabeledBox(row.Panel, "Message column:", 10, 120, 80, 250, msg);
            row.Lvl = AddLabeledBox(row.Panel, "Level column:", 400, 530, 80, 160, lvl);
            row.Code = AddLabeledBox(row.Panel, "Code column:", 10, 120, 116, 250, code);

            Tip(row.Name, "Label shown on the report for these rows, e.g. \"ProcedureErrors\".");
            Tip(row.Ts, "Name of the column that holds the event date/time — used to place the row on the timeline.\nMust match a column in your SELECT.");
            Tip(row.Msg, "Column holding the message text shown in the timeline and grouped issues.");
            Tip(row.Lvl, "Optional: severity column (maps to Error / Warning / Info). Leave empty if you have none.");
            Tip(row.Code, "Optional: error number / event id column, shown in brackets before the message.");

            row.Panel.Controls.Add(new Label
            {
                Text = "SQL (read-only SELECT):",
                Location = new Point(10, 150),
                AutoSize = true
            });
            row.Sql = new TextBox
            {
                Multiline = true,
                Location = new Point(10, 172),
                Width = 778,
                Height = 66,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                Text = sql ?? ""
            };
            Tip(row.Sql, "A SELECT statement. It MUST filter with @From and @To (DateTime), e.g.:\n"
                + "  WHERE ErrorDate >= @From AND ErrorDate <= @To\n"
                + "Both are replaced with the incident window. Only this one query runs — nothing is written to the database.");
            row.Panel.Controls.Add(row.Sql);

            _sqlQueryRows.Add(row);
            _sqlQueriesPanel.Controls.Add(row.Panel);
        }

        private static TextBox AddLabeledBox(Panel parent, string label, int labelX, int boxX, int y, int width, string value)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(labelX, y + 3), AutoSize = true });
            var box = new TextBox { Location = new Point(boxX, y), Width = width, Text = value ?? "" };
            parent.Controls.Add(box);
            return box;
        }

        // ================= CONNECTION STRING BUILDER =================
        // The user never has to write a connection string: they type the server (IP or host),
        // database, user name and password into four boxes (or tick Windows authentication),
        // and the string is assembled here, previewed live, and saved to plant.config.xml.

        /// <summary>Shown as a hint wherever an example is useful.</summary>
        private const string ConnExample =
            "Server=10.0.0.5;Database=MyDatabase;User Id=report_user;Password=********;";

        /// <summary>Reads an existing connection string back into the four simple fields.
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
            string server = (_sqlServerBox.Text ?? "").Trim();
            string db = (_sqlDbBox.Text ?? "").Trim();
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

        /// <summary>Keeps the preview box in sync with whatever the user is typing.</summary>
        private void RefreshConnPreview()
        {
            if (_sqlConnPreview == null)
                return;
            string conn = BuildConnectionString();
            _sqlConnPreview.Text = conn.Length == 0
                ? "(incomplete — enter Server and Database)  e.g. " + ConnExample
                : MaskPassword(conn);
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

                if (fullConn.Length > 0 && queries.Count > 0)
                {
                    cfg.SqlQueryConnections = new List<SqlQueryConnection>
                    {
                        new SqlQueryConnection { Name = _sqlConnName, ConnectionString = fullConn }
                    };
                    cfg.SqlQueries = queries;
                    cfg.EnableSqlQueries = _cfgEnableSql.Checked;
                }
                else
                {
                    cfg.SqlQueryConnections = new List<SqlQueryConnection>();
                    cfg.SqlQueries = new List<SqlQueryDef>();
                    cfg.EnableSqlQueries = false;
                }

                cfg.Save(_configBox.Text);

                _configStatus.ForeColor = ColorSuccess;
                _configStatus.Text = "Saved to " + _configBox.Text;
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
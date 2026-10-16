using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PlantDownRca.Config;
using PlantDownRca.Core;
using PlantDownRca.Report;
using PlantDownRca.UI;

namespace PlantDownRca
{
    internal static class Program
    {
        /// <summary>
        /// No args  -> WinForms UI.
        /// CLI      -> PlantDownRca.exe --from "2026-10-06 14:00" --to "2026-10-06 15:00" [--config plant.config.xml] [--out C:\Temp\RCA]
        /// Merge    -> PlantDownRca.exe --merge "C:\RCA\run-sql" "C:\RCA\run-app" [--out C:\RCA\merged]
        /// Built as a console exe on purpose: CLI runs keep a real console. UI runs detach from it
        /// immediately (FreeConsole), so double-clicking shows ONLY the application window.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                // UI mode: Windows gives a console-subsystem exe a console window.
                // Drop it right away so the user sees just the app — no black cmd box.
                // (CLI/merge runs have args, so they keep their console.)
                try { FreeConsole(); } catch (Exception) { /* not attached to one */ }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }

            try
            {
                if (HasFlag(args, "--merge"))
                    return RunMerge(args);
                return RunCli(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAILED: " + ex.Message);
                return 1;
            }
        }

        private static int RunCli(string[] args)
        {
            DateTime from = ParseDate(GetValue(args, "--from"), DateTime.Now.AddHours(-2));
            DateTime to = ParseDate(GetValue(args, "--to"), DateTime.Now);
            string configPath = GetValue(args, "--config") ?? AppConfig.DefaultConfigPathNextToExe();
            string outFolder = GetValue(args, "--out") ?? RunEngine.DefaultOutputFolder();

            var cfg = AppConfig.Load(configPath);
            Console.WriteLine("Plant: " + cfg.PlantName + "  Server: " + Environment.MachineName);
            Console.WriteLine("Window: " + from + " -> " + to);

            var run = RunEngine.Run(cfg, from, to, delegate(string s) { Console.WriteLine(s); });
            ReportWriters.WriteAll(run, outFolder);

            Console.WriteLine("Done. Report: " + Path.Combine(outFolder, "Report.html"));
            return 0;
        }

        private static int RunMerge(string[] args)
        {
            var folders = new List<string>();
            bool afterMergeFlag = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--merge") { afterMergeFlag = true; continue; }
                if (args[i] == "--out") { i++; continue; }
                if (afterMergeFlag && !args[i].StartsWith("--"))
                    folders.Add(args[i]);
            }
            if (folders.Count == 0)
            {
                Console.Error.WriteLine("Usage: PlantDownRca.exe --merge <runFolder1> <runFolder2> [--out <folder>]");
                return 1;
            }

            string outFolder = GetValue(args, "--out") ?? RunEngine.DefaultOutputFolder() + "-merged";
            var merged = PackageMerger.Merge(folders);
            ReportWriters.WriteAll(merged, outFolder);
            Console.WriteLine("Merged " + folders.Count + " package(s). Report: " + Path.Combine(outFolder, "Report.html"));
            return 0;
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (var a in args)
            {
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        private static DateTime ParseDate(string value, DateTime fallback)
        {
            DateTime parsed;
            if (!string.IsNullOrEmpty(value) && DateTime.TryParse(value, out parsed))
                return parsed;
            return fallback;
        }

        /// <summary>Detaches this process from its console window (UI mode only).</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
    }
}

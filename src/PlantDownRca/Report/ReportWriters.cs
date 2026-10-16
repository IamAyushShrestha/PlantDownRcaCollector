using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using PlantDownRca.Core;
using PlantDownRca.Models;

namespace PlantDownRca.Report
{
    public static class ReportWriters
    {
        public static void WriteAll(RunResult run, string outFolder)
        {
            Directory.CreateDirectory(outFolder);
            WriteHtml(run, Path.Combine(outFolder, "Report.html"));
            WriteCsv(run, Path.Combine(outFolder, "timeline.csv"));
            WriteSummary(run, Path.Combine(outFolder, "summary.txt"));
            WritePackage(run, Path.Combine(outFolder, "entries.xml"));
        }

        // ---------- HTML (single self-contained file: attach to ticket, open anywhere) ----------
        // Design: plain log-document. Monospace data, thin rules, no decorative graphics.
        // Only color: red for error-level text. Search inputs filter rows client-side.

        public static void WriteHtml(RunResult run, string path)
        {
            var timeline = TimelineBuilder.BuildMergedTimeline(run.Entries);
            var untimed = TimelineBuilder.Untimed(run.Entries);
            var groups = TimelineBuilder.Group(run.Entries);

            var servers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in run.Entries)
                servers.Add(e.ServerName);

            var errorCount = 0;
            var criticalCount = 0;
            var warningCount = 0;
            foreach (var g in groups)
            {
                if (g.Level >= EntryLevel.Critical)
                    criticalCount += g.Count;
                else if (g.Level >= EntryLevel.Error)
                    errorCount += g.Count;
                else
                    warningCount += g.Count;
            }

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>");
            sb.Append("<title>Plant Down RCA Report</title>");
            sb.Append("<style>");

            // Plain log-document styling: monospace data, thin rules, no decorative graphics.
            sb.Append(":root{--bg:#ffffff;--fg:#111111;--muted:#555555;--line:#d9d9d9;--hover:#f4f4f4;--err:#b71c1c;}");
            sb.Append("@media(prefers-color-scheme:dark){:root{--bg:#101010;--fg:#e8e8e8;--muted:#9b9b9b;--line:#333333;--hover:#1c1c1c;--err:#ff8a80;}}");
            sb.Append("*{box-sizing:border-box;}");
            sb.Append("body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.55 system-ui,-apple-system,'Segoe UI',sans-serif;}");
            // Wide enough that long log messages stay on one line on a desktop, still capped so
            // prose lines don't get absurdly long on ultrawide monitors.
            sb.Append(".wrap{max-width:1800px;margin:0 auto;padding:1.5rem 1.5rem 4rem;}");
            sb.Append("h1{font-size:1.5rem;margin:0 0 .4rem;}");
            sb.Append("h2{font-size:1.05rem;margin:2.2rem 0 .7rem;border-bottom:1px solid var(--line);padding-bottom:.3rem;}");
            sb.Append("h3{font-size:.95rem;margin:1.1rem 0 .4rem;}");
            sb.Append(".meta{color:var(--muted);font-size:.92rem;margin:.15rem 0;}");
            sb.Append(".meta b{color:var(--fg);font-weight:600;}");
            sb.Append(".summary{font-family:ui-monospace,'Cascadia Mono',Consolas,monospace;font-size:.9rem;margin:.9rem 0 .3rem;}");
            sb.Append(".note{color:var(--muted);font-size:.88rem;margin:.6rem 0;}");
            // Tables: short fields stay on one line; only message/text columns wrap. The scroll
            // wrapper keeps narrow screens usable instead of squeezing every column into two lines.
            sb.Append(".tw{overflow-x:auto;-webkit-overflow-scrolling:touch;}");
            sb.Append("table{width:100%;border-collapse:collapse;margin:.5rem 0 1rem;font-size:.85rem;}");
            sb.Append("th{text-align:left;font-weight:600;white-space:nowrap;padding:.45rem .55rem;border-bottom:1px solid var(--line);}");
            sb.Append("td{padding:.4rem .55rem;border-bottom:1px solid var(--line);vertical-align:top;}");
            sb.Append("td.n{white-space:nowrap;}");
            sb.Append("td.w{overflow-wrap:anywhere;}");
            sb.Append("tbody tr:hover{background:var(--hover);}");
            sb.Append(".mono{font-family:ui-monospace,'Cascadia Mono',Consolas,monospace;}");
            sb.Append(".err{color:var(--err);font-weight:700;}");
            sb.Append(".muted{color:var(--muted);}");
            sb.Append(".filter{margin:.3rem 0 .8rem;}");
            sb.Append(".filter input{font:inherit;font-size:.9rem;padding:.4rem .6rem;width:100%;max-width:440px;color:var(--fg);background:var(--bg);border:1px solid var(--line);border-radius:3px;}");
            sb.Append(".filter input::placeholder{color:var(--muted);opacity:.9;}");
            sb.Append("pre{border:1px solid var(--line);padding:.6rem .8rem;overflow-x:auto;font-size:.85rem;margin:.4rem 0 1rem;}");
            sb.Append("details{margin:.35rem 0 0;}summary{cursor:pointer;font-size:.82rem;color:var(--muted);}details[open] summary{margin-bottom:.35rem;}");
            sb.Append(".footer{border-top:1px solid var(--line);margin-top:2.5rem;padding-top:.8rem;font-size:.85rem;color:var(--muted);}");
            sb.Append("@media print{.filter{display:none;}}");
            sb.Append("</style>");
            sb.Append("<script>");
            sb.Append("function filterTable(tableId,searchId){");
            sb.Append("var input=document.getElementById(searchId);");
            sb.Append("var filter=input.value.toUpperCase();");
            sb.Append("var table=document.getElementById(tableId);");
            sb.Append("var rows=table.getElementsByTagName('tr');");
            sb.Append("for(var i=1;i<rows.length;i++){");
            sb.Append("var text=rows[i].textContent||rows[i].innerText;");
            sb.Append("rows[i].style.display=text.toUpperCase().indexOf(filter)>-1?'':'none';}");
            sb.Append("}");
            sb.Append("</script>");
            sb.Append("</head><body><div class='wrap'>");

            // Header: plain document meta, no graphics
            sb.Append("<h1>Plant Down RCA Report</h1>");
            sb.Append("<p class='meta'><b>Plant:</b> ").Append(E(run.PlantName)).Append(" | <b>Servers:</b> ").Append(E(run.ServerName)).Append("</p>");
            sb.Append("<p class='meta'><b>Window:</b> ").Append(F(run.WindowFrom)).Append(" \u2192 ").Append(F(run.WindowTo)).Append(" | <b>Generated:</b> ").Append(F(run.RanAt)).Append("</p>");
            sb.Append("<div class='summary'>Critical: ").Append(criticalCount)
              .Append("   Errors: ").Append(errorCount)
              .Append("   Warnings: ").Append(warningCount)
              .Append("   Total entries: ").Append(timeline.Count + untimed.Count).Append("</div>");

            if (servers.Count > 1)
                sb.Append("<p class='note'>Multi-server report: cross-server ordering depends on clock sync (NTP). If events appear out of sequence, verify clock skew.</p>");

            // Collectors
            sb.Append("<h2>Collection Summary</h2>");
            sb.Append("<div class='tw'><table><thead><tr><th>Collector</th><th>Status</th><th>Files</th><th>Lines</th><th>Matched</th><th>Notes</th></tr></thead><tbody>");
            foreach (var c in run.Collectors)
            {
                sb.Append("<tr>");
                sb.Append("<td class='n'><b>").Append(E(c.Name)).Append("</b></td>");
                sb.Append("<td class='n'>").Append(c.Success ? "OK" : "<span class='err'>FAILED</span>").Append("</td>");
                sb.Append("<td class='n'>").Append(c.FilesRead).Append("</td>");
                sb.Append("<td class='n'>").Append(c.LinesScanned.ToString("N0")).Append("</td>");
                sb.Append("<td class='n'><b>").Append(c.EntriesMatched).Append("</b></td>");
                sb.Append("<td class='w muted'>").Append(E(string.Join(" | ", c.Notes.ToArray()))).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");

            // Health snapshot
            if (run.Snapshot != null)
            {
                sb.Append("<h2>System Health</h2>");
                sb.Append("<p class='note'>Snapshot at ").Append(F(run.Snapshot.CollectedAt)).Append(" | OS: ").Append(E(run.Snapshot.OsVersion)).Append("</p>");
                sb.Append("<h3>Storage</h3>");
                sb.Append("<pre>").Append(E(string.Join("\n", run.Snapshot.DriveLines.ToArray()))).Append("</pre>");
                sb.Append("<h3>Services</h3>");
                sb.Append("<pre>").Append(E(string.Join("\n", run.Snapshot.ServiceLines.ToArray()))).Append("</pre>");
                if (run.Snapshot.Notes.Count > 0)
                    sb.Append("<p class='note'>").Append(E(string.Join(" | ", run.Snapshot.Notes.ToArray()))).Append("</p>");
            }

            // Grouped issues
            sb.Append("<h2>Grouped Issues</h2>");
            if (groups.Count == 0)
            {
                sb.Append("<p class='note'>No errors or warnings matched in this window. Check collector notes above.</p>");
            }
            else
            {
                sb.Append("<div class='filter'><input type='text' id='groupSearch' placeholder='Filter by source, level, or message...' onkeyup='filterTable(\"groupTable\",\"groupSearch\")'></div>");
                sb.Append("<div class='tw'><table class='mono' id='groupTable'><thead><tr><th>Count</th><th>First seen</th><th>Last seen</th><th>Level</th><th>Source</th><th>Tags</th><th>Sample</th></tr></thead><tbody>");
                foreach (var g in groups)
                {
                    string lvl = E(g.Level.ToString());
                    sb.Append("<tr>");
                    sb.Append("<td class='n'>").Append(g.Count).Append("x</td>");
                    sb.Append("<td class='n'>").Append(g.FirstSeen == DateTime.MinValue ? "\u2014" : F(g.FirstSeen)).Append("</td>");
                    sb.Append("<td class='n'>").Append(g.LastSeen == DateTime.MinValue ? "\u2014" : F(g.LastSeen)).Append("</td>");
                    sb.Append("<td class='n'>").Append(g.Level >= EntryLevel.Error ? "<span class='err'>" + lvl + "</span>" : lvl).Append("</td>");
                    sb.Append("<td class='n'><b>").Append(E(g.Source)).Append("</b></td>");
                    sb.Append("<td class='w'>").Append(E(string.Join(", ", g.Tags.ToArray()))).Append("</td>");
                    sb.Append("<td class='w'>").Append(E(Truncate(g.SampleMessage, 200))).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
            }

            // Timeline
            sb.Append("<h2>Timeline</h2>");
            sb.Append("<div class='filter'><input type='text' id='timelineSearch' placeholder='Filter by time, server, source, level, or message...' onkeyup='filterTable(\"timelineTable\",\"timelineSearch\")'></div>");
            sb.Append("<div class='tw'><table class='mono' id='timelineTable'><thead><tr><th>Time</th><th>Server</th><th>Source</th><th>Level</th><th>Message</th></tr></thead><tbody>");
            bool firstErrorMarked = false;
            foreach (var e in timeline)
            {
                bool isFirstError = !firstErrorMarked && e.Level >= EntryLevel.Error;
                if (isFirstError) firstErrorMarked = true;

                string lvl = E(e.Level.ToString());
                sb.Append("<tr>");
                sb.Append("<td class='n'>").Append(e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append("</td>");
                sb.Append("<td class='n'>").Append(E(e.ServerName)).Append("</td>");
                sb.Append("<td class='n'>").Append(E(e.Source)).Append("</td>");
                sb.Append("<td class='n'>").Append(e.Level >= EntryLevel.Error ? "<span class='err'>" + lvl + "</span>" : lvl).Append("</td>");

                var msg = !string.IsNullOrEmpty(e.Code) ? "[" + E(e.Code) + "] " + E(Truncate(e.Message, 300)) : E(Truncate(e.Message, 300));
                if (isFirstError) msg += " <span class='err'>\u25C4 FIRST ERROR</span>";

                // Multi-line entries (stack traces, wrapped messages, merged SQL explanations) keep
                // their full text behind a disclosure so the evidence stays readable in the browser.
                if (!string.IsNullOrEmpty(e.RawText) && e.RawText.IndexOf('\n') >= 0)
                {
                    int lineCount = 1;
                    foreach (char c in e.RawText)
                        if (c == '\n') lineCount++;
                    msg += "<details><summary>full entry (" + lineCount + " lines)</summary>"
                         + "<pre>" + E(e.RawText) + "</pre></details>";
                }

                sb.Append("<td class='w'>").Append(msg).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");

            if (!firstErrorMarked)
                sb.Append("<p class='note'><b>No errors found.</b> Timeline shows warnings only, or adjust the time window/collectors.</p>");

            // Untimed
            if (untimed.Count > 0)
            {
                sb.Append("<h2>Untimed Entries</h2>");
                sb.Append("<p class='note'>").Append(untimed.Count).Append(" entries could not be timestamped. These are kept and never dropped.</p>");
                sb.Append("<div class='tw'><table class='mono'><thead><tr><th>Server</th><th>Source</th><th>Level</th><th>Message</th><th>File</th></tr></thead><tbody>");
                foreach (var e in untimed)
                {
                    string lvl = E(e.Level.ToString());
                    sb.Append("<tr>");
                    sb.Append("<td class='n'>").Append(E(e.ServerName)).Append("</td>");
                    sb.Append("<td class='n'>").Append(E(e.Source)).Append("</td>");
                    sb.Append("<td class='n'>").Append(e.Level >= EntryLevel.Error ? "<span class='err'>" + lvl + "</span>" : lvl).Append("</td>");
                    sb.Append("<td class='w'>").Append(E(Truncate(e.Message, 250))).Append("</td>");
                    sb.Append("<td class='w muted'>").Append(E(e.OriginFile)).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
            }

            // Footer
            sb.Append("<div class='footer'>Generated by Plant Down RCA | files: Report.html, timeline.csv, summary.txt, entries.xml</div>");

            sb.Append("</div></body></html>");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // ---------- CSV ----------

        public static void WriteCsv(RunResult run, string path)
        {
            var timeline = TimelineBuilder.BuildMergedTimeline(run.Entries);
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Server,Source,Level,Code,Message,OriginFile");
            foreach (var e in timeline)
            {
                sb.Append(Csv(e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(",");
                sb.Append(Csv(e.ServerName)).Append(",");
                sb.Append(Csv(e.Source)).Append(",");
                sb.Append(Csv(e.Level.ToString())).Append(",");
                sb.Append(Csv(e.Code)).Append(",");
                sb.Append(Csv(e.Message)).Append(",");
                sb.Append(Csv(e.OriginFile)).AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // ---------- Plain-text summary (paste into a ticket) ----------

        public static void WriteSummary(RunResult run, string path)
        {
            var groups = TimelineBuilder.Group(run.Entries);
            var timeline = TimelineBuilder.BuildMergedTimeline(run.Entries);
            var sb = new StringBuilder();
            sb.AppendLine("PLANT DOWN RCA SUMMARY");
            sb.AppendLine("Plant: " + run.PlantName);
            sb.AppendLine("Server(s): " + run.ServerName);
            sb.AppendLine("Window: " + F(run.WindowFrom) + " -> " + F(run.WindowTo));
            sb.AppendLine("Entries: " + run.Entries.Count);
            sb.AppendLine();
            sb.AppendLine("TOP GROUPED ERRORS:");
            foreach (var g in groups)
            {
                sb.AppendLine(string.Format("  [{0}x] {1} .. {2}  {3}  {4}  {5}",
                    g.Count, F(g.FirstSeen), F(g.LastSeen), g.Level, g.Source, Truncate(g.SampleMessage, 160)));
                if (g.Tags.Count > 0)
                    sb.AppendLine("        Tags: " + string.Join(", ", g.Tags.ToArray()));
            }
            sb.AppendLine();
            sb.AppendLine("EARLIEST ERROR:");
            foreach (var e in timeline)
            {
                if (e.Level >= EntryLevel.Error)
                {
                    sb.AppendLine("  " + F(e.Timestamp) + "  " + e.ServerName + "  " + e.Source + "  " + Truncate(e.Message, 300));
                    break;
                }
            }
            sb.AppendLine();
            sb.AppendLine("COLLECTORS:");
            foreach (var c in run.Collectors)
                sb.AppendLine("  " + c.Name + ": " + (c.Success ? "OK" : "FAILED") + ", matched " + c.EntriesMatched +
                              (c.Notes.Count > 0 ? " | " + string.Join(" ", c.Notes.ToArray()) : ""));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // ---------- Machine-readable package for merge mode ----------

        public static void WritePackage(RunResult run, string path)
        {
            var pkg = new RunPackage
            {
                PlantName = run.PlantName,
                ServerName = run.ServerName,
                ServerRole = run.ServerRole,
                WindowFrom = run.WindowFrom,
                WindowTo = run.WindowTo,
                Entries = run.Entries,
                Collectors = run.Collectors,
                Snapshot = run.Snapshot
            };
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(RunPackage));
            using (var writer = new StreamWriter(path))
            {
                serializer.Serialize(writer, pkg);
            }
        }

        private static string E(string text)
        {
            return WebUtility.HtmlEncode(text ?? "");
        }

        private static string F(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        private static string Csv(string field)
        {
            field = field ?? "";
            if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }
}

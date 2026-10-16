# Tests

End-to-end sample test for the collector. It uses **synthetic logs only** — no real plant
data, no database, no network access. Everything it writes stays inside `tests\_work\`
(git-ignored).

## Run it

```powershell
powershell -ExecutionPolicy Bypass -File tests\run-samples.ps1
```

The script:

1. Builds `PlantDownRca.exe` (Release) if it is not already built.
2. Copies `tests\SampleLogs\` into `tests\_work\SampleLogs\` and rewrites the sample dates
   to **today**, so the incident window always covers them.
3. Generates `tests\_work\plant.config.xml` pointing only at those copies
   (`DiscoveryEnabled=false` so nothing outside the samples is read).
4. Runs the collector in CLI mode over the whole day.
5. Asserts 19 checks against the produced report and exits `0` on success, `1` on failure —
   so it can run in a pipeline.

## What the checks cover

| Area | Check |
|---|---|
| Output | `Report.html`, `timeline.csv`, `summary.txt`, `entries.xml` are all written |
| Dedupe | six `Timeout after … for device …` lines collapse into one `6x` group |
| Pattern tags | `DISK_FULL`, `KEPWARE_PLC_COMMS_LOSS`, `SERVICE_CRASHED`, `SQL_DEADLOCK`, `SQL_FAILOVER_OR_AG_MOVE` |
| SQL ERRORLOG | `Error: 9002` + its explanation line 20 ms later merge into one entry |
| Multi-line | the `SocketException` stack trace stays attached to its entry **and** is shown in `Report.html` |
| Never dropped | untimed lines land in the "Untimed Entries" section |
| Coverage | entries from application logs, SQL logs and Kepware logs are all present |

## Sample data layout

```
tests/SampleLogs/
  AppLogs/AppServer.log      ISO + log4net style, repeated timeouts, stack trace, disk full
  AppLogs/AppWorker.txt      US time format, untimed header line, service crash
  SqlLogs/ERRORLOG           SQL Server ERRORLOG: 9002, 823/I/O, 18456, AG move, deadlock
  SqlLogs/SQLAGENT.OUT       SQL Agent job failure
  KepwareLogs/KEPServerEX.log  controller comms loss, bad tag quality (06-Oct event format)
```

The dates in those files are fixed (`2026-10-15`) **on purpose**: the runner rewrites them
to today, so the samples never go stale.

## Adding a new sample

1. Drop another `.log` / `.txt` file into the relevant folder under `tests\SampleLogs\`.
2. Use one of the supported timestamp formats at the start of each line
   (`2026-10-15 07:00:00,123`, `10/15/2026 7:00:00 AM`, `15-Oct-2026 07:00:00`).
3. Date strings `2026-10-15`, `10/15/2026` and `15-Oct-2026` are the ones rewritten by the
   runner — keep using them so the file stays time-independent.
4. Add an assertion in `run-samples.ps1` if the sample is meant to prove something new.

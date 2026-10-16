# sql-guard-test.ps1 - verifies the enforced read-only SELECT guard in SqlQueryCollector.
# The collector must accept a single read-only SELECT (or WITH ... SELECT) and refuse
# anything that could write to the database.
# Run:  powershell -ExecutionPolicy Bypass -File tests\sql-guard-test.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\PlantDownRca\bin\Release\PlantDownRca.exe'
if (-not (Test-Path $exe)) { throw "PlantDownRca.exe not found at $exe - build Release first." }

$asm = [System.Reflection.Assembly]::LoadFrom($exe)
$t = $asm.GetType('PlantDownRca.Collectors.SqlQueryCollector')
$m = $t.GetMethod('IsReadOnlySelect',
    [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Public)
if (-not $m) { throw "IsReadOnlySelect not found - was the guard removed?" }

$pass = 0; $fail = 0
function Expect($sql, $shouldAllow, $label) {
    $callArgs = @($sql, $null)
    $ok = $m.Invoke($null, $callArgs)
    if ($ok -eq $shouldAllow) { $script:pass++; Write-Host "  PASS  [$label]" }
    else { $script:fail++; Write-Host "  FAIL  [$label] expected allowed=$shouldAllow got $ok  reason=$($callArgs[1])" }
}

Write-Host "--- should ALLOW (read-only SELECT) ---"
Expect "SELECT LogTime, Message FROM dbo.Errors WHERE LogTime BETWEEN @From AND @To" $true "plain select"
Expect "select * from t where ts >= @From and ts <= @To" $true "lowercase"
Expect "  `r`n  SELECT 1" $true "leading whitespace"
Expect "-- a comment`r`nSELECT 1" $true "leading line comment"
Expect "/* block */ SELECT 1" $true "leading block comment"
Expect "WITH x AS (SELECT 1 AS a) SELECT a FROM x" $true "CTE"
Expect "SELECT * FROM t WHERE msg = 'DELETE failed'" $true "literal contains DELETE"
Expect "SELECT * FROM t -- do not DROP this" $true "comment contains DROP"
Expect "SELECT CreatedDate, UpdatedTime FROM t" $true "column names contain CREATE/UPDATE substrings"
Expect "SELECT TOP 1000 LogTime, Message FROM dbo.Errors WHERE LogTime BETWEEN @From AND @To ORDER BY LogTime" $true "example query"
Expect "SELECT 1;" $true "trailing semicolon only"

Write-Host "--- should REJECT (writes / chaining / procs) ---"
Expect "DELETE FROM t" $false "delete"
Expect "UPDATE t SET a = 1" $false "update"
Expect "INSERT INTO t VALUES (1)" $false "insert"
Expect "DROP TABLE t" $false "drop"
Expect "TRUNCATE TABLE t" $false "truncate"
Expect "ALTER TABLE t ADD c int" $false "alter"
Expect "CREATE TABLE t (a int)" $false "create"
Expect "EXEC sp_who" $false "exec"
Expect "EXECUTE dbo.Proc" $false "execute"
Expect "MERGE t AS x USING y ON 1=1" $false "merge"
Expect "GRANT SELECT ON t TO u" $false "grant"
Expect "BACKUP DATABASE x TO DISK='c:\a.bak'" $false "backup"
Expect "SELECT * INTO newtable FROM t" $false "select into"
Expect "SELECT 1; DROP TABLE t" $false "chained statement"
Expect "SELECT 1; DELETE FROM t" $false "chained delete"
Expect "EXEC xp_cmdshell 'dir'" $false "xp_cmdshell"
Expect "SELECT * FROM t; EXEC sp_x" $false "chained exec"
Expect "" $false "empty"
Expect "   " $false "whitespace only"

Write-Host ""
if ($fail -gt 0) { Write-Host "FAILED: $fail of $($pass + $fail)"; exit 1 }
Write-Host "ALL $pass CHECKS PASSED."
exit 0

# Restart the client into the test server and wait for THIS launch's probe ready line.
# The BepInEx log is appended across runs, so readiness is judged from the byte offset recorded
# before the launch — an old "gc ready" must not answer for a new one.
$ErrorActionPreference = "Continue"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$log = Join-Path $g "BepInEx\LogOutput.log"

$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) {
    Write-Output ("stopping valheim pid " + ($p.Id -join ","))
    $p | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 10
    $p = Get-Process -Name valheim -ErrorAction SilentlyContinue
    if ($p) { $p | Stop-Process -Force; Start-Sleep -Seconds 6; Write-Output "force killed" }
}

$offset = 0
if (Test-Path $log) { $offset = (Get-Item $log).Length }
Write-Output ("log offset " + $offset)

Start-ScheduledTask -TaskName "HusnisseLegsLaunch"
Write-Output "launched HusnisseLegsLaunch"

$ready = $null
for ($i = 0; $i -lt 150; $i++) {
    Start-Sleep -Seconds 4
    if (-not (Test-Path $log)) { continue }
    $fs = [System.IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
    try {
        $len = $fs.Length
        if ($len -le $offset) { continue }
        $fs.Position = $offset
        $buf = New-Object byte[] ($len - $offset)
        $read = $fs.Read($buf, 0, $buf.Length)
        $text = [System.Text.Encoding]::UTF8.GetString($buf, 0, $read)
    } finally { $fs.Close() }
    $m = [regex]::Matches($text, "gc ready [^\r\n]*")
    if ($m.Count -gt 0) { $ready = $m[$m.Count - 1].Value; Write-Output ("READY after " + (($i + 1) * 4) + "s: " + $ready); break }
}
if (-not $ready) { Write-Output "TIMEOUT waiting for gc ready" }
Write-Output "== tail =="
Get-Content $log -Tail 6

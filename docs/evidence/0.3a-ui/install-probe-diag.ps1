# Install the rebuilt gate probe (diagnostics: player hp/dead, window visibility, container
# in-use flag and distance), then relaunch the client and wait for this launch's ready line.
$ErrorActionPreference = "Continue"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$log = Join-Path $g "BepInEx\LogOutput.log"
$probe = Join-Path $g "BepInEx\plugins\BootstrapBoys-GateClientProbe\HusnisseGateClientProbe.dll"

$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) {
    Write-Output ("stopping valheim pid " + ($p.Id -join ","))
    $p | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 10
    $p = Get-Process -Name valheim -ErrorAction SilentlyContinue
    if ($p) { $p | Stop-Process -Force; Start-Sleep -Seconds 6; Write-Output "force killed" }
}

Copy-Item -LiteralPath "C:\Users\Hermes\stage\HusnisseGateClientProbe-diag.dll" -Destination $probe -Force
Write-Output ("probe " + (Get-FileHash -Algorithm SHA256 -LiteralPath $probe).Hash)

$offset = 0
if (Test-Path $log) { $offset = (Get-Item $log).Length }
Start-ScheduledTask -TaskName "HusnisseLegsLaunch"
Write-Output ("launched, log offset " + $offset)

$ready = $null
for ($i = 0; $i -lt 120; $i++) {
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
    if ($m.Count -gt 0) { $ready = $m[$m.Count - 1].Value; Write-Output ("READY " + $ready); break }
}
if (-not $ready) { Write-Output "TIMEOUT" }

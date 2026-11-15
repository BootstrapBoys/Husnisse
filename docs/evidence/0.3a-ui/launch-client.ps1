# Launch the client into the test server with the same character (nissetester) and wait for the
# gate probe to report ready. -joinserverwithcharacter means no menu clicks are involved.
$ErrorActionPreference = "Continue"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$log = Join-Path $g "BepInEx\LogOutput.log"

$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) {
    Write-Output ("valheim already running pid=" + ($p.Id -join ",") + " - not launching")
} else {
    Write-Output ("log before launch: mtime=" + (Get-Item $log).LastWriteTime.ToString("HH:mm:ss") + " size=" + (Get-Item $log).Length)
    Start-ScheduledTask -TaskName "HusnisseLegsLaunch"
    Write-Output "launched HusnisseLegsLaunch"
}

for ($i = 0; $i -lt 150; $i++) {
    Start-Sleep -Seconds 4
    if (-not (Test-Path $log)) { continue }
    $ready = Select-String -Path $log -Pattern "gc ready " -ErrorAction SilentlyContinue | Select-Object -Last 1
    if ($ready) {
        # only trust a ready line written by this launch (log is appended across runs)
        if ($ready.Line -match "gc ready") {
            Write-Output ("READY after " + (($i + 1) * 4) + "s: " + $ready.Line.Trim())
            break
        }
    }
    if ($i -eq 149) { Write-Output "TIMEOUT waiting for gc ready" }
}
Write-Output "== probe tail =="
Select-String -Path $log -Pattern "Gate Client Probe" -ErrorAction SilentlyContinue |
    Select-Object -Last 6 | ForEach-Object { Write-Output $_.Line }

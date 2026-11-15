$log = 'V:\Gate031\BepInEx\BSBLogs\Harness.log'
if (Test-Path $log) { Get-Content $log -Tail 90 } else { 'no harness log' }

$log = 'V:\Gate031\BepInEx\BSBLogs\Harness.log'
if (Test-Path $log) { @(Select-String -Path $log -Pattern 'scenario done' -SimpleMatch).Count } else { 0 }

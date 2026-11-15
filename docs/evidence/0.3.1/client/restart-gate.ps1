$dst = 'V:\Gate031'
$flag = Join-Path $dst 'BepInEx\config\softstop.flag'
if (Test-Path $flag) { Remove-Item $flag -Force }

$procs = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($dst) }
foreach ($p in $procs) {
  Write-Output ('stopping pid ' + $p.ProcessId)
  Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 4

$src = 'C:\Users\WinGSM\stage\TestHarness.dll'
$to = Join-Path $dst 'BepInEx\plugins\BootstrapBoys-TestHarness\TestHarness.dll'
Copy-Item -LiteralPath $src -Destination $to -Force
Write-Output ('deployed TestHarness.dll ' + (Get-FileHash $to -Algorithm SHA256).Hash)

foreach ($f in @((Join-Path $dst 'BepInEx\LogOutput.log'), (Join-Path $dst 'logs\server.log'), (Join-Path $dst 'BepInEx\BSBLogs\Harness.log'), (Join-Path $dst 'BepInEx\BSBLogs\Husnisse.log'))) {
  if (Test-Path $f) { Remove-Item -LiteralPath $f -Force -ErrorAction SilentlyContinue }
}

$cmd = 'cmd.exe /c "' + (Join-Path $dst 'start-test.cmd') + '"'
$r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $cmd; CurrentDirectory = $dst }
Write-Output ('start request rc=' + $r.ReturnValue + ' pid=' + $r.ProcessId)

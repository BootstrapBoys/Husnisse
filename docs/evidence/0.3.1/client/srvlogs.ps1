Get-ChildItem 'V:\ValheimTest2' -Filter *.log -Recurse -Depth 2 -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTime -Descending | Select-Object -First 8 FullName, Length, LastWriteTime | Format-Table -AutoSize | Out-String
$ds = 'V:\ValheimTest2\DedicatedServer.log'
if (Test-Path $ds) { Write-Output '--- DedicatedServer.log tail ---'; Get-Content $ds -Tail 25 }
$world = 'V:\ValheimTest2\saves\worlds_local'
Get-ChildItem $world -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String

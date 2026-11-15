$p = Get-Process valheim -ErrorAction SilentlyContinue
if ($p) { Write-Output ('REFUSING: a client is already running (pid ' + $p.Id + ')'); exit 0 }
Start-ScheduledTask -TaskName 'HusnisseLegsLaunch'
Write-Output 'launch requested'

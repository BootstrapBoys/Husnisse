$t = Get-ScheduledTask -TaskName 'HusnisseLegsLaunch'
$a = $t.Actions[0]
$a.Arguments = [regex]::Replace($a.Arguments, '192\.168\.1\.136:\d+', '192.168.1.136:2556')
Set-ScheduledTask -TaskName 'HusnisseLegsLaunch' -Action $a | Out-Null
Write-Output ('task args: ' + (Get-ScheduledTask -TaskName 'HusnisseLegsLaunch').Actions[0].Arguments)

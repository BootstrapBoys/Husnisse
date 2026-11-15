# Launch the joining client from the interactive console session.
# SSH runs in the service session, which has no desktop; the scheduled task runs in session 1.
Start-ScheduledTask -TaskName "HusnisseLegsLaunch"
Start-Sleep -Seconds 3
Get-CimInstance Win32_Process -Filter "name like '%valheim%'" |
    Select-Object ProcessId, Name, SessionId | Format-Table -AutoSize

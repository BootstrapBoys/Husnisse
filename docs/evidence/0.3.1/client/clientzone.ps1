$log = Join-Path $env:USERPROFILE 'AppData\LocalLow\IronGate\Valheim\Player.log'
Write-Output '--- client lines 18:17-18:19 ---'
Select-String -LiteralPath $log -Pattern '18:1[89]:' | Select-Object -Last 40 | ForEach-Object { Write-Output $_.Line }
Write-Output '--- client destroy/zone/cull lines ---'
Select-String -LiteralPath $log -Pattern 'Destroy|destroy|ZNetScene|zone|Zone|out of range|Unload' | Select-Object -Last 20 | ForEach-Object { Write-Output $_.Line }

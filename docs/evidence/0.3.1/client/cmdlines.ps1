Get-CimInstance Win32_Process -Filter "Name like '%valheim%'" | ForEach-Object {
  Write-Output ('pid ' + $_.ProcessId + ' :: ' + $_.CommandLine)
}

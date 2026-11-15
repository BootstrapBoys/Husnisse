$ErrorActionPreference = "Continue"
Write-Output "== scheduled tasks matching husnisse/valheim =="
Get-ScheduledTask | Where-Object { $_.TaskName -match 'usnisse|alheim|egs' } | ForEach-Object {
    $act = ($_.Actions | ForEach-Object { $_.Execute + ' ' + $_.Arguments }) -join ' ;; '
    Write-Output ($_.TaskName + " | " + $_.State + " | " + $act)
}
Write-Output "== steam =="
$s = Get-Process -Name steam -ErrorAction SilentlyContinue
if ($s) { Write-Output ("steam pid=" + ($s.Id -join ",")) } else { Write-Output "steam NOT running" }
Write-Output "== steam webhelper/exe path =="
Get-Process -Name steam -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Path

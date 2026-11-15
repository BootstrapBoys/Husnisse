$p = 'V:\Gate031\saves\worlds_local\MTTest'
if (Test-Path -LiteralPath $p) {
  $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $name = 'MTTest.pre-031client-' + $stamp
  Rename-Item -LiteralPath $p -NewName $name
  Write-Output ('renamed ' + $p + ' -> ' + $name)
} else {
  Write-Output 'no world to rename'
}
Write-Output ('fresh world present: ' + (-not (Test-Path -LiteralPath $p)))

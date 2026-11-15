$log = 'V:\ValheimTest2\BepInEx\LogOutput.log'
Write-Output ('log exists: ' + (Test-Path $log) + ' size ' + (Get-Item $log).Length)
foreach ($pat in @('Husnisse', 'Jotunn', 'TestHarness', 'plugins to load', 'Game server connected', 'Exception')) {
  $hits = Select-String -Path $log -Pattern $pat -SimpleMatch | Select-Object -First 6
  Write-Output ('--- ' + $pat + ' (' + ($hits | Measure-Object).Count + ')')
  $hits | ForEach-Object { Write-Output $_.Line }
}

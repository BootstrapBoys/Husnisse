# Restore the rig: the 12 parked plugin folders back, the gate config values back to the tree's.
$plugins = "V:\ValheimTest\BepInEx\plugins"
$disabled = "V:\ValheimTest\BepInEx\plugins.disabled"

foreach ($dir in Get-ChildItem $disabled -Directory) {
    $target = Join-Path $plugins $dir.Name
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    Move-Item -LiteralPath $dir.FullName -Destination $target
    Write-Host ("  restored " + $dir.Name)
}

$cfg = "V:\ValheimTest\BepInEx\config\com.bootstrapboys.husnisse.cfg"
$text = [System.IO.File]::ReadAllText($cfg)
foreach ($pair in @(@("Feeding Required", "true"), @("Bowl Drain Minutes", "20"), @("Debug Logging", "true"), @("Allow Orders", "true"))) {
    $key = [regex]::Escape($pair[0])
    $text = [regex]::Replace($text, "(?m)^$key\s*=.*$", ($pair[0] + " = " + $pair[1]))
}
[System.IO.File]::WriteAllText($cfg, $text, (New-Object System.Text.UTF8Encoding $false))

if (Test-Path "V:\gate-evidence") { Remove-Item -Recurse -Force "V:\gate-evidence" }

Write-Host "== plugins =="
Get-ChildItem $plugins -Directory | Select-Object -ExpandProperty Name
Write-Host ("count=" + (Get-ChildItem $plugins -Directory).Count)
Write-Host "== config =="
Get-Content $cfg | Select-String -Pattern "^(Feeding Required|Bowl Drain Minutes|Debug Logging|Allow Orders) = "

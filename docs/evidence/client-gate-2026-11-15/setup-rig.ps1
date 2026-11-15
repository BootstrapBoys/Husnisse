# Gate rig setup: minimal matching plugin set on the server + the gate config values.
# Run while the rig is stopped. Every folder moved is listed so it can be restored.
$ErrorActionPreference = "Stop"
$plugins = "V:\ValheimTest\BepInEx\plugins"
$disabled = "V:\ValheimTest\BepInEx\plugins.disabled"
New-Item -ItemType Directory -Force -Path $disabled | Out-Null

$keep = @(
    "BootstrapBoys-Husnisse",
    "ValheimModding-Jotunn",
    "shudnal-ConditionalConfigSync",
    "ValheimModding-JsonDotNET",
    "ValheimModding-YamlDotNet",
    "BootstrapBoys-TestHarness",
    "BootstrapBoys-ServerSoftStop"
)

Write-Host "== parked =="
foreach ($dir in Get-ChildItem $plugins -Directory) {
    if ($keep -contains $dir.Name) { continue }
    $target = Join-Path $disabled $dir.Name
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    Move-Item -LiteralPath $dir.FullName -Destination $target
    Write-Host ("  moved " + $dir.Name + " -> plugins.disabled")
}

Write-Host "== kept =="
Get-ChildItem $plugins -Directory | Select-Object -ExpandProperty Name

$cfg = "V:\ValheimTest\BepInEx\config\com.bootstrapboys.husnisse.cfg"
$text = [System.IO.File]::ReadAllText($cfg)
$pairs = @(
    @("Feeding Required", "false"),
    @("Bowl Drain Minutes", "1"),
    @("Debug Logging", "true"),
    @("Allow Orders", "true"),
    @("Enabled", "true")
)
foreach ($pair in $pairs) {
    $key = [regex]::Escape($pair[0])
    $replacement = $pair[0] + " = " + $pair[1]
    $text = [regex]::Replace($text, "(?m)^$key\s*=.*$", $replacement)
}
[System.IO.File]::WriteAllText($cfg, $text, (New-Object System.Text.UTF8Encoding $false))
Write-Host "== config =="
Get-Content $cfg | Select-String -Pattern "^(Feeding Required|Bowl Drain Minutes|Debug Logging|Allow Orders|Enabled) = "

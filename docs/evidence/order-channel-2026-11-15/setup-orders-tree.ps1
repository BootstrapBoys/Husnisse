# Prepare V:\ValheimOrders (port 2856) as the order-channel card's own tree.
# Run while that tree is stopped. Copies only what the mod needs, from the live install.
$ErrorActionPreference = "Stop"

$dst = "V:\ValheimOrders"
$live = "C:\PowerShellGSM\servers\valheim"
$srcTree = "V:\ValheimTest"
$plugins = Join-Path $dst "BepInEx\plugins"

foreach ($name in @("shudnal-ConditionalConfigSync", "ValheimModding-YamlDotNet", "ValheimModding-JsonDotNET")) {
    $from = Join-Path $live ("BepInEx\plugins\" + $name)
    $to = Join-Path $plugins $name
    if (-not (Test-Path $from)) { Write-Host ("missing in live install: " + $name); continue }
    if (Test-Path $to) { Remove-Item -Recurse -Force $to }
    Copy-Item -LiteralPath $from -Destination $to -Recurse -Force
    Write-Host ("copied " + $name)
}

# The prepared fixture world, so scenarios get the same ground the main tree has.
$fixFrom = Join-Path $srcTree "fixtures"
$fixTo = Join-Path $dst "fixtures"
if (Test-Path $fixFrom) {
    if (Test-Path $fixTo) { Remove-Item -Recurse -Force $fixTo }
    Copy-Item -LiteralPath $fixFrom -Destination $fixTo -Recurse -Force
    Write-Host ("fixture copied: " + ((Get-ChildItem $fixTo -Recurse -File).Count) + " files")
} else {
    Write-Host "no fixture in the main tree to copy"
}

# Localisation file and a config with this card's switches.
$cfgFrom = Join-Path $srcTree "BepInEx\config\com.bootstrapboys.husnisse.cfg"
$cfgTo = Join-Path $dst "BepInEx\config\com.bootstrapboys.husnisse.cfg"
New-Item -ItemType Directory -Force -Path (Split-Path $cfgTo) | Out-Null
Copy-Item -LiteralPath $cfgFrom -Destination $cfgTo -Force
$locFrom = Join-Path $srcTree "BepInEx\config\Husnisse"
if (Test-Path $locFrom) {
    $locTo = Join-Path $dst "BepInEx\config\Husnisse"
    if (Test-Path $locTo) { Remove-Item -Recurse -Force $locTo }
    Copy-Item -LiteralPath $locFrom -Destination $locTo -Recurse -Force
}

$text = [System.IO.File]::ReadAllText($cfgTo)
$pairs = @(
    @("Feeding Required", "false"),   # per-bowl opt-in: the Alt toggle has something to toggle
    @("Bowl Drain Minutes", "20"),
    @("Debug Logging", "true"),       # [Orders] and [Bowl] lines are debug lines
    @("Allow Orders", "true"),
    @("Enabled", "true")
)
foreach ($pair in $pairs) {
    $key = [regex]::Escape($pair[0])
    $text = [regex]::Replace($text, "(?m)^$key\s*=.*$", ($pair[0] + " = " + $pair[1]))
}
[System.IO.File]::WriteAllText($cfgTo, $text, (New-Object System.Text.UTF8Encoding $false))

Write-Host "== plugins =="
Get-ChildItem $plugins -Directory | Select-Object -ExpandProperty Name
Write-Host ("count=" + (Get-ChildItem $plugins -Directory).Count)
Write-Host "== config =="
Get-Content $cfgTo | Select-String -Pattern "^(Feeding Required|Bowl Drain Minutes|Debug Logging|Allow Orders|Enabled) = "
Write-Host "== port =="
Select-String -LiteralPath (Join-Path $dst "start-test.cmd") -Pattern "-port" | ForEach-Object { $_.Line }

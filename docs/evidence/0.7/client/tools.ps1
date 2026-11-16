# Client-rig helper for the 0.3b panel gate. One script, selected by -Mode, because the guest's
# default shell is cmd.exe and every call is an encoded PowerShell command.
#   batch -Wait N   send stage\cmd.txt to the probe, wait N s, print ONLY the log lines this batch
#                   produced (byte offset taken before the send, so no earlier batch can bleed in)
#   send            stage\cmd.txt -> %USERPROFILE%\husnisse-probe.cmd, no read back
#   tail -Lines N   last N lines of the client's BepInEx log
#   reset           reset the command file so a fresh client does not replay the last batch
#   grep            the gate/probe lines of the current log
#   install         copy stage\Husnisse.dll + stage\HusnisseGateClientProbe.dll, verify hashes
#   stage           zip the client log and the capture folder into stage\client-evidence.zip
param([string]$Mode = "tail", [int]$Lines = 40, [int]$Wait = 5)

$ErrorActionPreference = "Continue"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$log = Join-Path $g "BepInEx\LogOutput.log"
$stage = "C:\Users\Hermes\stage"
$cmd = Join-Path $env:USERPROFILE "husnisse-probe.cmd"

function Hash([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return "absent" }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
}

function LogFrom([long]$offset) {
    if (-not (Test-Path $log)) { return "" }
    $fs = [System.IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
    try {
        $length = $fs.Length
        if ($length -le $offset) { return "" }
        $fs.Position = $offset
        $buffer = New-Object byte[] ($length - $offset)
        $read = $fs.Read($buffer, 0, $buffer.Length)
        return [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
    } finally { $fs.Close() }
}

switch ($Mode) {
    "batch" {
        $offset = 0
        if (Test-Path $log) { $offset = (Get-Item $log).Length }
        $text = Get-Content -LiteralPath (Join-Path $stage "cmd.txt") -Raw
        Set-Content -Path $cmd -Value $text -Encoding ASCII
        Start-Sleep -Seconds $Wait
        $new = LogFrom $offset
        foreach ($line in ($new -split "`r?`n")) {
            if ($line -match "Gate Client Probe|Husnisse\]|Exception|Error") {
                Write-Output ($line -replace "^.*Gate Client Probe\] ", "")
            }
        }
    }
    "send" {
        $text = Get-Content -LiteralPath (Join-Path $stage "cmd.txt") -Raw
        Set-Content -Path $cmd -Value $text -Encoding ASCII
        Write-Output ("sent: " + ($text -replace "`r?`n", " | "))
    }
    "tail" {
        if (Test-Path $log) { Get-Content $log -Tail $Lines } else { Write-Output "no log" }
    }
    "reset" {
        Set-Content -Path $cmd -Value "# idle" -Encoding ASCII
        Write-Output ("cmd file reset: " + (Get-Content $cmd -Raw).Trim())
    }
    "grep" {
        if (-not (Test-Path $log)) { Write-Output "no log"; break }
        Select-String -Path $log -Pattern "gc loaded|gc ready|gc no client|content registered|Husnisse 0.3|Exception|Error" |
            ForEach-Object { Write-Output $_.Line }
        Write-Output "== tail =="
        Get-Content $log -Tail 3
    }
    "install" {
        $hus = Join-Path $g "BepInEx\plugins\BootstrapBoys-Husnisse"
        New-Item -ItemType Directory -Force -Path $hus | Out-Null
        $probe = Join-Path $g "BepInEx\plugins\BootstrapBoys-GateClientProbe"
        New-Item -ItemType Directory -Force -Path $probe | Out-Null

        # A running client locks its plugin DLLs ("user-mapped section open"); report that instead of
        # letting a silent Copy-Item failure read as a successful install.
        foreach ($pair in @(@((Join-Path $stage "Husnisse.dll"), (Join-Path $hus "Husnisse.dll")),
                            @((Join-Path $stage "HusnisseGateClientProbe.dll"), (Join-Path $probe "HusnisseGateClientProbe.dll")))) {
            try {
                Copy-Item -LiteralPath $pair[0] -Destination $pair[1] -Force -ErrorAction Stop
                Write-Output ("copied " + (Split-Path $pair[1] -Leaf))
            } catch {
                Write-Output ("COPY FAILED for " + $pair[1] + " : " + $_.Exception.Message)
            }
        }

        Write-Output "== installed =="
        Write-Output ("  stage Husnisse.dll      " + (Hash (Join-Path $stage "Husnisse.dll")))
        Write-Output ("  plugin Husnisse.dll     " + (Hash (Join-Path $hus "Husnisse.dll")))
        Write-Output ("  stage probe             " + (Hash (Join-Path $stage "HusnisseGateClientProbe.dll")))
        Write-Output ("  plugin probe            " + (Hash (Join-Path $probe "HusnisseGateClientProbe.dll")))
        Write-Output "== plugin folders =="
        Get-ChildItem (Join-Path $g "BepInEx\plugins") -Directory | Select-Object -ExpandProperty Name
    }
    "stage" {
        $out = Join-Path $stage "client-evidence"
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        if (Test-Path $log) { Copy-Item -LiteralPath $log -Destination (Join-Path $out "client-bepinex.log") -Force }
        $caps = Join-Path ([Environment]::GetFolderPath("Desktop")) "husnisse-captures"
        if (Test-Path $caps) {
            Get-ChildItem $caps -Filter *.png | ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $out $_.Name) -Force
            }
        }
        if (Test-Path $cmd) { Copy-Item -LiteralPath $cmd -Destination (Join-Path $out "last-probe-command.txt") -Force }
        $zip = Join-Path $stage "client-evidence.zip"
        if (Test-Path $zip) { Remove-Item $zip -Force }
        Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -Force
        Write-Output ("zip " + $zip + " " + (Get-Item $zip).Length)
        Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize
    }
    default { Write-Output ("unknown mode " + $Mode) }
}

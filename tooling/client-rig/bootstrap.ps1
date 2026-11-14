# Run in the Windows 11 client VM as Administrator.
# Enables OpenSSH with key auth for the Hermes client-rig key, opens the firewall,
# and prints the VM address plus the Gale profile layout.
#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"
$pub = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIGZIeYVqA9A0Zf94FpMEfHw5jA/LPuyU4OAzKn2+IlLP hermes@husnisse-client"
$log = Join-Path $env:USERPROFILE "Desktop\husnisse-bootstrap.txt"

Start-Transcript -Path $log -Force | Out-Null
try {
    Write-Host "== identity =="
    Write-Host "host=$env:COMPUTERNAME user=$env:USERNAME"
    Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notmatch '^127\.' } |
        Sort-Object InterfaceMetric |
        Format-Table IPAddress, InterfaceAlias, PrefixLength -AutoSize

    Write-Host "== OpenSSH Server =="
    $cap = Get-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
    if ($cap.State -ne "Installed") {
        Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 | Out-Null
    }
    Set-Service sshd -StartupType Automatic
    Start-Service sshd
    if (-not (Get-NetFirewallRule -Name "OpenSSH-Server-In-TCP" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name "OpenSSH-Server-In-TCP" -DisplayName "OpenSSH Server (sshd)" `
            -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 -Profile Any | Out-Null
    }

    # Current user key (works for non-admin accounts).
    $sshDir = Join-Path $env:USERPROFILE ".ssh"
    New-Item -ItemType Directory -Force -Path $sshDir | Out-Null
    $userKeys = Join-Path $sshDir "authorized_keys"
    if (-not (Test-Path $userKeys) -or -not (Select-String -Path $userKeys -SimpleMatch $pub -Quiet)) {
        Add-Content -Path $userKeys -Value $pub
    }
    & icacls $sshDir /inheritance:r /grant "${env:USERNAME}:F" /grant "SYSTEM:F" /grant "Administrators:F" | Out-Null
    & icacls $userKeys /inheritance:r /grant "${env:USERNAME}:F" /grant "SYSTEM:F" /grant "Administrators:F" | Out-Null

    # Administrators use this file instead of the per-user authorized_keys.
    $adminKeys = Join-Path $env:ProgramData "ssh\administrators_authorized_keys"
    if (-not (Test-Path $adminKeys) -or -not (Select-String -Path $adminKeys -SimpleMatch $pub -Quiet)) {
        Add-Content -Path $adminKeys -Value $pub
    }
    & icacls $adminKeys /inheritance:r /grant "Administrators:F" /grant "SYSTEM:F" | Out-Null

    Restart-Service sshd
    Write-Host "sshd: $((Get-Service sshd).Status)"

    Write-Host "== Gale =="
    foreach ($p in @(
        (Join-Path $env:APPDATA "Gale"),
        (Join-Path $env:LOCALAPPDATA "Gale")
    )) {
        if (Test-Path $p) {
            Write-Host "--- $p"
            Get-ChildItem -Path $p -Depth 3 -ErrorAction SilentlyContinue |
                Select-Object FullName | Format-Table -AutoSize
        }
    }

    Write-Host "== Valheim =="
    foreach ($p in @(
        "C:\Program Files (x86)\Steam\steamapps\common\Valheim",
        "D:\SteamLibrary\steamapps\common\Valheim",
        "E:\SteamLibrary\steamapps\common\Valheim",
        "V:\SteamLibrary\steamapps\common\Valheim"
    )) {
        if (Test-Path $p) { Write-Host "FOUND $p" }
    }

    Write-Host "== done =="
    Write-Host "Send the IPv4 address above to Hermes. Log: $log"
}
finally {
    Stop-Transcript | Out-Null
}

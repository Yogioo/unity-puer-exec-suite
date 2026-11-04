#Requires -Version 5.1
<#
.SYNOPSIS
    Installs com.yogioo.puer-exec-suite into a Unity project as an embedded package.

.DESCRIPTION
    Copies (or clones) this package into <project>/Packages/com.yogioo.puer-exec-suite.
    Unity picks embedded packages up automatically, so no manifest edit is required.
    By default the package folder is added to the project .gitignore so teammates and
    CI never get the module enabled by accident.

.PARAMETER Project
    Unity project root (the folder that contains Assets/ and ProjectSettings/). Default: current directory.

.PARAMETER Source
    Git URL or local directory of this package. Default: the public GitHub repo.

.PARAMETER Enable
    Also write the per-machine opt-in marker (UserSettings/PuerExecSuite/enabled).

.PARAMETER Update
    git pull --ff-only inside the already installed package folder.

.PARAMETER Remove
    Delete the installed package folder and the opt-in marker.

.PARAMETER Force
    Reinstall over an existing folder.

.PARAMETER NoIgnore
    Do not touch the project .gitignore.

.EXAMPLE
    .\install.ps1 -Project C:\projects\MyGame -Enable

.EXAMPLE
    .\install.ps1 -Project C:\projects\MyGame -Update

.EXAMPLE
    .\install.ps1 -Project C:\projects\MyGame -Remove
#>
[CmdletBinding()]
param(
    [string]$Project = ".",
    [string]$Source = "https://github.com/Yogioo/unity-puer-exec-suite.git",
    [string]$PackageName = "com.yogioo.puer-exec-suite",
    [switch]$Enable,
    [switch]$Update,
    [switch]$Remove,
    [switch]$Force,
    [switch]$NoIgnore
)

$ErrorActionPreference = 'Stop'

function Resolve-ProjectRoot {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "Project path not found: $Path" }
    $root = (Resolve-Path -LiteralPath $Path).ProviderPath
    if (-not (Test-Path -LiteralPath (Join-Path $root 'Assets'))) { throw "Not a Unity project (no Assets/): $root" }
    if (-not (Test-Path -LiteralPath (Join-Path $root 'ProjectSettings\ProjectVersion.txt'))) {
        throw "Not a Unity project (no ProjectSettings/ProjectVersion.txt): $root"
    }
    return $root
}

function Write-Utf8NoBom {
    param([string]$Path, [string[]]$Lines)
    $text = ($Lines -join "`r`n") + "`r`n"
    [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($false)))
}

function Add-IgnoreEntry {
    param([string]$Root, [string]$Entry)
    if ($NoIgnore) { return }
    $gitignore = Join-Path $Root '.gitignore'
    $lines = @()
    if (Test-Path -LiteralPath $gitignore) {
        $lines = @(Get-Content -LiteralPath $gitignore)
    }
    $normalized = '/' + $Entry.Trim('/') + '/'
    foreach ($line in $lines) {
        if ($line.Trim() -eq $normalized) { return }
    }
    $block = @('', '# puer-exec suite: per-machine, default off', $normalized)
    Write-Utf8NoBom -Path $gitignore -Lines ($lines + $block)
    Write-Host "  ignore  $normalized -> .gitignore"
}

function Remove-IgnoreEntry {
    param([string]$Root, [string]$Entry)
    $gitignore = Join-Path $Root '.gitignore'
    if (-not (Test-Path -LiteralPath $gitignore)) { return }
    $normalized = '/' + $Entry.Trim('/') + '/'
    $lines = @(Get-Content -LiteralPath $gitignore)
    $kept = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line.Trim() -eq $normalized) { continue }
        if ($line.Trim() -eq '# puer-exec suite: per-machine, default off') { continue }
        $kept.Add($line)
    }
    while ($kept.Count -gt 0 -and $kept[$kept.Count - 1].Trim() -eq '') { $kept.RemoveAt($kept.Count - 1) }
    if ($kept.Count -ne $lines.Count) {
        Write-Utf8NoBom -Path $gitignore -Lines $kept.ToArray()
        Write-Host "  ignore  $normalized removed from .gitignore"
    }
}

function Install-FromSource {
    param([string]$From, [string]$To)
    $isGitUrl = $From -match '^(https?|git|ssh)://' -or $From -match '^git@' -or $From -match '\.git$'
    if ($isGitUrl) {
        if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git not found on PATH' }
        & git clone --depth 1 $From $To
        if ($LASTEXITCODE -ne 0) {
            throw "git clone failed: $From`n  If github.com over https is blocked here, retry with:`n    -Source git@github.com:Yogioo/unity-puer-exec-suite.git`n    -Source https://ghproxy.net/https://github.com/Yogioo/unity-puer-exec-suite.git`n  or download the zip and pass -Source <extracted folder>."
        }
        return
    }
    if (-not (Test-Path -LiteralPath $From)) { throw "Source not found: $From" }
    $fromPath = (Resolve-Path -LiteralPath $From).ProviderPath
    if ($fromPath.TrimEnd('\') -eq $To.TrimEnd('\')) { return }
    New-Item -ItemType Directory -Path $To -Force | Out-Null
    Get-ChildItem -LiteralPath $fromPath -Force |
        Where-Object { $_.Name -ne '.git' } |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $To -Recurse -Force }
}

$root = Resolve-ProjectRoot -Path $Project
$packagesDir = Join-Path $root 'Packages'
$target = Join-Path $packagesDir $PackageName
$flag = Join-Path $root 'UserSettings\PuerExecSuite\enabled'

Write-Host "project : $root"
Write-Host "package : $target"
Write-Host ''

if ($Remove) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host '  removed package folder'
    }
    else {
        Write-Host '  package folder not present'
    }
    if (Test-Path -LiteralPath $flag) {
        Remove-Item -LiteralPath $flag -Force
        Write-Host '  removed opt-in marker'
    }
    Remove-IgnoreEntry -Root $root -Entry "Packages/$PackageName"
    Write-Host ''
    Write-Host 'Done. Reopen or refocus the Unity Editor to unload the package.'
    return
}

if (-not (Test-Path -LiteralPath $packagesDir)) { New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null }

if ($Update) {
    if (-not (Test-Path -LiteralPath (Join-Path $target '.git'))) {
        throw "No git checkout at $target. Re-run without -Update (or with -Force) to reinstall."
    }
    & git -C $target pull --ff-only
    if ($LASTEXITCODE -ne 0) { throw 'git pull failed' }
    Write-Host '  updated'
    Write-Host ''
    Write-Host 'Done. Refocus the Unity Editor to reimport.'
    return
}

if (Test-Path -LiteralPath $target) {
    if (-not $Force) {
        throw "$target already exists. Use -Update to pull, -Force to reinstall, or -Remove to uninstall."
    }
    $resolved = (Resolve-Path -LiteralPath $target).ProviderPath
    if ($resolved.Length -le $packagesDir.Length) { throw "Refusing to delete $resolved" }
    Remove-Item -LiteralPath $target -Recurse -Force
    Write-Host '  removed previous install (-Force)'
}

New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null
Install-FromSource -From $Source -To $target
Write-Host "  install $target"

Add-IgnoreEntry -Root $root -Entry "Packages/$PackageName"

if ($Enable) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $flag) -Force | Out-Null
    Write-Utf8NoBom -Path $flag -Lines @('enabled')
    Write-Host "  enable  $flag"
}

$cli = Join-Path $target 'Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe'
Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. Refocus or reopen the Unity Editor; the package compiles editor-only.'
Write-Host '  2. Off by default. To drive this Editor from a CLI either:'
Write-Host "       a) launch with the activation switch:"
Write-Host "          Unity.exe -projectPath `"$root`" -unityPuerExecControl"
Write-Host '       b) or opt in on this machine (persists across Editor restarts):'
Write-Host '          menu Tools/Puer Exec Suite/Enable On This Machine'
Write-Host "     CLI: $cli"

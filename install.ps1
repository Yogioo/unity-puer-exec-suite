#Requires -Version 5.1
<#
.SYNOPSIS
    Installs com.yogioo.puer-exec-suite into a Unity project as a local, gitignored tree.

.DESCRIPTION
    Two install locations:

      -Location assets   (default) <project>/Assets/Editor/PuerExecSuite
                         Unity compiles it as project code; the Package Manager never
                         sees it, so packages-lock.json is never touched.

      -Location packages           <project>/Packages/com.yogioo.puer-exec-suite
                         A UPM embedded package; functionally identical, but UPM then
                         records and later prunes a packages-lock.json entry, which
                         makes the lock field dirty on whichever side lacks the folder.

    By default the install path is added to the project .gitignore so teammates and CI
    never pick the module up.

.PARAMETER Project
    Unity project root (the folder that contains Assets/ and ProjectSettings/). Default: current directory.

.PARAMETER Source
    Git URL or local directory of this package. Default: the public GitHub repo.

.PARAMETER Location
    assets (default) or packages. See the description above.

.PARAMETER Enable
    Also write the per-machine opt-in marker (UserSettings/PuerExecSuite/enabled).

.PARAMETER EnableProject
    Also write the shared opt-in marker (ProjectSettings/PuerExecSuite/enabled). Commit it to
    opt the whole team in; a teammate can veto locally with UserSettings/PuerExecSuite/disabled.

.PARAMETER Update
    git pull --ff-only inside the already installed tree.

.PARAMETER Remove
    Delete the installed tree from both locations and drop the machine markers.

.PARAMETER Force
    Reinstall over an existing tree.

.PARAMETER NoIgnore
    Do not touch the project .gitignore.

.EXAMPLE
    .\install.ps1 -Project C:\projects\MyGame -Enable

.EXAMPLE
    .\install.ps1 -Project C:\projects\MyGame -Location packages

.EXAMPLE
    .\install.ps1 -Project C:\projects\MyGame -Update
#>
[CmdletBinding()]
param(
    [string]$Project = ".",
    [string]$Source = "https://github.com/Yogioo/unity-puer-exec-suite.git",
    [ValidateSet('assets', 'packages')]
    [string]$Location = 'assets',
    [switch]$Enable,
    [switch]$EnableProject,
    [switch]$Update,
    [switch]$Remove,
    [switch]$Force,
    [switch]$NoIgnore
)

$ErrorActionPreference = 'Stop'

$PackageName = 'com.yogioo.puer-exec-suite'
$AssetRel = 'Assets/Editor/PuerExecSuite'
$PackageRel = "Packages/$PackageName"
$CliTail = 'Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe'

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
    param([string]$Root, [string]$Relative)
    if ($NoIgnore) { return }
    $gitignore = Join-Path $Root '.gitignore'
    $lines = @()
    if (Test-Path -LiteralPath $gitignore) { $lines = @(Get-Content -LiteralPath $gitignore) }
    $entry = '/' + $Relative.Trim('/') + '/'
    foreach ($line in $lines) {
        if ($line.Trim() -eq $entry) { return }
    }
    $block = @('', '# puer-exec suite: per-machine, default off', $entry)
    Write-Utf8NoBom -Path $gitignore -Lines ($lines + $block)
    Write-Host "  ignore  $entry -> .gitignore"
}

function Remove-IgnoreEntry {
    param([string]$Root, [string]$Relative)
    $gitignore = Join-Path $Root '.gitignore'
    if (-not (Test-Path -LiteralPath $gitignore)) { return }
    $entry = '/' + $Relative.Trim('/') + '/'
    $lines = @(Get-Content -LiteralPath $gitignore)
    $kept = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line.Trim() -eq $entry) { continue }
        if ($line.Trim() -eq '# puer-exec suite: per-machine, default off') { continue }
        $kept.Add($line)
    }
    while ($kept.Count -gt 0 -and $kept[$kept.Count - 1].Trim() -eq '') { $kept.RemoveAt($kept.Count - 1) }
    if ($kept.Count -ne $lines.Count) {
        Write-Utf8NoBom -Path $gitignore -Lines $kept.ToArray()
        Write-Host "  ignore  $entry removed from .gitignore"
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
$relTarget = if ($Location -eq 'packages') { $PackageRel } else { $AssetRel }
$target = Join-Path $root $relTarget
$flag = Join-Path $root 'UserSettings\PuerExecSuite\enabled'
$flagDisabled = Join-Path $root 'UserSettings\PuerExecSuite\disabled'
$projectFlag = Join-Path $root 'ProjectSettings\PuerExecSuite\enabled'
$otherTarget = Join-Path $root $(if ($Location -eq 'packages') { $AssetRel } else { $PackageRel })
$otherRel = if ($Location -eq 'packages') { $AssetRel } else { $PackageRel }

function Write-Markers {
    if ($Enable) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $flag) -Force | Out-Null
        Write-Utf8NoBom -Path $flag -Lines @('enabled')
        if (Test-Path -LiteralPath $flagDisabled) { Remove-Item -LiteralPath $flagDisabled -Force }
        Write-Host "  enable  $flag (this machine)"
    }
    if ($EnableProject) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $projectFlag) -Force | Out-Null
        Write-Utf8NoBom -Path $projectFlag -Lines @('enabled')
        Write-Host "  enable  $projectFlag (commit this to share with the team)"
    }
}

Write-Host "project : $root"
Write-Host "location: $Location"
Write-Host "target  : $target"
Write-Host ''

if ($Remove) {
    foreach ($rel in @($AssetRel, $PackageRel)) {
        $path = Join-Path $root $rel
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            Write-Host "  removed $rel"
            Remove-IgnoreEntry -Root $root -Relative $rel
        }
    }
    if (Test-Path -LiteralPath $flag) {
        Remove-Item -LiteralPath $flag -Force
        Write-Host '  removed per-machine opt-in marker'
    }
    if (Test-Path -LiteralPath $flagDisabled) {
        Remove-Item -LiteralPath $flagDisabled -Force
        Write-Host '  removed per-machine veto marker'
    }
    if (Test-Path -LiteralPath $projectFlag) {
        Write-Host '  kept ProjectSettings/PuerExecSuite/enabled (shared team decision; delete it yourself if unintended)'
    }
    Write-Host ''
    Write-Host 'Done. Reopen or refocus the Unity Editor to unload the module.'
    return
}

if ($Update) {
    foreach ($rel in @($AssetRel, $PackageRel)) {
        $path = Join-Path $root $rel
        if (-not (Test-Path -LiteralPath (Join-Path $path '.git'))) { continue }
        & git -C $path pull --ff-only
        if ($LASTEXITCODE -ne 0) { throw "git pull failed in $rel" }
        Write-Host "  updated $rel"
    }
    Write-Host ''
    Write-Host 'Done. Refocus the Unity Editor to reimport.'
    return
}

if (Test-Path -LiteralPath $otherTarget) {
    throw "$otherRel is also installed. Use -Remove first, or you will get duplicate assembly names."
}

if (Test-Path -LiteralPath $target) {
    if (-not $Force) {
        if ($Enable -or $EnableProject) {
            Write-Host '  already installed; updating markers only'
            Write-Markers
            return
        }
        throw "$target already exists. Use -Update to pull, -Force to reinstall, or -Remove to uninstall."
    }
    $resolved = (Resolve-Path -LiteralPath $target).ProviderPath
    if ($resolved.Length -le (Join-Path $root 'Assets').Length) { throw "Refusing to delete $resolved" }
    Remove-Item -LiteralPath $target -Recurse -Force
    Write-Host '  removed previous install (-Force)'
}

New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
Install-FromSource -From $Source -To $target
Write-Host "  install $target"

Add-IgnoreEntry -Root $root -Relative $relTarget

Write-Markers

$cli = Join-Path $target $CliTail
Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. Refocus or reopen the Unity Editor; the module compiles editor-only.'
Write-Host '  2. Off by default. Enable once, then it auto-activates on every Editor start:'
Write-Host '       menu Tools/Puer Exec Suite/Enable On This Machine     (this machine only)'
Write-Host '       menu Tools/Puer Exec Suite/Enable For This Project   (committed, whole team)'
Write-Host '     Per-process, for an isolated log:'
Write-Host "       Unity.exe -projectPath `"$root`" -unityPuerExecControl"
Write-Host "     CLI: $cli"

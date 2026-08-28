[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $ToolboxPath,

    [string] $WorkRoot = '',
    [string] $OutputFile = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Stop-Packaging {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "Validation packaging failed: $Message"
}

function Get-SingleManifestValue {
    param(
        [Parameter(Mandatory = $true)][string] $Content,
        [Parameter(Mandatory = $true)][string] $Pattern,
        [Parameter(Mandatory = $true)][string] $Description
    )

    $found = [regex]::Matches($Content, $Pattern)
    if ($found.Count -ne 1) {
        Stop-Packaging "Expected exactly one $Description, found $($found.Count)."
    }
    return $found[0].Groups[1].Value
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$toolbox = (Resolve-Path -LiteralPath $ToolboxPath).Path
$buildOutput = Join-Path $root 'source\bin\Release'
if (-not (Test-Path -LiteralPath $buildOutput -PathType Container)) {
    Stop-Packaging "Release output directory not found: $buildOutput"
}

$requiredFiles = [ordered]@{
    'extension.yaml' = 'source\extension.yaml'
    'GameRoutines.dll' = $null
    'icon.png' = 'source\icon.png'
    'Localization/en_US.xaml' = 'source\Localization\en_US.xaml'
}

foreach ($relativePath in $requiredFiles.Keys) {
    $buildPath = Join-Path $buildOutput $relativePath
    if (-not (Test-Path -LiteralPath $buildPath -PathType Leaf)) {
        Stop-Packaging "Required Release output is missing: $relativePath"
    }
    if ((Get-Item -LiteralPath $buildPath).Length -eq 0) {
        Stop-Packaging "Required Release output is empty: $relativePath"
    }

    $sourceRelativePath = $requiredFiles[$relativePath]
    if ($null -ne $sourceRelativePath) {
        $sourcePath = Join-Path $root $sourceRelativePath
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            Stop-Packaging "Required source file is missing: $sourceRelativePath"
        }
        $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
        $buildHash = (Get-FileHash -LiteralPath $buildPath -Algorithm SHA256).Hash
        if ($sourceHash -cne $buildHash) {
            Stop-Packaging "Release output does not match its source file: $relativePath"
        }
    }
}

$debugSymbols = @(Get-ChildItem -LiteralPath $buildOutput -Recurse -File -Filter 'GameRoutines*.pdb')
if ($debugSymbols.Count -gt 0) {
    Stop-Packaging "Game Routines debug symbols were generated:`n$($debugSymbols.FullName -join "`n")"
}

$manifestContent = [System.IO.File]::ReadAllText((Join-Path $buildOutput 'extension.yaml'))
$addonId = Get-SingleManifestValue -Content $manifestContent -Pattern '(?m)^Id:\s*(\S+)\s*$' -Description 'extension Id'
$version = Get-SingleManifestValue -Content $manifestContent -Pattern '(?m)^Version:\s*(\S+)\s*$' -Description 'extension Version'
if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    Stop-Packaging "Extension version must use X.Y.Z: $version"
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = [System.IO.Path]::GetTempPath()
}
if (-not (Test-Path -LiteralPath $WorkRoot -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $WorkRoot)
}
$resolvedWorkRoot = (Resolve-Path -LiteralPath $WorkRoot).Path
$runDirectory = Join-Path $resolvedWorkRoot ("GameRoutines-CI-" + [guid]::NewGuid().ToString('N'))
$stagingDirectory = Join-Path $runDirectory 'staging'
$packageDirectory = Join-Path $runDirectory 'package'
[void](New-Item -ItemType Directory -Path $stagingDirectory)
[void](New-Item -ItemType Directory -Path $packageDirectory)

foreach ($relativePath in $requiredFiles.Keys) {
    $sourcePath = Join-Path $buildOutput $relativePath
    $destinationPath = Join-Path $stagingDirectory $relativePath
    $destinationParent = Split-Path -Parent $destinationPath
    if (-not (Test-Path -LiteralPath $destinationParent -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $destinationParent)
    }
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
}

$expectedEntries = @('extension.yaml', 'GameRoutines.dll', 'icon.png', 'Localization/en_US.xaml')
$stagedEntries = @(
    Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File |
        ForEach-Object { $_.FullName.Substring($stagingDirectory.Length + 1).Replace('\', '/') }
)
if ($stagedEntries.Count -ne $expectedEntries.Count -or
    @($stagedEntries | Where-Object { $expectedEntries -cnotcontains $_ }).Count -ne 0) {
    Stop-Packaging "Staging inventory is not the exact release allowlist:`n$($stagedEntries -join "`n")"
}

Write-Host "Packing allowlisted files with Toolbox: $toolbox"
& $toolbox pack $stagingDirectory $packageDirectory
$toolboxExitCode = $LASTEXITCODE
if ($toolboxExitCode -ne 0) {
    Stop-Packaging "Toolbox exited with code $toolboxExitCode."
}

$packages = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '*.pext')
if ($packages.Count -ne 1) {
    Stop-Packaging "Expected exactly one .pext, found $($packages.Count)."
}

$expectedPackageName = "$addonId`_$($version.Replace('.', '_')).pext"
if ($packages[0].Name -cne $expectedPackageName) {
    Stop-Packaging "Toolbox generated $($packages[0].Name); expected $expectedPackageName."
}

$packagePath = $packages[0].FullName
if ($packages[0].Length -eq 0) {
    Stop-Packaging 'Toolbox generated an empty package.'
}

if (-not [string]::IsNullOrWhiteSpace($OutputFile)) {
    Add-Content -LiteralPath $OutputFile -Encoding utf8 -Value "package_path=$packagePath"
}

Write-Host "Validation package created: $packagePath"
Write-Output $packagePath

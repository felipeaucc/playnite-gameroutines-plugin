[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [ValidateSet('local', 'pull_request', 'push', 'workflow_dispatch')]
    [string] $EventName = 'local',

    [string] $BeforeSha = '',
    [string] $BaseSha = '',
    [string] $Ref = '',
    [string] $OutputFile = '',
    [switch] $SkipCleanCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Stop-Validation {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "Repository validation failed: $Message"
}

function Invoke-GitCheck {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $gitOutput = & git -C $script:Root @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($gitOutput) {
        $gitOutput | ForEach-Object { Write-Host $_ }
    }

    if ($exitCode -ne 0) {
        Stop-Validation "git $($Arguments -join ' ') exited with code $exitCode."
    }
}

function Get-SingleMatchValue {
    param(
        [Parameter(Mandatory = $true)][string] $Content,
        [Parameter(Mandatory = $true)][string] $Pattern,
        [Parameter(Mandatory = $true)][string] $Description
    )

    $found = [regex]::Matches($Content, $Pattern)
    if ($found.Count -ne 1) {
        Stop-Validation "Expected exactly one $Description, found $($found.Count)."
    }

    return $found[0].Groups[1].Value
}

function Test-ThreePartVersion {
    param([Parameter(Mandatory = $true)][string] $Value)
    return $Value -match '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
}

function Read-InstallerManifest {
    param([Parameter(Mandatory = $true)][string] $Path)

    $lines = [System.IO.File]::ReadAllLines($Path)
    $significant = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $lines.Length; $index++) {
        if (-not [string]::IsNullOrWhiteSpace($lines[$index])) {
            $significant.Add([pscustomobject]@{ Index = $index; Text = $lines[$index] })
        }
    }

    if ($significant.Count -lt 3) {
        Stop-Validation 'installer.yaml must contain AddonId, Packages, and at least one package.'
    }

    if ($significant[0].Text -notmatch '^AddonId:\s*([A-Za-z0-9_-]+)\s*$') {
        Stop-Validation 'installer.yaml must begin with one nonempty root AddonId.'
    }
    $addonId = $Matches[1]

    if ($significant[1].Text -notmatch '^Packages:\s*$') {
        Stop-Validation 'installer.yaml must contain Packages immediately after AddonId.'
    }

    $packages = [System.Collections.Generic.List[object]]::new()
    $currentPackage = $null

    for ($position = 2; $position -lt $significant.Count; $position++) {
        $lineNumber = $significant[$position].Index + 1
        $line = [string]$significant[$position].Text

        if ($line -match '^  - Version:\s*(\S+)\s*$') {
            $versionValue = $Matches[1]
            $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            [void]$seen.Add('Version')
            $changelog = [System.Collections.Generic.List[string]]::new()
            $currentPackage = [ordered]@{
                Version = $versionValue
                RequiredApiVersion = $null
                ReleaseDate = $null
                PackageUrl = $null
                Changelog = $changelog
                ChangelogDeclared = $false
                InChangelog = $false
                Seen = $seen
                StartLine = $lineNumber
            }
            $packages.Add($currentPackage)
            continue
        }

        if ($null -eq $currentPackage) {
            Stop-Validation "Unexpected content before the first package at installer.yaml line $lineNumber."
        }

        if ($line -match '^    (RequiredApiVersion|ReleaseDate|PackageUrl):\s*(\S.*?)\s*$') {
            $fieldName = $Matches[1]
            $fieldValue = $Matches[2]
            if (-not $currentPackage['Seen'].Add($fieldName)) {
                Stop-Validation "Duplicate $fieldName in installer package beginning at line $($currentPackage['StartLine'])."
            }
            $currentPackage[$fieldName] = $fieldValue
            $currentPackage['InChangelog'] = $false
            continue
        }

        if ($line -match '^    Changelog:\s*$') {
            if (-not $currentPackage['Seen'].Add('Changelog')) {
                Stop-Validation "Duplicate Changelog in installer package beginning at line $($currentPackage['StartLine'])."
            }
            $currentPackage['ChangelogDeclared'] = $true
            $currentPackage['InChangelog'] = $true
            continue
        }

        if ($line -match '^      -\s+(.+?)\s*$') {
            if (-not $currentPackage['InChangelog']) {
                Stop-Validation "Changelog entry without a Changelog field at installer.yaml line $lineNumber."
            }
            $entry = $Matches[1]
            if ([string]::IsNullOrWhiteSpace($entry)) {
                Stop-Validation "Empty changelog entry at installer.yaml line $lineNumber."
            }
            $currentPackage['Changelog'].Add($entry)
            continue
        }

        Stop-Validation "Unexpected structure or indentation at installer.yaml line ${lineNumber}: $line"
    }

    if ($packages.Count -eq 0) {
        Stop-Validation 'installer.yaml Packages must contain at least one package.'
    }

    return [pscustomobject]@{
        AddonId = $addonId
        Packages = $packages
    }
}

$script:Root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$extensionPath = Join-Path $script:Root 'source\extension.yaml'
$assemblyInfoPath = Join-Path $script:Root 'source\Properties\AssemblyInfo.cs'
$installerPath = Join-Path $script:Root 'installer.yaml'

foreach ($requiredPath in @($extensionPath, $assemblyInfoPath, $installerPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        Stop-Validation "Required file not found: $requiredPath"
    }
}

Write-Host 'Checking changed-line whitespace...'
$validSha = '^[0-9a-fA-F]{40}$'
$allZeroSha = '^0{40}$'
if ($EventName -eq 'pull_request' -and $BaseSha -match $validSha) {
    Invoke-GitCheck -Arguments @('diff', '--check', "$BaseSha...HEAD")
}
elseif ($EventName -eq 'push' -and $Ref.StartsWith('refs/heads/', [System.StringComparison]::Ordinal) -and
        $BeforeSha -match $validSha -and $BeforeSha -notmatch $allZeroSha) {
    Invoke-GitCheck -Arguments @('diff', '--check', "$BeforeSha..HEAD")
}
else {
    & git -C $script:Root rev-parse --verify 'HEAD^' *> $null
    if ($LASTEXITCODE -eq 0) {
        Invoke-GitCheck -Arguments @('diff', '--check', 'HEAD^..HEAD')
    }
    else {
        Invoke-GitCheck -Arguments @('show', '--check', '--format=', 'HEAD')
    }
}

Write-Host 'Checking tracked-file hygiene...'
$trackedFiles = & git -C $script:Root ls-files
if ($LASTEXITCODE -ne 0) {
    Stop-Validation 'Unable to enumerate tracked files.'
}

$forbiddenTracked = [System.Collections.Generic.List[string]]::new()
foreach ($trackedFile in $trackedFiles) {
    $normalized = $trackedFile.Replace('\', '/')
    if ($normalized -match '(?i)(^|/)(bin|obj|packages|\.vs|artifacts?|release|debug)(/|$)' -or
        $normalized -match '(?i)\.(pext|pdb|dll|exe|nupkg|snupkg|log|dmp|tmp)$') {
        $forbiddenTracked.Add($normalized)
    }
}
if ($forbiddenTracked.Count -gt 0) {
    Stop-Validation "Tracked build or release outputs found:`n$($forbiddenTracked -join "`n")"
}

Write-Host 'Checking extension and assembly versions...'
$extensionContent = [System.IO.File]::ReadAllText($extensionPath)
$extensionId = Get-SingleMatchValue -Content $extensionContent -Pattern '(?m)^Id:\s*(\S+)\s*$' -Description 'extension Id'
$extensionVersion = Get-SingleMatchValue -Content $extensionContent -Pattern '(?m)^Version:\s*(\S+)\s*$' -Description 'extension Version'
if (-not (Test-ThreePartVersion -Value $extensionVersion)) {
    Stop-Validation "Extension version must use X.Y.Z with no leading zeroes: $extensionVersion"
}

$assemblyInfoContent = [System.IO.File]::ReadAllText($assemblyInfoPath)
$assemblyVersion = Get-SingleMatchValue -Content $assemblyInfoContent -Pattern '(?m)^\[assembly:\s*AssemblyVersion\("([^"]+)"\)\]\s*$' -Description 'AssemblyVersion attribute'
$assemblyFileVersion = Get-SingleMatchValue -Content $assemblyInfoContent -Pattern '(?m)^\[assembly:\s*AssemblyFileVersion\("([^"]+)"\)\]\s*$' -Description 'AssemblyFileVersion attribute'
$expectedAssemblyVersion = "$extensionVersion.0"
if ($assemblyVersion -cne $expectedAssemblyVersion) {
    Stop-Validation "AssemblyVersion $assemblyVersion does not match $expectedAssemblyVersion."
}
if ($assemblyFileVersion -cne $expectedAssemblyVersion) {
    Stop-Validation "AssemblyFileVersion $assemblyFileVersion does not match $expectedAssemblyVersion."
}

Write-Host 'Checking installer manifest structure and history...'
$installer = Read-InstallerManifest -Path $installerPath
if ($installer.AddonId -cne $extensionId) {
    Stop-Validation "Installer AddonId $($installer.AddonId) does not match extension Id $extensionId."
}

$knownVersions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$previousVersion = $null
for ($packageIndex = 0; $packageIndex -lt $installer.Packages.Count; $packageIndex++) {
    $package = $installer.Packages[$packageIndex]
    $packageVersion = [string]$package['Version']
    if (-not (Test-ThreePartVersion -Value $packageVersion)) {
        Stop-Validation "Installer package version must use X.Y.Z with no leading zeroes: $packageVersion"
    }
    if (-not $knownVersions.Add($packageVersion)) {
        Stop-Validation "Duplicate installer package version: $packageVersion"
    }

    foreach ($requiredField in @('RequiredApiVersion', 'ReleaseDate', 'PackageUrl')) {
        if ([string]::IsNullOrWhiteSpace([string]$package[$requiredField])) {
            Stop-Validation "Package $packageVersion is missing $requiredField."
        }
    }
    if (-not $package['ChangelogDeclared'] -or $package['Changelog'].Count -eq 0) {
        Stop-Validation "Package $packageVersion must contain a nonempty Changelog list."
    }

    $apiVersionValue = [string]$package['RequiredApiVersion']
    $parsedApiVersion = $null
    if ($apiVersionValue -notmatch '^[0-9]+\.[0-9]+(?:\.[0-9]+){0,2}$' -or
        -not [version]::TryParse($apiVersionValue, [ref]$parsedApiVersion)) {
        Stop-Validation "Package $packageVersion has invalid RequiredApiVersion: $apiVersionValue"
    }

    $releaseDateValue = [string]$package['ReleaseDate']
    $parsedReleaseDate = [datetime]::MinValue
    if ($releaseDateValue -notmatch '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' -or
        -not [datetime]::TryParseExact(
            $releaseDateValue,
            'yyyy-MM-dd',
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::None,
            [ref]$parsedReleaseDate)) {
        Stop-Validation "Package $packageVersion has invalid ReleaseDate: $releaseDateValue"
    }

    $underscoredVersion = $packageVersion.Replace('.', '_')
    $expectedUrl = "https://github.com/felipeaucc/playnite-gameroutines-plugin/releases/download/v$packageVersion/$extensionId`_$underscoredVersion.pext"
    if ([string]$package['PackageUrl'] -cne $expectedUrl) {
        Stop-Validation "Package $packageVersion URL must be exactly $expectedUrl"
    }

    $parsedPackageVersion = [version]$packageVersion
    if ($null -ne $previousVersion -and $previousVersion.CompareTo($parsedPackageVersion) -le 0) {
        Stop-Validation "Installer packages must be in strict descending order: $previousVersion then $parsedPackageVersion."
    }
    $previousVersion = $parsedPackageVersion
}

$currentInstallerVersion = [string]$installer.Packages[0]['Version']
if ($currentInstallerVersion -cne $extensionVersion) {
    Stop-Validation "Current installer version $currentInstallerVersion does not match extension version $extensionVersion."
}

if ($Ref.StartsWith('refs/tags/', [System.StringComparison]::Ordinal)) {
    if ($Ref -notmatch '^refs/tags/v((0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*))$') {
        Stop-Validation "Version tag must use exact vX.Y.Z syntax: $Ref"
    }
    if ($Matches[1] -cne $extensionVersion) {
        Stop-Validation "Tag version $($Matches[1]) does not match extension version $extensionVersion."
    }
}

if (-not $SkipCleanCheck) {
    Write-Host 'Checking that tracked files remain unchanged...'
    $status = & git -C $script:Root status --porcelain --untracked-files=no
    if ($LASTEXITCODE -ne 0) {
        Stop-Validation 'Unable to inspect repository status.'
    }
    if ($status) {
        Stop-Validation "Tracked files are modified after validation:`n$($status -join "`n")"
    }
}

$shortSha = (& git -C $script:Root rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    Stop-Validation 'Unable to determine the current commit SHA.'
}

if (-not [string]::IsNullOrWhiteSpace($OutputFile)) {
    Add-Content -LiteralPath $OutputFile -Encoding utf8 -Value "version=$extensionVersion"
    Add-Content -LiteralPath $OutputFile -Encoding utf8 -Value "addon_id=$extensionId"
    Add-Content -LiteralPath $OutputFile -Encoding utf8 -Value "short_sha=$shortSha"
}

Write-Host "Repository validation passed for Game Routines $extensionVersion."

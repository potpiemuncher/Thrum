<#
.SYNOPSIS
    Builds the Thrum installer (Thrum_<version>_x64_setup.exe) with Inno Setup.

.DESCRIPTION
    Compiles installer\Thrum.iss from the packaged Thrum folder that
    utils\post-build.py produces (bin\x64\Release\Thrum). The CI and release
    workflows run it after packaging; it also works on any Windows PC with
    Inno Setup 6.3 or later.

    -InstallInnoSetup installs Inno Setup with Chocolatey when ISCC.exe is not
    found. The workflows pass it; on a PC, install Inno Setup yourself from
    https://jrsoftware.org/isdl.php instead.

.EXAMPLE
    .\utils\build-installer.ps1 -PackageDir .\bin\x64\Release\Thrum -Version 0.9.0-beta.3 -OutputDir .\bin\x64\Release
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDir,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$BinaryVersion = '',
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,
    [string]$OutputBaseFilename = '',
    [switch]$InstallInnoSetup
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$issPath = Join-Path -Path $repoRoot -ChildPath 'installer\Thrum.iss'
$package = (Resolve-Path -LiteralPath $PackageDir).Path
if (-not (Test-Path -LiteralPath (Join-Path -Path $package -ChildPath 'Thrum.exe'))) {
    throw "No Thrum.exe in $package. Pass the packaged Thrum folder (bin\x64\Release\Thrum)."
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDir).Path

if (-not $OutputBaseFilename) {
    $OutputBaseFilename = "Thrum_${Version}_x64_setup"
}

# VersionInfoVersion takes up to four numbers: the numeric part of the
# version, padded (0.9.0-beta.3 -> 0.9.0.0). A CI version (a date) gives 0.0.0.0.
if (-not $BinaryVersion) {
    $BinaryVersion = '0.0.0.0'
    if ($Version -match '^(\d+(\.\d+){0,3})(-|$)') {
        $BinaryVersion = $Matches[1]
    }
}
$parts = @($BinaryVersion.Split('.'))
while ($parts.Count -lt 4) {
    $parts += '0'
}
$BinaryVersion = ($parts | Select-Object -First 4) -join '.'

# Where the Inno Setup 6 installer puts it, for all users or for one.
$innoDirs = @(
    @(${env:ProgramFiles(x86)}, $env:ProgramFiles) |
        Where-Object { $_ } |
        ForEach-Object { Join-Path -Path $_ -ChildPath 'Inno Setup 6' }
    @($env:LOCALAPPDATA) |
        Where-Object { $_ } |
        ForEach-Object { Join-Path -Path $_ -ChildPath 'Programs\Inno Setup 6' }
)
$iscc = $innoDirs |
    ForEach-Object { Join-Path -Path $_ -ChildPath 'ISCC.exe' } |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $iscc) {
    $command = Get-Command -Name 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) {
        $iscc = $command.Source
    }
}
if (-not $iscc -and $InstallInnoSetup) {
    choco install innosetup --yes --no-progress
    if ($LASTEXITCODE -ne 0) {
        throw "Installing Inno Setup failed (choco exit code $LASTEXITCODE)."
    }
    $iscc = $innoDirs |
        ForEach-Object { Join-Path -Path $_ -ChildPath 'ISCC.exe' } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}
if (-not $iscc) {
    throw 'Inno Setup 6 (ISCC.exe) was not found. Install it from https://jrsoftware.org/isdl.php, or pass -InstallInnoSetup.'
}

Write-Output "Compiling $issPath with $iscc"
& $iscc "/DAppVersion=$Version" "/DBinaryVersion=$BinaryVersion" "/DPackageDir=$package" `
    "/DOutputDir=$output" "/DOutputBaseFilename=$OutputBaseFilename" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE."
}

$setup = Join-Path -Path $output -ChildPath "$OutputBaseFilename.exe"
if (-not (Test-Path -LiteralPath $setup)) {
    throw "ISCC reported success but $setup is missing."
}

$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
Write-Output "Built $setup"
Write-Output "SHA-256 $hash"

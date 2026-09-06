# Signs a Thrum release archive's executables, timestamps them, and verifies the
# result. Written for a hardware-token or certificate-store signing certificate,
# which is the common case for an individual open-source publisher: the private
# key never leaves the token, and because the token must be physically present,
# this cannot run in CI. That is why signing is a local step in the release
# procedure rather than a workflow job. See docs/dev/ADR-0005-code-signing.md.
#
#   .\extras\sign-release.ps1 -Archive .\Thrum_0.9.0-beta.1_x64.zip `
#       -Thumbprint <cert thumbprint>
#
# Add -WhatIf to see what would be signed without signing anything.
#
# The script refuses rather than guesses: no certificate, no timestamp server,
# or a verification failure all stop it with the reason, and it never leaves a
# half-signed archive in place of the original.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Release archive produced by the release workflow.
    [Parameter(Mandatory)][string]$Archive,

    # SHA-1 thumbprint of the signing certificate, as shown by
    # Get-ChildItem Cert:\CurrentUser\My. With a token, insert it first.
    [Parameter(Mandatory)][string]$Thumbprint,

    # RFC 3161 timestamp authority. Timestamping is not optional: without it
    # every signature stops validating the day the certificate expires.
    [string]$TimestampUrl = "http://timestamp.digicert.com",

    # Only these are signed. Satellite resource assemblies and the .NET runtime
    # files are Microsoft's and already carry their own signatures; re-signing
    # them would be both pointless and wrong.
    [string[]]$SignPatterns = @("Thrum.exe", "Thrum.dll"),

    [string]$SignToolPath
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    if ($SignToolPath) {
        if (-not (Test-Path -LiteralPath $SignToolPath)) {
            throw "signtool.exe not found at the path given: $SignToolPath"
        }
        return $SignToolPath
    }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin")
    $candidates = foreach ($root in $roots) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -Recurse -Filter signtool.exe `
                -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match "\\x64\\" }
        }
    }

    # Newest SDK wins; older signtool builds predate some RFC 3161 switches.
    $chosen = $candidates | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $chosen) {
        throw ("signtool.exe was not found. Install the Windows SDK, or pass " +
            "-SignToolPath. Signing cannot be faked, so this is fatal.")
    }
    return $chosen.FullName
}

function Assert-Certificate([string]$thumb) {
    $normalized = ($thumb -replace "[^0-9A-Fa-f]", "").ToUpperInvariant()
    $cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $normalized } | Select-Object -First 1
    if (-not $cert) {
        throw ("No certificate with thumbprint $normalized is available. If " +
            "it lives on a hardware token, insert the token and make sure its " +
            "middleware has published the certificate to your store.")
    }
    if (-not $cert.HasPrivateKey) {
        throw ("The certificate $normalized has no private key in this store, " +
            "so it cannot sign. A public certificate alone is not enough.")
    }

    $daysLeft = [int]($cert.NotAfter - (Get-Date)).TotalDays
    Write-Host ("Certificate: " + $cert.Subject)
    Write-Host ("Expires:     " + $cert.NotAfter.ToString("yyyy-MM-dd") +
        " ($daysLeft days)")
    if ($daysLeft -lt 0) {
        throw "That certificate expired on $($cert.NotAfter.ToString('yyyy-MM-dd'))."
    }
    if ($daysLeft -lt 30) {
        Write-Warning ("It expires in $daysLeft days. Timestamped signatures " +
            "stay valid past expiry, but you will not be able to sign after it.")
    }
    return $cert
}

$signtool = Find-SignTool
Write-Host "signtool:    $signtool"
Assert-Certificate -thumb $Thumbprint | Out-Null

$archiveItem = Get-Item -LiteralPath $Archive
$work = Join-Path ([IO.Path]::GetTempPath()) ("thrum-sign-" +
    [Guid]::NewGuid().ToString("N"))
$extract = Join-Path $work "unpacked"
New-Item -ItemType Directory -Path $extract -Force | Out-Null

try {
    Expand-Archive -LiteralPath $archiveItem.FullName -DestinationPath $extract -Force

    $targets = foreach ($pattern in $SignPatterns) {
        Get-ChildItem -LiteralPath $extract -Recurse -Filter $pattern -File `
            -ErrorAction SilentlyContinue
    }
    $targets = $targets | Sort-Object FullName -Unique
    if (-not $targets) {
        throw ("Nothing matched " + ($SignPatterns -join ", ") + " inside the " +
            "archive. Signing nothing and reporting success would be worse " +
            "than failing, so this is an error.")
    }

    Write-Host ""
    Write-Host "Will sign:"
    $targets | ForEach-Object {
        Write-Host ("  " + $_.FullName.Substring($extract.Length + 1))
    }

    if (-not $PSCmdlet.ShouldProcess($archiveItem.Name, "sign and repackage")) {
        Write-Host ""
        Write-Host "-WhatIf: nothing was signed and the archive is untouched."
        return
    }

    foreach ($target in $targets) {
        Write-Host ""
        Write-Host ("Signing " + $target.Name + " ...")
        & $signtool sign /sha1 $Thumbprint /fd SHA256 `
            /tr $TimestampUrl /td SHA256 /v $target.FullName
        if ($LASTEXITCODE -ne 0) {
            throw ("signtool failed on " + $target.Name + " with exit code " +
                $LASTEXITCODE + ". Nothing has been written back to the archive.")
        }
    }

    # Verify from the signature on disk, not from signtool's own exit code, so a
    # missing timestamp or an untrusted chain is caught here rather than by a user.
    Write-Host ""
    Write-Host "Verification:"
    $bad = @()
    foreach ($target in $targets) {
        $signature = Get-AuthenticodeSignature -LiteralPath $target.FullName
        $stamped = $null -ne $signature.TimeStamperCertificate
        Write-Host ("  " + $target.Name + ": " + $signature.Status +
            "  timestamped=" + $stamped)
        if ($signature.Status -ne "Valid") { $bad += ($target.Name + " status=" + $signature.Status) }
        if (-not $stamped) { $bad += ($target.Name + " has no timestamp") }
    }
    if ($bad.Count -gt 0) {
        throw ("Verification failed and the original archive is unchanged:`n  " +
            ($bad -join "`n  "))
    }

    # Repackage beside the original rather than over it. The published digest of
    # the unsigned archive stays true, and the signed archive gets its own.
    $signedArchive = Join-Path $archiveItem.DirectoryName `
        ($archiveItem.BaseName + "-signed" + $archiveItem.Extension)
    if (Test-Path -LiteralPath $signedArchive) {
        Remove-Item -LiteralPath $signedArchive -Force
    }
    Compress-Archive -Path (Join-Path $extract "*") -DestinationPath $signedArchive

    $digest = (Get-FileHash -LiteralPath $signedArchive -Algorithm SHA256).Hash
    Write-Host ""
    Write-Host "Signed archive: $signedArchive"
    Write-Host ("Size:           " + (Get-Item -LiteralPath $signedArchive).Length)
    Write-Host "SHA-256:        $digest"
    Write-Host ""
    Write-Host ("Publish that digest with the release. The original unsigned " +
        "archive was left untouched.")
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

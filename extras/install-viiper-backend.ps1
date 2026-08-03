<#
.SYNOPSIS
    Installs or repairs the VIIPER backend, and — only on explicit terms — the
    usbip-win2 kernel driver it depends on.

.DESCRIPTION
    Every decision that can end with something being executed, installed or
    replaced is made by the application, not by this script. The script fetches
    bytes, runs installers and swaps files; Thrum's installer policy decides
    whether it may. That split exists because the release manifest, the pinned
    digests and the driver gate already live in the application, are covered by
    its test suite, and must not be duplicated into a copy that then gets to
    decide whether a kernel driver is installed.

    Consequences worth stating plainly:

      * Nothing downloaded is executed before its SHA-256 — and, where the
        publisher signs, its Authenticode chain and signer — have been checked
        against a pinned identity.
      * Nothing newer is accepted just because it is newer. A usbip-win2 release
        this build does not recognise is left exactly as it is, and setup says
        so rather than "repairing" it.
      * The package pair Windows actually bound is validated after the driver
        step, through the same gate as the -viiperdriverdiagnostic switch.
      * No autostart entry is created. Thrum starts the backend when a profile
        needs it and stops it on exit. A pre-existing entry is reported, never
        adopted, and removed only when asked.
      * Every backend this script starts is started with the update notifier
        disabled.

.PARAMETER NoPause
    Do not wait for a key press at the end. Passed by Thrum.

.PARAMETER RemoveViiperAutostart
    Remove any pre-existing VIIPER logon entry (the HKCU Run value and/or the
    RunVIIPER task). Without this, an existing entry is only reported.

.PARAMETER AppExecutable
    Full path to the application executable that provides the installer policy.
    Defaults to the executable next to this script's parent folder. Setup
    refuses to continue without it: it is what performs every verification.

.PARAMETER UsbipInstallerFile
    Use an already-downloaded usbip-win2 installer instead of fetching it. The
    file is verified against the pin exactly as a download would be, so this is
    an offline convenience and a test hook, never a way past a check.

.PARAMETER ViiperBackendFile
    The same, for the VIIPER backend release archive.

.NOTES
    Exit codes: 0 success (driver pair validated), 1 refused or failed,
    3 installed but validation deferred until Windows restarts.
#>
param(
    [switch]$NoPause,
    [switch]$RemoveViiperAutostart,
    [string]$AppExecutable,
    [string]$UsbipInstallerFile,
    [string]$ViiperBackendFile
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$script:ExitCode = 0
$script:RebootRecommended = $false
$script:DriverValidated = $false
$script:Refused = $false
$script:InstallDir = Join-Path $env:LOCALAPPDATA "VIIPER"
$script:LogPath = Join-Path $script:InstallDir "install.log"
$script:TempDir = Join-Path ([IO.Path]::GetTempPath()) (
    "Thrum-VIIPER-Setup-" + [Guid]::NewGuid().ToString("N"))

# Kept in step with ProductInfo.ExeBaseName by a guard test; this script cannot
# read a C# constant, and the executable is what makes every check possible.
$script:DefaultAppExecutableName = "Thrum.exe"

# Inno Setup's silent switches, in one place so the driver installer can never be
# launched interactively by accident. /VERYSILENT suppresses the wizard and the
# progress window, /SUPPRESSMSGBOXES answers its message boxes, and /NORESTART keeps
# the reboot decision here rather than in the installer.
$script:UsbipSilentArguments = @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART")

function Write-SetupLog([string]$message, [ConsoleColor]$color =
        [ConsoleColor]::Gray) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host $message -ForegroundColor $color
    try {
        Add-Content -LiteralPath $script:LogPath -Value (
            "[$timestamp] $message") -Encoding UTF8
    }
    catch { }
}

function Write-Step([string]$message) {
    Write-Host ""
    Write-SetupLog "== $message ==" Cyan
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ConvertTo-VersionFromObject([object]$value) {
    if ($null -eq $value) { return $null }
    if ($value -is [Version]) { return $value }

    try {
        if ($value -is [string]) {
            $text = $value.Trim()
        }
        else {
            $text = [string]$value
            if ($null -eq $text) { return $null }
            $text = $text.Trim()
        }
    }
    catch { return $null }

    if ($text.Length -eq 0) { return $null }

    $parsed = $null
    if ([Version]::TryParse($text, [ref]$parsed)) {
        return $parsed
    }

    return $null
}

<#
    The only usbip-win2 probe this script still performs, and it answers one
    narrow question: does an uninstall entry claim a release label?

    It is deliberately not used to decide anything. The release label is a hint
    that something is registered even when no packages are bound; the identity
    decision belongs to the driver gate, which reads the packages Windows
    actually loaded. Reading usbip2_ude.sys's FileVersion, as this script used
    to, answers neither question: that file carries a DriverVer such as
    1.45.29.368, which is not the 0.9.7.x release label and compares greater
    than every floor anyone would write.
#>
function Get-UsbipRegisteredRelease {
    foreach ($root in @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )) {
        $entry = Get-ItemProperty $root -ErrorAction SilentlyContinue |
            Where-Object {
                $displayName = $_.DisplayName
                if ($null -eq $displayName) { return $false }
                $nameText = $displayName -as [string]
                return $nameText -match "USB/IP|USBip"
            } |
            Select-Object -First 1
        if ($entry -and $entry.DisplayVersion) {
            $version = ConvertTo-VersionFromObject $entry.DisplayVersion
            if ($version) { return $version.ToString() }
            return ([string]$entry.DisplayVersion).Trim()
        }
    }

    return ""
}

function Resolve-AppExecutable([string]$explicitPath) {
    if ($explicitPath) {
        if (Test-Path -LiteralPath $explicitPath) { return $explicitPath }
        throw "The application executable was not found at '$explicitPath'."
    }

    $candidate = Join-Path (Split-Path -Parent $PSScriptRoot) `
        $script:DefaultAppExecutableName
    if (Test-Path -LiteralPath $candidate) { return $candidate }

    throw (
        "Setup could not find $($script:DefaultAppExecutableName) next to the " +
        "'extras' folder. That executable performs every digest, signature and " +
        "driver-package check this script depends on, so setup stops here " +
        "rather than installing anything unverified. Run setup from inside the " +
        "application, or pass -AppExecutable.")
}

<#
    Runs one installer-policy verb and returns its exit code plus the key/value
    result. Fail-closed at every step: a missing helper, a missing result file,
    an unreadable result, or a result whose reported exit code disagrees with
    the process exit code all throw. Setup never proceeds on silence.
#>
function Invoke-InstallerPolicy([string[]]$policyArgs) {
    $outFile = Join-Path $script:TempDir (
        "policy-" + [Guid]::NewGuid().ToString("N") + ".txt")
    $arguments = @("-viiperinstallerpolicy") + $policyArgs + @("--out", $outFile)

    # Start-Process validates -ArgumentList as not-null-or-empty per element, so one
    # empty argument aborts setup before anything is verified. An absent option and an
    # empty one mean the same thing to the policy (ReadOption returns null either way),
    # so dropping empties here loses no information and removes the landmine.
    $quoted = @()
    foreach ($argument in $arguments) {
        if ([string]::IsNullOrEmpty($argument)) { continue }
        if ($argument -match '\s') { $quoted += ('"' + $argument + '"') }
        else { $quoted += $argument }
    }

    $process = Start-Process -FilePath $script:AppExecutable `
        -ArgumentList $quoted -Wait -PassThru -WindowStyle Hidden
    $exitCode = $process.ExitCode

    if (-not (Test-Path -LiteralPath $outFile)) {
        throw (
            "The verification helper produced no result for " +
            "'$($policyArgs -join ' ')' (exit code $exitCode). Setup cannot " +
            "continue without one.")
    }

    $data = @{}
    $reportedExit = $null
    foreach ($line in [IO.File]::ReadAllLines($outFile,
            [Text.Encoding]::UTF8)) {
        if ([string]::IsNullOrEmpty($line)) { continue }
        $separator = $line.IndexOf('=')
        if ($separator -lt 1) { continue }
        $key = $line.Substring(0, $separator)
        $value = $line.Substring($separator + 1)
        if ($key -eq "log") { Write-SetupLog $value }
        elseif ($key -eq "exitcode") { $reportedExit = $value }
        else { $data[$key] = $value }
    }

    if ($reportedExit -ne ([string]$exitCode)) {
        throw (
            "The verification helper's result does not match its exit code " +
            "(reported '$reportedExit', process $exitCode). Setup treats that " +
            "as unverified and stops.")
    }

    return @{ ExitCode = $exitCode; Data = $data }
}

function Invoke-Download([string]$url, [string]$outFile) {
    $lastError = $null
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Write-SetupLog "Downloading $url (attempt $attempt of 3)"
            Invoke-WebRequest -Uri $url -OutFile $outFile -UseBasicParsing `
                -TimeoutSec 60 -Headers @{ "User-Agent" =
                    "Thrum-VIIPER-Setup" }
            if (-not (Test-Path -LiteralPath $outFile) -or
                (Get-Item -LiteralPath $outFile).Length -le 0) {
                throw "The downloaded file was empty."
            }
            return
        }
        catch {
            $lastError = $_.Exception
            if ($attempt -lt 3) { Start-Sleep -Seconds $attempt }
        }
    }

    throw "Download failed after three attempts: $($lastError.Message)"
}

<#
    Fetches a pinned artefact and hands it to the verifier before anything is
    done with it. There is no code path that returns an unverified file: a
    refusal deletes the download and throws.

    A caller-supplied local file takes the place of the download and nothing
    else. It is copied in and verified against the same pin by the same call,
    so staging a corrupted or wrongly-signed artefact exercises the refusal
    rather than bypassing it — which is exactly what the VM run sheet's
    negative cases need.
#>
function Get-VerifiedPinnedFile([string]$component, [hashtable]$pins,
        [string]$destination, [string]$stagedFile) {
    $url = $pins["$component.url"]
    $expected = $pins["$component.sha256"]
    if (-not $url -or -not $expected) {
        throw "No pinned download is defined for '$component'."
    }

    Write-SetupLog (
        "Pinned $component release $($pins["$component.release"]): " +
        "$($pins["$component.filename"]), expected SHA-256 $expected.")

    if ($stagedFile) {
        if (-not (Test-Path -LiteralPath $stagedFile)) {
            throw "The staged $component file '$stagedFile' was not found."
        }
        Write-SetupLog (
            "Using a staged local file instead of downloading. It is verified " +
            "against the same pin.")
        Copy-Item -LiteralPath $stagedFile -Destination $destination -Force
    }
    else {
        Invoke-Download $url $destination
    }

    $verification = Invoke-InstallerPolicy @(
        "verify-file", "--component", $component, "--path", $destination)
    if ($verification.ExitCode -ne 0) {
        try {
            Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
        }
        catch { }
        throw (
            "$($verification.Data['summary']) " +
            "The downloaded file was discarded and nothing was run from it.")
    }

    Write-SetupLog $verification.Data['summary'] Green
}

function Expand-AndVerifyViiperPayload([hashtable]$pins,
        [string]$archivePath, [string]$extractionDir) {
    $payloadName = $pins['viiper.payload.filename']
    $payloadDigest = $pins['viiper.payload.sha256']
    $payloadSize = $pins['viiper.payload.size']
    if (-not $payloadName -or -not $payloadDigest -or -not $payloadSize) {
        throw "The VIIPER archive pin does not define a complete payload pin."
    }

    New-Item -ItemType Directory -Path $extractionDir -Force | Out-Null
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractionDir `
        -Force

    $payloadPath = Join-Path $extractionDir $payloadName
    $licensesPath = Join-Path $extractionDir "licenses.txt"
    if (-not (Test-Path -LiteralPath $licensesPath -PathType Leaf)) {
        throw (
            "The verified VIIPER archive did not contain licenses.txt. " +
            "Nothing from it was installed.")
    }

    $verification = Invoke-InstallerPolicy @(
        "verify-file", "--component", "viiper", "--scope", "payload",
        "--path", $payloadPath)
    if ($verification.ExitCode -ne 0) {
        try {
            Remove-Item -LiteralPath $extractionDir -Recurse -Force `
                -ErrorAction SilentlyContinue
        }
        catch { }
        throw (
            "$($verification.Data['summary']) " +
            "The extracted payload was discarded and nothing was installed " +
            "from the archive.")
    }

    Write-SetupLog $verification.Data['summary'] Green
    return @{
        ExecutablePath = $payloadPath
        LicensesPath = $licensesPath
    }
}

function Install-ViiperAtomically([string]$candidatePath,
        [string]$candidateLicensesPath, [string]$viiperPath) {
    $newPath = "$viiperPath.new"
    $backupPath = "$viiperPath.previous"
    $licensesPath = Join-Path (Split-Path -Parent $viiperPath) "licenses.txt"
    $newLicensesPath = "$licensesPath.new"
    $backupLicensesPath = "$licensesPath.previous"
    $hadViiper = Test-Path -LiteralPath $viiperPath
    $hadLicenses = Test-Path -LiteralPath $licensesPath
    Copy-Item -LiteralPath $candidatePath -Destination $newPath -Force
    Copy-Item -LiteralPath $candidateLicensesPath `
        -Destination $newLicensesPath -Force

    # An explicit repair/update may replace a running backend. Stop only the
    # VIIPER process and leave Thrum and every physical Bluetooth device
    # alone.
    $stopped = Stop-ViiperProcesses "backend replacement"
    if (-not $stopped) {
        throw "Unable to stop the currently running VIIPER process automatically during install. " +
              "Please close viiper.exe manually and try again."
    }
    Start-Sleep -Milliseconds 300

    try {
        if ($hadViiper) {
            [IO.File]::Replace($newPath, $viiperPath, $backupPath, $true)
        }
        else {
            Move-Item -LiteralPath $newPath -Destination $viiperPath -Force
        }

        if ($hadLicenses) {
            [IO.File]::Replace($newLicensesPath, $licensesPath,
                $backupLicensesPath, $true)
        }
        else {
            Move-Item -LiteralPath $newLicensesPath `
                -Destination $licensesPath -Force
        }
    }
    catch {
        if ($hadViiper -and (Test-Path -LiteralPath $backupPath)) {
            Copy-Item -LiteralPath $backupPath -Destination $viiperPath -Force
        }
        elseif (-not $hadViiper) {
            Remove-Item -LiteralPath $viiperPath -Force `
                -ErrorAction SilentlyContinue
        }
        if ($hadLicenses -and
            (Test-Path -LiteralPath $backupLicensesPath)) {
            Copy-Item -LiteralPath $backupLicensesPath `
                -Destination $licensesPath -Force
        }
        elseif (-not $hadLicenses) {
            Remove-Item -LiteralPath $licensesPath -Force `
                -ErrorAction SilentlyContinue
        }
        throw
    }
}

function Get-RunningViiperProcesses {
    try {
        Get-CimInstance Win32_Process -Filter "Name='viiper.exe'" -ErrorAction SilentlyContinue
    }
    catch {
        @()
    }
}

<#
    Stops every viiper.exe on the machine, retrying and escalating, and returns
    false rather than pretending.

    Worth knowing next to Thrum's runtime policy, which is the opposite: at
    runtime the application refuses to stop a backend it did not start or that
    is hosting a device. Here the rule is different on purpose — an install is
    an explicit, elevated, user-initiated act, and a running image cannot be
    replaced on Windows while it is held. The two policies are not in conflict;
    they answer different questions.
#>
function Stop-ViiperProcesses([string]$operation) {
    $attempts = 12
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        $processes = @(Get-RunningViiperProcesses)
        if ($processes.Count -eq 0) { return $true }

        if ($attempt -eq 1) {
            Write-SetupLog "Stopping VIIPER process(es) for $operation..." Yellow
        }

        foreach ($process in $processes) {
            if ($process.ProcessId -eq $PID) { continue }
            try {
                $identifier = if ($process.ExecutablePath) {
                    $process.ExecutablePath
                }
                else {
                    $process.ProcessId
                }
                Write-SetupLog "Stopping viiper PID=$($process.ProcessId) ($identifier)." Yellow
                Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
            }
            catch { }
        }

        Start-Sleep -Milliseconds 300

        $remaining = @(Get-RunningViiperProcesses)
        if ($remaining.Count -eq 0) { return $true }

        if ($attempt -ge 3) {
            foreach ($process in $remaining) {
                if ($process.ProcessId -eq $PID) { continue }
                try {
                    & taskkill.exe /PID $process.ProcessId /T /F | Out-Null
                }
                catch { }
            }
            Start-Sleep -Milliseconds 200
        }
    }

    Write-SetupLog (
        "A VIIPER process is still running after stop attempts. " +
        "Please close viiper.exe manually and rerun Install/Repair."
    ) Yellow
    return $false
}

function Test-ViiperApi([int]$timeoutMilliseconds = 1000) {
    $client = $null
    try {
        $client = [Net.Sockets.TcpClient]::new()
        $client.NoDelay = $true
        $client.SendTimeout = $timeoutMilliseconds
        $client.ReceiveTimeout = $timeoutMilliseconds
        $connect = $client.BeginConnect("127.0.0.1", 3242, $null, $null)
        if (-not $connect.AsyncWaitHandle.WaitOne($timeoutMilliseconds)) {
            return $false
        }
        $client.EndConnect($connect)
        $stream = $client.GetStream()
        $bytes = [Text.Encoding]::UTF8.GetBytes("ping`0")
        $stream.Write($bytes, 0, $bytes.Length)
        $buffer = New-Object byte[] 512
        $read = $stream.Read($buffer, 0, $buffer.Length)
        if ($read -le 0) { return $false }
        $response = [Text.Encoding]::UTF8.GetString($buffer, 0, $read)
        return $response.IndexOf("VIIPER",
            [StringComparison]::OrdinalIgnoreCase) -ge 0
    }
    catch { return $false }
    finally { if ($client) { $client.Dispose() } }
}

<#
    Starts the backend for the verification step with the update notifier
    disabled.

    The argument vector is not written here: it comes from the same constant the
    application spawns with. VIIPER's bundled updater still points at the parent
    project's releases and its "Update Now" pipes a remote script into an
    elevated shell, so every path that starts a backend has to disable it —
    including this one, which is not an autostart entry and was missed by the
    runtime fix (issue #8).
#>
function Start-AndVerifyViiper([string]$viiperPath, [hashtable]$pins) {
    if (Test-ViiperApi) { return $true }

    $serverArgs = $pins['viiper.serverargs']
    if (-not $serverArgs) {
        throw "The backend start arguments were not reported by the policy helper."
    }

    Write-SetupLog "Starting the backend for verification: viiper.exe $serverArgs"
    $environmentName = $pins['viiper.updatenotifyenv']
    $environmentValue = $pins['viiper.updatenotifyvalue']
    if ($environmentName) {
        # Belt and braces, exactly as the application does it: the flag is what
        # takes effect, the variable is what a re-exec would inherit.
        Set-Item -Path ("Env:" + $environmentName) -Value $environmentValue
    }

    Start-Process -FilePath $viiperPath -ArgumentList $serverArgs `
        -WindowStyle Hidden | Out-Null
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        Start-Sleep -Milliseconds 500
        if (Test-ViiperApi) { return $true }
    }
    return $false
}

try {
    if (-not (Test-Administrator)) {
        throw "Administrator permission is required. Launch setup from Thrum so Windows can request it automatically."
    }

    New-Item -ItemType Directory -Path $script:InstallDir -Force | Out-Null
    New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    Write-SetupLog ""
    Write-SetupLog "Thrum VIIPER virtual controller setup" Green
    Write-SetupLog "Installing or repairing VIIPER and usbip-win2."

    $script:AppExecutable = Resolve-AppExecutable $AppExecutable
    Write-SetupLog "Verification helper: $script:AppExecutable"

    Write-Step "Pinned packages"
    $pins = (Invoke-InstallerPolicy @("pins")).Data

    Write-Step "Checking usbip-win2"
    # On a machine with no usbip-win2 installed - the ordinary first-time case - there is
    # no registered release, so the option is omitted rather than passed empty.
    $registered = Get-UsbipRegisteredRelease
    $usbipPolicyArgs = @("usbip-decision")
    if (-not [string]::IsNullOrEmpty($registered)) {
        $usbipPolicyArgs += @("--uninstall-version", $registered)
    }
    $usbipDecision = Invoke-InstallerPolicy $usbipPolicyArgs
    $action = $usbipDecision.Data['action']

    switch ($action) {
        "InstallPinned" {
            $installerPath = Join-Path $script:TempDir $pins['usbip.filename']
            Get-VerifiedPinnedFile "usbip" $pins $installerPath $UsbipInstallerFile

            Write-SetupLog "Windows may briefly restart USB hub devices." Yellow
            # usbip-win2 ships an Inno Setup installer. "/S" is NSIS's silent switch;
            # Inno ignores unknown switches and shows its wizard, so setup would block
            # forever on a dialog the caller may never see. These are Inno's.
            $installer = Start-Process -FilePath $installerPath `
                -ArgumentList $script:UsbipSilentArguments -PassThru -Wait
            if ($installer.ExitCode -notin @(0, 1641, 3010)) {
                throw "usbip-win2 setup failed with exit code $($installer.ExitCode)."
            }
            if ($installer.ExitCode -in @(1641, 3010)) {
                $script:RebootRecommended = $true
                Write-SetupLog "The installer asked for a Windows restart." Yellow
            }
        }
        "AlreadyPinned" {
            Write-SetupLog $usbipDecision.Data['summary'] Green
        }
        "LeaveRecognisedReleaseAlone" {
            Write-SetupLog $usbipDecision.Data['summary'] Yellow
        }
        "RefuseUnrecognisedInstall" {
            $script:Refused = $true
            Write-SetupLog $usbipDecision.Data['summary'] Red
        }
        default {
            # An action nobody wrote a branch for is not a licence to guess.
            throw (
                "The installer policy returned an unrecognised usbip-win2 " +
                "action '$action'. Setup stops rather than guessing what it " +
                "means.")
        }
    }

    Write-Step "Validating the installed driver packages"
    $validation = Invoke-InstallerPolicy @("validate-installed")
    if ($validation.ExitCode -eq 0) {
        $script:DriverValidated = $true
        Write-SetupLog $validation.Data['summary'] Green
    }
    else {
        Write-SetupLog $validation.Data['summary'] Yellow
        if (-not $script:Refused -and $action -eq "InstallPinned") {
            # A pair that is not bound yet is the ordinary outcome of installing
            # a kernel driver, not evidence of a bad one.
            $script:RebootRecommended = $true
            Write-SetupLog (
                "Restart Windows, then run Install / Repair again so the " +
                "installed packages can be validated.") Yellow
        }
    }

    Write-Step "Installing VIIPER"
    $viiperPath = Join-Path $script:InstallDir "viiper.exe"
    $archivePath = Join-Path $script:TempDir $pins['viiper.filename']
    Get-VerifiedPinnedFile "viiper" $pins $archivePath $ViiperBackendFile
    $extractionDir = Join-Path $script:TempDir "viiper-extracted"
    $payload = Expand-AndVerifyViiperPayload $pins $archivePath $extractionDir
    Install-ViiperAtomically $payload.ExecutablePath $payload.LicensesPath `
        $viiperPath
    Write-SetupLog (
        "VIIPER and its licenses.txt were installed to $script:InstallDir") Green

    Write-Step "Startup behaviour"
    # No autostart entry is created here, by either mechanism. Thrum starts the
    # backend when a profile needs it and stops it again on exit, so a logon
    # entry would start a backend the application never owns, never stops, and
    # whose self-updater is enabled.
    $autostartArgs = @("autostart")
    if ($RemoveViiperAutostart) { $autostartArgs += "--remove" }
    $autostart = Invoke-InstallerPolicy $autostartArgs
    if ($autostart.ExitCode -ne 0) {
        Write-SetupLog $autostart.Data['summary'] Yellow
    }
    elseif ($autostart.Data['action'] -eq "OfferRemoval") {
        Write-SetupLog $autostart.Data['summary'] Yellow
        Write-SetupLog (
            "To remove it now, rerun setup with -RemoveViiperAutostart, or use " +
            "Settings -> VIIPER in Thrum.") Yellow
    }
    else {
        Write-SetupLog $autostart.Data['summary']
    }

    Write-Step "Verification"
    if (Start-AndVerifyViiper $viiperPath $pins) {
        Write-SetupLog "VIIPER API is ready." Green
        # The .previous backup is kept deliberately. Rollback that only exists
        # inside the install window is not rollback: the failure this protects
        # against is a backend that installs cleanly and then misbehaves.
        $backupPath = "$viiperPath.previous"
        if (Test-Path -LiteralPath $backupPath) {
            Write-SetupLog (
                "The previous backend was kept at $backupPath for rollback.")
        }
    }
    elseif ($script:RebootRecommended) {
        Write-SetupLog "VIIPER is installed; restart Windows to finish driver setup." Yellow
    }
    else {
        throw "VIIPER installed, but its local API did not start. See $script:LogPath"
    }

    if ($script:Refused) {
        $script:ExitCode = 1
        Write-SetupLog (
            "Setup finished, but the usbip-win2 packages on this machine are " +
            "not ones this build recognises. Virtual controllers stay blocked " +
            "until that is resolved; nothing was installed over them.") Red
    }
    elseif ($script:DriverValidated) {
        $script:ExitCode = 0
        Write-SetupLog "Setup complete. VIIPER is ready for Thrum." Green
    }
    elseif ($script:RebootRecommended) {
        $script:ExitCode = 3
        Write-SetupLog (
            "Setup complete. Restart Windows once, then run Install / Repair " +
            "again so the driver packages can be validated.") Yellow
    }
    else {
        $script:ExitCode = 1
        Write-SetupLog (
            "Setup finished, but the installed driver packages could not be " +
            "validated. Virtual controllers stay blocked until they are.") Red
    }
}
catch {
    $script:ExitCode = 1
    Write-Host ""
    Write-SetupLog "Setup could not finish: $($_.Exception.Message)" Red
    Write-SetupLog "Details were saved to $script:LogPath" Yellow
}
finally {
    if (Test-Path -LiteralPath $script:TempDir) {
        Remove-Item -LiteralPath $script:TempDir -Recurse -Force `
            -ErrorAction SilentlyContinue
    }
    if (-not $NoPause) {
        Write-Host ""
        Read-Host "Press Enter to close"
    }
}

exit $script:ExitCode

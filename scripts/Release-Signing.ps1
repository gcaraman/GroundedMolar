Set-StrictMode -Version Latest

function Invoke-ReleaseDotNet {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $dotnetPath
    $startInfo.UseShellExecute = $false
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $startInfo.Environment['APPDATA'] = Join-Path $RepositoryRoot '.appdata'
    $startInfo.Environment['DOTNET_CLI_HOME'] = Join-Path $RepositoryRoot '.dotnet-cli'
    $startInfo.Environment['NUGET_PACKAGES'] = Join-Path $RepositoryRoot '.nuget\packages'
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if (-not $process) { throw 'Failed to start dotnet.' }
    try {
        $process.WaitForExit()
        return $process.ExitCode
    }
    finally { $process.Dispose() }
}

function Find-ReleaseSignTool {
    param([string]$ExplicitPath, [string]$RepositoryRoot)

    if ($ExplicitPath) {
        $resolved = Resolve-Path -LiteralPath $ExplicitPath -ErrorAction Stop
        return $resolved.Path
    }

    $candidateRoots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\ArtifactSigningClientTools'),
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
        (Join-Path $RepositoryRoot '.nuget\packages\microsoft.windows.sdk.buildtools\10.0.28000.2526')
    )
    foreach ($candidateRoot in $candidateRoots) {
        if (-not $candidateRoot -or -not (Test-Path -LiteralPath $candidateRoot)) { continue }
        $candidate = Get-ChildItem -LiteralPath $candidateRoot -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '[\\/]x64[\\/]' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }

    $toolProject = Join-Path $RepositoryRoot 'build\ReleaseTools.csproj'
    $exitCode = Invoke-ReleaseDotNet -RepositoryRoot $RepositoryRoot -Arguments @(
        'restore', $toolProject,
        '--configfile', (Join-Path $RepositoryRoot 'build\NuGet.Config'),
        '--locked-mode')
    if ($exitCode -ne 0) { throw 'Failed to restore the pinned Windows SDK signing tools.' }
    $packageRoot = Join-Path $RepositoryRoot '.nuget\packages\microsoft.windows.sdk.buildtools\10.0.28000.2526'
    $restored = Get-ChildItem -LiteralPath $packageRoot -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]x64[\\/]' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $restored) { throw 'The pinned Windows SDK package did not contain x64 SignTool.' }
    return $restored.FullName
}

function Invoke-ReleaseSignature {
    param(
        [Parameter(Mandatory = $true)][string]$SignToolPath,
        [Parameter(Mandatory = $true)][ValidateSet('CertificateStore', 'ArtifactSigning')][string]$SigningMode,
        [Parameter(Mandatory = $true)][string[]]$Path,
        [string]$CertificateThumbprint,
        [ValidateSet('CurrentUser', 'LocalMachine')][string]$CertificateStoreScope = 'CurrentUser',
        [string]$ArtifactSigningMetadataPath,
        [string]$ArtifactSigningDlibPath,
        [string]$TimestampUrl = 'http://timestamp.acs.microsoft.com'
    )

    if ($Path.Count -eq 0) { return }
    $arguments = @('sign', '/v', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', '/d', 'MolarMap')
    if ($SigningMode -eq 'CertificateStore') {
        if (-not $CertificateThumbprint) { throw 'CertificateStore signing requires -CertificateThumbprint.' }
        $normalizedThumbprint = $CertificateThumbprint -replace '[^0-9A-Fa-f]', ''
        if ($normalizedThumbprint.Length -ne 40) { throw 'The certificate thumbprint must contain exactly 40 hexadecimal characters.' }
        $arguments += @('/sha1', $normalizedThumbprint)
        if ($CertificateStoreScope -eq 'LocalMachine') { $arguments += '/sm' }
    }
    else {
        if (-not $ArtifactSigningMetadataPath -or -not $ArtifactSigningDlibPath) {
            throw 'ArtifactSigning requires -ArtifactSigningMetadataPath and -ArtifactSigningDlibPath.'
        }
        $metadata = (Resolve-Path -LiteralPath $ArtifactSigningMetadataPath -ErrorAction Stop).Path
        $dlib = (Resolve-Path -LiteralPath $ArtifactSigningDlibPath -ErrorAction Stop).Path
        $arguments += @('/dlib', $dlib, '/dmdf', $metadata)
    }
    $arguments += $Path
    & $SignToolPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Authenticode signing failed for: $($Path -join ', ')" }
}

function Assert-ValidAuthenticodeSignature {
    param([Parameter(Mandatory = $true)][string[]]$Path)

    foreach ($item in $Path) {
        $signature = Get-AuthenticodeSignature -LiteralPath $item
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Authenticode validation failed for ${item}: $($signature.Status) $($signature.StatusMessage)"
        }
        if (-not $signature.TimeStamperCertificate) {
            throw "The signature on $item has no trusted timestamp."
        }
    }
}

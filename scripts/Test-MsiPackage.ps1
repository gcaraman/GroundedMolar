param(
    [Parameter(Mandatory = $true)][string]$MsiPath,
    [Parameter(Mandatory = $true)][Version]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$resolvedMsi = (Resolve-Path -LiteralPath $MsiPath -ErrorAction Stop).Path
$extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("MolarMap-msi-test-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extractRoot | Out-Null
try {
    $arguments = @('/a', $resolvedMsi, '/qn', "TARGETDIR=$extractRoot", '/L*v', (Join-Path $extractRoot 'administrative-install.log'))
    $process = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\msiexec.exe') -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "MSI administrative extraction failed with exit code $($process.ExitCode)." }
    $executable = Get-ChildItem -LiteralPath $extractRoot -Filter MolarMap.exe -File -Recurse | Select-Object -First 1
    if (-not $executable) { throw 'The MSI did not contain MolarMap.exe.' }
    $actualVersion = [Version]([System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable.FullName).FileVersion -split '[+-]', 2)[0]
    if ($actualVersion -ne $ExpectedVersion) { throw "MSI payload version $actualVersion did not match $ExpectedVersion." }
    if (-not (Get-ChildItem -LiteralPath $extractRoot -Filter ooz.exe -File -Recurse | Select-Object -First 1)) {
        throw 'The MSI did not contain ooz.exe.'
    }

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @($resolvedMsi, 0))
    $view = $database.GetType().InvokeMember(
        'OpenView',
        'InvokeMethod',
        $null,
        $database,
        @('SELECT `Target`,`Icon_` FROM `Shortcut` WHERE `Shortcut` = ''MolarMapStartMenuShortcut'''))
    try {
        $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)
        $record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
        if (-not $record) { throw 'The MSI did not contain the MolarMap Start-menu shortcut.' }

        $shortcutTarget = $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @(1))
        $shortcutIcon = $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @(2))
        if ($shortcutTarget -ne 'MainFeature') {
            throw "The Start-menu shortcut does not advertise the MolarMap feature: $shortcutTarget"
        }
        if ($shortcutIcon -ne 'MolarMap.exe') {
            throw "The Start-menu shortcut does not reference its EXE-format icon resource: $shortcutIcon"
        }
    }
    finally {
        $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null)
    }

    Add-Type -AssemblyName System.Drawing
    $embeddedIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($executable.FullName)
    if (-not $embeddedIcon) { throw 'MolarMap.exe did not contain an icon for the installed shortcut.' }
    $embeddedIcon.Dispose()

    $iconView = $database.GetType().InvokeMember(
        'OpenView',
        'InvokeMethod',
        $null,
        $database,
        @('SELECT `Data` FROM `Icon` WHERE `Name` = ''MolarMap.exe'''))
    try {
        $iconView.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $iconView, $null)
        $iconRecord = $iconView.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $iconView, $null)
        if (-not $iconRecord) { throw 'The MSI did not contain the EXE-format shortcut icon resource.' }
        $iconSize = $iconRecord.GetType().InvokeMember('DataSize', 'GetProperty', $null, $iconRecord, @(1))
        if ($iconSize -ne $executable.Length) {
            throw "The MSI shortcut icon resource is incomplete: $iconSize bytes; expected $($executable.Length)."
        }
    }
    finally {
        $iconView.GetType().InvokeMember('Close', 'InvokeMethod', $null, $iconView, $null)
    }

    Write-Host "Verified MSI payload version $actualVersion, required executables, and EXE-format advertised-shortcut icon."
}
finally {
    if (Test-Path -LiteralPath $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force }
}

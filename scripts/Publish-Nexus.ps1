param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.1',
    [ValidateSet('None', 'CertificateStore', 'ArtifactSigning')]
    [string]$SigningMode = 'None',
    [string]$PublisherName,
    [string]$CertificateThumbprint,
    [ValidateSet('CurrentUser', 'LocalMachine')]
    [string]$CertificateStoreScope = 'CurrentUser',
    [string]$ArtifactSigningMetadataPath,
    [string]$ArtifactSigningDlibPath,
    [string]$SignToolPath,
    [string]$TimestampUrl = 'http://timestamp.acs.microsoft.com'
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Publish-Release.ps1') @PSBoundParameters

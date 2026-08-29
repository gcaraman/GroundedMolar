param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts\verification'
. (Join-Path $PSScriptRoot 'Release-Signing.ps1')

$buildExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
    'build', (Join-Path $repositoryRoot 'MolarMap.slnx'),
    '--configuration', $Configuration,
    '--artifacts-path', $artifactsRoot,
    '--configfile', (Join-Path $repositoryRoot 'NuGet.Config'))
if ($buildExitCode -ne 0) { throw 'Repository build failed.' }

$testExitCode = Invoke-ReleaseDotNet -RepositoryRoot $repositoryRoot -Arguments @(
    'run', '--project', (Join-Path $repositoryRoot 'tests\GroundedMolar.Tests'),
    '--configuration', $Configuration,
    '--no-build',
    '--artifacts-path', $artifactsRoot)
if ($testExitCode -ne 0) { throw 'Repository regression tests failed.' }

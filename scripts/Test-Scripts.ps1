# Copyright © 2026 myNOC LLC. All rights reserved.
#requires -Version 7.4
[CmdletBinding()]
param([string] $ResultsPath = 'TestResults/powershell.xml')

$ErrorActionPreference = 'Stop'
Import-Module Pester -RequiredVersion 5.7.1 -ErrorAction Stop
$configuration = New-PesterConfiguration
$configuration.Run.Path = Join-Path $PSScriptRoot '../tests/ci'
$configuration.Run.PassThru = $true
$configuration.Output.Verbosity = 'Detailed'
$configuration.TestResult.Enabled = $true
$configuration.TestResult.OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ResultsPath)
$result = Invoke-Pester -Configuration $configuration
if ($result.FailedCount -gt 0 -or $result.TotalCount -eq 0 -or $result.Result -ne 'Passed') { exit 1 }

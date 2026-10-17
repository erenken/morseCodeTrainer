# Copyright © 2026 myNOC LLC. All rights reserved.
#requires -Version 7.4
<# .SYNOPSIS
Fail closed on missing reports; gate total and changed shared C# and JavaScript lines.
#>
[CmdletBinding()]
param(
	[string] $Report = 'TestResults/**/coverage.cobertura.xml',
	[string] $Base,
	[string] $Lcov,
	[ValidateRange(0, 100)][double] $Minimum = 90
)

. "$PSScriptRoot/Invoke-CapturedCommand.ps1"

function ConvertTo-SourcePath {
	param([string] $Value)
	$value = $Value.Replace('\', '/')
	$index = $value.IndexOf('src/', [StringComparison]::Ordinal)
	if ($index -ge 0) { return $value.Substring($index) }
	return $value
}

function Test-CoverageScope {
	param([string] $Path)
	return ($Path.StartsWith('src/MorseCodeTrainer.UI/wwwroot/', [StringComparison]::Ordinal) -and $Path.EndsWith('.js')) -or
		($Path.EndsWith('.cs') -and -not $Path.Contains('/obj/') -and
		($Path.StartsWith('src/MorseCodeTrainer.Core/', [StringComparison]::Ordinal) -or
		$Path.StartsWith('src/MorseCodeTrainer.UI/Services/', [StringComparison]::Ordinal)))
}

function Find-CoverageReport {
	param([string] $Pattern)
	$fullPattern = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Pattern).Replace('\', '/')
	$wildcard = $fullPattern.IndexOfAny([char[]]'*?')
	if ($wildcard -lt 0) {
		if (Test-Path -LiteralPath $fullPattern -PathType Leaf) { Get-Item -LiteralPath $fullPattern }
		return
	}
	$root = $fullPattern.Substring(0, $fullPattern.LastIndexOf('/', $wildcard) + 1)
	# Unlike ordinary PowerShell wildcards, **/ includes zero or more directories.
	$expression = '^' + [regex]::Escape($fullPattern).Replace('\*\*/', '(?:.*/)?').Replace('\*', '[^/]*').Replace('\?', '[^/]') + '$'
	if (Test-Path -LiteralPath $root -PathType Container) {
		Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object { $_.FullName.Replace('\', '/') -cmatch $expression }
	}
}

function Read-CoberturaCoverage {
	param([string] $Pattern)
	$reports = @(Find-CoverageReport $Pattern)
	if ($reports.Count -eq 0) { throw 'No Cobertura report found. Tests must run with coverage enabled.' }
	$coverage = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
	foreach ($file in $reports) {
		$document = [System.Xml.XmlDocument]::new()
		$document.XmlResolver = $null
		$settings = [System.Xml.XmlReaderSettings]::new()
		$settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
		$reader = [System.Xml.XmlReader]::Create($file.FullName, $settings)
		try { $document.Load($reader) } finally { $reader.Dispose() }
		$sources = @($document.SelectNodes('//sources/source') | ForEach-Object { $_.InnerText })
		foreach ($item in $document.SelectNodes('//class')) {
			$path = ConvertTo-SourcePath $item.GetAttribute('filename')
			if (-not (Test-CoverageScope $path)) {
				foreach ($source in $sources) {
					$candidate = ConvertTo-SourcePath ($source.TrimEnd('/', '\') + '/' + $path)
					if (Test-CoverageScope $candidate) { $path = $candidate; break }
				}
			}
			if (-not (Test-CoverageScope $path)) { continue }
			if (-not $coverage.ContainsKey($path)) { $coverage[$path] = @{} }
			foreach ($line in $item.SelectNodes('./lines/line')) {
				$number = [int]$line.GetAttribute('number')
				$coverage[$path][$number] = $coverage[$path][$number] -or ([int]$line.GetAttribute('hits') -gt 0)
			}
		}
	}
	if (@($coverage.Values | Where-Object Count -gt 0).Count -eq 0) { throw 'Report contains no executable shared C# lines.' }
	return ,$coverage
}

function Add-LcovCoverage {
	param([Collections.IDictionary] $Coverage, [string] $Path)
	$source = $null
	$found = $false
	foreach ($line in [IO.File]::ReadAllLines($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))) {
		if ($line.StartsWith('SF:')) { $source = ConvertTo-SourcePath $line.Substring(3) }
		elseif ($line -eq 'end_of_record') { $source = $null }
		elseif ($source -and (Test-CoverageScope $source) -and $line.StartsWith('DA:')) {
			$fields = $line.Substring(3).Split(',')
			$number = $fields[0]
			$hits = $fields[1]
			if (-not $Coverage.ContainsKey($source)) { $Coverage[$source] = @{} }
			$Coverage[$source][[int]$number] = $Coverage[$source][[int]$number] -or ([int]$hits -gt 0)
			$found = $true
		}
	}
	if (-not $found) { throw 'No shared JavaScript coverage found in LCOV report.' }
}

function ConvertFrom-GitDiff {
	param([string] $Diff)
	$changed = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
	$path = $null
	foreach ($line in ($Diff -split '\r?\n')) {
		if ($line.StartsWith('+++ ')) {
			$path = if ($line.StartsWith('+++ b/')) { $line.Substring(6).TrimEnd("`t") } else { $null }
		}
		elseif ($path -and (Test-CoverageScope $path) -and $line.StartsWith('@@')) {
			$hunk = [regex]::Match($line, '\+(\d+)(?:,(\d+))? @@')
			if (-not $hunk.Success) { throw 'Could not parse a Git diff hunk.' }
			$start = [int]$hunk.Groups[1].Value
			$count = if ($hunk.Groups[2].Success) { [int]$hunk.Groups[2].Value } else { 1 }
			if (-not $changed.ContainsKey($path)) { $changed[$path] = [Collections.Generic.HashSet[int]]::new() }
			for ($number = $start; $number -lt $start + $count; $number++) { $null = $changed[$path].Add($number) }
		}
	}
	return ,$changed
}

function Test-CoverageThreshold {
	param([Collections.IDictionary] $Coverage, [AllowNull()][Collections.IDictionary] $Changed, [double] $Minimum)
	$totalCount = 0
	$totalHits = 0
	foreach ($lines in $Coverage.Values) {
		foreach ($hit in $lines.Values) { $totalCount++; if ($hit) { $totalHits++ } }
	}
	if ($totalCount -eq 0) { throw 'Report contains no executable shared code lines.' }
	$rate = 100.0 * $totalHits / $totalCount
	Write-Host ('Shared code line coverage: {0:F2}% ({1}/{2})' -f $rate, $totalHits, $totalCount)
	$passed = $rate -ge $Minimum
	if ($null -ne $Changed) {
		foreach ($path in $Changed.Keys) {
			if ($Changed[$path].Count -gt 0 -and -not $Coverage.ContainsKey($path)) {
				throw "Changed source files missing from coverage: $path"
			}
		}
		$patchCount = 0
		$patchHits = 0
		foreach ($path in $Coverage.Keys) {
			if (-not $Changed.ContainsKey($path)) { continue }
			foreach ($number in $Coverage[$path].Keys) {
				if ($Changed[$path].Contains([int]$number)) {
					$patchCount++
					if ($Coverage[$path][$number]) { $patchHits++ }
				}
			}
		}
		if ($patchCount -gt 0) {
			$patchRate = 100.0 * $patchHits / $patchCount
			Write-Host ('Changed shared code line coverage: {0:F2}% ({1}/{2})' -f $patchRate, $patchHits, $patchCount)
			$passed = $passed -and $patchRate -ge $Minimum
		}
		else { Write-Host 'No changed executable shared code lines; total coverage still enforced.' }
	}
	return $passed
}

function Invoke-CoverageGate {
	param([string] $Report, [string] $Base, [string] $Lcov, [double] $Minimum = 90)
	$coverage = Read-CoberturaCoverage $Report
	if ($Lcov) { Add-LcovCoverage $coverage $Lcov }
	$changed = $null
	if ($Base) {
		if ($Base -cnotmatch '^[a-fA-F0-9]{40,64}$') { throw 'Base must be a full commit SHA.' }
		$diff = Invoke-CapturedCommand git @('-c', 'core.quotepath=false', 'diff', '--no-ext-diff', '--no-textconv',
			'--no-renames', '--unified=0', "$Base...HEAD", '--', 'src')
		$changed = ConvertFrom-GitDiff $diff
	}
	return Test-CoverageThreshold $coverage $changed $Minimum
}

if ($MyInvocation.InvocationName -ne '.') {
	$ErrorActionPreference = 'Stop'
	try {
		if (-not (Invoke-CoverageGate -Report $Report -Base $Base -Lcov $Lcov -Minimum $Minimum)) { exit 1 }
	}
	catch {
		[Console]::Error.WriteLine("Coverage gate failed: $($_.Exception.Message)")
		exit 1
	}
}

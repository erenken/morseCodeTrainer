# Copyright © 2026 myNOC LLC. All rights reserved.
BeforeAll {
	$script:gatePath = Join-Path $PSScriptRoot '../../scripts/Check-Coverage.ps1'
	. $script:gatePath
	function Write-Report([string] $Path, [string] $Xml) {
		$null = New-Item -ItemType Directory -Force (Split-Path $Path -Parent)
		[IO.File]::WriteAllText($Path, $Xml)
	}
	$script:source = 'src/MorseCodeTrainer.Core/A.cs'
	$script:reportXml = '<coverage><class filename="src/MorseCodeTrainer.Core/A.cs"><lines><line number="1" hits="1"/></lines></class></coverage>'
}

Describe 'Shared code coverage gate' {
	It 'uses source roots for relative report paths' {
		$report = Join-Path $TestDrive 'coverage.xml'
		Write-Report $report '<coverage><sources><source>D:/repo/src/</source></sources><class filename="MorseCodeTrainer.Core/A.cs"><lines><line number="1" hits="1"/></lines></class></coverage>'
		$coverage = Read-CoberturaCoverage $report
		$coverage[$source][1] | Should -BeTrue
		$coverage.Count | Should -Be 1
	}

	It 'merges duplicate classes and reports and normalizes Windows paths' {
		Write-Report "$TestDrive/reports/one/coverage.xml" '<coverage><class filename="D:\repo\src\MorseCodeTrainer.Core\A.cs"><lines><line number="1" hits="0"/></lines></class><class filename="src/MorseCodeTrainer.Core/A.cs"><lines><line number="2" hits="0"/></lines></class></coverage>'
		Write-Report "$TestDrive/reports/two/nested/coverage.xml" '<coverage><class filename="src/MorseCodeTrainer.Core/A.cs"><lines><line number="1" hits="1"/></lines></class></coverage>'
		$coverage = Read-CoberturaCoverage "$TestDrive/reports/**/coverage.xml"
		$coverage[$source][1] | Should -BeTrue
		$coverage[$source][2] | Should -BeFalse
	}

	It 'matches recursive reports at the root and in nested directories' {
		Write-Report "$TestDrive/glob/coverage.xml" $reportXml
		Write-Report "$TestDrive/glob/one/two/coverage.xml" $reportXml
		@(Find-CoverageReport "$TestDrive/glob/**/coverage.xml").Count | Should -Be 2
	}

	It 'fails closed for absent, malformed, or out-of-scope reports' {
		{ Read-CoberturaCoverage "$TestDrive/missing.xml" } | Should -Throw '*No Cobertura*'
		Write-Report "$TestDrive/invalid.xml" '<coverage>'
		{ Read-CoberturaCoverage "$TestDrive/invalid.xml" } | Should -Throw
		Write-Report "$TestDrive/empty.xml" '<coverage><class filename="src/MorseCodeTrainer.Core/obj/A.cs"><lines><line number="1" hits="1"/></lines></class></coverage>'
		{ Read-CoberturaCoverage "$TestDrive/empty.xml" } | Should -Throw '*no executable*'
	}

	It 'rejects XML external entities' {
		Write-Report "$TestDrive/entity.xml" '<!DOCTYPE coverage [<!ENTITY test SYSTEM "file:///etc/passwd">]><coverage>&test;</coverage>'
		{ Read-CoberturaCoverage "$TestDrive/entity.xml" } | Should -Throw
	}

	It 'includes added lines and single-line hunks, but excludes deletions' {
		$diff = "+++ b/$source`n@@ -1,0 +2,3 @@`n+x`n@@ -7 +10 @@`n+x`n@@ -20,3 +22,0 @@"
		$changed = ConvertFrom-GitDiff $diff
		@($changed[$source] | Sort-Object) | Should -Be @(2, 3, 4, 10)
		{ ConvertFrom-GitDiff "+++ b/$source`n@@ invalid @@" } | Should -Throw '*diff hunk*'
	}

	It 'fails changed-code coverage even when total coverage passes' {
		$lines = @{}
		1..100 | ForEach-Object { $lines[$_] = $_ -lt 100 }
		Test-CoverageThreshold @{ $source = $lines } @{ $source = @(100) } 90 | Should -BeFalse
		Test-CoverageThreshold @{ $source = $lines } @{ $source = @(1) } 90 | Should -BeTrue
	}

	It 'accepts exactly 90 percent and rejects below 90 percent' {
		$lines = @{}
		1..10 | ForEach-Object { $lines[$_] = $_ -le 9 }
		Test-CoverageThreshold @{ $source = $lines } $null 90 | Should -BeTrue
		$lines[9] = $false
		Test-CoverageThreshold @{ $source = $lines } $null 90 | Should -BeFalse
	}

	It 'fails for a changed file absent from coverage' {
		{ Test-CoverageThreshold @{ $source = @{ 1 = $true } } @{ 'src/MorseCodeTrainer.Core/B.cs' = @(1) } 90 } | Should -Throw '*missing from coverage*'
	}

	It 'still enforces total coverage when no executable lines changed' {
		Test-CoverageThreshold @{ $source = @{ 1 = $false } } @{} 90 | Should -BeFalse
		Test-CoverageThreshold @{ $source = @{ 1 = $true } } @{ $source = @(2) } 90 | Should -BeTrue
		{ Test-CoverageThreshold @{} @{} 90 } | Should -Throw '*no executable*'
	}

	It 'merges JavaScript hits and checks changed JavaScript lines' {
		$report = Join-Path $TestDrive 'coverage.lcov'
		[IO.File]::WriteAllText($report, "SF:src/MorseCodeTrainer.UI/wwwroot/media.js`nDA:1,0`nDA:2,0`nend_of_record`nSF:src/MorseCodeTrainer.UI/wwwroot/media.js`nDA:1,1,checksum`nend_of_record`n")
		$coverage = @{}
		Add-LcovCoverage $coverage $report
		$js = 'src/MorseCodeTrainer.UI/wwwroot/media.js'
		$coverage[$js][1] | Should -BeTrue
		Test-CoverageThreshold $coverage @{ $js = @(2) } 90 | Should -BeFalse
		[IO.File]::WriteAllText($report, '')
		{ Add-LcovCoverage @{} $report } | Should -Throw '*No shared JavaScript*'
		{ Add-LcovCoverage @{} "$TestDrive/missing.lcov" } | Should -Throw
	}

	It 'rejects untrusted Git refs before invoking Git' {
		Write-Report "$TestDrive/base.xml" $reportXml
		Mock Invoke-CapturedCommand { throw 'Unexpected command.' }
		{ Invoke-CoverageGate -Report "$TestDrive/base.xml" -Base '--help' } | Should -Throw '*full commit SHA*'
		Should -Invoke Invoke-CapturedCommand -Times 0 -Exactly
	}

	It 'handles real UTF-8 Git diffs and source paths containing spaces' {
		$repository = Join-Path $TestDrive 'unicode repository'
		$null = New-Item -ItemType Directory -Force "$repository/src/MorseCodeTrainer.Core"
		Push-Location $repository
		try {
			function Invoke-TestGit([string[]] $CommandArguments) {
				Invoke-CapturedCommand git (@('-c', 'user.name=Coverage tests', '-c', 'user.email=coverage@example.invalid') + $CommandArguments)
			}
			$null = Invoke-TestGit @('init', '--initial-branch=main')
			$null = Invoke-TestGit @('commit', '--allow-empty', '-m', 'Base')
			$base = Invoke-TestGit @('rev-parse', 'HEAD')
			[IO.File]::WriteAllText("$repository/src/MorseCodeTrainer.Core/Éxample file.cs", 'var label = "Morse ✏";')
			$null = Invoke-TestGit @('add', 'src')
			$null = Invoke-TestGit @('commit', '-m', 'Unicode source')
			Write-Report "$repository/coverage.xml" '<coverage><class filename="src/MorseCodeTrainer.Core/Éxample file.cs"><lines><line number="1" hits="1"/></lines></class></coverage>'
			$output = Invoke-CapturedCommand pwsh @('-NoProfile', '-File', $gatePath, '-Report', "$repository/coverage.xml", '-Base', $base)
			$output | Should -Match 'Changed shared code line coverage: 100.00% \(1/1\)'
		}
		finally { Pop-Location }
	}

	It 'returns a failing process exit code for uncovered lines and missing reports' {
		Write-Report "$TestDrive/fail.xml" '<coverage><class filename="src/MorseCodeTrainer.Core/A.cs"><lines><line number="1" hits="0"/></lines></class></coverage>'
		{ Invoke-CapturedCommand pwsh @('-NoProfile', '-File', $gatePath, '-Report', "$TestDrive/fail.xml") } | Should -Throw
		{ Invoke-CapturedCommand pwsh @('-NoProfile', '-File', $gatePath, '-Report', "$TestDrive/absent.xml") } | Should -Throw
	}
}

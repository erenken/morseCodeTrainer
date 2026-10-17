# Copyright © 2026 myNOC LLC. All rights reserved.
#requires -Version 7.4

function Invoke-CapturedCommand {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory)][string] $Tool,
		[Parameter(Mandatory)][string[]] $Arguments,
		[AllowNull()][string] $StandardInput,
		[string] $WorkingDirectory = (Get-Location).Path
	)

	# Keep credentials out of command arguments, files, and failure diagnostics.
	$process = $null
	try {
		$command = Get-Command $Tool -CommandType Application -ErrorAction Stop | Select-Object -First 1
		$info = [System.Diagnostics.ProcessStartInfo]::new($command.Source)
		$info.UseShellExecute = $false
		$info.CreateNoWindow = $true
		$info.WorkingDirectory = $WorkingDirectory
		$info.RedirectStandardInput = $true
		$info.RedirectStandardOutput = $true
		$info.RedirectStandardError = $true
		$info.StandardInputEncoding = [System.Text.UTF8Encoding]::new($false)
		$info.StandardOutputEncoding = [System.Text.Encoding]::UTF8
		$info.StandardErrorEncoding = [System.Text.Encoding]::UTF8
		foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
		$process = [System.Diagnostics.Process]::Start($info)
		$stdout = $process.StandardOutput.ReadToEndAsync()
		$stderr = $process.StandardError.ReadToEndAsync()
		if ($null -ne $StandardInput) { $process.StandardInput.Write($StandardInput) }
		$process.StandardInput.Close()
		$process.WaitForExit()
		$output = $stdout.GetAwaiter().GetResult()
		$null = $stderr.GetAwaiter().GetResult()
		if ($process.ExitCode -ne 0) { throw 'Command failed.' }
		return $output.TrimEnd()
	}
	catch {
		# Neither the original exception nor either captured stream is safe to log.
		throw "$Tool failed. Check its installation, authentication, permissions, and operation status."
	}
	finally {
		if ($null -ne $process) { $process.Dispose() }
	}
}

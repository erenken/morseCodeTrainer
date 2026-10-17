# Copyright © 2026 myNOC LLC. All rights reserved.
BeforeAll {
	$script:setupPath = Join-Path $PSScriptRoot '../../scripts/Setup-Azure.ps1'
	. $script:setupPath -Subscription '00000000-0000-0000-0000-000000000001'
	$script:testToken = 'test-only-sensitive-token'
	$script:subscriptionId = '00000000-0000-0000-0000-000000000001'
}

Describe 'Azure setup' {
	BeforeEach {
		$script:calls = [Collections.Generic.List[object]]::new()
		$script:state = @{
			Existing = $true; Policies = @(@{ name = 'main'; type = 'branch' }); Token = $testToken
			Fail = ''; Main = $true; Admin = $true; DeploymentState = 'Succeeded'; AccountState = 'Enabled'
			BranchPolicy = @{ custom_branch_policies = $true; protected_branches = $false }
		}
		$script:options = @{
			Subscription = $subscriptionId; Repository = 'example/trainer'; Location = 'eastus2'
			ResourceGroup = 'rg-morse-code-trainer-prod'; Name = 'stapp-morse-code-trainer-prod'; Sku = 'Free'; Preview = $false
		}
		Mock Invoke-SetupCommand {
			param($Tool, $Arguments, $StandardInput)
			$script:calls.Add(@{ Tool = $Tool; Arguments = $Arguments; StandardInput = $StandardInput })
			$operation = $Arguments -join ' '
			if ($state.Fail -and $operation.StartsWith($state.Fail)) { throw 'Simulated CLI failure.' }
			if ($Tool -eq 'az') {
				if ($operation.StartsWith('account show')) { return @{ id = $subscriptionId; state = $state.AccountState } | ConvertTo-Json }
				if ($operation.StartsWith('deployment sub what-if')) { return '{"changes":[{"resourceId":"/test/resource","changeType":"Create"}]}' }
				if ($operation.StartsWith('deployment sub create')) {
					return @{ properties = @{ provisioningState = $state.DeploymentState; outputs = @{
						resourceGroupName = @{ value = 'rg-morse-code-trainer-prod' }
						staticWebAppName = @{ value = 'stapp-morse-code-trainer-prod' }
						staticWebAppUrl = @{ value = 'https://example.azurestaticapps.net' }
					} } } | ConvertTo-Json -Depth 10
				}
				if ($operation.StartsWith('staticwebapp secrets list')) { return $state.Token }
			}
			if ($Tool -eq 'gh' -and $Arguments[0] -eq 'api') {
				$endpoint = $Arguments[1]
				if ('--method' -in $Arguments) { return '{}' }
				if ($endpoint.EndsWith('/deployment-branch-policies')) { return ConvertTo-Json -InputObject @(@{ branch_policies = $state.Policies }) -Depth 10 }
				if ($endpoint.EndsWith('/environments/production')) {
					return @{ deployment_branch_policy = $state.BranchPolicy; protection_rules = @(@{ type = 'required_reviewers'; reviewers = @(123) }) } | ConvertTo-Json -Depth 10
				}
				if ($endpoint.EndsWith('/environments')) {
					$items = if ($state.Existing) { @(@{ name = 'production' }) } else { @() }
					return ConvertTo-Json -InputObject @(@{ environments = @($items) }) -Depth 10
				}
				return @{ permissions = @{ admin = $state.Admin }; default_branch = $(if ($state.Main) { 'main' } else { 'other' }) } | ConvertTo-Json
			}
			if ($Tool -eq 'gh' -and ($operation.StartsWith('secret set') -or $operation.StartsWith('variable set'))) { return '' }
			throw "Unexpected CLI operation: $Tool $operation"
		}
	}

	It 'deploys Bicep and transfers the token only through secret stdin' {
		$state.Existing = $false
		$state.Policies = @()
		$result = Invoke-AzureSetup $options
		$deploy = @($calls | Where-Object { ($_.Arguments -join ' ').StartsWith('deployment sub create') })[0]
		$deploy.Arguments | Should -Contain ([IO.Path]::GetFullPath((Join-Path (Split-Path (Split-Path $setupPath -Parent) -Parent) 'infra/main.bicep')))
		$deploy.Arguments | Should -Contain 'sku=Free'
		$tokenCalls = @($calls | Where-Object StandardInput -EQ $testToken)
		$tokenCalls.Count | Should -Be 1
		$tokenCalls[0].Tool | Should -Be 'gh'
		$tokenCalls[0].Arguments[0..2] | Should -Be @('secret', 'set', 'AZURE_STATIC_WEB_APPS_API_TOKEN')
		$tokenCalls[0].Arguments | Should -Contain 'production'
		($calls.Arguments -join ' ') | Should -Not -Match $testToken
		($result | ConvertTo-Json) | Should -Not -Match $testToken
		$calls[-1].Arguments[0..2] | Should -Be @('variable', 'set', 'AZURE_STATIC_WEB_APPS_ENABLED')
		$writes = @($calls | Where-Object { '--method' -in $_.Arguments })
		$writes.Count | Should -Be 2
		($writes[-1].StandardInput | ConvertFrom-Json).name | Should -Be 'main'
		foreach ($call in ($calls | Where-Object Tool -EQ az)) { $call.Arguments | Should -Contain $subscriptionId }
	}

	It 'previews without writing GitHub or reading the token' {
		$options.Preview = $true
		$result = Invoke-AzureSetup $options
		$result.preview | Should -BeTrue
		$result.changes[0].changeType | Should -Be 'Create'
		$calls.Count | Should -Be 2
		@($calls | Where-Object Tool -NE az).Count | Should -Be 0
	}

	It 'stops before GitHub configuration after failed provisioning' {
		$state.Fail = 'deployment sub create'
		{ Invoke-AzureSetup $options } | Should -Throw '*Simulated*'
		@($calls | Where-Object { ($_.Arguments -join ' ') -match 'environments|variable set' }).Count | Should -Be 0
	}

	It 'requires a succeeded Azure deployment state' {
		$state.DeploymentState = 'Failed'
		{ Invoke-AzureSetup $options } | Should -Throw '*successful infrastructure*'
		@($calls | Where-Object { ($_.Arguments -join ' ') -match 'environments|secrets list' }).Count | Should -Be 0
	}

	It 'rejects empty tokens before secret storage or deployment enablement' {
		$state.Token = " `n "
		{ Invoke-AzureSetup $options } | Should -Throw '*empty*'
		@($calls | Where-Object { ($_.Arguments -join ' ') -match '^(secret|variable) set' }).Count | Should -Be 0
	}

	It 'does not enable deployment when secret storage fails' {
		$state.Fail = 'secret set'
		{ Invoke-AzureSetup $options } | Should -Throw '*Simulated*'
		@($calls | Where-Object { $_.Arguments[0] -eq 'variable' }).Count | Should -Be 0
	}

	It 'preserves existing reviewers and branch policies' {
		$null = Invoke-AzureSetup $options
		@($calls | Where-Object { '--method' -in $_.Arguments }).Count | Should -Be 0
	}

	It 'rejects broader or tag-only branch policies without modifying them' -ForEach @(
		@{ Policy = @{ name = '*'; type = 'branch' } }
		@{ Policy = @{ name = 'main'; type = 'tag' } }
	) {
		$state.Policies = @($Policy)
		{ Invoke-AzureSetup $options } | Should -Throw '*other than main*'
		@($calls | Where-Object { '--method' -in $_.Arguments -or $_.Arguments[0] -eq 'staticwebapp' }).Count | Should -Be 0
	}

	It 'rejects unrestricted environment policies without overwriting them' {
		$state.BranchPolicy = $null
		{ Invoke-AzureSetup $options } | Should -Throw '*selected deployment branches*'
		@($calls | Where-Object { '--method' -in $_.Arguments }).Count | Should -Be 0
	}

	It 'requires main as the default branch before creating resources' {
		$state.Main = $false
		{ Invoke-AzureSetup $options } | Should -Throw '*default branch*'
		@($calls | Where-Object { $_.Arguments[0] -eq 'deployment' }).Count | Should -Be 0
	}

	It 'requires repository admin access before creating resources' {
		$state.Admin = $false
		{ Invoke-AzureSetup $options } | Should -Throw '*admin access*'
		@($calls | Where-Object { $_.Arguments[0] -eq 'deployment' }).Count | Should -Be 0
	}

	It 'rejects a disabled subscription before other operations' {
		$state.AccountState = 'Disabled'
		{ Invoke-AzureSetup $options } | Should -Throw '*not enabled*'
		$calls.Count | Should -Be 1
	}

	It 'rejects shell metacharacters in resource names before invoking a CLI' -ForEach @(
		@{ InvalidName = 'bad;name' }, @{ InvalidName = 'bad&name' }, @{ InvalidName = '../other' }, @{ InvalidName = ('a' * 41) }, @{ InvalidName = '' }
	) {
		{ & $setupPath -Subscription $subscriptionId -Name $InvalidName } | Should -Throw
		$calls.Count | Should -Be 0
	}
}

Describe 'Captured native commands' {
	It 'passes stdin without placing it in process arguments' {
		$output = Invoke-CapturedCommand pwsh @('-NoProfile', '-Command', '[Console]::In.ReadToEnd()') -StandardInput $testToken
		$output | Should -BeExactly $testToken
	}

	It 'redacts failure stdout, stderr, stdin and command arguments' {
		$command = '[Console]::Out.Write([Console]::In.ReadToEnd()); [Console]::Error.Write("test-only-sensitive-token"); exit 7'
		try {
			Invoke-CapturedCommand pwsh @('-NoProfile', '-Command', $command) -StandardInput $testToken
			throw 'Expected failure was not raised.'
		}
		catch {
			$_.Exception.Message | Should -Match '^pwsh failed\.'
			$_.Exception.Message | Should -Not -Match $testToken
		}
	}

	It 'keeps success stdout separate from stderr' {
		Invoke-CapturedCommand pwsh @('-NoProfile', '-Command', '[Console]::Out.Write("result"); [Console]::Error.Write("diagnostic")') | Should -BeExactly 'result'
	}
}

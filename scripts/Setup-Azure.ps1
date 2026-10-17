# Copyright © 2026 myNOC LLC. All rights reserved.
#requires -Version 7.4
<#
.SYNOPSIS
Provision with Bicep and transfer the deployment token to GitHub through stdin.
.PARAMETER Preview
Run Azure what-if only; do not read deployment credentials or change GitHub.
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory)][guid] $Subscription,
	[ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
	[string] $Repository = 'erenken/morseCodeTrainer',
	[ValidateSet('eastus2', 'centralus', 'westus2', 'westeurope', 'eastasia')]
	[string] $Location = 'eastus2',
	[ValidatePattern('^[a-z0-9][a-z0-9-]{0,39}$')]
	[string] $ResourceGroup = 'rg-morse-code-trainer-prod',
	[ValidatePattern('^[a-z0-9][a-z0-9-]{0,39}$')]
	[string] $Name = 'stapp-morse-code-trainer-prod',
	[ValidateSet('Free', 'Standard')][string] $Sku = 'Free',
	[switch] $Preview
)

. "$PSScriptRoot/Invoke-CapturedCommand.ps1"

function Invoke-SetupCommand {
	param([string] $Tool, [string[]] $Arguments, [AllowNull()][string] $StandardInput)
	Invoke-CapturedCommand -Tool $Tool -Arguments $Arguments -StandardInput $StandardInput `
		-WorkingDirectory (Split-Path $PSScriptRoot -Parent)
}

function Initialize-ProductionEnvironment {
	param([string] $Repository)
	$endpoint = "repos/$Repository/environments"
	$pages = Invoke-SetupCommand gh @('api', $endpoint, '--paginate', '--slurp') | ConvertFrom-Json -AsHashtable
	$environments = @($pages | ForEach-Object { $_.environments })
	if ('production' -notin $environments.name) {
		$body = @{ deployment_branch_policy = @{ protected_branches = $false; custom_branch_policies = $true } } | ConvertTo-Json -Compress
		$null = Invoke-SetupCommand gh @('api', "$endpoint/production", '--method', 'PUT', '--input', '-') $body
	}

	# Preserve reviewers and wait timers. Reject broader policies instead of replacing them.
	$configuration = Invoke-SetupCommand gh @('api', "$endpoint/production") | ConvertFrom-Json -AsHashtable
	$policy = $configuration.deployment_branch_policy
	if (-not $policy.custom_branch_policies -or $policy.protected_branches) {
		throw 'The production environment must use selected deployment branches: main only. Adjust its branch policy in GitHub Settings, then rerun setup.'
	}
	$branchEndpoint = "$endpoint/production/deployment-branch-policies"
	$pages = Invoke-SetupCommand gh @('api', $branchEndpoint, '--paginate', '--slurp') | ConvertFrom-Json -AsHashtable
	$policies = @($pages | ForEach-Object { $_.branch_policies })
	foreach ($item in $policies) {
		if ($item.name -ne 'main' -or ($item.type -and $item.type -ne 'branch')) {
			throw 'The production environment permits branches or tags other than main.'
		}
	}
	if ($policies.Count -eq 0) {
		$body = @{ name = 'main'; type = 'branch' } | ConvertTo-Json -Compress
		$null = Invoke-SetupCommand gh @('api', $branchEndpoint, '--method', 'POST', '--input', '-') $body
	}
}

function Invoke-AzureSetup {
	param([hashtable] $Options)
	$account = Invoke-SetupCommand az @('account', 'show', '--subscription', $Options.Subscription,
		'--output', 'json', '--only-show-errors') | ConvertFrom-Json -AsHashtable
	if ($account.state -ne 'Enabled') { throw 'The selected Azure subscription is not enabled.' }
	$subscriptionId = $account.id
	$deploymentArguments = @(
		'--subscription', $subscriptionId, '--name', 'morse-code-trainer-infrastructure',
		'--location', $Options.Location, '--template-file', (Join-Path (Split-Path $PSScriptRoot -Parent) 'infra/main.bicep'),
		'--parameters', "location=$($Options.Location)", "resourceGroupName=$($Options.ResourceGroup)",
		"staticWebAppName=$($Options.Name)", "sku=$($Options.Sku)", '--output', 'json', '--only-show-errors'
	)
	if ($Options.Preview) {
		$previewResult = Invoke-SetupCommand az (@('deployment', 'sub', 'what-if') + $deploymentArguments +
			@('--no-pretty-print', '--result-format', 'ResourceIdOnly')) | ConvertFrom-Json -AsHashtable
		return @{ preview = $true; changes = @($previewResult.changes | ForEach-Object {
			@{ resourceId = $_.resourceId; changeType = $_.changeType }
		}) }
	}

	$repo = Invoke-SetupCommand gh @('api', "repos/$($Options.Repository)") | ConvertFrom-Json -AsHashtable
	if (-not $repo.permissions.admin) { throw 'GitHub repository admin access is needed to configure the environment.' }
	if ($repo.default_branch -ne 'main') { throw 'This deployment pipeline expects the default branch to be main.' }

	$deployment = Invoke-SetupCommand az (@('deployment', 'sub', 'create') + $deploymentArguments) | ConvertFrom-Json -AsHashtable
	if ($deployment.properties.provisioningState -ne 'Succeeded') { throw 'Azure did not report a successful infrastructure deployment.' }
	$outputs = $deployment.properties.outputs
	$resourceGroupName = $outputs.resourceGroupName.value
	$appName = $outputs.staticWebAppName.value
	$url = $outputs.staticWebAppUrl.value
	if (-not $resourceGroupName -or -not $appName -or -not $url) { throw 'Azure deployment outputs are incomplete.' }

	Initialize-ProductionEnvironment -Repository $Options.Repository
	$token = Invoke-SetupCommand az @('staticwebapp', 'secrets', 'list', '--subscription', $subscriptionId,
		'--resource-group', $resourceGroupName, '--name', $appName, '--query', 'properties.apiKey', '--output', 'tsv', '--only-show-errors')
	if ([string]::IsNullOrWhiteSpace($token)) { throw 'Azure returned an empty Static Web App deployment token.' }
	try {
		$null = Invoke-SetupCommand gh @('secret', 'set', 'AZURE_STATIC_WEB_APPS_API_TOKEN', '--repo', $Options.Repository,
			'--env', 'production') $token.Trim()
	}
	finally { $token = $null }

	$null = Invoke-SetupCommand gh @('variable', 'set', 'AZURE_STATIC_WEB_APPS_URL', '--repo', $Options.Repository,
		'--env', 'production', '--body', $url)
	# Enable only after both the environment and its secret are configured.
	$null = Invoke-SetupCommand gh @('variable', 'set', 'AZURE_STATIC_WEB_APPS_ENABLED', '--repo', $Options.Repository, '--body', 'true')
	return @{
		resourceGroup = $resourceGroupName; staticWebApp = $appName; url = $url
		githubEnvironment = 'production'; secretName = 'AZURE_STATIC_WEB_APPS_API_TOKEN'
		contentDeployment = 'Ready for the Release workflow on main.'
	}
}

if ($MyInvocation.InvocationName -ne '.') {
	$ErrorActionPreference = 'Stop'
	try {
		Invoke-AzureSetup @{
			Subscription = $Subscription.ToString(); Repository = $Repository; Location = $Location
			ResourceGroup = $ResourceGroup; Name = $Name; Sku = $Sku; Preview = $Preview.IsPresent
		} | ConvertTo-Json -Depth 10
	}
	catch {
		[Console]::Error.WriteLine("Setup failed: $($_.Exception.Message)")
		exit 1
	}
}

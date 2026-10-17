# Azure Static Web Apps deployment

Copyright © 2026 myNOC LLC. All rights reserved.

The browser SPA runs on a Free Azure Static Web App in East US 2. Bicep creates the dedicated resource group and app. No API, database, Key Vault, or server is required. The Free plan has quotas and no SLA; select Standard explicitly if those requirements change.

## Provisioned environment

Created through Bicep on October 17, 2026 in the myNOC LLC subscription:

- Resource group: `rg-morse-code-trainer-prod`
- Static Web App: `stapp-morse-code-trainer-prod` (Free, East US 2)
- HTTPS endpoint: [happy-hill-06d0f030f.6.azurestaticapps.net](https://happy-hill-06d0f030f.6.azurestaticapps.net)
- GitHub environment: `production`, restricted to the `main` branch

The resource and deployment secret are configured. Application content awaits the first successful Release workflow after merging to `main`.

## One-time setup

Install Python 3.11+, Azure CLI, GitHub CLI, and the Bicep compiler. Sign in to an Azure account with permission to deploy at subscription scope and a GitHub account with repository admin access.

```powershell
az login
gh auth login
az bicep install --version v0.48.1
az account list --query "[].{name:name,id:id}" -o table
```

Use the intended subscription ID explicitly. The script does not change your Azure CLI default subscription.

```powershell
python scripts/setup_azure.py --subscription <subscription-id> --what-if
python scripts/setup_azure.py --subscription <subscription-id>
```

The script:

1. Deploys `infra/main.bicep` at subscription scope, creating `rg-morse-code-trainer-prod` and `stapp-morse-code-trainer-prod`.
2. Creates the GitHub `production` environment with a deployment branch policy allowing only `main`.
3. Retrieves the Static Web App deployment token into memory and sends it to `gh secret set` through standard input. It stores the encrypted environment secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.
4. Stores the non-secret environment variable `AZURE_STATIC_WEB_APPS_URL` and sets the repository variable `AZURE_STATIC_WEB_APPS_ENABLED=true`.

It never writes the token to a file, command argument, ARM output, or console. Failed CLI output is suppressed because diagnostics can contain credentials. Use Azure deployment history and CLI authentication checks to diagnose failures. Do not run this script under a debugger or shell transcript that records variable values.

Rerunning setup applies the same Bicep resources and refreshes the GitHub secret. Existing environment reviewers and wait timers are preserved; a broader branch policy is rejected. If GitHub configuration fails after Azure provisioning, the resources remain available and rerunning setup completes configuration.

Options include `--repository owner/repo`, `--location eastus2`, `--resource-group`, `--name`, and `--sku Free|Standard`. Defaults match `infra/main.bicepparam`. The parameter file can also be used directly with Azure CLI, but direct Bicep deployment does not configure GitHub:

```powershell
az deployment sub create --subscription <subscription-id> --location eastus2 --name morse-code-trainer-infrastructure --parameters infra/main.bicepparam
```

## When deployment runs

Work-branch pushes do not trigger workflows. PRs run the existing tests, coverage gates, native builds, and Bicep compilation. They do not receive the production secret or deploy previews.

After a PR merges to `main`, the Release workflow runs all validation and creates the GitHub prerelease. It then calls `deploy-web.yml`, which downloads that same run's `web-spa` artifact and deploys it to the `production` environment. Deployment never rebuilds the app in Azure: the pinned .NET SDK has already produced the files. A failed validation or release blocks deployment.

A manual Release workflow run on `main` can retry deployment. Until setup enables `AZURE_STATIC_WEB_APPS_ENABLED`, the deploy job is skipped and normal validation/releases still work. Set that variable to `false` to pause web deployments. Once enabled, a missing token fails the deploy job rather than pretending to deploy.

The infrastructure setup creates hosting, but does not publish local or unreviewed work-branch files. The first application upload occurs after the code reaches `main` and the Release workflow succeeds.

## Hosting and credentials

`src/MorseCodeTrainer.Web/wwwroot/staticwebapp.config.json` is copied into the published SPA. It provides SPA navigation fallback, preserves missing framework/static asset errors, supplies WebAssembly MIME types, and allows same-origin camera/microphone access through the Permissions-Policy header. Browser permission prompts still apply. The default Azure hostname uses HTTPS; custom domains can be added later with DNS validation.

The deployment credential is a GitHub environment secret, not a runtime browser setting. There is no Azure service-principal client secret or GitHub personal access token stored in this repository or pipeline. Bicep has no repository token and disables Azure's automatic workflow generation, preserving the PR-only trigger policy.

If the Azure deployment token is rotated in the portal, rerun the setup script to update the GitHub secret. Do not reset the token merely to rerun a failed deployment.

## Verification

```powershell
az bicep build --file infra/main.bicep --outfile artifacts/infra/main.json
az bicep build-params --file infra/main.bicepparam --outfile artifacts/infra/parameters.json
python -m unittest discover -s tests/ci
dotnet publish src/MorseCodeTrainer.Web -c Release --warnaserror -o artifacts/web
```

Confirm that `artifacts/web/wwwroot/staticwebapp.config.json` exists. After the first upload, open the HTTPS URL from the GitHub deployment, test app startup and translation, then verify microphone/camera behavior on actual devices.

References: [Bicep provisioning](https://learn.microsoft.com/en-us/azure/static-web-apps/publish-bicep), [deploying prebuilt files](https://learn.microsoft.com/en-us/azure/static-web-apps/build-configuration#skip-building-front-end-app), [Static Web Apps configuration](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration), [deployment tokens](https://learn.microsoft.com/en-us/azure/static-web-apps/deployment-token-management).

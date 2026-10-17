# Copyright © 2026 myNOC LLC. All rights reserved.
"""Provision with Bicep and transfer the deployment token to GitHub via stdin."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
ENVIRONMENT = "production"
TOKEN_SECRET = "AZURE_STATIC_WEB_APPS_API_TOKEN"


class SetupError(RuntimeError):
    pass


class Cli:
    def run(self, tool, *arguments, stdin=None):
        executable = shutil.which(tool)
        if not executable:
            raise SetupError(f"Install {tool} and sign in before running setup.")
        result = subprocess.run(
            [executable, *arguments], input=stdin, capture_output=True,
            text=True, encoding="utf-8", cwd=ROOT,
        )
        if result.returncode:
            # CLI diagnostics may contain credential material. Never echo either stream.
            raise SetupError(
                f"{tool} {arguments[0]} failed (exit {result.returncode}). "
                "Check CLI authentication, permissions, and the Azure deployment status."
            )
        return result.stdout.strip()


def ensure_production_environment(cli, repository):
    endpoint = f"repos/{repository}/environments"
    pages = json.loads(cli.run("gh", "api", endpoint, "--paginate", "--slurp"))
    environments = [item for page in pages for item in page["environments"]]
    if not any(item["name"] == ENVIRONMENT for item in environments):
        cli.run(
            "gh", "api", f"{endpoint}/{ENVIRONMENT}", "--method", "PUT", "--input", "-",
            stdin=json.dumps({"deployment_branch_policy": {
                "protected_branches": False, "custom_branch_policies": True,
            }}),
        )

    # Preserve existing reviewers/wait timers; never overwrite environment protections.
    configuration = json.loads(cli.run("gh", "api", f"{endpoint}/{ENVIRONMENT}"))
    policy = configuration.get("deployment_branch_policy") or {}
    if not policy.get("custom_branch_policies") or policy.get("protected_branches"):
        raise SetupError(
            "The production environment must use selected deployment branches: main only. "
            "Adjust its deployment branch policy in GitHub Settings, then rerun setup."
        )
    branch_endpoint = f"{endpoint}/{ENVIRONMENT}/deployment-branch-policies"
    pages = json.loads(cli.run("gh", "api", branch_endpoint, "--paginate", "--slurp"))
    policies = [item for page in pages for item in page["branch_policies"]]
    if any(item["name"] != "main" or item.get("type", "branch") != "branch" for item in policies):
        raise SetupError("The production environment permits branches or tags other than main.")
    if not policies:
        cli.run(
            "gh", "api", branch_endpoint, "--method", "POST", "--input", "-",
            stdin=json.dumps({"name": "main", "type": "branch"}),
        )


def setup(options, cli):
    # Pin every Azure command to the explicit subscription, without changing az defaults.
    account = json.loads(cli.run(
        "az", "account", "show", "--subscription", options.subscription,
        "--output", "json", "--only-show-errors",
    ))
    subscription = account["id"]
    if account["state"] != "Enabled":
        raise SetupError("The selected Azure subscription is not enabled.")

    deployment_arguments = [
        "--subscription", subscription,
        "--name", "morse-code-trainer-infrastructure",
        "--location", options.location,
        "--template-file", str(ROOT / "infra/main.bicep"),
        "--parameters", f"location={options.location}",
        f"resourceGroupName={options.resource_group}",
        f"staticWebAppName={options.name}", f"sku={options.sku}",
        "--output", "json", "--only-show-errors",
    ]
    if options.what_if:
        preview = json.loads(cli.run(
            "az", "deployment", "sub", "what-if", *deployment_arguments,
            "--no-pretty-print", "--result-format", "ResourceIdOnly",
        ))
        return {"preview": True, "changes": [
            {"resourceId": item["resourceId"], "changeType": item["changeType"]}
            for item in preview.get("changes", [])
        ]}

    repository = json.loads(cli.run("gh", "api", f"repos/{options.repository}"))
    if not repository.get("permissions", {}).get("admin"):
        raise SetupError("GitHub repository admin access is needed to configure the environment.")
    if repository["default_branch"] != "main":
        raise SetupError("This deployment pipeline expects the repository's default branch to be main.")

    deployment = json.loads(cli.run(
        "az", "deployment", "sub", "create", *deployment_arguments,
    ))
    outputs = deployment["properties"]["outputs"]
    if deployment["properties"]["provisioningState"] != "Succeeded":
        raise SetupError("Azure did not report a successful infrastructure deployment.")
    resource_group = outputs["resourceGroupName"]["value"]
    name = outputs["staticWebAppName"]["value"]
    url = outputs["staticWebAppUrl"]["value"]

    ensure_production_environment(cli, options.repository)
    token = cli.run(
        "az", "staticwebapp", "secrets", "list",
        "--subscription", subscription, "--resource-group", resource_group,
        "--name", name, "--query", "properties.apiKey",
        "--output", "tsv", "--only-show-errors",
    ).strip()
    if not token:
        raise SetupError("Azure returned an empty Static Web App deployment token.")
    try:
        # stdin avoids process-list, command-history, and command-line logging exposure.
        cli.run("gh", "secret", "set", TOKEN_SECRET, "--repo", options.repository,
                "--env", ENVIRONMENT, stdin=token)
    finally:
        token = None

    cli.run("gh", "variable", "set", "AZURE_STATIC_WEB_APPS_URL",
            "--repo", options.repository, "--env", ENVIRONMENT, "--body", url)
    # Enable deployments only after both the secret and environment have been configured.
    cli.run("gh", "variable", "set", "AZURE_STATIC_WEB_APPS_ENABLED",
            "--repo", options.repository, "--body", "true")
    return {
        "resourceGroup": resource_group, "staticWebApp": name, "url": url,
        "githubEnvironment": ENVIRONMENT, "secretName": TOKEN_SECRET,
        "contentDeployment": "Ready for the Release workflow on main.",
    }


def resource_name(value):
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,39}", value):
        raise argparse.ArgumentTypeError("Use 1–40 lowercase letters, digits, or hyphens.")
    return value


def repository_name(value):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", value):
        raise argparse.ArgumentTypeError("Use owner/repository.")
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--subscription", required=True, type=lambda value: str(uuid.UUID(value)),
                        help="Azure subscription ID; never changes your default subscription.")
    parser.add_argument("--repository", default="erenken/morseCodeTrainer", type=repository_name)
    parser.add_argument("--location", default="eastus2",
                        choices=["eastus2", "centralus", "westus2", "westeurope", "eastasia"])
    parser.add_argument("--resource-group", default="rg-morse-code-trainer-prod", type=resource_name)
    parser.add_argument("--name", default="stapp-morse-code-trainer-prod", type=resource_name)
    parser.add_argument("--sku", default="Free", choices=["Free", "Standard"])
    parser.add_argument("--what-if", action="store_true", help="Preview Azure changes; do not write GitHub settings.")
    options = parser.parse_args()
    try:
        print(json.dumps(setup(options, Cli()), indent=2))
        return 0
    except (SetupError, ValueError, KeyError, OSError) as error:
        print(f"Setup failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

# Copyright © 2026 myNOC LLC. All rights reserved.
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("setup_azure", Path(__file__).parents[2] / "scripts/setup_azure.py")
azure = importlib.util.module_from_spec(spec)
spec.loader.exec_module(azure)

TOKEN = "test-only-sensitive-token"
SUBSCRIPTION = "00000000-0000-0000-0000-000000000001"


class FakeCli:
    def __init__(self, *, existing=False, policies=None, token=TOKEN, fail=None, main=True):
        self.calls = []
        self.existing = existing
        self.policies = [{"name": "main", "type": "branch"}] if policies is None else policies
        self.token = token
        self.fail = fail
        self.main = main

    def run(self, tool, *arguments, stdin=None):
        self.calls.append((tool, arguments, stdin))
        if self.fail and arguments[:len(self.fail)] == self.fail:
            raise azure.SetupError("Simulated CLI failure.")
        if tool == "az":
            if arguments[:2] == ("account", "show"):
                return json.dumps({"id": SUBSCRIPTION, "state": "Enabled"})
            if arguments[:3] == ("deployment", "sub", "what-if"):
                return json.dumps({"changes": [{"resourceId": "/test/resource", "changeType": "Create"}]})
            if arguments[:3] == ("deployment", "sub", "create"):
                return json.dumps({"properties": {"provisioningState": "Succeeded", "outputs": {
                    "resourceGroupName": {"value": "rg-morse-code-trainer-prod"},
                    "staticWebAppName": {"value": "stapp-morse-code-trainer-prod"},
                    "staticWebAppUrl": {"value": "https://example.azurestaticapps.net"},
                }}})
            if arguments[:3] == ("staticwebapp", "secrets", "list"):
                return self.token
        if tool == "gh" and arguments[0] == "api":
            endpoint = arguments[1]
            if "--method" in arguments:
                return "{}"
            if endpoint.endswith("/deployment-branch-policies"):
                return json.dumps([{"branch_policies": self.policies}])
            if endpoint.endswith("/environments/production"):
                return json.dumps({
                    "deployment_branch_policy": {
                        "custom_branch_policies": True, "protected_branches": False,
                    },
                    "protection_rules": [{"type": "required_reviewers", "reviewers": [123]}],
                })
            if endpoint.endswith("/environments"):
                return json.dumps([{"environments": [{"name": "production"}] if self.existing else []}])
            return json.dumps({"permissions": {"admin": True}, "default_branch": "main" if self.main else "other"})
        if tool == "gh" and arguments[:2] in (("secret", "set"), ("variable", "set")):
            return ""
        raise AssertionError(f"Unexpected CLI operation: {tool} {arguments}")


def options(**overrides):
    return argparse.Namespace(**{
        "subscription": SUBSCRIPTION, "repository": "example/trainer", "location": "eastus2",
        "resource_group": "rg-morse-code-trainer-prod", "name": "stapp-morse-code-trainer-prod",
        "sku": "Free", "what_if": False, **overrides,
    })


class AzureSetupTests(unittest.TestCase):
    def test_deploys_bicep_and_sends_token_only_through_secret_stdin(self):
        cli = FakeCli(policies=[])
        result = azure.setup(options(), cli)
        deploy = next(args for tool, args, _ in cli.calls if args[:3] == ("deployment", "sub", "create"))
        self.assertIn(str(azure.ROOT / "infra/main.bicep"), deploy)
        self.assertIn("sku=Free", deploy)
        sensitive_calls = [(tool, args, stdin) for tool, args, stdin in cli.calls if stdin == TOKEN]
        self.assertEqual(1, len(sensitive_calls))
        tool, args, _ = sensitive_calls[0]
        self.assertEqual(("gh", ("secret", "set", azure.TOKEN_SECRET)), (tool, args[:3]))
        self.assertIn("production", args)
        self.assertTrue(all(TOKEN not in arg for _, args, _ in cli.calls for arg in args))
        self.assertNotIn(TOKEN, json.dumps(result))
        self.assertEqual(("variable", "set", "AZURE_STATIC_WEB_APPS_ENABLED"), cli.calls[-1][1][:3])
        policies = [json.loads(stdin) for _, args, stdin in cli.calls if "--method" in args]
        self.assertIn({"name": "main", "type": "branch"}, policies)

    def test_preview_never_writes_github_or_reads_the_token(self):
        cli = FakeCli()
        result = azure.setup(options(what_if=True), cli)
        self.assertTrue(result["preview"])
        self.assertEqual("Create", result["changes"][0]["changeType"])
        self.assertEqual(2, len(cli.calls))
        self.assertTrue(all(tool == "az" for tool, _, _ in cli.calls))

    def test_failed_provisioning_does_not_touch_environment_or_enable_deployment(self):
        cli = FakeCli(fail=("deployment", "sub", "create"))
        with self.assertRaises(azure.SetupError):
            azure.setup(options(), cli)
        self.assertFalse(any("environments" in " ".join(args) for _, args, _ in cli.calls))
        self.assertFalse(any(args[:2] == ("variable", "set") for _, args, _ in cli.calls))

    def test_empty_token_does_not_write_secret_or_enable_deployment(self):
        cli = FakeCli(token=" \n ")
        with self.assertRaisesRegex(azure.SetupError, "empty"):
            azure.setup(options(), cli)
        self.assertFalse(any(args[:2] in (("secret", "set"), ("variable", "set")) for _, args, _ in cli.calls))

    def test_failed_secret_storage_does_not_enable_deployment(self):
        cli = FakeCli(fail=("secret", "set"))
        with self.assertRaises(azure.SetupError):
            azure.setup(options(), cli)
        self.assertFalse(any(args[:2] == ("variable", "set") for _, args, _ in cli.calls))

    def test_existing_environment_keeps_reviewers_and_branch_policy(self):
        cli = FakeCli(existing=True)
        azure.setup(options(), cli)
        self.assertFalse(any("--method" in args for _, args, _ in cli.calls))

    def test_wider_environment_policy_is_rejected_without_modifying_it(self):
        cli = FakeCli(existing=True, policies=[{"name": "*", "type": "branch"}])
        with self.assertRaisesRegex(azure.SetupError, "other than main"):
            azure.setup(options(), cli)
        self.assertFalse(any("--method" in args for _, args, _ in cli.calls))
        self.assertFalse(any(args[:3] == ("staticwebapp", "secrets", "list") for _, args, _ in cli.calls))

    def test_wrong_default_branch_is_rejected_before_creating_resources(self):
        cli = FakeCli(main=False)
        with self.assertRaisesRegex(azure.SetupError, "default branch"):
            azure.setup(options(), cli)
        self.assertFalse(any(args[:3] == ("deployment", "sub", "create") for _, args, _ in cli.calls))

    @patch.object(azure.shutil, "which", return_value="/usr/bin/gh")
    @patch.object(azure.subprocess, "run")
    def test_cli_redacts_error_streams_and_command_arguments(self, run, _which):
        run.return_value = subprocess.CompletedProcess([], 1, stdout=TOKEN, stderr=TOKEN)
        with self.assertRaises(azure.SetupError) as error:
            azure.Cli().run("gh", "secret", "set", "EXAMPLE", stdin=TOKEN)
        self.assertNotIn(TOKEN, str(error.exception))
        self.assertEqual(TOKEN, run.call_args.kwargs["input"])
        self.assertNotIn(TOKEN, run.call_args.args[0])
        self.assertTrue(run.call_args.kwargs["capture_output"])

    def test_resource_names_reject_shell_metacharacters(self):
        for value in ("bad;name", "bad&name", "../other", "a" * 41, ""):
            with self.subTest(value=value), self.assertRaises(argparse.ArgumentTypeError):
                azure.resource_name(value)


if __name__ == "__main__":
    unittest.main()

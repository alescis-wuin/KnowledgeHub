"""Offline regression tests for the ARCH-A0.1/A1.1 validator."""

from __future__ import annotations

import importlib.util
import json
import os
import subprocess  # nosec B404
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

MODULE_PATH = Path(__file__).with_name("kh_a1_1_validate.py")
SPEC = importlib.util.spec_from_file_location("kh_a1_1_validate", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("validator_module_unavailable")
kh = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(kh)


def command(root: Path, *args: str) -> str:
    p = subprocess.run(  # nosec B603, B607
        ["git", "-C", str(root), *args],
        check=True,
        capture_output=True,
        env={
            **os.environ,
            "GIT_AUTHOR_NAME": "Test",
            "GIT_AUTHOR_EMAIL": "t@invalid",
            "GIT_COMMITTER_NAME": "Test",
            "GIT_COMMITTER_EMAIL": "t@invalid",
        },
    )
    return p.stdout.decode().strip()


class ValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory(prefix="kh-a11-test-")
        self.addCleanup(self.tmp.cleanup)
        self.home = Path(self.tmp.name)
        self.root = self.home / "repository"
        self.root.mkdir()
        command(self.root, "init", "-b", kh.BRANCH)
        (self.root / "tracked.txt").write_text("baseline\n", encoding="utf-8")
        command(self.root, "add", "tracked.txt")
        command(self.root, "commit", "-m", "baseline")
        self.sha = command(self.root, "rev-parse", "HEAD")
        self.tree = command(self.root, "rev-parse", "HEAD^{tree}")
        for relative in kh.NEW_PATHS:
            item = self.root / relative
            item.parent.mkdir(parents=True, exist_ok=True)
            item.write_text("new file\n", encoding="utf-8")

    def test_allowed_delta_and_git_identity(self) -> None:
        original_sha, original_tree = kh.EXPECTED_SHA, kh.EXPECTED_TREE
        kh.EXPECTED_SHA, kh.EXPECTED_TREE = self.sha, self.tree
        try:
            state = kh.preflight(self.root)
        finally:
            kh.EXPECTED_SHA, kh.EXPECTED_TREE = original_sha, original_tree
        self.assertTrue(state["expected_pending_delta_matches"])
        self.assertTrue(state["expected_head_matches"])
        self.assertEqual(state["pending_new_files_count"], 3)
        self.assertEqual(state["worktree_inventory"]["registered_worktrees"], 1)
        self.assertNotIn(str(self.root), json.dumps(state))
        self.assertEqual(state["off_machine_scope"]["windows_maui"], "not_observed")

    def test_reject_extra_untracked_or_modified_tracked(self) -> None:
        (self.root / "extra.txt").write_text("x", encoding="utf-8")
        self.assertFalse(kh.preflight(self.root)["expected_pending_delta_matches"])
        (self.root / "extra.txt").unlink()
        (self.root / "tracked.txt").write_text("changed", encoding="utf-8")
        self.assertFalse(kh.preflight(self.root)["expected_pending_delta_matches"])

    def test_reject_output_inside_repository(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "output_inside_worktree"):
            kh.secure_output_dir(self.root / "reports", self.root)

    def test_preflight_reports_outside_repository(self) -> None:
        destination = kh.secure_output_dir(self.home / "private-reports", self.root)
        self.assertTrue(destination.exists())
        self.assertFalse(str(destination).startswith(str(self.root)))
        p = kh.write_return_report(
            destination,
            {
                "mode": "preflight",
                "private_log_directory": str(destination / "private-logs"),
                "qualification_status": "local_preflight_pass_external_scope_pending",
            },
        )
        with zipfile.ZipFile(p) as archive:
            self.assertEqual(set(archive.namelist()), {"README.txt", "summary.json"})
            payload = json.loads(archive.read("summary.json"))
            self.assertNotIn("private_log_directory", payload)
            self.assertNotIn(str(destination), str(payload))

    def test_parse_porcelain_reject_tracked_modification_and_renames(self) -> None:
        self.assertEqual(kh.parse_porcelain("?? a.py\0?? b.py\0"), ({"a.py", "b.py"}, False))
        self.assertTrue(kh.parse_porcelain(" M tracked.txt\0")[1])
        self.assertTrue(kh.parse_porcelain("R  source.txt\0dest.txt\0")[1])

    def test_trx_requires_all_passed_and_nonempty(self) -> None:
        folder = self.home / "trx"
        folder.mkdir()
        trx = folder / "result.trx"
        trx.write_text(
            """<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results><UnitTestResult testId=\"abc\" testName=\"example\" outcome=\"Passed\" /></Results></TestRun>""",
            encoding="utf-8",
        )
        self.assertEqual(kh.parse_trx(folder)["passed"], 1)
        trx.write_text(trx.read_text().replace('outcome="Passed"', 'outcome="NotExecuted"'))
        with self.assertRaisesRegex(ValueError, "trx_not_all_passed"):
            kh.parse_trx(folder)
        trx.write_text("<TestRun/>")
        with self.assertRaisesRegex(ValueError, "trx_empty"):
            kh.parse_trx(folder)
        trx.write_text('<!DOCTYPE TestRun [<!ENTITY dangerous "xx">]><TestRun/>')
        with self.assertRaisesRegex(ValueError, "trx_dtd_forbidden"):
            kh.parse_trx(folder)

    def test_validation_matrix_does_not_restore_maui_solution(self) -> None:
        source = MODULE_PATH.read_text(encoding="utf-8")
        self.assertIn("KnowledgeHub.Tests.Unit/KnowledgeHub.Tests.Unit.csproj", source)
        self.assertIn(
            "KnowledgeHub.Observability.Verify/KnowledgeHub.Observability.Verify.csproj", source
        )
        self.assertNotIn('"dotnet", "restore", "KnowledgeHub.slnx"', source)
        self.assertNotIn('"dotnet", "build", "KnowledgeHub.slnx"', source)

    def test_external_command_logs_private_and_reports_failure(self) -> None:
        logs = self.home / "logs"
        logs.mkdir()
        result = kh.step(
            "negative", [sys.executable, "-c", "print('SECRET'); exit(7)"], self.root, logs, 10
        )
        self.assertEqual(result["status"], "failed")
        self.assertEqual(result["exit_code"], 7)
        self.assertNotIn("SECRET", str(result))
        self.assertIn(b"SECRET", (logs / "negative.log").read_bytes())


if __name__ == "__main__":
    unittest.main()

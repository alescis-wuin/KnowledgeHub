#!/usr/bin/env python3
"""KnowledgeHub ARCH-A0.1/A1.1 local diagnostic and validation.

No automatic checkout, migration, cleanup, push or deploy. Builds and test runs are
opt-in and use an isolated git worktree selected by the operator.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess  # nosec B404
import sys
import time
import uuid
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from xml.parsers import expat

EXPECTED_SHA = "77559ae509024638407aec58e1883e135af5ba7a"
EXPECTED_TREE = "781ef402c11287bda86a47f33c1b01bccd517d7c"
BRANCH = "feature/arch-a1-1-validation"
NEW_PATHS = frozenset(
    {
        "docs/architecture/ARCH-A1.1-qualification-v1.md",
        "scripts/architecture/kh_a1_1_validate.py",
        "scripts/architecture/test_kh_a1_1_validate.py",
    }
)
PROJECTS = (
    "KnowledgeHub.Api",
    "KnowledgeHub.Application",
    "KnowledgeHub.Client",
    "KnowledgeHub.CorpusImporter",
    "KnowledgeHub.Domain",
    "KnowledgeHub.Infrastructure",
    "KnowledgeHub.Tests.Integration",
    "KnowledgeHub.Tests.Unit",
    "KnowledgeHub.Evaluation",
    "KnowledgeHub.Observability.Verify",
)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def git(root: Path, *args: str) -> str:
    proc = subprocess.run(  # nosec B603, B607
        ["git", "-C", str(root), *args],
        capture_output=True,
        check=False,
        timeout=30,
    )
    if proc.returncode:
        raise RuntimeError(f"git_{args[0]}_failed")
    return proc.stdout.decode("utf-8", errors="replace").strip()


def git_tree_snapshot(root: Path) -> dict[str, Any]:
    paths = git(root, "ls-files", "-z").split("\x00")
    paths = [p for p in paths if p]
    counts = {
        "tracked_files": len(paths),
        "csharp": sum(p.endswith(".cs") for p in paths),
        "csproj": sum(p.endswith(".csproj") for p in paths),
        "architecture_documents": sum(p.startswith("docs/architecture/") for p in paths),
    }
    counts["paths_sha256"] = digest("\n".join(sorted(paths)).encode())
    return counts


def parse_porcelain(stdout: str) -> tuple[set[str], bool]:
    """Return safe file paths only for strict allowed-delta comparison."""
    unexpected_encoding = False
    names: set[str] = set()
    for line in stdout.split("\x00"):
        if not line:
            continue
        if len(line) < 4 or line[2] != " ":
            unexpected_encoding = True
            continue
        # Strict new-file format is allowed; renamed/deleted files are not.
        if line[:3] != "?? ":
            unexpected_encoding = True
            continue
        names.add(line[3:])
    return names, unexpected_encoding


def safe_worktree_inventory(root: Path) -> dict[str, int]:
    # Deliberately no worktree paths, branch names or filenames in shared report.
    lines = git(root, "worktree", "list", "--porcelain").splitlines()
    locations = [line[9:] for line in lines if line.startswith("worktree ")]
    dirty = 0
    inaccessible = 0
    for location in locations:
        try:
            s = git(Path(location), "status", "--porcelain=v1", "-z", "--untracked-files=all")
            if s:
                dirty += 1
        except (RuntimeError, subprocess.TimeoutExpired, OSError):
            inaccessible += 1
    return {
        "registered_worktrees": len(locations),
        "registered_worktrees_dirty": dirty,
        "registered_worktrees_inaccessible": inaccessible,
        "external_windows_or_lab_repositories_observed": 0,
    }


def preflight(root: Path) -> dict[str, Any]:
    if not root.is_dir():
        raise RuntimeError("missing_worktree")
    actual_root = Path(git(root, "rev-parse", "--show-toplevel")).resolve()
    if actual_root != root.resolve():
        raise RuntimeError("worktree_root_mismatch")
    head = git(root, "rev-parse", "HEAD")
    tree = git(root, "rev-parse", "HEAD^{tree}")
    branch = git(root, "branch", "--show-current")
    status = git(root, "status", "--porcelain=v1", "-z", "--untracked-files=all")
    new_files, changed_tracked = parse_porcelain(status)
    # Running before package application is not a meaningful qualification.
    allowed = not changed_tracked and new_files == NEW_PATHS
    return {
        "head": head,
        "tree": tree,
        "expected_head_matches": head == EXPECTED_SHA,
        "expected_tree_matches": tree == EXPECTED_TREE,
        "expected_branch_matches": branch == BRANCH,
        "expected_pending_delta_matches": allowed,
        "pending_new_files_count": len(new_files),
        "unexpected_git_status": changed_tracked or not allowed,
        "repo_inventory": git_tree_snapshot(root),
        "worktree_inventory": safe_worktree_inventory(root),
        "off_machine_scope": {
            "windows_maui": "not_observed",
            "windows_avalonia": "not_observed",
            "kh0012_lab": "not_observed",
        },
    }


def secure_output_dir(parent: Path, root: Path) -> Path:
    parent = parent.expanduser()
    if parent.is_symlink():
        raise RuntimeError("output_parent_symlink")
    parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    parent = parent.resolve(strict=True)
    # Reports must remain outside every worktree associated with this clone.
    areas = [root.resolve()]
    for line in git(root, "worktree", "list", "--porcelain").splitlines():
        if line.startswith("worktree "):
            areas.append(Path(line[9:]).resolve())
    if any(parent == area or area in parent.parents for area in areas):
        raise RuntimeError("output_inside_worktree")
    name = (
        "run-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:12]
    )
    child = parent / name
    child.mkdir(mode=0o700)
    return child


def step(
    name: str,
    argv: list[str],
    cwd: Path,
    log_dir: Path,
    timeout: int,
) -> dict[str, Any]:
    start = time.monotonic()
    logfile = log_dir / (name + ".log")
    result: dict[str, Any] = {"name": name, "status": "not_executed", "exit_code": None}
    try:
        with logfile.open("xb") as handle:
            os.chmod(logfile, 0o600)
            proc = subprocess.run(  # nosec B603
                argv,
                cwd=cwd,
                stdout=handle,
                stderr=subprocess.STDOUT,
                check=False,
                timeout=timeout,
                env=os.environ.copy(),
            )
        result["exit_code"] = proc.returncode
        result["status"] = "passed" if proc.returncode == 0 else "failed"
    except FileNotFoundError:
        result["status"] = "unavailable"
    except subprocess.TimeoutExpired:
        result["status"] = "timeout"
    except OSError:
        result["status"] = "infrastructure_error"
    result["duration_seconds"] = round(time.monotonic() - start, 3)
    result["private_log_sha256"] = digest(logfile.read_bytes()) if logfile.is_file() else None
    return result


def skipped(name: str, reason: str) -> dict[str, Any]:
    return {"name": name, "status": "blocked", "reason": reason, "exit_code": None}


def parse_trx(directory: Path) -> dict[str, Any]:
    files = sorted(directory.glob("*.trx"))
    if len(files) != 1:
        raise ValueError("trx_count_invalid")
    # Expat with DTD/external entities explicitly prohibited and a hard size bound.
    # Keep the parser dependency-free; do not expand untrusted XML entities.
    parser = expat.ParserCreate(namespace_separator="}")
    counts: dict[str, int] = {}
    identities: list[tuple[str, str, str]] = []

    def forbid_dtd(*_args: object) -> None:
        raise ValueError("trx_dtd_forbidden")

    def start_element(name: str, attrs: dict[str, str]) -> None:
        if name.rsplit("}", 1)[-1] != "UnitTestResult":
            return
        outcome = attrs.get("outcome", "Missing")
        counts[outcome] = counts.get(outcome, 0) + 1
        identities.append((attrs.get("testId", ""), attrs.get("testName", ""), outcome))

    parser.StartDoctypeDeclHandler = forbid_dtd
    parser.EntityDeclHandler = forbid_dtd
    parser.ExternalEntityRefHandler = lambda *_args: 0
    parser.StartElementHandler = start_element
    total_size = 0
    with files[0].open("rb") as handle:
        while True:
            chunk = handle.read(65536)
            if not chunk:
                parser.Parse(b"", True)
                break
            total_size += len(chunk)
            if total_size > 16 * 1024 * 1024:
                raise ValueError("trx_size_exceeded")
            parser.Parse(chunk, False)
    if not identities:
        raise ValueError("trx_empty")
    if counts.get("Passed", 0) != len(identities):
        raise ValueError("trx_not_all_passed")
    return {
        "result_count": len(identities),
        "passed": counts.get("Passed", 0),
        "other_outcomes": 0,
        "identity_sha256": digest(json.dumps(sorted(identities), sort_keys=True).encode()),
    }


def log_has_marker(log_dir: Path, name: str, marker: bytes) -> bool:
    log = log_dir / (name + ".log")
    return log.is_file() and marker in log.read_bytes()


def run_qualification(root: Path, run_dir: Path, with_containers: bool) -> list[dict[str, Any]]:
    log_dir = run_dir / "private-logs"
    log_dir.mkdir(mode=0o700)
    (run_dir / "unit-trx").mkdir(mode=0o700)
    (run_dir / "integration-trx").mkdir(mode=0o700)
    (run_dir / "evaluation").mkdir(mode=0o700)
    (run_dir / "observability").mkdir(mode=0o700)
    results: list[dict[str, Any]] = []

    def execute(name: str, args: list[str], seconds: int = 600) -> dict[str, Any]:
        rec = step(name, args, root, log_dir, seconds)
        results.append(rec)
        print(f"[{name}] {rec['status']}", flush=True)
        return rec

    if shutil.which("dotnet") is None:
        return [skipped("dotnet", "dotnet_unavailable")]
    sdk = execute("dotnet_sdk_version", ["dotnet", "--version"], 30)
    if sdk["status"] != "passed":
        return results + [skipped("dotnet_restore", "sdk_check_failed")]
    version_log = (log_dir / "dotnet_sdk_version.log").read_text().strip()
    sdk_ok = (
        bool(re.fullmatch(r"10\.0\.4\d\d", version_log)) and int(version_log.split(".")[-1]) >= 403
    )
    results.append(
        {
            "name": "sdk_version_compatible",
            "status": "passed" if sdk_ok else "blocked",
            "required": "10.0.403 latestPatch, feature band 10.0.4xx",
        }
    )
    if not sdk_ok:
        return results

    # Linux backend matrix: the main KnowledgeHub.slnx contains a MAUI Android
    # project. Do not restore/build that solution on this host or require MAUI.
    # Restore the six top-level projects; transitive refs cover all nine Linux
    # projects, including Domain/Application/Infrastructure.
    restore_targets = [
        ("api", "KnowledgeHub.Api/KnowledgeHub.Api.csproj"),
        ("importer", "KnowledgeHub.CorpusImporter/KnowledgeHub.CorpusImporter.csproj"),
        ("unit", "KnowledgeHub.Tests.Unit/KnowledgeHub.Tests.Unit.csproj"),
        ("integration", "KnowledgeHub.Tests.Integration/KnowledgeHub.Tests.Integration.csproj"),
        ("evaluation", "KnowledgeHub.Evaluation/KnowledgeHub.Evaluation.csproj"),
        (
            "observability",
            "KnowledgeHub.Observability.Verify/KnowledgeHub.Observability.Verify.csproj",
        ),
    ]
    build_targets = [
        ("domain", "KnowledgeHub.Domain/KnowledgeHub.Domain.csproj"),
        ("application", "KnowledgeHub.Application/KnowledgeHub.Application.csproj"),
        ("infrastructure", "KnowledgeHub.Infrastructure/KnowledgeHub.Infrastructure.csproj"),
        *restore_targets,
    ]
    restorations: dict[str, bool] = {}
    for label, target in restore_targets:
        result = execute(
            "restore_" + label,
            ["dotnet", "restore", target, "-p:NuGetAudit=true", "-p:TreatWarningsAsErrors=true"],
            600,
        )
        restorations[label] = result["status"] == "passed"
    restores_passed = all(restorations.values())
    builds: dict[str, bool] = {}
    for label, target in build_targets:
        if not restores_passed:
            results.append(skipped("build_" + label, "restore_not_passed"))
            continue
        result = execute(
            "build_" + label,
            [
                "dotnet",
                "build",
                target,
                "-c",
                "Release",
                "--no-restore",
                "-p:TreatWarningsAsErrors=true",
            ],
            900,
        )
        builds[label] = result["status"] == "passed"
    for label, target in build_targets:
        if not builds.get(label):
            results.append(skipped("format_" + label, "project_build_not_passed"))
            continue
        execute(
            "format_" + label,
            ["dotnet", "format", "whitespace", target, "--verify-no-changes", "--no-restore"],
            300,
        )
    if builds.get("unit"):
        rec = execute(
            "test_unit",
            [
                "dotnet",
                "test",
                "KnowledgeHub.Tests.Unit/KnowledgeHub.Tests.Unit.csproj",
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--logger",
                "trx;LogFileName=result.trx",
                "--results-directory",
                str(run_dir / "unit-trx"),
                "-p:TreatWarningsAsErrors=true",
            ],
            1200,
        )
        if rec["status"] == "passed":
            try:
                rec["trx"] = parse_trx(run_dir / "unit-trx")
            except (ValueError, expat.ExpatError, OSError):
                rec["status"] = "failed"
                rec["reason"] = "trx_invalid_or_skipped"
    else:
        results.append(skipped("test_unit", "unit_project_build_failed"))

    if builds.get("evaluation"):
        rec = execute(
            "eval_self_test",
            [
                "dotnet",
                "run",
                "--project",
                "KnowledgeHub.Evaluation/KnowledgeHub.Evaluation.csproj",
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--",
                "--self-test",
                "--baseline",
                str(root / "evaluation/kh007-reference-v1"),
                "--report",
                str(run_dir / "evaluation"),
            ],
            600,
        )
        if rec["status"] == "passed" and not log_has_marker(
            log_dir, "eval_self_test", b"KH007_REFERENCE_RESULT=pass"
        ):
            rec["status"] = "failed"
            rec["reason"] = "evaluation_marker_missing"
    else:
        results.append(skipped("eval_self_test", "evaluation_build_failed"))

    if not with_containers:
        results.extend(
            [
                skipped("docker_available", "container_tests_not_authorized"),
                skipped("test_integration", "container_tests_not_authorized"),
                skipped("observability_e2e", "container_tests_not_authorized"),
            ]
        )
        return results
    if shutil.which("docker") is None:
        results.extend(
            [
                skipped("docker_available", "docker_unavailable"),
                skipped("test_integration", "docker_unavailable"),
                skipped("observability_e2e", "docker_unavailable"),
            ]
        )
        return results
    docker = execute("docker_available", ["docker", "info", "--format", "{{.ServerVersion}}"], 30)
    if docker["status"] != "passed":
        results.extend(
            [
                skipped("test_integration", "docker_unavailable"),
                skipped("observability_e2e", "docker_unavailable"),
            ]
        )
        return results
    if builds.get("integration"):
        rec = execute(
            "test_integration",
            [
                "dotnet",
                "test",
                "KnowledgeHub.Tests.Integration/KnowledgeHub.Tests.Integration.csproj",
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--logger",
                "trx;LogFileName=result.trx",
                "--results-directory",
                str(run_dir / "integration-trx"),
                "-p:TreatWarningsAsErrors=true",
            ],
            1800,
        )
        if rec["status"] == "passed":
            try:
                rec["trx"] = parse_trx(run_dir / "integration-trx")
            except (ValueError, expat.ExpatError, OSError):
                rec["status"] = "failed"
                rec["reason"] = "trx_invalid_or_skipped"
    else:
        results.append(skipped("test_integration", "integration_project_build_failed"))
    if builds.get("observability"):
        rec = execute(
            "observability_e2e",
            [
                "dotnet",
                "run",
                "--project",
                "KnowledgeHub.Observability.Verify/KnowledgeHub.Observability.Verify.csproj",
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--",
                str(run_dir / "observability"),
            ],
            1800,
        )
        if rec["status"] == "passed" and not log_has_marker(
            log_dir, "observability_e2e", b"KH0054_E2E_RESULT=pass"
        ):
            rec["status"] = "failed"
            rec["reason"] = "observability_marker_missing"
    else:
        results.append(skipped("observability_e2e", "observability_build_failed"))
    return results


def write_return_report(run_dir: Path, summary: dict[str, Any]) -> Path:
    summary_path = run_dir / "summary-private.json"
    summary_path.write_text(json.dumps(summary, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    os.chmod(summary_path, 0o600)
    shareable = dict(summary)
    shareable.pop("private_log_directory", None)
    # No absolute paths, worktree names, credentials or raw logs are exported.
    out = run_dir / "RETURN_REPORT.zip"
    with zipfile.ZipFile(out, "x", compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr("summary.json", json.dumps(shareable, sort_keys=True, indent=2) + "\n")
        z.writestr(
            "README.txt",
            "KnowledgeHub ARCH-A0.1/A1.1. This ZIP contains only sanitized metadata and aggregated test counts; private raw logs and TRX remain with the operator. PASS is meaningful only for actually executed checks.\n",
        )
    os.chmod(out, 0o600)
    return out


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="KnowledgeHub ARCH-A0.1/A1.1 local qualification")
    parser.add_argument(
        "--root", required=True, type=Path, help="Dedicated worktree from the approved main SHA"
    )
    parser.add_argument(
        "--output-parent",
        required=True,
        type=Path,
        help="Private report directory outside all worktrees",
    )
    parser.add_argument("--mode", required=True, choices=("preflight", "full"))
    parser.add_argument(
        "--allow-container-tests",
        action="store_true",
        help="Authorize ephemeral Testcontainers PostgreSQL/OTLP checks",
    )
    args = parser.parse_args(argv)
    if args.mode == "preflight" and args.allow_container_tests:
        parser.error("--allow-container-tests is only for --mode full")
    try:
        snapshot = preflight(args.root)
        run_dir = secure_output_dir(args.output_parent, args.root)
    except (RuntimeError, OSError, subprocess.TimeoutExpired) as exc:
        print("ARCH_A1_1_PREFLIGHT=blocked; reason=" + str(exc), file=sys.stderr)
        return 3
    required = (
        snapshot["expected_head_matches"]
        and snapshot["expected_tree_matches"]
        and snapshot["expected_branch_matches"]
        and snapshot["expected_pending_delta_matches"]
        and snapshot["worktree_inventory"]["registered_worktrees_inaccessible"] == 0
    )
    worktree_count = snapshot["worktree_inventory"]["registered_worktrees"]
    print(f"ARCH_A0_1_LINUX_WORKTREES_OBSERVED={worktree_count}", flush=True)
    summary: dict[str, Any] = {
        "schema_version": "1.0",
        "milestone": "ARCH-A0.1-ARCH-A1.1",
        "expected_head": EXPECTED_SHA,
        "expected_tree": EXPECTED_TREE,
        "mode": args.mode,
        "preflight": snapshot,
        "checks": [],
        "qualification_status": "blocked",
        "external_workspaces": "not_observed_from_this_linux_worktree",
        "private_log_directory": str(run_dir / "private-logs"),
    }
    if not required:
        summary["qualification_status"] = "blocked_preflight"
    elif args.mode == "preflight":
        summary["qualification_status"] = "local_preflight_pass_external_scope_pending"
    else:
        summary["checks"] = run_qualification(args.root, run_dir, args.allow_container_tests)
        summary["qualification_status"] = (
            "locally_passed_external_A0_1_pending"
            if summary["checks"] and all(x["status"] == "passed" for x in summary["checks"])
            else "blocked_or_failed"
        )
    try:
        post = {
            "head_unchanged": git(args.root, "rev-parse", "HEAD") == EXPECTED_SHA,
            "tree_unchanged": git(args.root, "rev-parse", "HEAD^{tree}") == EXPECTED_TREE,
        }
        names, changed = parse_porcelain(
            git(args.root, "status", "--porcelain=v1", "-z", "--untracked-files=all")
        )
        post["only_expected_delta"] = not changed and names == NEW_PATHS
        summary["post_check"] = post
        if not all(post.values()):
            summary["qualification_status"] = "blocked_worktree_changed"
    except (RuntimeError, subprocess.TimeoutExpired, OSError):
        summary["qualification_status"] = "blocked_post_check"
    report = write_return_report(run_dir, summary)
    print("ARCH_A0_1_PREFLIGHT=" + ("pass" if required else "blocked"))
    print("ARCH_A1_1_STATUS=" + summary["qualification_status"])
    print("RETURN_REPORT=" + str(report))
    print("RETURN_REPORT_SHA256=" + digest(report.read_bytes()))
    return (
        0
        if required
        and summary["qualification_status"]
        in ("local_preflight_pass_external_scope_pending", "locally_passed_external_A0_1_pending")
        else 2
    )


if __name__ == "__main__":
    sys.exit(main())

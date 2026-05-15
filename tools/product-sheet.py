#!/usr/bin/env python
# Copyright (c) 2026 MMPWorks LLC
# Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
"""
product-sheet.py — validates every CAPABILITY.yaml in the monorepo
against the schema, and generates docs/product-sheet.md.

Invocations:
    python tools/product-sheet.py              # validate + generate
    python tools/product-sheet.py --validate-only

Called by build.sh as a CI gate. Failing validation fails the build.

Status:
    This is the scaffolding pass. Validation emits a friendly "nothing
    to validate" when the src/ tree is empty; the full schema-level
    validator lands alongside the first sink migration.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

# Optional import — only needed once the first real sink arrives.
try:
    import yaml  # type: ignore[import-not-found]
    _HAS_YAML = True
except ImportError:
    _HAS_YAML = False


REPO_ROOT = Path(__file__).resolve().parent.parent
SRC_DIR = REPO_ROOT / "src"
DOCS_DIR = REPO_ROOT / "docs"

# Minimal set of required top-level keys every CAPABILITY.yaml must
# carry. The full schema — every field, every type — lives in
# CAPABILITY-SCHEMA.md; we enforce the hard-required subset here.
#
# Field set depends on `kind`. Sinks (kind: sink) ship a NuGet package
# and need the operator-facing config / capability fields. Migration
# guides (kind: migration-guide) point at an existing sink instead of
# shipping their own; they need the navigation fields but skip the
# package fields. Pipeline-shape capabilities (kind: async,
# periodic-batching, in-memory, map) sit between — they're library
# packages without an operator-facing config form.
REQUIRED_KEYS_SINK = [
    "name",
    "package_id",
    "version",
    "kind",
    "category",
    "purpose",
    "ships",
    "requires",
    "config",
    "minimum_edition",
    "aot_compatible",
    "thread_safety",
    "maintenance",
    "changelog",
]

REQUIRED_KEYS_MIGRATION_GUIDE = [
    "name",
    "package_id",
    "version",
    "kind",
    "category",
    "purpose",
    "herald_api",
    "guide",
    "maintenance",
]


def required_keys_for(kind: str | None) -> list[str]:
    """Pick the validator field-set based on the manifest's `kind`."""
    if kind == "migration-guide":
        return REQUIRED_KEYS_MIGRATION_GUIDE
    return REQUIRED_KEYS_SINK


def find_manifests() -> list[Path]:
    """Return every CAPABILITY.yaml file under src/."""
    if not SRC_DIR.exists():
        return []
    return sorted(SRC_DIR.glob("*/CAPABILITY.yaml"))


def validate_manifest(path: Path) -> list[str]:
    """Validate one manifest. Returns a list of error messages (empty on success)."""
    errors: list[str] = []

    if not _HAS_YAML:
        errors.append(
            f"{path.relative_to(REPO_ROOT)}: PyYAML is not installed; "
            f"run `pip install pyyaml` to enable full validation."
        )
        return errors

    try:
        with path.open("r", encoding="utf-8") as fh:
            manifest = yaml.safe_load(fh)
    except Exception as ex:  # pragma: no cover — IO / parse errors
        errors.append(f"{path.relative_to(REPO_ROOT)}: YAML parse error — {ex}")
        return errors

    if not isinstance(manifest, dict):
        errors.append(f"{path.relative_to(REPO_ROOT)}: top level must be a mapping.")
        return errors

    kind = manifest.get("kind")
    for key in required_keys_for(kind):
        if key not in manifest:
            errors.append(
                f"{path.relative_to(REPO_ROOT)}: missing required field `{key}`."
            )

    # Sanity: the directory name must match the sink's `name` field.
    expected_dir = path.parent.name
    declared_name = manifest.get("name")
    if declared_name and declared_name != expected_dir:
        errors.append(
            f"{path.relative_to(REPO_ROOT)}: `name: {declared_name}` does not "
            f"match the directory name `{expected_dir}`."
        )

    # Sanity: package_id must start with the Herald.Sinks. namespace
    # (post-naming-contract — historical manifests used MMP.Herald.Sinks.).
    # Migration guides set package_id to null because they don't ship a
    # NuGet package, so skip the check when the value is null or omitted.
    package_id = manifest.get("package_id")
    if package_id and not str(package_id).startswith("Herald.Sinks."):
        errors.append(
            f"{path.relative_to(REPO_ROOT)}: `package_id: {package_id}` must "
            f"start with `Herald.Sinks.`."
        )

    return errors


def generate_product_sheet(manifests: list[Path]) -> str:
    """Produce docs/product-sheet.md content from all valid manifests."""
    if not _HAS_YAML:
        return (
            "# Herald.Sinks — product sheet\n\n"
            "Generation requires PyYAML (`pip install pyyaml`). "
            "Run `python tools/product-sheet.py` once it is installed.\n"
        )

    if not manifests:
        return (
            "# Herald.Sinks — product sheet\n\n"
            "The monorepo currently carries no sinks. The first sink "
            "migration populates this page.\n"
        )

    lines = [
        "# Herald.Sinks — product sheet",
        "",
        "Generated from each sink's CAPABILITY.yaml. Regenerate via "
        "`python tools/product-sheet.py` or `bash build.sh`.",
        "",
    ]

    for manifest_path in manifests:
        with manifest_path.open("r", encoding="utf-8") as fh:
            manifest = yaml.safe_load(fh) or {}

        name = manifest.get("name", manifest_path.parent.name)
        purpose = manifest.get("purpose", "").strip()
        version = manifest.get("version", "?")
        category = manifest.get("category", "?")
        vendor = manifest.get("vendor", {}).get("name", "?")
        minimum_edition = manifest.get("minimum_edition", "?")
        aot = manifest.get("aot_compatible", "?")
        maintenance = manifest.get("maintenance", {}).get("level", "?")

        lines.extend(
            [
                f"## {name}",
                "",
                f"**Vendor:** {vendor}  ",
                f"**Version:** {version}  ",
                f"**Category:** {category}  ",
                f"**Minimum edition:** {minimum_edition}  ",
                f"**AOT compatible:** {aot}  ",
                f"**Maintenance:** {maintenance}  ",
                "",
                purpose,
                "",
            ]
        )

        capabilities = manifest.get("capabilities", [])
        if capabilities:
            lines.append("**Capabilities**")
            lines.append("")
            for cap in capabilities:
                lines.append(f"- {cap}")
            lines.append("")

        limitations = manifest.get("limitations", [])
        if limitations:
            lines.append("**Limitations**")
            lines.append("")
            for lim in limitations:
                lines.append(f"- {lim}")
            lines.append("")

        lines.append("---")
        lines.append("")

    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate + generate Herald.Sinks product sheet.")
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Validate CAPABILITY.yaml files; do not regenerate the product sheet.",
    )
    args = parser.parse_args()

    manifests = find_manifests()
    if not manifests:
        print("[product-sheet] No CAPABILITY.yaml files found under src/. "
              "Nothing to validate.")
        if not args.validate_only:
            DOCS_DIR.mkdir(parents=True, exist_ok=True)
            (DOCS_DIR / "product-sheet.md").write_text(
                generate_product_sheet([]),
                encoding="utf-8",
            )
        return 0

    total_errors: list[str] = []
    for path in manifests:
        total_errors.extend(validate_manifest(path))

    if total_errors:
        print("[product-sheet] Validation failed:", file=sys.stderr)
        for err in total_errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(f"[product-sheet] Validated {len(manifests)} sink manifest(s).")

    if not args.validate_only:
        DOCS_DIR.mkdir(parents=True, exist_ok=True)
        (DOCS_DIR / "product-sheet.md").write_text(
            generate_product_sheet(manifests),
            encoding="utf-8",
        )
        print(f"[product-sheet] Wrote {DOCS_DIR / 'product-sheet.md'}.")

    return 0


if __name__ == "__main__":
    sys.exit(main())

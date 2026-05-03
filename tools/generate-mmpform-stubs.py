#!/usr/bin/env python
"""Generate placeholder mmpform files for every real sink in the
Herald.Sinks tree, and migrate each sink's CAPABILITY.yaml to the
v2 form-schema contract.

For every real sink (kind: sink) that doesn't already ship an
mmpform:

  * Writes `configuration-{kind}.mmpform` with one of two placeholders:
    - "coming soon"  — the sink has operator-facing config to surface
                       (had a `dashboard_config:` block OR has a
                       non-null `config:` uri/host/alias)
    - "no config"    — nothing to configure; sink is wired by runtime

  * Updates CAPABILITY.yaml:
    - Bumps `configContract:` to 2 (adds the field if absent)
    - Adds `formSchema: configuration-{kind}.mmpform`
    - Removes the legacy `dashboard_config:` block

Migration-guide stubs (kind: migration-guide, package_id: null) are
skipped entirely — they don't ship as installable sinks.

Sinks that already ship a `configuration-*.mmpform` keep their
existing form (Herald.Sinks.File and the four pilot sinks). Their
CAPABILITY.yaml is still migrated to the v2 contract if it isn't
already there.

Usage (from the repo root):
    python Modules/Herald.Sinks/tools/generate-mmpform-stubs.py
    python Modules/Herald.Sinks/tools/generate-mmpform-stubs.py --dry-run
"""

import argparse
import re
import sys
from pathlib import Path

SINKS_ROOT = Path(__file__).resolve().parents[1] / "src"

# YAML lookups are line-based so we preserve comments + ordering.
RE_TOP_KEY = re.compile(r"^[A-Za-z_][\w\-]*:")
RE_NAME = re.compile(r"^name:\s*(?P<v>\S+)")
RE_KIND_TOP = re.compile(r"^kind:\s*(?P<v>\S+)")
RE_PURPOSE_BLOCK = re.compile(r"^purpose:\s*>\s*$")
RE_PURPOSE_INLINE = re.compile(r"^purpose:\s+(?P<v>.+)$")
RE_CONFIG_KIND = re.compile(r"^\s{2}kind:\s*(?P<v>\S+)")
RE_CONFIG_BLOCK = re.compile(r"^config:\s*$")
RE_CONFIG_FIELD_NULL = re.compile(r"^\s{2}(?P<f>uri|host|alias):\s*null\s*$")
RE_CONFIG_FIELD_VALUE = re.compile(r"^\s{2}(?P<f>uri|host|alias):\s*(?P<v>.+)$")
RE_DASHBOARD_CONFIG = re.compile(r"^dashboard_config:\s*$")
RE_CONFIG_CONTRACT = re.compile(r"^configContract:\s*(?P<v>\d+)\s*$")
RE_FORM_SCHEMA = re.compile(r"^formSchema:\s*\S+\s*$")


def parse_capability(path: Path) -> dict:
    """Pull the fields we need out of a CAPABILITY.yaml.

    Hand-rolled rather than PyYAML because we want to preserve the
    original file when we rewrite it; PyYAML's dump strips comments.
    """
    info = {
        "name": None,
        "top_kind": None,
        "config_kind": None,
        "purpose_first_sentence": "",
        "has_dashboard_config": False,
        "has_config_block_with_values": False,
        "has_config_contract_v2": False,
        "has_form_schema": False,
    }

    in_config_block = False
    in_purpose_block = False
    purpose_lines: list[str] = []

    with path.open("r", encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\n")
            if not line.startswith(" ") and not line.startswith("\t"):
                in_config_block = False
                if in_purpose_block:
                    # Block-scalar purpose ends when the next top-level key
                    # begins — finalize before we drop the flag, otherwise
                    # the accumulated lines are never assigned.
                    if purpose_lines:
                        info["purpose_first_sentence"] = _first_sentence(
                            " ".join(purpose_lines)
                        )
                    in_purpose_block = False
                if RE_TOP_KEY.match(line):
                    if (m := RE_NAME.match(line)):
                        info["name"] = m.group("v")
                        continue
                    if (m := RE_KIND_TOP.match(line)):
                        info["top_kind"] = m.group("v")
                        continue
                    if (m := RE_PURPOSE_INLINE.match(line)):
                        # YAML block-scalar markers (>, |, with optional +/-)
                        # also match the inline regex, so disambiguate here.
                        value = m.group("v").strip()
                        if value in (">", ">-", ">+", "|", "|-", "|+"):
                            in_purpose_block = True
                            purpose_lines = []
                        else:
                            info["purpose_first_sentence"] = _first_sentence(value)
                        continue
                    if RE_PURPOSE_BLOCK.match(line):
                        in_purpose_block = True
                        purpose_lines = []
                        continue
                    if RE_CONFIG_BLOCK.match(line):
                        in_config_block = True
                        continue
                    if RE_DASHBOARD_CONFIG.match(line):
                        info["has_dashboard_config"] = True
                        continue
                    if RE_CONFIG_CONTRACT.match(line):
                        info["has_config_contract_v2"] = (
                            RE_CONFIG_CONTRACT.match(line).group("v") == "2"
                        )
                        continue
                    if RE_FORM_SCHEMA.match(line):
                        info["has_form_schema"] = True
                        continue
            else:
                if in_purpose_block:
                    stripped = line.strip()
                    if stripped:
                        purpose_lines.append(stripped)
                    elif purpose_lines:
                        in_purpose_block = False
                        info["purpose_first_sentence"] = _first_sentence(
                            " ".join(purpose_lines)
                        )
                    continue
                if in_config_block:
                    if (m := RE_CONFIG_KIND.match(line)):
                        info["config_kind"] = m.group("v")
                        continue
                    if RE_CONFIG_FIELD_NULL.match(line):
                        continue
                    if RE_CONFIG_FIELD_VALUE.match(line):
                        info["has_config_block_with_values"] = True
                        continue

    if in_purpose_block and purpose_lines:
        info["purpose_first_sentence"] = _first_sentence(" ".join(purpose_lines))

    return info


def _first_sentence(text: str) -> str:
    """Trim a purpose blob down to one sentence for the form header."""
    text = text.strip().strip('"').strip("'")
    if not text:
        return ""
    if (idx := text.find(".")) != -1:
        return text[: idx + 1]
    return text


def display_name(name: str) -> str:
    """`Herald.Sinks.Datadog` -> `Datadog`."""
    return name.removeprefix("Herald.Sinks.")


def write_mmpform(path: Path, sink_name: str, purpose: str, has_config: bool) -> None:
    label_text = (
        "Configuration UI coming soon. Until then, wire this sink via JSON "
        "config — see the per-sink README for the supported keys."
        if has_config
        else "This sink takes no operator-facing configuration. The runtime "
        "wires it without dashboard input."
    )
    header_kind = "configuration UI placeholder" if has_config else "no configuration required"
    purpose_one_line = purpose.replace('"', "'") or f"{display_name(sink_name)} sink."

    body = (
        f"# {display_name(sink_name)} sink — {header_kind}.\n"
        f"# Flat form: every field on its own row, no collapsible sections.\n"
        f"\n"
        f"columns: 12\n"
        f"\n"
        f"__properties = []\n"
        f"\n"
        f'[container("{display_name(sink_name)} sink", "{purpose_one_line}")]\n'
        f"\n"
        f'  - [label(12, "{label_text}", style=note)]\n'
    )
    path.write_text(body, encoding="utf-8")


def migrate_capability(path: Path, info: dict) -> None:
    """Rewrite CAPABILITY.yaml in place to the v2 contract.

    - Bumps `configContract:` to 2 (or adds it after `version:`)
    - Adds `formSchema:` after the contract bump (if absent)
    - Removes the `dashboard_config:` block end-to-end
    """
    src = path.read_text(encoding="utf-8")
    lines = src.splitlines(keepends=True)

    out: list[str] = []
    i = 0
    config_contract_set = False
    form_schema_set = False
    dashboard_skipped = False
    inserted_after_version = False

    config_kind = info.get("config_kind") or "default"
    form_schema_value = f"configuration-{config_kind}.mmpform"

    while i < len(lines):
        line = lines[i]
        stripped = line.lstrip()

        if RE_DASHBOARD_CONFIG.match(line.rstrip("\n")):
            dashboard_skipped = True
            i += 1
            while i < len(lines):
                nxt = lines[i]
                if nxt.startswith(" ") or nxt.startswith("\t") or nxt.strip() == "":
                    i += 1
                    continue
                break
            while out and out[-1].strip() == "":
                out.pop()
            out.append("\n")
            continue

        if RE_CONFIG_CONTRACT.match(line.rstrip("\n")):
            out.append("configContract: 2\n")
            config_contract_set = True
            i += 1
            continue

        if RE_FORM_SCHEMA.match(line.rstrip("\n")):
            out.append(f"formSchema: {form_schema_value}\n")
            form_schema_set = True
            i += 1
            continue

        out.append(line)

        if (
            not inserted_after_version
            and not config_contract_set
            and stripped.startswith("version:")
        ):
            inserts: list[str] = []
            if not info.get("has_config_contract_v2"):
                inserts.append("configContract: 2\n")
                config_contract_set = True
            if not info.get("has_form_schema"):
                inserts.append(f"formSchema: {form_schema_value}\n")
                form_schema_set = True
            out.extend(inserts)
            inserted_after_version = True

        i += 1

    if not config_contract_set:
        out.insert(0, "configContract: 2\n")
    if not form_schema_set:
        idx = next(
            (k for k, ln in enumerate(out) if ln.startswith("configContract:")),
            0,
        )
        out.insert(idx + 1, f"formSchema: {form_schema_value}\n")

    path.write_text("".join(out), encoding="utf-8")


def process(dry_run: bool) -> int:
    sinks = sorted(p for p in SINKS_ROOT.iterdir() if p.is_dir())
    written = 0
    skipped_existing = 0
    migration_guides = 0
    capability_updates = 0

    for sink_dir in sinks:
        cap_path = sink_dir / "CAPABILITY.yaml"
        if not cap_path.exists():
            continue

        info = parse_capability(cap_path)

        if info["top_kind"] == "migration-guide":
            migration_guides += 1
            continue

        if info["top_kind"] != "sink":
            print(f"  ?? {sink_dir.name}: unexpected kind {info['top_kind']!r}")
            continue

        config_kind = info["config_kind"] or sink_dir.name.removeprefix("Herald.Sinks.").lower()

        existing_forms = list(sink_dir.glob("configuration-*.mmpform"))
        if existing_forms:
            skipped_existing += 1
        else:
            target = sink_dir / f"configuration-{config_kind}.mmpform"
            has_config = info["has_dashboard_config"] or info["has_config_block_with_values"]
            if dry_run:
                print(f"  + {sink_dir.name}/{target.name}  ({'config' if has_config else 'no-config'})")
            else:
                write_mmpform(target, info["name"], info["purpose_first_sentence"], has_config)
                written += 1

        needs_migration = (
            not info["has_config_contract_v2"]
            or not info["has_form_schema"]
            or info["has_dashboard_config"]
        )
        if needs_migration:
            if dry_run:
                print(f"  ~ {sink_dir.name}/CAPABILITY.yaml (bump v2, retire dashboard_config)")
            else:
                migrate_capability(cap_path, info)
                capability_updates += 1

    print()
    print(f"Mmpforms written: {written}")
    print(f"Mmpforms skipped (already shipped): {skipped_existing}")
    print(f"Migration-guides skipped: {migration_guides}")
    print(f"CAPABILITY.yaml files migrated: {capability_updates}")

    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="Print actions without writing.")
    args = parser.parse_args()
    return process(args.dry_run)


if __name__ == "__main__":
    sys.exit(main())

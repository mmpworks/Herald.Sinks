"""
Strip the Herald.Core ProjectReference from each sink csproj.

The reference moved to Directory.Build.props as a Condition-guarded
ProjectReference (monorepo) / PackageReference (standalone) pair. Per-sink
csprojs no longer declare the dependency directly.

Handles both forms found in the tree:
  * single-line:    <ProjectReference Include="...Herald.Core.csproj" />
  * multi-line:     <ProjectReference Include="..."> ... </ProjectReference>

The enclosing <ItemGroup>...</ItemGroup> is removed only when the Herald.Core
ref is its sole item. If the ItemGroup contains other items, only the
ProjectReference and its child metadata are dropped; the ItemGroup stays.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

SINKS_ROOT = Path(__file__).resolve().parent.parent / "src"


def strip_core_ref(text: str) -> tuple[str, bool]:
    """Return (new_text, changed).

    Matches any depth of parent-dir traversal (`..\\..\\Core\\…`,
    `..\\..\\..\\..\\Core\\…`, etc.) and either path separator
    (Windows backslash or POSIX forward-slash). Different IDEs and
    `dotnet add reference` on non-Windows emit different separators;
    the file is portable at MSBuild evaluation time, so the sweep
    has to handle both.
    """
    # Path component for "any number of parent traversals, then Core,
    # then Herald.Core.csproj". Backslashes in the regex are doubled
    # because Python raw-strings; the character class [/\\] accepts
    # both slash directions.
    core_path = r"(?:\.\.[/\\])+Core[/\\]Herald\.Core\.csproj"

    # Match an ItemGroup whose sole content is the Herald.Core
    # ProjectReference. Multi-line form (with closing tag).
    pattern_lone_itemgroup_multiline = re.compile(
        r"\n[ \t]*<ItemGroup>[ \t\r\n]*"
        + r"<ProjectReference Include=\"" + core_path + r"\">"
        + r".*?</ProjectReference>[ \t\r\n]*"
        + r"</ItemGroup>\n",
        re.DOTALL,
    )
    new = pattern_lone_itemgroup_multiline.sub("\n", text)
    if new != text:
        return new, True

    # Single-line form.
    pattern_lone_itemgroup_singleline = re.compile(
        r"\n[ \t]*<ItemGroup>[ \t\r\n]*"
        + r"<ProjectReference Include=\"" + core_path + r"\" />[ \t\r\n]*"
        + r"</ItemGroup>\n",
        re.DOTALL,
    )
    new = pattern_lone_itemgroup_singleline.sub("\n", text)
    if new != text:
        return new, True

    # Fallback: strip just the ProjectReference if it's inside a shared ItemGroup.
    pattern_ref_multiline = re.compile(
        r"[ \t]*<ProjectReference Include=\"" + core_path + r"\">"
        + r".*?</ProjectReference>[ \t\r\n]*",
        re.DOTALL,
    )
    new = pattern_ref_multiline.sub("", text)
    if new != text:
        return new, True

    pattern_ref_singleline = re.compile(
        r"[ \t]*<ProjectReference Include=\"" + core_path + r"\" />[ \t\r\n]*",
    )
    new = pattern_ref_singleline.sub("", text)
    return new, new != text


def main() -> int:
    csprojs = sorted(SINKS_ROOT.glob("Herald.Sinks.*/Herald.Sinks.*.csproj"))
    touched = 0
    skipped = 0
    unmatched: list[Path] = []
    for csproj in csprojs:
        text = csproj.read_text(encoding="utf-8")
        if "Herald.Core.csproj" not in text:
            skipped += 1
            continue
        new_text, changed = strip_core_ref(text)
        if changed:
            csproj.write_text(new_text, encoding="utf-8")
            touched += 1
            print(f"  stripped: {csproj.relative_to(SINKS_ROOT.parent)}")
        else:
            unmatched.append(csproj)
            print(
                f"  ERROR — Herald.Core.csproj present but no pattern matched: "
                f"{csproj.relative_to(SINKS_ROOT.parent)}",
                file=sys.stderr,
            )

    print(f"\nSummary: {touched} csprojs updated, {skipped} skipped (no Herald.Core ref).")
    # Non-zero exit when the sweep saw a Herald.Core.csproj reference it
    # couldn't strip. Catches future template imports that drift from the
    # expected ProjectReference shape — e.g. a tools-emitted forward-slash
    # path or an ItemGroup that bundles Core with other refs the script
    # doesn't know how to split.
    if unmatched:
        print(
            f"\n{len(unmatched)} csproj(s) need manual review — sweep cannot proceed.",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())

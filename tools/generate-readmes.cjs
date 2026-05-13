#!/usr/bin/env node
// Generate README.md for every Herald.Sinks.* package from its
// CAPABILITY.yaml manifest.
//
// Why this exists:
//   nuget.org's package page renders the README. Without one, every
//   sink's page is bare — just the package id, version, and a
//   "missing readme" notice. Authoring 85 READMEs by hand is busywork
//   and drifts the moment a CAPABILITY.yaml updates. The manifests
//   already carry the right fields (purpose, capabilities, limitations,
//   edition, AOT, vendor); this generator stamps them into a uniform
//   template so the nupkg ships with a real package page.
//
// How to run:
//   cd Modules/Herald.Sinks
//   node tools/generate-readmes.cjs
//
// Output:
//   src/Herald.Sinks.X/README.md  (one per sink with a csproj)
//
// Idempotent — runs full re-generation every invocation. Safe to wire
// into a pre-pack hook later if drift becomes a concern.

'use strict';

const fs = require('fs');
const path = require('path');

const SINKS_DIR = path.resolve(__dirname, '..', 'src');

// --- minimal YAML extractor -----------------------------------------------
// CAPABILITY.yaml has a regular shape; pull the fields we need without a
// full YAML parser dependency. Handles:
//   - scalar strings:    `key: value`
//   - block scalars:     `key: > \n  multi \n  line`
//   - block list of strings:  `key:\n  - item\n  - item`
//   - nested object scalar:   `parent.child` (one level deep)
//
// Limitations: doesn't parse arrays of objects, comments mid-value, or
// quoted strings with embedded colons. CAPABILITY.yaml across the 86
// sinks doesn't exercise those shapes for the fields we extract.
function parseManifest(yamlText) {
  const lines = yamlText.split(/\r?\n/);
  const out = {};
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Skip comments and blank lines at top level.
    if (/^\s*#/.test(line) || /^\s*$/.test(line)) { i++; continue; }

    // Only top-level keys (no leading whitespace).
    const m = line.match(/^([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$/);
    if (!m) { i++; continue; }

    const key = m[1];
    const rest = m[2].trim();
    i++;

    if (rest === '>' || rest === '>-' || rest === '|' || rest === '|-') {
      // Block scalar: collect indented continuation lines until indent drops.
      const buf = [];
      while (i < lines.length && /^\s+\S/.test(lines[i])) {
        buf.push(lines[i].replace(/^\s+/, ''));
        i++;
      }
      out[key] = buf.join(' ').replace(/\s+/g, ' ').trim();
    } else if (rest === '') {
      // Either a block list ("- item") or a nested object. List items
      // may span multiple lines via deeper-indent continuation; fold
      // those into the current item rather than dropping them.
      const items = [];
      const obj = {};
      let collected = false;
      while (i < lines.length && /^\s+\S/.test(lines[i])) {
        const cur = lines[i];
        const listMatch = cur.match(/^(\s+)-\s+(.*)$/);
        const kvMatch = cur.match(/^\s+([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$/);
        if (listMatch) {
          items.push(listMatch[2].trim().replace(/^["']|["']$/g, ''));
        } else if (kvMatch && items.length === 0) {
          // KV-shaped lines only count as nested-object entries when no
          // list items have started yet; otherwise treat as continuation.
          obj[kvMatch[1]] = kvMatch[2].trim().replace(/^["']|["']$/g, '');
        } else if (items.length > 0) {
          // Continuation of the previous list item.
          items[items.length - 1] += ' ' + cur.trim();
        }
        collected = true;
        i++;
      }
      if (items.length > 0) out[key] = items;
      else if (collected) out[key] = obj;
    } else {
      out[key] = rest.replace(/^["']|["']$/g, '');
    }
  }
  return out;
}

// --- render the markdown --------------------------------------------------
function renderReadme(m, sinkDirName) {
  const name = m.name || sinkDirName;
  const pkg = m.package_id || `MMP.${name}`;
  const purpose = m.purpose || `Herald sink: ${name}.`;
  const edition = m.minimum_edition || 'Community';
  const aot = String(m.aot_compatible) === 'true';
  const threadSafety = m.thread_safety || 'See source for thread-safety contract.';
  const sinkKind = (m.config && m.config.kind) || '';
  const vendor = m.vendor || null;
  const capabilities = Array.isArray(m.capabilities) ? m.capabilities : [];
  const limitations = Array.isArray(m.limitations) ? m.limitations : [];
  const requires = m.requires && m.requires.external && Array.isArray(m.requires.external)
    ? m.requires.external : [];

  const tierLine = edition === 'Community'
    ? 'works on the free Apache 2.0 Herald.Core. No license key required.'
    : edition === 'Pro'
      ? 'requires a Pro license at runtime — see https://github.com/mmpworks/Herald for licensing.'
      : edition === 'Enterprise'
        ? 'requires an Enterprise license at runtime — see https://github.com/mmpworks/Herald for licensing.'
        : `requires the ${edition} edition.`;

  let md = '';
  md += `# ${pkg}\n\n`;
  md += `> ${purpose}\n\n`;
  md += `Part of [Herald](https://github.com/mmpworks/Herald) — high-performance structured logging for .NET 8, 9, and 10.\n\n`;

  md += `## Install\n\n`;
  md += '```bash\n';
  md += `dotnet add package ${pkg}\n`;
  md += '```\n\n';
  md += `The sink auto-registers into \`LogSinkProviderRegistry.Default\` via a \`[ModuleInitializer]\` on assembly load. No manual \`RegisterAll(...)\` or \`With*SinkProviders()\` call is required — \`dotnet add package\` is the whole workflow.\n\n`;

  if (sinkKind) {
    md += `Sink kind: \`${sinkKind}\` (the identifier the Dashboard form and JSON config use to reference this sink).\n\n`;
  }

  if (capabilities.length > 0) {
    md += `## Capabilities\n\n`;
    for (const c of capabilities) md += `- ${c}\n`;
    md += `\n`;
  }

  if (limitations.length > 0) {
    md += `## Limitations\n\n`;
    for (const l of limitations) md += `- ${l}\n`;
    md += `\n`;
  }

  if (requires.length > 0) {
    md += `## External requirements\n\n`;
    for (const r of requires) md += `- ${r}\n`;
    md += `\n`;
  }

  md += `## Tier & runtime\n\n`;
  md += `- **Edition**: ${edition} — ${tierLine}\n`;
  md += `- **AOT-compatible**: ${aot ? 'yes' : 'no'}\n`;
  md += `- **Targets**: .NET 8 / 9 / 10\n`;
  md += `- **Thread safety**: ${threadSafety}\n`;
  md += `\n`;

  if (vendor && vendor.name) {
    md += `## Vendor\n\n`;
    md += vendor.url
      ? `${vendor.name} — ${vendor.url}\n\n`
      : `${vendor.name}\n\n`;
  }

  md += `## Configuration\n\n`;
  md += `Per-sink config form lives in \`configuration*.mmpform\` at the package root and inside the assembly as an embedded resource. The Herald Dashboard renders it at runtime; JSON config follows the same shape. See \`CAPABILITY.yaml\` shipped at the package root for the full manifest (schema reference: [CAPABILITY-SCHEMA.md](https://github.com/mmpworks/Herald.Sinks/blob/main/CAPABILITY-SCHEMA.md)).\n\n`;

  md += `## License\n\n`;
  md += `Apache 2.0. Copyright (c) 2026 MMPWorks LLC. See LICENSE shipped at the package root.\n\n`;
  md += `---\n\n`;
  md += `*Generated from \`CAPABILITY.yaml\`. Re-run \`Modules/Herald.Sinks/tools/generate-readmes.cjs\` after manifest edits to refresh.*\n`;

  return md;
}

// --- walk all sink dirs ---------------------------------------------------
function main() {
  if (!fs.existsSync(SINKS_DIR)) {
    console.error(`Source dir not found: ${SINKS_DIR}`);
    process.exit(2);
  }

  const dirs = fs.readdirSync(SINKS_DIR, { withFileTypes: true })
    .filter(d => d.isDirectory() && d.name.startsWith('Herald.Sinks.'))
    .map(d => d.name)
    .sort();

  let written = 0;
  let skipped = 0;

  for (const dirName of dirs) {
    const sinkDir = path.join(SINKS_DIR, dirName);
    const manifestPath = path.join(sinkDir, 'CAPABILITY.yaml');
    const csprojPath = path.join(sinkDir, `${dirName}.csproj`);
    const readmePath = path.join(sinkDir, 'README.md');

    if (!fs.existsSync(csprojPath)) {
      skipped++;
      continue;
    }
    if (!fs.existsSync(manifestPath)) {
      console.warn(`[skip] no CAPABILITY.yaml: ${dirName}`);
      skipped++;
      continue;
    }

    const yamlText = fs.readFileSync(manifestPath, 'utf8');
    const m = parseManifest(yamlText);
    const md = renderReadme(m, dirName);
    fs.writeFileSync(readmePath, md, 'utf8');
    written++;
  }

  console.log(`Generated ${written} READMEs (${skipped} skipped).`);
}

main();

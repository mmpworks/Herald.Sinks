# CAPABILITY.yaml schema

Every sink in this repo ships a `CAPABILITY.yaml` file at the root of its `src/Herald.Sinks.<Name>/` directory. The schema is stable: the product-sheet generator, the NuGet pack step, and the Herald release pipeline all read these fields without prompting.

No field is optional without a default stated below. A sink that omits a required field fails the monorepo build.

## Full schema

```yaml
# ─── Identity ─────────────────────────────────────────────────────────
name: Herald.Sinks.<Name>                # csproj name, no namespace prefix
package_id: MMP.Herald.Sinks.<Name>      # published NuGet id
version: <semver>                        # matches csproj <Version>
configContract: 1 | 2                    # sink-config contract version.
                                         #   1 (default if omitted): legacy flat
                                         #     `dashboard_config:` form + flat
                                         #     commit shape.
                                         #   2: ships a `configuration-*.mmpform`
                                         #     with a `__properties` block and
                                         #     commits travel as a
                                         #     `properties: {…}` sub-object.
                                         #   The dashboard reads this field and
                                         #   dispatches the matching commit
                                         #   shape; the server accepts either
                                         #   shape during the transition.
kind: sink                               # reserved; always "sink" in this repo
category: >                              # one of: observability | cloud-archive |
                                         #   alerting | analytics | community
  observability

# ─── Human-readable ──────────────────────────────────────────────────
purpose: >                               # one-to-three sentences; flows into
                                         # the NuGet package Description
  Posts log events to <destination>. <one-sentence reason
  to use this sink vs. alternatives>.

vendor:                                  # the owning product
  name: <Vendor Name>
  url: https://example.com

# ─── What ships ──────────────────────────────────────────────────────
ships:                                   # public types consumers use
  - <Name>LogSink
  - <Name>LogSinkProvider
  # include every public type the sink exposes so the product sheet
  # accurately names the API surface

# ─── Dependencies ────────────────────────────────────────────────────
requires:
  core_version: ">=1.0.0"                # minimum Herald.Core version
  nuget:                                 # external NuGet deps beyond Core
    - name: System.Net.Http              # omit if only BCL
      version: ">=4.3.4"
  external:                              # runtime-side prerequisites
    - <destination> account              # e.g. "Datadog API key"
    - reachable HTTP endpoint            # transport assumptions

# ─── JSON-config surface (low-level runtime shape) ───────────────────
# Describes how a handwritten JSON config file maps onto the sink
# provider. Dashboard uses dashboard_config below instead; this block
# remains for operators editing config by hand and for the product
# sheet's "raw config" section.
config:
  kind: <sink-kind-string>               # value for "kind" in JSON config
  uri: <endpoint-or-null>                # what Uri means for this sink
  host: <host-or-null>                   # what Host means for this sink
  alias: <alias-or-null>                 # what Alias means for this sink
  notes: >                               # anything operators need to know
    e.g. default endpoint, override patterns, auth shape

# ─── Dashboard-facing configuration ──────────────────────────────────
# The Dashboard reads this list to auto-generate the sink's
# configuration form. Every entry is a single form field. A sink is
# only as good as what it exposes here — this is the UI contract.
#
# Five required fields per entry:
#   property  — the config key the provider reads at bootstrap
#   name      — human label shown above the field in the Dashboard
#   help      — longer operator guidance shown below the field
#   tooltip   — short hint on hover (one sentence, no line breaks)
#   width     — layout hint. Two accepted forms:
#               1. semantic token: xs | s | m | l | xl
#                    xs ≈ 3 cols, s ≈ 4 cols, m ≈ 6 cols, l = 12, xl = 12
#               2. explicit Vuetify-style column count, integer 1..12
#                    e.g. width: 7  → field spans 7 of the 12 grid cols
#               Out-of-range or unrecognised values default to 6.
#
# Plus a control type (explicit widget name — NOT a data type) and
# control-specific options.
dashboard_config:
  - property: <config-key>               # matches a key the provider reads
    name: <Human label>                  # e.g. "API key"
    help: >                              # multi-line operator guidance
      Longer explanation of what this field controls, when to change it,
      gotchas worth knowing. Flows into the generated product sheet.
    tooltip: <one-sentence hint>         # hover text; no newlines
    width: <token-or-1..12>              # see field rules: xs|s|m|l|xl OR integer 1..12

    control: <control-type>              # required — see control list below

    required: <true | false>             # default false
    default: <value-or-omit>             # applied when left blank
    placeholder: <string-or-omit>        # example value shown in the field

    group: <group-name-or-omit>          # optional section label
    visible_when: <expression-or-omit>   # e.g. "auth_mode == 'api_key'"

    # ─── Per-field error message overrides ──────────────────────────
    # Standard error codes are documented below. Every code has a
    # sensible default message the Dashboard uses unless the sink
    # author overrides it here. Overrides let the sink speak in
    # destination-specific language ("API key must be 40 characters"
    # vs the generic "value is too short").
    errors:
      <error-code>: "<operator-facing message>"
      # Example:
      # required: "Datadog API key is required before events can be sent."
      # pattern:  "API key must be a 32-character hex string."

# ─── Control types ───────────────────────────────────────────────────
# Every control is one concrete widget. Sinks pick the widget, not a
# data type — this keeps the Dashboard deterministic and lets new
# widgets ship without touching existing sinks.
#
#  control: text               plain single-line string input
#    options: min_length, max_length
#
#  control: patterned-text     regex-validated text input
#    options: pattern (named-capture), min_length, max_length
#    standard errors: pattern, min-length, max-length
#
#  control: secret             masked password-style input;
#                              Dashboard never re-emits after save
#    options: min_length
#
#  control: number             integer or decimal input with spinner
#    options: min, max, step, integer (bool — true = int only),
#             unit (text rendered next to the field)
#    standard errors: min, max
#
#  control: checkbox           boolean toggle
#
#  control: select             single-select dropdown
#    options: values OR values_source (required; see below)
#    standard errors: out-of-set
#
#  control: multiselect        multi-select dropdown
#    options: values OR values_source, min_entries, max_entries
#    standard errors: out-of-set, min-entries, max-entries
#
#  control: combobox           text input with a suggestion dropdown;
#                              user can type a custom value too
#    options: values OR values_source (suggestions),
#             pattern (validation for custom entries)
#
# ─── values + values_source (select / multiselect / combobox) ────────
# Pick ONE of the two shapes:
#
# 1. Static list. Two forms accepted:
#
#    # Plain string form — each entry is both the stored value and the
#    # displayed text.
#    values:
#      - us-east-1
#      - us-west-2
#      - eu-west-1
#
#    # Structured form — explicit value/text/description/disabled per entry.
#    values:
#      - value: trace
#        text: "Trace (verbose)"
#        description: "Every pipeline step logs. Dev only."
#      - value: info
#        text: "Info (recommended)"
#      - value: deprecated-mode
#        text: "Legacy"
#        disabled: true                 # visible but not selectable
#
#    Parser accepts both. Mixing shapes in one list is legal — the
#    plain-string entries are expanded to {value: X, text: X} at load.
#
# 2. Dynamic list populated at runtime by a method on the sink.
#    The Dashboard calls the method via the management API; the method
#    returns the option list. Useful for regions, buckets, topics, any
#    list that depends on the caller's credentials or live state.
#
#    values_source:
#      method: GetAvailableRegions   # static method on the sink
#                                    # public static Task<IReadOnlyList<SelectOption>>
#                                    #     GetAvailableRegions(
#                                    #         IReadOnlyDictionary<string,string> currentConfig,
#                                    #         CancellationToken cancellationToken);
#                                    # SelectOption is (Value, Text, Description?, Disabled?)
#      refresh: on-load              # on-load | on-focus | manual
#                                    #   on-load:  fetched when the form opens
#                                    #   on-focus: fetched each time the field
#                                    #             receives focus
#                                    #   manual:   only fetched when the
#                                    #             operator clicks "refresh"
#      depends_on:                   # optional — re-fetch when these
#        - region                    # sibling fields change value
#        - api_key
#      timeout_seconds: 5            # Dashboard aborts after this
#      fallback:                     # shown when the method fails or times out
#        - us-east-1
#      error_action: fallback        # fallback | disable | error-banner
#                                    #   fallback:      use the fallback list
#                                    #   disable:       leave the field empty + disabled
#                                    #   error-banner:  show an error + allow retry
#
# The reflection contract: the method must be `public static` on a
# type inside the sink's assembly. The management API resolves the
# method by full name (`{assembly}::{type}.{method}`) at invoke time.
# Instance methods, non-public methods, or methods outside the sink's
# assembly are rejected — this is an eval-arbitrary-code guard.
#
#  control: url                URL input with URL validation built in
#    options: schemes (whitelist, e.g. ["https"])
#    standard errors: url-malformed, url-scheme-not-allowed
#
#  control: duration           ISO 8601 duration input (e.g. "PT10S")
#    options: min, max (ISO 8601 durations)
#    standard errors: duration-malformed, min, max
#
#  control: key-value-list     add/remove key=value pairs
#    options: key_label, value_label, key_pattern, value_pattern,
#             min_entries, max_entries
#    standard errors: key-pattern, value-pattern, min-entries, max-entries
#
#  control: tag-list           add/remove string entries (no values)
#    options: value_label, pattern, min_entries, max_entries
#    standard errors: pattern, min-entries, max-entries

# ─── Standard error codes ────────────────────────────────────────────
# The Dashboard and the product-sheet generator both recognise these
# codes. A sink can override any code's message with a field-level
# `errors:` block; the codes themselves don't change, so i18n
# tooling keys off them without scanning messages.
#
#   required           field is empty but required
#   pattern            value fails the supplied regex
#   min                number / duration below min
#   max                number / duration above max
#   min-length         string shorter than min_length
#   max-length         string longer than max_length
#   min-entries        list / map has fewer entries than min_entries
#   max-entries        list / map has more entries than max_entries
#   out-of-set         select / multiselect value not in values list
#   key-pattern        key-value-list key fails key_pattern
#   value-pattern      tag-list or key-value-list value fails pattern
#   url-malformed      url control input is not a valid URL
#   url-scheme-not-allowed  url scheme not in the allowed list
#   duration-malformed duration control input is not ISO 8601
#
# Sinks MAY define custom codes for destination-specific validation
# (e.g., a Datadog sink validating API-key format). Custom codes must
# start with the sink's short name to avoid collisions:
#   errors:
#     datadog-api-key-format: "Datadog API keys are 32 hex characters."

# ─── Connectivity probe (optional) ───────────────────────────────────
# If the sink can verify its configuration without sending a live event
# (ping, auth-only probe, HEAD request), it declares the probe method
# here. The Dashboard renders a "Test connection" button that calls it;
# omit the block and the Dashboard hides the button.
connectivity_probe:
  method: <method-name>                  # public static method on the sink:
                                         # Task<bool> Probe(IReadOnlyDictionary<string,string> config)
  timeout_seconds: 10
  success_message: "Ingest endpoint reachable."
  failure_message: "Could not reach the endpoint with the supplied credentials."

# ─── Capabilities (feature bullets) ──────────────────────────────────
capabilities:
  - Batched delivery via IBatchedLogSink        # one bullet per capability
  - Level mapping to <destination>'s vocabulary # operator-facing features
  - <destination>-native exception shape

# ─── Limitations (what we do NOT do) ─────────────────────────────────
limitations:
  - Feature operators sometimes expect but this sink does not support
  - Operational caveat worth naming up front

# ─── Compliance + operational ────────────────────────────────────────
minimum_edition: <Community | Pro | Enterprise>
aot_compatible: <true | false>
thread_safety: >
  one-line threading contract — e.g. "thread-safe: shared HttpClient,
  synchronous Send" or "not thread-safe: caller serializes"

test_coverage: tests/Herald.Sinks.<Name>.Tests/<Name>LogSinkTests.cs (<N> tests)

# ─── Metapackage membership ──────────────────────────────────────────
product_pack:                            # metapackages that include this sink
  - Herald.Business
  - Herald.Game.Pro
  # Empty list is legal for community-only sinks not in any official bundle.

# ─── Maintenance status ──────────────────────────────────────────────
maintenance:
  level: <active | maintained | deprecated | archived>
  owner: <GitHub handle or team>
  last_audit: <YYYY-MM-DD>               # last time someone reviewed the sink

# ─── Breaking changes ────────────────────────────────────────────────
changelog:                               # high-level, one entry per version
  - version: 1.0.0
    date: <YYYY-MM-DD>
    summary: Initial release
  # include only version-bumping changes; leave patch-level bugs to git log
```

## Field-by-field rules

### `name`
Must match the csproj filename without extension. The build validates this on every compile.

### `package_id`
Must start with `MMP.Herald.Sinks.` — the monorepo enforces this prefix so consumers searching NuGet for Herald sinks find the whole catalog together.

### `version`
Must match the `<Version>` element in the csproj. A release-pack script reads this to decide which NuGets to publish; a mismatch fails the script.

### `category`
One of the fixed values listed. Categories drive the product-sheet grouping. To add a new category, edit this schema and the product-sheet generator in the same PR.

### `purpose`
The first sentence flows into the NuGet package Description field. Keep it under 300 characters — NuGet truncates longer values in search results.

### `ships`
Every public type name. Readers use this to know what they can `using` without opening the repo. Missing entries aren't flagged by the build but show up as gaps in the generated product sheet.

### `requires.core_version`
The minimum Herald.Core version tested against. Bump this whenever the sink uses a new Core contract; the release pipeline verifies consumer compatibility.

### `requires.external`
Runtime prerequisites operators need. "Datadog API key" is legitimate; "a fast network connection" isn't — keep entries to things the operator must configure before the sink works.

### `config`
Mirrors the `LoggingRuntimeSinkDefinition` shape Herald uses. Each of `uri`, `host`, `alias` either describes what the field means OR is `null` (the sink doesn't consume it). The product sheet renders a JSON config example from this block.

### `capabilities` / `limitations`
Parallel lists. Together they describe what the sink does and doesn't do. A reader scanning the product sheet scans these two lists first.

### `minimum_edition`
Matches `ILogSinkProvider.MinimumEdition`. The JSON-config validation uses this to reject a sink in a Community-edition build.

### `aot_compatible`
Must reflect reality. Set to `false` if the sink's dependency graph carries `[RequiresUnreferencedCode]` paths OR native binaries. A misstatement here is what causes AOT-published consumers to ship broken binaries; the monorepo's AOT-publish CI gate verifies the claim on every build.

### `thread_safety`
One-line contract. Readers scanning for "can I share this sink across threads?" find the answer here without reading code.

### `product_pack`
Metapackages in the official Herald release that include this sink. The release pipeline reads this to build the metapackage `.nuspec` files. Community-only sinks not in any official pack ship with an empty list.

### `maintenance`
- `active` — current owner responding to issues
- `maintained` — current owner on standby; bug fixes land, features unlikely
- `deprecated` — replaced by another sink; consumers should migrate
- `archived` — no longer tested against new Core versions; install at your own risk

The product sheet groups sinks by maintenance level so new consumers don't start on archived code.

### `changelog`
One-line per bumped version. The release pipeline reads the most recent entry to generate release notes; bugs that ship as patches live in `git log` only, not here.

## Validation

`tools/product-sheet.py` fails when:
- A sink directory exists without a `CAPABILITY.yaml`
- A required field is missing
- `name`, `package_id`, or `version` disagree with the csproj
- `category` or `maintenance.level` use unknown values
- A `ships` entry names a type the csproj doesn't export

These checks run in CI. The monorepo does not produce a release with a failing capability manifest.

## Worked example — Datadog sink

What a real sink's manifest looks like. The Dashboard renders this as eight form fields laid out on a grid, grouped into Endpoint / Auth / Telemetry sections, with a "Test connection" button at the bottom.

```yaml
name: Herald.Sinks.Datadog
package_id: MMP.Herald.Sinks.Datadog
version: 1.0.0
kind: sink
category: observability

purpose: >
  Posts log events to Datadog's HTTP log intake. Works against both
  the public site intake and a local Datadog Agent.

vendor:
  name: Datadog
  url: https://docs.datadoghq.com/api/latest/logs/

ships:
  - DatadogLogSink
  - DatadogLogSinkProvider

requires:
  core_version: ">=1.0.0"
  external:
    - Datadog API key (DD-API-KEY)
    - Reachable HTTP endpoint (public intake or local Agent)

config:
  kind: datadog
  uri: "Datadog intake URL, e.g. https://http-intake.logs.datadoghq.com"
  host: "Service name (Datadog 'service' attribute)"
  alias: "DD-API-KEY header value"
  notes: >
    Uri optional — defaults to the public US intake. Set to
    http://localhost:8126 to route through a local Datadog Agent.

dashboard_config:
  - property: endpoint
    name: Ingest endpoint
    help: >
      Public US intake is the default. Use https://http-intake.logs.datadoghq.eu
      for the EU region, http://localhost:8126 to route through a local
      Datadog Agent, or a site-specific intake (us3, us5) as needed.
    tooltip: Datadog HTTP log intake URL or local Agent address.
    width: l
    control: url
    required: false
    default: https://http-intake.logs.datadoghq.com
    placeholder: https://http-intake.logs.datadoghq.com
    group: Endpoint
    schemes: [http, https]
    errors:
      url-malformed: "Endpoint must be a full URL including scheme."

  - property: api_key
    name: API key
    help: >
      The Datadog DD-API-KEY header value. Stored encrypted by the
      Dashboard; rotation clears the stored value.
    tooltip: Datadog DD-API-KEY — masked after save.
    width: m
    control: secret
    required: true
    min_length: 32
    group: Auth
    errors:
      required: "Datadog API key is required before events can be sent."
      min-length: "API keys are at least 32 characters."

  - property: service
    name: Service name
    help: >
      Populates Datadog's "service" attribute on every event. Used for
      routing in the Datadog UI and for correlating logs with traces.
    tooltip: Datadog service tag for every event emitted here.
    width: m
    control: patterned-text
    required: true
    default: herald
    pattern: "^(?<svc>[a-z0-9-]+)$"
    group: Telemetry
    errors:
      pattern: "Lowercase letters, numbers, and hyphens only."

  - property: ddsource
    name: Source
    help: >
      Populates Datadog's "ddsource" attribute. Defaults to "herald".
      Change for per-pipeline differentiation (e.g. "herald-audit" vs
      "herald-game").
    tooltip: Datadog ddsource attribute, defaults to "herald".
    width: s
    control: text
    required: false
    default: herald
    group: Telemetry

  - property: hostname
    name: Host name
    help: >
      Populates Datadog's "hostname" attribute. When blank the Dashboard
      uses the process's Environment.MachineName.
    tooltip: Hostname reported to Datadog; defaults to machine name.
    width: s
    control: text
    required: false
    group: Telemetry

  - property: static_tags
    name: Static tags
    help: >
      Tags attached to every event, joined into ddtags. Common pairs:
      env=prod, version=1.2.3, region=us-east-1. Category is added
      automatically per event; do not add it here.
    tooltip: Key/value pairs attached to every event as ddtags.
    width: l
    control: key-value-list
    required: false
    group: Telemetry
    key_label: Tag key
    value_label: Tag value
    key_pattern: "^(?<k>[a-zA-Z_][a-zA-Z0-9_]*)$"
    max_entries: 32
    errors:
      key-pattern: "Tag keys start with a letter or underscore."
      max-entries: "Datadog accepts at most 32 static tags per sink."

  - property: min_level
    name: Minimum level
    help: >
      Events below this level are dropped before they reach Datadog.
      Save pipeline budget when Datadog is an alerting sink, not a
      primary log store.
    tooltip: Pipeline-level floor for events sent to this sink.
    width: s
    control: select
    required: false
    default: info
    # Structured value/text form — each entry pairs the stored value
    # with the displayed label, with an optional description.
    values:
      - value: trace
        text: "Trace (verbose)"
        description: "Every pipeline step logs. Dev only."
      - value: debug
        text: "Debug"
      - value: info
        text: "Info (recommended)"
      - value: warn
        text: "Warn"
      - value: error
        text: "Error"
      - value: critical
        text: "Critical"

  - property: site
    name: Datadog site
    help: >
      Pulled live from Datadog's public sites endpoint via your
      configured API key. Refreshes when you rotate the key. Falls
      back to the four common sites if the lookup fails.
    tooltip: Datadog site (us1, eu, us3, us5, ap1, gov...)
    width: s
    control: select
    required: false
    group: Endpoint
    # Dynamic — Dashboard calls Datadog's static GetSites method
    # server-side with the current API key, renders the returned list.
    values_source:
      method: GetSites
      refresh: on-focus
      depends_on: [api_key]
      timeout_seconds: 5
      error_action: fallback
      fallback:
        - value: datadoghq.com
          text: "US1 (datadoghq.com)"
        - value: datadoghq.eu
          text: "EU (datadoghq.eu)"
        - value: us3.datadoghq.com
          text: "US3 (us3.datadoghq.com)"
        - value: us5.datadoghq.com
          text: "US5 (us5.datadoghq.com)"

capabilities:
  - Batched delivery via IBatchedLogSink
  - Level mapping to Datadog status (trace→debug, fatal→emergency)
  - Exception triple (error.message / error.kind / error.stack)
  - Static tags merge with per-event category into ddtags

limitations:
  - No compression today (gzip support is a future enhancement)
  - Static tags only via Dashboard or code; JSON config limited

minimum_edition: Enterprise
aot_compatible: true
thread_safety: Thread-safe — shared HttpClient, synchronous Send.
test_coverage: tests/Herald.Sinks.Datadog.Tests/DatadogLogSinkTests.cs (10 tests)

product_pack:
  - Herald.Business
  - Herald.Game.Pro

maintenance:
  level: active
  owner: smuchow1962
  last_audit: 2026-04-24

changelog:
  - version: 1.0.0
    date: 2026-04-24
    summary: Initial release

connectivity_probe:
  method: ProbeAsync
  timeout_seconds: 10
  success_message: Connected to Datadog intake.
  failure_message: Could not reach the intake with the supplied key.
```

## What the Dashboard renders from this

A form laid out like:

```
┌─ Endpoint ──────────────────────────────────────────────────────────┐
│ [Ingest endpoint                                            ] (l)   │
│  Help text about intakes and Agent routing...                       │
└─────────────────────────────────────────────────────────────────────┘

┌─ Auth ──────────────────────────────────────────────────────────────┐
│ [API key (masked)                       ] (m)                       │
│  Help: stored encrypted, rotation clears.                           │
└─────────────────────────────────────────────────────────────────────┘

┌─ Telemetry ─────────────────────────────────────────────────────────┐
│ [Service name                           ] (m)  [Source  ] (s)       │
│  ... help ...                                   ... help ...         │
│                                                                      │
│ [Host name ] (s)   [Minimum level (select) ] (s)                    │
│                                                                      │
│ Static tags (l, full row):                                          │
│  ┌ Tag key ┬ Tag value ┐                                            │
│  │ env     │ prod      │ [×]                                        │
│  │ version │ 1.2.3     │ [×]                                        │
│  └─────────┴───────────┘ [+ add]                                    │
└─────────────────────────────────────────────────────────────────────┘

  [ Test connection ]                    [ Save ]  [ Cancel ]
```

Widths (`s`, `m`, `l`) give the Dashboard layout control without over-specifying pixels — the UI decides the actual width for a row, small = a quarter, medium = half, large = full row. Groups render as accordion sections or vertical dividers depending on form length. Required fields get a visible marker; `help` lives under the field; `tooltip` lives on hover.

No per-sink Dashboard code. A new community sink's YAML lands in the monorepo and the Dashboard shows it with the right form on the next release.

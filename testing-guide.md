# Herald sink testing guide

This guide walks through how to test a sink end-to-end against the contracts the Herald management API and dashboard rely on. Every Herald sink ships against the same three contracts, and there are reference test files in `Herald.Sinks.File.Tests` and `Modules/Core/tests` you can copy from.

The patterns here apply to any sink — file-based, network, custom. The file-sink tests are the canonical example because they cover all three contracts.

## What the tests prove

A sink that ships into the Herald ecosystem speaks three contracts, and every one of them needs a test that fails when the contract breaks:

1. **The JSON binding shape** — the camelCase keys the dashboard POSTs and the management API deserialises. A typed record with `[JsonPropertyName]` attributes is the source of truth.
2. **The management-API round-trip** — `CommitFull` parses the JSON, calls the matching builder method, and an `Inspect()` afterward shows the values applied. The other direction (`GetPipelineFlow` → form refresh) returns the same values.
3. **The per-kind form discovery** — `ILogSinkProvider.GetFormSchemaText()` resolves the right `configuration-{SinkKind}.mmpform` resource so the dashboard renders the right form.

Plus, when the sink does something concrete on disk (rolling, pruning, tee, etc.), an end-to-end test that pushes one write through the runtime and asserts the side-effect — the only way to prove the wiring survives a hot-swap.

## Reference test files

Look at these before writing your own:

- `Modules/Core/tests/Configuration/FileSinkConfigTests.cs` — JSON shape (Contract 1)
- `Modules/Core/tests/Addons/CommitFullFileSinkTests.cs` — management API round-trip + end-to-end pruning (Contracts 2 + 4)
- `Modules/Herald.Sinks/tests/Herald.Sinks.File.Tests/FormSchemaDiscoveryTests.cs` — embedded form lookup (Contract 3)
- `Modules/Core/tests/Rolling/RetentionPolicyTests.cs` — direct policy/writer tests where useful

## Setting up a test project

The project layout for a sink test looks the same as `Herald.Sinks.File.Tests`. From `Herald.Sinks/tests/Herald.Sinks.<YourSink>.Tests/`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Herald.Sinks.<YourSink>\Herald.Sinks.<YourSink>.csproj" />
  </ItemGroup>
</Project>
```

`Directory.Build.props` at the `Herald.Sinks` repo root takes care of common test packages and target frameworks; no extra wiring needed.

## Contract 1: the JSON binding shape

Every sink that takes structured config exposes a typed record matching the form's `binding` names. Deserialisation needs to handle:

- All keys present
- Optional keys omitted (deserialise as `null`)
- Empty `{}` (defaults populated correctly)
- Unknown future keys (silently ignored — payload evolution)
- Round-trip stability (serialise → deserialise → equivalent)

### Example

```csharp
// Lines from FileSinkConfigTests.cs
[Fact]
public void Deserializes_every_v2_field_from_camelCase_json()
{
    const string json = """
    {
      "logDirectory":     "logs",
      "logFileTemplate":  "app-server",
      "logExtension":     "ndjson",
      "rollingLogsEnabled": true,
      "rollingInterval":  "hourly",
      "maxFileSize":      "10MB",
      "retentionDays":    14
    }
    """;

    var config = JsonSerializer.Deserialize<FileSinkConfig>(json);

    config.Should().NotBeNull();
    config!.LogDirectory.Should().Be("logs");
    config.LogFileTemplate.Should().Be("app-server");
    config.RollingLogsEnabled.Should().BeTrue();
    config.RetentionDays.Should().Be(14);
}
```

### Technical notes

The JSON shape is what the dashboard form posts. The form's `binding` (e.g. `{logDirectory}`) becomes the JSON key. If the record's `[JsonPropertyName]` doesn't match the form's binding, edits silently no-op. This test is your earliest signal that they drifted apart.

## Contract 2: the management-API round-trip

This is the most important contract for an installed sink. It proves:

- A commit POST applies values to the builder
- The values survive the hot-swap that runs after every commit
- The next `GetPipelineFlow` call returns the same values back to the dashboard

The pattern: instantiate a builder + management API, post a commit JSON, assert against `Inspect()` and against `GetPipelineFlow().Sinks[i].Config`.

### Example: each field individually

```csharp
// Lines adapted from CommitFullFileSinkTests.cs
[Fact]
public void CommitFull_applies_retentionDays()
{
    var (builder, api) = CreatePipeline();
    var json = CommitJson($$"""
        {
          "logDirectory":     "{{TempDirAsForward}}",
          "logFileTemplate":  "app",
          "logExtension":     "log",
          "rollingLogsEnabled": true,
          "rollingInterval":  "daily",
          "retentionDays":    21
        }
        """);

    api.CommitFull(json).Success.Should().BeTrue();

    builder.Inspect().RetentionDays.Should().Be(21);
}
```

Cover every field your sink accepts. If a field doesn't appear in `Inspect()`, the management API forgot to wire it through to the builder. That's the regression this test catches.

### Example: the round-trip

```csharp
[Fact]
public void Round_trip_returns_every_committed_v2_field_through_GetPipelineFlow()
{
    var (_, api) = CreatePipeline();
    var json = CommitJson(/* full payload */);

    api.CommitFull(json).Success.Should().BeTrue();

    var sink = api.GetPipelineFlow().Sinks.Single(s => s.SinkId == YourSinkKind);
    var config = sink.Config!;

    config["yourField"].Should().Be(committedValue);
    // ...one assertion per field
}
```

### Edge cases worth testing

- **Disabled feature toggles** (e.g. `rollingLogsEnabled: false`) — verify the corresponding fields are omitted from the response. The dashboard form treats missing keys as "not set".
- **Required-field gates** (e.g. file sinks need a `logFileTemplate` to build a path). Document the gate and prove it stays a no-op without throwing.
- **Repeated commits with the same payload** — the resulting state should be byte-identical. A test like `Round_trip_preserves_path_under_repeated_commits` catches identity churn that would force a hot-swap on every save.

## Contract 3: per-kind form-schema discovery

A package that ships multiple providers (`Herald.Sinks.File` ships text and JSON) embeds one `configuration-{SinkKind}.mmpform` per provider. The default `ILogSinkProvider.GetFormSchemaText()` resolves the right one off the assembly.

### Example

```csharp
// Lines from FormSchemaDiscoveryTests.cs
[Fact]
public void TextFileSinkProvider_returns_its_per_kind_mmpform()
{
    var provider = new TextFileSinkProvider();

    var formText = provider.GetFormSchemaText();

    formText.Should().NotBeNullOrEmpty();
    formText!.Should().Contain("Text File");                 // sentinel from text form
    formText.Should().NotContain("Writes NDJSON log files"); // sentinel that would mean json form leaked
}
```

The sentinels matter: pick a string from your form that **wouldn't** appear in any sibling provider's form. That way a wiring mistake (text provider returning the json form) fails the test loudly.

### Bind your form to the wire contract

Lock the form's binding names to the JSON record at the test layer:

```csharp
[Fact]
public void Form_uses_the_v2_binding_names()
{
    var form = new YourSinkProvider().GetFormSchemaText();

    form.Should().Contain("{yourField1}");
    form.Should().Contain("{yourField2}");
    form.Should().Contain("{yourField3}");
}
```

That keeps the `.mmpform` and the typed record from drifting apart silently. If a binding rename in the form breaks the JSON contract, the test fails before anyone files a bug.

## End-to-end: write triggers the side-effect

For sinks that do work on disk or over the network, the contracts above are necessary but not sufficient. The wiring between the runtime pipeline and the actual writer needs its own test.

The pattern:

1. Set up the side-effect's preconditions (pre-existing files, mocked endpoint, etc.)
2. Build the pipeline + management API normally
3. Commit the config that should trigger the behaviour
4. Push **one** event through `result.Logger.Info(...)`
5. Assert the side-effect happened

### Example: retention pruning

```csharp
// Lines from CommitFullFileSinkTests.cs
[Fact]
public void CommitFull_with_retentionDays_prunes_older_files_on_next_write()
{
    var oldFile = Path.Combine(_tempDir, "app-19000101.log");
    File.WriteAllText(oldFile, "ancient log data");
    File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-90));

    var (_, result, api) = CreatePipelineWithLogger();
    api.CommitFull(/* retentionDays: 30, rolling enabled */).Success.Should().BeTrue();

    result.Logger.Info(LogCategory.App, "trigger retention sweep");

    File.Exists(oldFile).Should().BeFalse();
}
```

### Technical notes

The original `result.Logger` reference survives the hot-swap that runs inside `CommitFull` — the inner kernel gets replaced but the outer wrapper stays. So tests can hold one logger reference across multiple commits and rely on every write routing through the latest config.

If your sink's side-effect happens **per write** (counters, batched HTTP, file rolls), one event is enough to prove wiring. If it happens on a timer (flush every N seconds), the test will need a clock abstraction or a longer harness.

## Running the tests

The Herald.Sinks repo wraps `dotnet test` for you:

```bash
bash build.sh --release --test
```

Or per-sink:

```bash
dotnet test "Modules/Herald.Sinks/tests/Herald.Sinks.<YourSink>.Tests/"
```

For Core-side contracts (`FileSinkConfig`, `CommitFull`, etc.), tests live in the Core test project:

```bash
bash build.sh --core --test
```

## Common pitfalls

- **Backslashes in temp paths on Windows.** `Path.GetTempPath()` returns Windows-style separators. Convert to forward slashes before embedding in JSON, or the assertions on path round-trips will be brittle. The reference tests use a `TempDirAsForward` helper.
- **`Inspect()` reflects the builder, not the live pipeline.** That matters when a commit triggers a hot-swap — `Inspect()` shows the new state immediately, but holding an old logger reference still routes through the new pipeline. Both are correct, just remember which one each test is asserting against.
- **`FormatBytes` round-trip is integer-truncating.** `5MB` → `5242880 bytes` → `"5MB"` is stable, but `1572864 bytes` → `"1MB"` (integer division). If your tests use unusual sizes, pick clean multiples of the unit you care about.
- **The form-schema lookup falls back to `configuration.mmpform`** when no per-kind file is embedded. A package that ships only one provider can use either layout; a multi-provider package like `Herald.Sinks.File` should use the per-kind layout so each provider gets its own form.
- **Embedded resources are picky about names.** `Directory.Build.props` in the Herald.Sinks repo globs `configuration*.mmpform` and assigns each one its filename as the logical name. The default `GetFormSchemaText()` looks for `configuration-{SinkKind}.mmpform` first; the kind has to match `SinkKind` exactly (e.g. `text_file`, not `textfile`).

## What good test coverage looks like

A sink ships with confidence when:

- Every field of its typed config record has a JSON deserialisation test
- Every field has a `CommitFull` apply test asserting against `Inspect()`
- The full payload has a round-trip test against `GetPipelineFlow`
- Each provider has a form-discovery test with a unique sentinel
- The form's bindings have a test linking them to the typed record
- Any disk/network side-effect has a one-write end-to-end test
- Edge cases (disabled toggles, missing required fields, repeated commits) each have a test asserting the documented behaviour

The reference test files in this repo cover all of the above for the file sinks. Adapt them for your sink and the contract regressions become impossible to ship without a CI failure.

## tests/_shared

Vendored copies of Herald.Core's test helpers (`Modules/Core/tests/Helpers/*.cs`),
linked into every per-sink test project via `<Compile Include="..\_shared\*.cs" />`.

These helpers depend only on the public Herald.OSS surface (`MMP.Herald.Time`,
`MMP.Herald.Events`, `MMP.Herald.Pipeline`, `MMP.Herald.Pipeline.Kernel`,
`MMP.Herald.Templating`, `MMP.Herald.Levels`) — no Core internals.

**Refresh policy:** when Core's helpers change, copy the updated files back
into this directory. The cross-repo bridge that used to live in each test
csproj (`<Compile Include="..\..\..\Core\tests\Helpers\*.cs" ... />`) only
worked inside the monorepo and broke standalone clones.

# OhanaCli Phase 4 Validation and Quality Gates

## Build and run

```bash
dotnet build src/PokemonGreen.OhanaCli/OhanaCli.sln
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- --help
```

If tests are present:

```bash
dotnet test src/PokemonGreen.OhanaCli/tests/OhanaCli.App.Tests/OhanaCli.App.Tests.csproj
```

## Validation workflow

1. Diagnose sample entries for blocker triage:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- diagnose "<sample.garc>" --start 0 --end 20
```

The output includes per-entry model/mesh/texture counts and animation segment summaries:
- `segmentSummary=euler=...,quaternion=...,matrix=...,axisAngle=...`
- `notes=...` with animation count, nested container clues, or parse errors.

2. Convert with animation diagnostics enabled:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "<sample.garc>" -o "<out>" -f dae -a 0 --diag-anim
```

`--diag-anim` forwards exporter diagnostics for each bone, including:
- detected segment mode (`transformEuler`, `transformQuaternion`, `transformMatrix`, `transformAxisAngle`)
- keyframe/vector/matrix counts
- explicit skip reasons when a bone is not exported.

3. Batch gate for parity trend checks:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- batch "<garc-dir>" -o "<out>" -f dae -a 0 -n 20 --diag-anim
```

Batch summary prints totals with clear failure breakdown:
- `succeeded`
- `partial`
- `fatal`
- `failedTotal`

4. Optional scripted parity run across sample GARCs:

```powershell
powershell -ExecutionPolicy Bypass -File src/PokemonGreen.OhanaCli/scripts/Run-ManualParityChecks.ps1 -SampleGarcDir "<samples>" -OutputDir "<logs>" -Limit 20
```

## Exit code gates

- `0`: success, no failures
- `2`: partial failure (mixed success/failure)
- `1`: fatal failure (nothing succeeded, invalid input, or unhandled error)

## Known remaining risks (from docs 18, 19, 20)

- Animation distortion risk remains format-side: missing or misinterpreted bone defaults and segment handling can still deform models in downstream tools.
- `isAxisAngle`, quaternion frame data, and full baked matrix segments are now diagnosable, but visual parity still depends on exporter sampling and importer conventions.
- Some Pokemon model variants still produce zero meshes (documented missing-ID ranges); these remain out of scope for Phase 4 and should be tracked via diagnose/batch logs.

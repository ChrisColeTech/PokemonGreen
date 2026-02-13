# Map Generation Workflow to Game Pipeline

## Generate / Regenerate in the editor

1. In the left sidebar `Random Generation` panel, choose:
   - `Archetype`
   - optional `Starter Template`
   - `Seed` (keep this fixed for reproducible runs)
2. Leave `Use current map size` enabled to generate into the current canvas dimensions, or disable it and set `Gen Width`/`Gen Height`.
3. Set `Repair Attempts` (default `3` is recommended).
4. Click `Generate`:
   - Runs generation passes including balancing, validation, and bounded repair.
   - Stores diagnostics + pass timings.
5. Click `Regenerate` to rerun with the same seed and settings for deterministic reproduction.

## Export and run map pipeline into the game

1. Export from editor via `File -> Export` (or `Save/Save As`) and write `*.map.json` into `Assets/Maps` in the main repo.
2. From repository root (`PokemonGreen`), run map generation:

```powershell
./tools/run-mapgen.ps1
```

3. Build the game solution:

```bash
dotnet build PokemonGreen.sln
```

4. Generated runtime map code is emitted to `src/PokemonGreen.Core/Maps/*.g.cs`.

## Troubleshooting validation / repair warnings

- `Major Issues > 0` after generation means hard constraints are still unresolved after repair attempts; try increasing `Repair Attempts` or using a less constrained dimension/template combination.
- `Could not place required building ...` warnings can occur during the initial placement pass; if no `minRequiredStructures` issue remains, repair successfully backfilled requirements.
- `Generation completed with ... unresolved hard constraint issue(s)` means the map should be reviewed before export.
- If runtime mapgen fails after export, verify schema and IDs using `docs/map-pipeline.md` at repo root and rerun mapgen from a clean `Assets/Maps` set.

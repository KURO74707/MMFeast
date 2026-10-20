# MMFeast

Unity prototype for a co-op action / cooking roguelike centered on a food truck.

## Open the project

1. Clone this repository.
2. In Unity Hub, add the repository folder (the folder containing `Assets`, `Packages`, and `ProjectSettings`).
3. Open with **Unity 6000.6.4f1**, as recorded in `ProjectSettings/ProjectVersion.txt`.
4. Allow Unity to restore packages and import assets, then open a prototype scene from `Assets`.

Commit `Assets` together with all `.meta` files, `Packages`, and `ProjectSettings`. Unity regenerates `Library`, temporary files, and IDE project files locally; these are ignored.

## Prototype documentation

- `PROTOTYPE_README.md`: prototype setup and gameplay.
- `GLOBAL_DATA_README.md`: shared designer configuration.
- `AGENTS.md`: project development conventions.
- `Validation/`: validation scripts and performance summaries.
- `NPC_PERFORMANCE_REPORT.txt`: latest NPC optimization measurements and limitations.

Large raw Profiler captures and `Validation/NpcPerformanceResults.zip` remain local and are excluded from source control. Summary results and validation source are included.

## Existing web prototype

The repository also retains its original TypeScript / Phaser web prototype in `src/`, with `index.html`, `package.json`, and `package-lock.json` at the root. These files are separate from the Unity project. Its original development commands remain `npm install`, `npm run dev`, and `npm run build`.

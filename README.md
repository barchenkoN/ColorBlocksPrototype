# Color Blocks Prototype

Mobile portrait gameplay prototype made with Unity 6.3 LTS (`6000.3.19f1`) and URP. It implements the requested five-lane / five-slot color-clearing loop with an original toy-tank presentation. No name, branding, art, materials, sounds, or UI from the reference application are included.

## iPhone gameplay

[Watch the 10-second physical iPhone recording](Demo/iPhone-gameplay.mp4). The video was captured from the installed iOS device build using the iPhone's built-in screen recorder.

## Open and run

1. Install Unity `6000.3.19f1` with Android Build Support, OpenJDK, and Android SDK/NDK Tools. iOS Build Support is only needed for an iOS export.
2. Open this folder as a Unity project.
3. Open `Assets/_Game/Scenes/Game.unity`.
4. Enter Play Mode. A mouse click emulates a mobile tap in the Editor.

The scene, presentation assets, level catalog, and five levels are committed. To regenerate them, use `Color Blocks > Rebuild Prototype Content`. The rebuild also runs structural and exhaustive solver validation before saving.

Build helpers exist under:

- `Color Blocks > Build > Android Development APK`
- `Color Blocks > Build > Export iOS Xcode Project`

For iPhone, select iOS in Unity Build Profiles and use the normal `Build` command, or run the equivalent menu helper above. Open the generated Xcode project on macOS, select a Development Team and the connected iPhone, then use Xcode `Run`. No Apple or Unity credentials belong in this repository.

For Android, select Android in Build Profiles and use `Build`, or run the Android menu helper. The committed settings target IL2CPP/ARM64 and Android API 26 or newer.

## Implemented gameplay

- Five independent vertical queues. Only the front unit in each lane is selectable.
- Five active slots. A selected unit always moves to the leftmost free slot.
- Units automatically attack a matching exposed frontier block and wait without spending ammo when no match exists.
- Up to six projectiles can be in flight per active unit; every projectile reserves one target and consumes exactly one charge.
- Slots are released only after a zero-charge unit finishes all pending shots and its exit animation.
- Deterministic column gravity. When the final layer of a stack is removed, every stack above it compacts downward without Rigidbody drift.
- Arbitrary stack depth in the model; shipped content intentionally uses a maximum of two layers.
- Level 3 is the first two-layer level. Removing the top layer reveals and animates the layer below.
- Win, quiescent `Out of Space` loss, restart, and continue states.
- Sequence: levels 1 and 2, then shuffled cycles of 3/4/5. Each cycle uses every level once and avoids a repeat at the cycle boundary.
- Portrait safe-area HUD, level pill, restart control, result cards, responsive perspective framing, audio, and light/soft/heavy native haptics.

Excluded as requested: tutorial, meta progression, shop, economy, ads, IAP, lives, boosters, blockers, online features, and copied reference branding.

## Exact rule interpretations

The reference leaves some implementation details implicit. This prototype uses the following deterministic rules:

1. Each board X coordinate is a vertical column.
2. Every column is compacted bottom-to-top at load time and again whenever a whole stack is cleared; stack order never changes.
3. The bottom stack in a column is its targetable frontier. Higher stacks fall toward it only after space opens below.
4. Inside the frontier stack, only its highest surviving depth layer is targetable. Every upper cube sits directly on the cube below with matching X/Y and exact face contact in Z. Removing the upper cube reveals the lower cube's full top face without moving it away from its physical support.
5. A reservation temporarily blocks that exact exposed layer so simultaneous projectiles cannot select it twice; the reservation remains attached while its stack moves.
6. A unit chooses a matching available column nearest its active slot; equal distances resolve to the lower X coordinate.
7. An exhausted unit leaves only after its final projectile resolves.
8. Loss is evaluated only when gameplay is stable: blocks remain, all five slots are occupied or no queue choice remains, no unit can hit the current frontier, and no move/projectile/exit is pending.

These rules produce the reference behavior where a unit can wait, a covered color can become available later, and careless selections can fill all five slots.

## Levels and difficulty

All block colors have exactly matching total ammo, all coordinates use the declared board bounds, and every level is solver-proven winnable.

| Level | Board | Occupied cells | Block layers | Double cells | Units | Design intent |
|---|---:|---:|---:|---:|---:|---|
| 1 | 8 x 7 | 50 | 50 | 0 | 6 | Dense three-color introduction |
| 2 | 9 x 7 | 57 | 57 | 0 | 7 | Four-color red-gated opening and reproducible bad-choice loss |
| 3 | 9 x 8 | 64 | 77 | 13 | 10 | First layered board; staged reveal and gravity opening |
| 4 | 10 x 8 | 72 | 90 | 18 | 10 | Full-width five-color layered planning |
| 5 | 10 x 10 | 92 | 122 | 30 | 15 | Reference-scale densest board and longest queue plan |

Levels 2 and 3 deliberately start with a red-only frontier. Selecting the red opening unit makes progress and exposes the next colors. Five incorrect blocker selections reach a real `Out of Space` state; this behavior is covered by both solver and live-scene tests.

## Architecture

`Assets/_Game` is isolated with assembly definitions:

- `Runtime/Core` - serializable level data, compacting board/frontier model, moving reservations, validation, level sequence, deterministic simulator, and memoized solver. It has no scene dependencies.
- `Runtime/Gameplay` - state machine and orchestration for queues, slots, input, combat, transitions, win, and loss.
- `Runtime/Presentation` - centralized `GameFeelProfile` for reference-matched layout and timing, code-native board/units/HUD, authored motion, pooled projectile and impact-effect meshes, and shared presentation materials.
- `Runtime/Services` - capped audio-source service and platform haptics behind a small interface.
- `Runtime/Bootstrap` - scene startup, resource checks, camera, lighting, orientation, and frame-rate setup.
- `Editor` - deterministic content rebuild, level diagnostics, custom Inspector, build helpers, and repeatable visual-capture tooling.
- `Tests` - EditMode model/authoring tests and PlayMode end-to-end gameplay tests.

The gameplay controller does not encode a fixed layer count. Each cell stores colors bottom-to-top; adding a third layer in level data automatically participates in targeting, reveal, destruction, solver validation, and win detection.

## Level authoring

Each `LevelDefinition` stores:

- width and height;
- occupied `(x, y)` cells;
- one or more colors per cell, ordered bottom-to-top;
- exactly five ordered unit lanes;
- color and positive charge count for every unit.

The custom Inspector reports invalid coordinates, duplicates, empty stacks, invalid lane count, invalid colors, non-positive charges, and insufficient ammo. `Color Blocks > Log Level Diagnostics` prints board size, layer count, unit count, explored solver states, and a valid lane-choice solution.

For production-scale authoring, add a grid painter, batch difficulty scoring, and automated generation of intentional fail paths. The current data-driven workflow is sufficient for the five-level MVP.

## Presentation, audio, and haptics

- Full 3D procedural presentation in URP. Board, blocks, stack layers, slots, tank units, projectiles, trails, and impact fragments are mesh objects with real Z depth, lit materials, perspective projection, and realtime soft shadows. Only the HUD is screen-space UI.
- Fixed reference tile scale with narrow seams and a calibrated 29.5-degree perspective view. Base cubes rest on the tray; upper cubes are real stacked meshes one cube depth closer to the camera, making both physical layers and their molded side details readable.
- A 0.18 s selection move with short overshoot, phase-preserved 0.067 s firing cadence, non-accumulating recoil and colored muzzle flash, pooled volumetric projectiles and trails, same-frame block replacement, pooled colored impact flash and 3-axis fragments, 0.10 s layer reveal, and deterministic Y-only stack fall with one positional overshoot and monotonic settle.
- Imported one-shot audio with bounded voice count and small pitch variation. No runtime tone generation.
- iOS uses cached `UIImpactFeedbackGenerator` instances for light, soft, and heavy feedback.
- Android uses predefined `VibrationEffect` feedback where available, with safe fallbacks and session-level failure disabling.

## Mobile and performance notes

- Target: 60 FPS, portrait, linear color, IL2CPP, ARM64.
- Android minimum API 26; custom manifest includes vibration permission.
- iOS deployment target 15.0.
- Mobile URP: HDR disabled, 2x MSAA, 1.0 render scale, 1024 px main-light shadow map, realtime soft shadows, and a calibrated 230-unit shadow distance for the narrow perspective lens.
- The main directional light provides short contact shadows; a weak shadow-free fill light preserves color and face readability on mobile.
- Shared mesh/materials and `MaterialPropertyBlock` tinting prevent per-object material churn.
- Projectile and impact-effect meshes are pooled; audio uses eight capped `AudioSource` voices.
- Input physics is limited to a tap raycast against currently selectable queue units.
- Combat update work is bounded by five active slots and compact, bounded grids.
- The HUD uses `CanvasScaler`, `SafeAreaFitter`, and a 1080 x 1920 reference layout.

Physical-device profiling is still recommended before a production release, particularly on a low-end Android device and a notched iPhone.

## Validation

Current automated coverage:

- **33 EditMode tests:** compacting/frontier rules, stack-order preservation, arbitrary depth, rejection of unsupported layers, moving reservations, deterministic priority, level sequence, malformed authoring data, exact ammo balance, dense canonical board/layer/unit counts, solver-proven completion, reproducible strategic losses, rounded 3D mesh integrity, measured perspective layout, camera fit at 720 x 1600 / 1080 x 1920 / 1170 x 2532, projected Z-layer separation, phase-preserved cadence, and the single-axis fall curve.
- **12 PlayMode tests:** clean perspective startup with realtime shadow checks, a real screen-space perspective raycast, physical Level 3 cube contact at spawn and throughout fall/reveal, volumetric projectile checks, selection-to-impact-to-gravity-to-restart integration, live solver completion of all five levels, truthful win/loss states, rapid repeated input/restart/result-action handling, and rapid-fire cadence/recoil-rest validation.

The Editor-only `VisualCapture` harness renders the live camera and HUD at 1080 x 1920, 720 x 1600, and 1170 x 2532. It additionally captures a simulated safe area, the initial layered Level 3 board, selection at approximately 200 ms, firing at approximately 430 ms, settled gravity, the dense Level 5 board, and win/loss states. Do not pass `-nographics` when using this renderer.

Example:

```powershell
Unity.exe -batchmode -projectPath <project> `
  -executeMethod ColorBlocks.Editor.VisualCapture.RunMobile `
  -captureOutput <output-folder> -logFile <capture-log>
```

## Known MVP limitations and production follow-up

- Visuals are a coherent original procedural style, not a final outsourced art pack.
- Safe area and aspect ratios are visually simulated in Editor; the submitted iOS build is additionally smoke-tested and recorded on a physical iPhone. A production release should add a broader iOS/Android device matrix.
- Audio has no user-facing mixer/settings screen because settings/meta UI is outside this assignment.
- No persistence is required for the five-level prototype. A production game should add save migration, localization, accessibility settings, analytics, and device-farm coverage.
- iOS native haptics cannot be executed in the Windows Editor and must be verified from an Xcode device build.

## Third-party assets and AI disclosure

- **Fredoka variable font** from Google Fonts, licensed under SIL Open Font License 1.1. License: `Assets/_Game/Art/Fonts/Fredoka-OFL.txt`.
- **Kenney audio clips** from Interface Sounds, UI Audio, and Impact Sounds, licensed CC0. License: `Assets/_Game/Audio/Kenney/Kenney-CC0-License.txt`.
- Unity URP, Input System, uGUI/TMP, and Test Framework packages are used under their Unity package licenses.
- OpenAI Codex assisted with reference research, architecture, implementation, original procedural presentation, tests, visual QA tooling, and documentation.

Approximate active project time: **15 hours** for research, planning, implementation, content, reference matching, and validation. This was an AI-assisted iteration, so the figure is an estimate rather than directly comparable manual engineering hours.

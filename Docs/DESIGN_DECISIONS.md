# Technical and design decisions (dated, with reasons)

Every choice that closes one path and leaves another open is recorded here, so that in six months nobody has to ask "why is this written this way". Format: date — decision — reason — the cost we accepted.

## 2026-10-17 — Sound input behind an `IEchoSource` interface

**Decision:** three implementations for the sound source: `MicrophoneEchoSource`, `SimulatedEchoSource`, `ScriptedEchoSource`. The rest of the game only ever deals with `Loudness` and `RawRms`.
**Reason:** Unity editor microphone support on Linux cannot be relied on and the lead programmer works on Linux; also the math tests need deterministic input.
**Cost:** one extra layer that must always stay in sync; the real microphone path does not run until week 3, on Windows.

## 2026-10-17 — The arc fracture mask is computed once per ping

**Decision:** `brokenAt[]` is computed at ping time from the ping's `seed` and every arc of that ping reads the same mask; over time only `R_k` and `A_k` change.
**Reason:** if each arc took fresh gaps, the arc train would become visual noise and nobody could understand the object's shape. With a fixed mask the pattern of segments moves toward the player as one coherent thing, and the eye sees several arcs as one object.
**Cost:** the breakup pattern is homogeneous across a whole ping (less variety between arcs of the same ping).
**Regression lock:** `BreakupTests` (week 2) asserts the mask is identical across the arcs of one ping.

## 2026-10-17 — Rendering with a fixed-capacity Mesh + a hand-written shader

**Decision:** one `MeshFilter/MeshRenderer` with pre-allocated arrays and one shader named `EchoArc`; LineRenderer, Sprite Shape and VFX were all considered and rejected.
**Reason:** per-sample thickness and colour are required (Spike teeth, the corner hot-spot), and an arc train means hundreds of segments per frame — with one draw call. Sprite Shape is built for closed spline shapes, and VFX Graph is not installed.
**Cost:** `Mesh.SetIndexCount` does not exist in Unity 6000.6, so the index buffer stays fixed and unused quads have to be collapsed onto a single degenerate point with zero colour; we keep vertex construction ourselves. **Trap:** because the index buffer always points at all `MaxQuads*4` vertices, the mesh's vertex count must never be reduced. Uploading only the used prefix (`SetVertices(verts, 0, count)`) shrinks the array and the editor floods every frame with `Mesh.vertices is too small` / `Mesh.colors is out of bounds` — this bug was seen in the project's first Play.

## 2026-10-17 — The ping origin is locked in world space, not on the player

**Decision:** `PingRecord.origin` is a world-space `Vector2` captured at ping time; the echo system does not depend on the player GameObject existing.
**Reason:** (1) arcs do not smear while the player moves, because distances were measured from that frozen origin; (2) the decoy marble can ping from the far side of the room and build the same arc train; (3) `Sandbox` works with no player at all.
**Balancing constraint:** `waveSpeed >= 1.6 * maxPlayerSpeed`, otherwise a returning arc can never pass the player and readability dies.

## 2026-10-17 — Wave signatures in u along the enemy's axis, not absolute angles

**Decision:** each enemy's profile is defined over `u ∈ [-1, 1]` of its angular span, and repeat periods (e.g. the teeth) are fractions of `u`, not degrees.
**Reason:** with 80 rays in a 64-degree cone, an enemy with a 20-degree span gets about 25 samples; defining "6 teeth over 360 degrees" aliases into noise. As a fraction of `u` a tooth always has at least 3 samples, and the silhouette rotates with the enemy's body.
**Cost:** if ray count is cut very far on a low quality tier the signature is abbreviated — which is why `SignatureTests` is written against `minRays`.

## 2026-10-17 — Levels as hand-authored scenes + prefabs, no Tilemap and no text format

**Decision:** each level is a `.unity` scene with geometry laid out in the editor, plus one `LevelDefinition` ScriptableObject for progression metadata.
**Reason:** tuning this game means reading arcs with your eyes; the Scene view with Snap, Undo and Play-in-editor makes that free. A Tilemap with `CompositeCollider2D` destroys the "one collider = one material" mapping, and a text format is overkill for four hand-tuned rooms.
**Cost:** the `.unity` files are large and merge-prone → so the Smart Merge driver gets registered, one scene per branch, and batched `ProjectSettings` edits.

## 2026-10-17 — One collider = one material = one GameObject

**Decision:** `EchoSurface` sits on the same GameObject that owns the collider; a multi-material collider is not allowed.
**Reason:** both technical (a hit returns only one collider) and design: separating material at the surface is exactly what makes the arc readable.

## 2026-10-17 — Enter Play Mode Options stays off (until week 5)

**Decision:** Domain Reload between Play sessions stays enabled.
**Reason:** with `m_EnterPlayModeOptions: 1` (the template's current state) statics (`SurfaceRegistry`, `NoiseBoard`, singletons) survive between Play sessions and create ghost-like bugs; in a project that will be full of static state this is poison in the early weeks.
**Cost:** a few extra seconds per Play. In week 5, once every static holder has a `ResetStaticState()`, it is switched back on.

## 2026-10-17 — `PlayerMock` reads `Keyboard.current` directly in week 1

**Decision:** in week 1 a temporary input script, without touching `.inputactions`; in week 3 `GameInput` on the `InputSystem_Actions.inputactions` map replaces it.
**Reason:** editing the input asset is an editor-centric, conflict-heavy job in git; in a week where the arc math has to be proven it is not worth the cost.
**Cost:** one temporary file that has to be deleted (reminded in `Docs/PLAN.md`, week 3).

## 2026-10-17 — Aiming with the absolute pointer position

**Decision:** the aim angle comes from the vector from the player to the pointer (per the GDD), not from mouse delta.
**Reason:** simpler and needs no sensitivity; on a black screen the OS cursor is itself a visual reference point.
**Cost:** near the screen edges fast turning is limited; if that becomes annoying, a delta mode with a sensitivity slider is added to Options (week 3).

## 2026-10-17 — Documentation outside `Assets/`, in the root `Docs/` folder

**Decision:** markdown docs live in `Docs/`, not `Assets/Docs/`.
**Reason:** inside `Assets/` the editor creates a `.meta` and a TextAsset for every `.md`, and they also end up in the AssetBundle of the build; neither is desirable.
**Cost:** they sit outside the assets, so at Play time we do not read from them (in-game text comes from `LocTable`).

## 2026-10-17 — Cone angle and the BREATH bar

**Decision:** a 64-degree cone (`coneHalfAngleDeg = 32`) as the default; the BREATH bar is out of scope.
**Reason:** the cone angle was an open question in the GDD itself; the number is in a ScriptableObject and is settled by balance testing. The breath bar overlapped with the `loudness` pulse in Shift mode and was not worth the fight.

## 2026-10-17 — Mandatory API substitutions in Unity 6000.6

**Decision:** the echo core was written at exactly the four points where this version's API asks for something different, instead of coding around 2022.3 assumptions.

| What was expected | What 6000.6 actually has | Where |
|---|---|---|
| `Physics2D.RaycastNonAlloc(...)` | Removed (SceneQuery) → `Physics2D.Raycast(origin, dir, ContactFilter2D, RaycastHit2D[], maxDist)`, which fills the caller's buffer | `Core/RayFan.cs`, filtered with `useTriggers = false` and `layerMask` |
| `Mesh.SetIndexCount(n)` | Does not exist → the index buffer is written once at fixed capacity and unused quads collapse to one degenerate point with zero colour | `Core/EchoRenderer.cs` |
| `Camera.renderPostProcessing` | Lives on the URP camera: `camera.GetUniversalAdditionalCameraData().renderPostProcessing` | `Scripts/Editor/SandboxSceneSetup.cs` |
| `Light2D` in `Unity.RenderPipelines.Universal.Runtime` | Lives in a separate assembly: `Unity.RenderPipelines.Universal.2D.Runtime` (must be added to `references`, otherwise CS0246) | `Assets/Scripts/Editor/EchoMaze.Editor.asmdef` |
| `Object.GetInstanceID()` | Obsolete → `GetEntityId()` | `Scripts/Editor/SandboxSceneSetup.cs` |
| `FindFirstObjectByType<T>()` and `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)` | Both obsolete (CS0618) → `FindAnyObjectByType<T>()` and `FindObjectsByType<T>(FindObjectsInactive.Exclude)` without the sort-mode argument | `Core/EchoDirector.cs`, `Gameplay/PlayerMock.cs`, `Scripts/Editor/SandboxSceneSetup.cs` |

**Cost:** this code does not compile on older Unity versions; that is exactly why `ProjectVersion.txt` and the README pin the version in `Docs/TEAM_SETUP.md`.

## 2026-10-17 — `gain0` from the GDD's 0.28 to 1 (a documented deviation from the design)

**Decision:** the default `A_0` in `EchoTuningProfile` and `EchoTuning.asset` sits at `1`, even though the GDD wrote `0.28`. `maxRadius` also moved from `9` to `6.5` and `baseThickness` from `0.022` to `0.05`.
**Reason:** the first playtest said "the wave only comes toward me, nothing comes back from the objects". Measured with an editor test instead of guessed: with the GDD numbers the pillar's dent on the arc was `0.119` world units; with the sandbox camera (12 world units over 1080 pixels ⇒ ~90 pixels per unit) that is `10` pixels on a `2`-pixel ribbon. The arc was effectively a circle and the shape never actually came back. With `gain0 = 1` that same dent becomes `38` pixels.
**Cost:** deviation from a number written in the GDD; if the designer wants the original number back, it has to be argued in the same pixel metric, not in the formula. The numbers are still in the ScriptableObject, so reverting them is a one-line change.
**Regression lock:** `Assets/Tests/EditMode/ArcReadabilityTests.cs` — four claims translated directly into pixels, run in CI (see `Docs/TEAM_SETUP.md` section 4a).

## 2026-10-17 — The outbound ring is causality only, never information

**Decision:** `emissionPulseSeconds` (default `0.16`) opens a plain line in the ping's cone outward from the origin, fading out as the returning arc collapses. It has no shape: a constant radius across all samples, no `d(θ)`.
**Reason:** the second complaint of the week 1 playtest was "sound only arrives, nothing leaves the player". The GDD's mathematical model shows the returning wave as a collapsing ring and never shows the emission side; without a visual reference to the origin, the player does not read themselves as the source.
**Cost:** 40 extra quads during the first 0.16 seconds of each ping (the `MaxQuads = 2400` budget has room at 200 rays). The clutter risk is contained by the short window and the squared fade; it is removed with `0` in the same ScriptableObject.
**Rule:** the ring's radius never passes the returning carrier (`ArcMath.EmissionRadius` clamps it), otherwise the two waves cross and the direction becomes unreadable. Lock: `EmissionPulse_NeverOvertakesTheReturningCarrier`.

## 2026-10-22 — Until the microphone, Space is the only ping input

**Decision:** `SourceMode` got two explicit values: `ManualOnly = 0` (the code default and the value `Sandbox.unity` already stores) and `Simulated = 1`. In `ManualOnly` no `IEchoSource` is created at all, so `DriveAutoPing` does nothing and only `ForcePing` makes a ping. Space moved out of `FixedUpdate` into `Update`, and its loudness is read from `profile.manualEchoLoudness`; the duplicate field `PlayerMock.manualLoudness` was deleted.
**Reason:** with `simulatedLoudness = 0.7` and `loudnessThreshold = 0.18`, the automatic driver fired a ping every 0.35 seconds: the three-ping pool stayed permanently full, the oldest ping was permanently dropped, and Space only added a fourth overlapping field on top of the clutter. The game's premise — "nothing exists until you make a sound" — was violated in the development scene, and the week 1 playtest verdict ("nothing comes back") was taken on a field that had never once been empty. The microphone is still the goal on both Windows and Linux; it just was not this step.
**Cost:** `None` was removed and `ManualOnly` took its slot; because the value stored in the scene stays 0, `Sandbox.unity` was not edited (and hand-editing YAML with the editor open is risky). The automatic gate path still has no rising edge or hysteresis and will not run until week 3 — there it must produce one ping per vocal event, not a barrage every 0.35 seconds. Also a genuine `NullReferenceException` was fixed in `EchoDirector.Awake` (`source.Open(...)` when the source is `null`): the source-less mode had never been run, so it had never burned.

## 2026-10-22 — The merge driver is wrapped, and verified by merging

**Decision:** `Tools/unity-yaml-merge.sh` becomes the registered git merge driver; `UnityYAMLMerge` is never pointed at git directly. `GitSetupValidator` registers the wrapper (`Register Unity Merge Driver`), and `Validate Git Setup` no longer reads config — it performs a real three-way merge on a copy of `Assets/Scenes/Sandbox.unity` where two branches edit adjacent lines of one GameObject, and passes only if both edits survive in `%A`.
**Reason:** the usual recipe (`<tool> merge -p %O %A %B %P`, which this repo documented and ticked off as done) does not work. Git hands merge drivers three extension-less temp files, and the tool selects its parser from the extension, so in real git usage it prints `Couldn't locate merge tool to handle extension` and exits 1 — and because an external driver's failure leaves no `<<<<<<<` markers, the working file ends up holding only our side. When the inputs *do* carry an extension it is worse: the tool writes its result to `%P` (which git ignores, since git reads `%A`) and exits **0**, so git reports a clean merge that silently deleted the other branch's scene edits. Both modes were reproduced here, on this project's own scene file.
**Cost:** one POSIX shell script, so a Windows registration cannot use it as-is (Git Bash would be required); whether `UnitySmartMerge.exe` has the same extension dependency is unverified, and the validator's probe is deliberately `/bin/sh`-only and reports `SKIP` there. The probe also depends on `Pillar_Concrete` staying in `Sandbox.unity`; if that object is renamed the probe reports `SKIP` instead of failing, which is a silent loss of the check — keep an eye on it when the scene gallery grows in week 2. `RegisterDriver` had a second bug: it looked for the tool under `applicationContentsPath/Data/Tools/`, but that property already ends in `Data`, so the menu item had never found the binary on any machine and always bailed to the "not found" dialog.

## Under decision

- The sign of `gainSign` in the formula: default `+1`; it will be set with a slider in the editor against the three test objects and recorded in `Docs/PLAYTEST_REPORT.md` (week 2).
- Is the corner hot-spot emergent enough from the breakup rule alone, or does it need an explicit vertex-proximity boost? (week 2, with the readability test).

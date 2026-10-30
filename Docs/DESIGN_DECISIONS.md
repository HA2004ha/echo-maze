# Technical and design decisions (dated, with reasons)

Every choice that closes one path and leaves another open is recorded here, so that in six months nobody has to ask "why is this written this way". Format: date — decision — reason — the cost we accepted.

## 2026-10-17 — Sound input behind an `IEchoSource` interface

**Decision:** three implementations for the sound source: `MicrophoneEchoSource`, `SimulatedEchoSource`, `ScriptedEchoSource`. The rest of the game only ever deals with `Loudness` and `RawRms`.
**Reason:** the plan assumed Unity editor microphone support on Linux could not be relied on, and the math tests need deterministic input.
**Cost:** one extra layer that must always stay in sync; the real microphone path does not run until week 2, on Windows. *(Amended 2026-10-27: GDD 3.0 puts the microphone in week 2, because "ambient noise and microphone hiss ruin the game" is the design's first listed risk and a Linux-only core would mean building six weeks of gameplay against a signal nobody has measured.)* *(Amended 2026-10-28, after measuring: the premise was wrong. The Linux editor exposes the real capture devices, records live audio and releases them on exit, so `MicrophoneEchoSource` is the primary path on the platform the whole team actually uses and `Sandbox` now ships with `sourceMode = Microphone`. The interface earns its keep anyway — `NoiseGate` is unit-tested through `ScriptedEchoSource`, which no microphone-based test could be deterministic about.)*

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
*(Amended 2026-10-27: "four hand-tuned rooms" is now three levels — the enemy-free maze, The Corridor and The Sump — and the mapping the sentence defends is one collider = one **shape**, not one material.)*
*(Amended 2026-10-30: the "no text format" half was overturned in week 3. `Level01_Maze` is built at runtime by `MazeLevel` from `Assets/Data/Levels/Level01_Maze.txt`, so the maze stays mergeable and its solvability is checked by `MazeGridTests`; see `TUNING.md`, "Chapter one: the maze". `Level02_Corridor` and `Sandbox` are still generated scenes.)*

## 2026-10-17 — One collider = one material = one GameObject

**Decision:** `EchoSurface` sits on the same GameObject that owns the collider; a multi-material collider is not allowed.
**Reason:** both technical (a hit returns only one collider) and design: separating material at the surface is exactly what makes the arc readable.
*(Amended 2026-10-27: GDD 3.0 deleted the material system, so the reason above no longer holds — but the rule does, for a better cause: one collider = one **wave signature**. The Crawler is two circle colliders with a gap and the Spike is a sawtooth polygon collider, so the existing ray pipeline draws their silhouettes with no special code. `EchoSurface.kind` is now a signature tag (`Plain / Absorber / Crawler / Spike`) and brightness comes from the curvature of the distance profile.)*

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
*(Amended 2026-10-27: GDD 3.0 names the replacement — footstep noise (walking is audible, `Shift` is silent) plus fear feedback (the arcs jitter as the nearest suspicious enemy closes in), with no UI. Loudness-driven cone width survives only as a tuning option: about 20° at a whisper to 45° at a shout, falling back to a constant 32° if playtests prefer it.)*

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
**Cost:** `None` was removed and `ManualOnly` took its slot; because the value stored in the scene stays 0, `Sandbox.unity` was not edited (and hand-editing YAML with the editor open is risky). The automatic gate path still has no rising edge or hysteresis and will not run until week 2 (`MicrophoneEchoSource` moved out of week 3 when GDD 3.0 made the microphone the primary input and the project's top risk) — there it must produce one ping per vocal event, not a barrage every 0.35 seconds. Also a genuine `NullReferenceException` was fixed in `EchoDirector.Awake` (`source.Open(...)` when the source is `null`): the source-less mode had never been run, so it had never burned.

## 2026-10-22 — The merge driver is wrapped, and verified by merging

**Decision:** `Tools/unity-yaml-merge.sh` becomes the registered git merge driver; `UnityYAMLMerge` is never pointed at git directly. `GitSetupValidator` registers the wrapper (`Register Unity Merge Driver`), and `Validate Git Setup` no longer reads config — it performs a real three-way merge on a copy of `Assets/Scenes/Sandbox.unity` where two branches edit adjacent lines of one GameObject, and passes only if both edits survive in `%A`.
**Reason:** the usual recipe (`<tool> merge -p %O %A %B %P`, which this repo documented and ticked off as done) does not work. Git hands merge drivers three extension-less temp files, and the tool selects its parser from the extension, so in real git usage it prints `Couldn't locate merge tool to handle extension` and exits 1 — and because an external driver's failure leaves no `<<<<<<<` markers, the working file ends up holding only our side. When the inputs *do* carry an extension it is worse: the tool writes its result to `%P` (which git ignores, since git reads `%A`) and exits **0**, so git reports a clean merge that silently deleted the other branch's scene edits. Both modes were reproduced here, on this project's own scene file.
**Cost:** one POSIX shell script, so a Windows registration cannot use it as-is (Git Bash would be required); whether `UnitySmartMerge.exe` has the same extension dependency is unverified, and the validator's probe is deliberately `/bin/sh`-only and reports `SKIP` there. The probe also depends on `Pillar_Concrete` staying in `Sandbox.unity`; if that object is renamed the probe reports `SKIP` instead of failing, which is a silent loss of the check — keep an eye on it when the scene gallery grows in week 2. `RegisterDriver` had a second bug: it looked for the tool under `applicationContentsPath/Data/Tools/`, but that property already ends in `Data`, so the menu item had never found the binary on any machine and always bailed to the "not found" dialog.

## 2026-10-27 — GDD 3.0 is the design; the live document sits at the repo root

**Decision:** [GDD.md](../GDD.md) at the repository root is the only design document. `Docs/GDD.md` (v1.1) stays on disk with a "superseded — history only" banner, and every other document links to the root file.
**Reason:** v3.0 is not a polish pass on v1.1: it deleted the material system, the BREATH bar, the health bar and one whole level, and it added the stealth half of the game (suspicion, doors, hollows, sanctuaries, noise zones, one-hit death, the bot). Keeping both files unreadable-equal is how `PLAN.md` and `TUNING.md` start arguing with the design; the banner makes the older one obviously inert instead of subtly authoritative.
**Cost:** a second GDD in the tree that a newcomer can open by mistake, and a root directory that no longer matches "documentation lives in `Docs/`" (see the 2026-10-17 entry above — that rule was about `Assets/`, so a root-level design file does not break it).

## 2026-10-27 — `SurfaceKind` loses Metal and Sponge and keeps its numbers

**Decision:** `Concrete` is renamed `Plain`; `Metal` (2) and `Sponge` (3) are deleted and their numeric slots stay empty; the remaining values (`Absorber = 4`, `Crawler = 5`, `Spike = 6`) do not move.
**Reason:** materials are gone, and the enum's remaining job is tagging a collider with the **signature** it casts, which is geometry. Unity serialises enums as ints, and `Sandbox.unity` already stores `kind: 1` on three objects — renaming the member is free, renumbering the others would silently re-tag every collider in the scene and in every prefab built later.
**Cost:** the fossil names `Pillar_Concrete` and `Wall_Concrete` stay in the scene, because `GitSetupValidator`'s scene-merge probe matches the literal `m_Name: Pillar_Concrete` and would report `SKIP` (a silent loss of the check) if it disappeared. Renaming them is an in-editor job that must land in the same commit as the probe update.

## 2026-10-28 — Brightness comes from the curvature of the distance profile

**Decision:** `SurfaceRead.NormalisedCurvature` is `|d[i+1] − 2·d[i] + d[i−1]| / (d[i] · Δθ²)` and `b = brightnessBase + brightnessGain · clamp01((curv − curvatureFloor) / curvatureRange)`. Nothing about an object's *material* affects how bright its arc is; only how bent the measured surface is.
**Reason:** GDD 3.0 deleted materials, and this form has the property the old per-ping normalisation did not: **a flat plane measures exactly 1.0** at normal incidence whatever its distance and whatever the ray count, so the thresholds are absolute (`curvatureFloor = 2` means "flat things stay dim", `curvatureRange = 6` means "a 0.6 m pillar at 3 m is mid-bright, a corner saturates"). Normalising by each ping's own maximum would have amplified a flat wall's floating-point noise into full contrast and made two pings incomparable.
**Cost:** three numbers to tune instead of one per material, and a hot-spot now has to *be earned* by geometry rather than assigned — a large flat wall at a grazing angle measures ~1.8 and stays dim, which may or may not be what the player should see. Also: peak curvature is not a fair way to compare shapes (a cylinder's tangent silhouette saturates it), so the tests measure brightness **at the nearest surface**, which is also what the player's eye does.

## 2026-10-28 — Linux is the microphone platform

**Decision:** the microphone is developed and measured on Linux, and the Linux x86_64 player is the delivery build. `Sandbox.unity` saves `sourceMode = Microphone`. Windows is dropped as a test target.
**Reason:** nobody on the team has a Windows machine, and the assumption that Unity cannot capture audio on Linux turned out to be wrong: on 2026-10-28 the editor listed three devices, recorded real samples at the requested rate, held a 0.002–0.003 RMS floor in a quiet room, and released the device on Play Mode exit. Keeping the Windows plan would have meant the only people who could ever test the game's primary input were not the people building it.
**Cost:** the Unity **Linux player's** microphone path was, at the time of writing, unverifiable from the editor. It has since been half-verified (row 2, 2026-10-28: the player lists four capture devices and opens one, logged at startup by `EchoDirector.ReportSourceOpened`) — what it still cannot be until a person speaks into it is the gate, the loudness range and the quit behaviour. PipeWire/PulseAudio device naming is unpredictable across distros, so `AutoSelect` can only skip a monitor it can *recognise by name*, and a user whose system default input is a loopback still has to pin the device themselves; the player run made that concrete, because the device it auto-selected, `Default Input Device`, is an alias whose target the code cannot see.

## 2026-10-28 — Week 2 enemy hearing is a deliberate stub on a permanent seam

**Decision:** `Crawler` in week 2 does two things and no more: a touch kills, and a noise inside the straight-line `H = ρ · R(L)` makes it walk to the noise and stand there for `lookAroundSeconds`, after which a hunt that reached nothing is abandoned (`huntSeconds`) and the creature returns to its route. There is no suspicion meter, no line of sight, no occlusion and no pathfinding. What is *not* a stub is the seam: every sound in the game — a ping, a footstep, a wall bump, a pulse, an enemy's own dragging walk — goes out through `EchoDirector.ReportNoise(position, loudness)` and comes back to listeners through `NoiseEmitted`.
**Reason:** the difference between the week-2 behaviour and the real design is exactly the thing week 3 builds (`EnemyBrain` with Dormant / Patrol / Curious / Investigate / Chase / Lost, and `NoiseBoard` deciding whether a wall, a closed door or a noise zone attenuates the sound). Writing that now would be writing it without the listening tests, and it would put the week-2 visual goal — a room you can walk around in with something alive in it — behind an alertness system nobody has balanced. The seam, in contrast, is designable today, because `Player`, `Crawler` and the debug window all already emit or consume it, and week 3 changes what `ReportNoise` *means*, not who calls it.
**Cost:** the stub is wrong in ways that will look like bugs. It hears through walls; it is fooled by a noise anywhere within a radius, so a shout at the far side of the room is indistinguishable from one behind it; a hunt with no pathfinding presses against geometry until the deadline instead of routing around it. `AHunt_IsGivenUpWhenNothingWasFound` asserts the give-up on purpose — the failure mode this entry is written against is the stub quietly becoming the design because it was never re-opened.
**Rules:**
- A new sound source calls `ReportNoise`; it does **not** poke `Crawler` directly. Anything else makes `NoiseBoard` a rewrite instead of a replacement.
- The creature ignores a noise closer than `arrivalRadius`, because its own footsteps are emitted at its own position and a thing that startled at its own walking would never patrol. That filter is a stub too — friend-or-foe belongs to `NoiseBoard`.
- Enemy footsteps are emitted from week 2 even though nothing can hear them until the week-4 landmark audio. They are not debug-drawn: the noise stream is the only honest place for them.
- `RunController` looks the crawlers up with `FindObjectsByType` at retry time instead of caching them at `Awake`, because week 3's section reset destroys and respawns enemies and a cached array would either miss the new ones or reach into destroyed ones. That is what the PlayMode suite caught: a `MissingReferenceException` from a previous test's crawler pending destruction.

## 2026-10-28 — A step between two surfaces breaks the line; a kink inside one surface is allowed to flare

**Decision:** the discrete second difference is only read as *shape* when the distance is continuous across the stencil. `SurfaceRead.IsSurfaceJump` calls a pair of neighbouring samples a jump when `|d[b] − d[a]| > edgeJumpFraction · max(d[a], d[b])` (`0.12` by default). A sample that straddles a jump gets curvature 0 and no brightness bonus, and the segment that would connect the two surfaces is **not drawn** — the line breaks there instead of spoking across the gap. A distance curve that is continuous but whose slope jumps — a real convex corner — keeps the full flare, and nothing about the corner is suppressed.
**Reason:** measured in `Level02_Corridor`, this was the week's worst defect and it looked like a tuning problem. Aiming a shout at the north wall, the fan crossed from `Box_Corner` (4.05 m) to `Wall_North` (5.66 m) beside it, and the second difference on the two samples straddling that crossing measured **12670** and **9075** — three orders of magnitude above the `8` that saturates a sample — so every threshold in the file was calibrated against *silhouettes* rather than *shapes*. `SurfaceReadTests` passed (wall < pillar < corner in model values) while the room itself was full of false corners. Dividing the peak by the crossing is the whole fix: `12670 → 236` on that cone and `12684 → 638` on the box-corner cone, with the two remaining genuine features (the pillar's ~10, the Crawler's ~11) untouched.
**Cost:** one more knob, and a threshold that is a *fraction* rather than an angle: a surface crossing at a shallow enough relative distance still counts as flat and gets the RNG's fracture treatment instead of a guaranteed break. And the honest measurement says the fix bought almost nothing in *brightness count* in this room (`full` went 20.8% → 19.7%) — which is not a failure of the rule but the point of the day: the samples that saturate belong to round objects that should saturate. The 47%-of-the-cone figure recorded during the first pass through the debug window turned out to be measured on a cone whose far wall was **outside the shout's reach**, so almost half of the "wall" samples were the alcove and the crawling thing in the frame. The numbers in `TUNING.md` are the corrected ones.
**Rules:**
- The break is a *connectivity* decision, so it belongs to the same per-ping pass as the fracture mask; nothing may recompute it per arc.
- `+inf` (a miss) is already a break by rule 3 of `TUNING.md`; `IsSurfaceJump` only speaks about pairs that both reflect.
- This entry is the reason `curvatureRange` is no longer justified by "a corner is in the tens": after the split, the tens belong to corners alone.

## 2026-10-28 — The player chooses the microphone in-game; nothing ships a pinned device

**Decision:** input selection is a UI feature, not a project setting. The week-3 calibration page lists `Microphone.devices` at run time and lets the player pick, with `Auto` (first device that is not a `Monitor of …`) as the default; the choice is stored as a **saved preference** and `EchoTuningProfile.microphoneDevice` becomes the field that preference is written to, not the field a designer is expected to fill in. A saved name that is no longer present falls back to `Auto` rather than failing to open. We do **not** ship a build pinned to a specific device string.
**Reason:** measured on 2026-10-28 in the Linux player: the machine exposes `Default Input Device`, `Family 17h/19h HD Audio Controller Analog Stereo`, and two `Monitor of …` loopbacks, and `AutoSelect` takes the first name that is not a monitor — which is the alias. Nothing that reads a *name* can tell what a PipeWire alias currently points at, so on a machine whose system default input is a monitor the game would answer its own sound effects forever, and the only current escape is editing a ScriptableObject in an editor the player does not have. Device names also differ per distro, per audio server and per headset, so a string pinned by one teammate is wrong on every other machine by construction.
**Cost:** a dropdown, a re-scan, and a live device swap (`Close` the clip, `Open` the next one) that has to leave the gate, the warmup discard and the ring position in a coherent state — plus one more thing that can fail in front of a tester. `Auto` also stays as a behaviour, so the alias problem is *reachable* in the default configuration and cannot be fixed by the picker alone; what the picker buys is the ability to get out of it. The fallback-to-`Simulated` warning in `EchoDirector` still names `EchoTuningProfile.microphoneDevice` until week 3 lands, because up to then it is the only mechanism that exists.
**Rules:**
- The selection logic is a pure function over a supplied device list (`list, savedName → device | auto | none`) and is unit-tested; no test has to plug a microphone in to prove it.
- The picker never writes a device name into a *committed* asset. It writes a user setting; the tuning asset's committed value stays `""`.
- Applying a choice needs no restart, and a device that disappears mid-session is reported in `Status` and re-selected, not thrown.

## 2026-10-28 — The character is four stills and a computed motion, with no Animator

**Decision:** the player is drawn by four imported poses plus motion computed in `PlayerVisual`: no `Animator`, no `.anim`, no `.controller`. Facing follows **movement**, and the mouse aim only decides when the player is standing still; the four poses cover four sectors and **vertical wins the ties**. The left profile is the one side texture with `flipX` rather than a second file. The art is drawn at **full opacity**, with no tint. Every motion write goes to a child `Visuals` transform, never to the root. The step bounce reads `Player`'s own footstep clock through two read-only getters instead of keeping a second one. The component is **optional**: incomplete setup disables it silently. The two 3/4 poses are imported with correct settings and left unwired.
**Reason:** with one still per pose the only motion available is the kind you compute, so a state machine would be the project's first `.anim`/`.controller` assets to arrive at a picture that is a sine wave and four `if`s — and `com.unity.2d.animation` has been installed and unused since week 1, which is a temptation worth closing off in writing. The bounce has to agree with the footstep *sound*, and that is emitted from `Player.nextStepTime`, which goes stale when the player stops and fires immediately when they resume; a second clock would keep nodding through a stop-and-listen and miss the resume, desynchronising the only two moments the game is teaching something about noise. Vertical wins ties because a WASD diagonal is *exactly* `|vx| = |vy|` — a tie is the common case, not a corner of the number line — and "entering or retreating from danger" carries more intent than which hand was raised first. The child transform is correctness rather than tidiness: the root carries a `Continuous`/`Interpolate` `Rigidbody2D` and a 0.25 collider, so a squash written there resizes what collides with walls and fights the interpolation. Full opacity because the art's own near-black body and cyan rim are the design's language for a thing that lives in the dark, and a code-side tint would put a second colour source on top of an asset the art team owns.
**Cost:** the motion ceiling is four stills plus squash, bob and lean — no in-between poses, no turn blending, no walk cycle. If the game later wants one, it replaces this file rather than extending it. Two getters on `Player` are permanent API for a consumer `Player` is not allowed to know about. `flipX` saves one texture — 2.67 MB measured at runtime (`Profiler.GetRuntimeMemorySizeLong`, 1024² DXT5 with 11 mip levels) — and buys a bit-exact mirror (measured: 0 differing pixels between the two delivered profiles), but it means an art request to draw the two sides *differently* — a shield, a lantern — becomes a code change. Poses swap instantly with no hysteresis, so a player walking exactly along a sector boundary can flicker. And making the visual optional means a scene built without the art shows the old placeholder dot and a warning rather than failing loudly, which is the right trade for a teammate mid-rebase and the wrong one for a shipping bug.
**Rules:**
- `Player` never references the visual. The dependency arrow is one way (Visual → Player), and that is what keeps every PlayMode rig in the suite art-free; `PlayerVisualTests.TheWeek2RigPlayer_NeedsNoArtAtAll` is the guard.
- No animation number goes into `EchoTuningProfile`. It is the balance brain, with `OnValidate` invariants the tests reflect over; a breathing rate is not balance. They are `[SerializeField]` on the component, as in `TouchMarks`.
- The bounce is derived from `Player.NextStepTime` and `Player.FootstepPeriod`. Re-deriving it from an independent timer is not merely fragile but provably wrong, and `TheBounce_IsLockedToTheFootstepClock` fails if anyone tries.
- Hysteresis against boundary flicker, if it is ever needed, goes in `PlayerVisual`. The pure `PlayerFacing.Resolve` stays boundary-deterministic so it stays testable. This was a promise until 2026-10-29 and is now kept: the band, the dwell and the cursor radius are the entry below, and the resolver still has no state and no notion of time.
- The `Player_34_*` poses stay unwired until something needs them — which is a statement about this commit, not about the camera. They were drawn for the 3/4 view, and the lead has since put them on the top-down diagonals (issue #2), so the next person to wire them does it for pose count and not for a viewpoint. Either way they are imported with the same settings as the rest, so nobody's first refresh commits PPU-100 defaults for them.
- Motion is measured off the `Visuals` transform. A number read from the root is not a measurement of the animation.

**Corrected 2026-10-29, by the 4096 re-delivery — the entry above stands as a record of a decision whose facts moved and then moved back.** Two of its load-bearing measurements were true of the 1024 originals and needed re-measuring against the files now in the repo. The mirror claim came first: the 4096 `Player_side_left.png` was **not** a mirror — `flip(Player_side)` differed from it in 35 % of sampled pixels, in the eye, the rim arc and the speculars, because that export kept the light fixed instead of mirroring it — and a `PlayerVisual.sideLeftSprite` slot shipped for a day on the strength of that measurement (`Docs/ISSUES.md` #5). The lead then re-exported the file by hand and the flop-compare returned **0 differing pixels**, so the slot was reverted, the file deleted, and the original decision stands on a fresh measurement rather than on the old one: one profile texture, two poses, `flipX` doing the second. The `Player_34_*` poses are no longer waiting on a camera: the lead has assigned them the top-down diagonals, which is what issue #2 now builds on — and those two files genuinely are ~25 % asymmetric about their own mirror, so the diagonal flips *do* move the light. That is accepted with the art ask written down, not papered over. The PPU changed with the canvas (640 → 2560) for the reason in `TUNING.md`: a sprite's world size is source pixels over PPU, and `maxTextureSize` does not move it. The cost line about instant pose swaps and boundary flicker has since been closed the way this entry promised it would be — in `PlayerVisual`, not in the resolver — see the 2026-10-29 entry below.

## 2026-10-28 — A touch mark is placed where the drawn body meets the wall

**Decision:** `TouchMarks.Flash` takes the contact point, the surface normal **and the body centre**, and pushes the dash outward from the body by `silhouetteClearance = 0.24` m along `normalize(contactPoint − bodyCenter)`. `Player` supplies the centre and keeps no opinion about geometry. The dash is therefore no longer at the point physics reports; it is at the point the *character* touches.
**Reason:** the hitbox is a `0.25` m circle and the drawn figure reaches `0.447` m sideways (`2289` of a `4096` canvas at PPU 2560), so the reported contact sits 18–22 screen pixels **inside the torso**, and because marks live on the `Echo` layer — above `Default`, where `Visuals` is, which is what lets the arcs read over the character — the dash is composited on top of the body it belongs to. The first candidate, `collision.collider.ClosestPoint(body.position)`, was written down as the fix and **measured to be a no-op**: for a circle against a flat box face the contact point *is* the nearest point on the wall, so both candidates are the same pixel. The fault is not which surface point is chosen, it is that a surface point chosen for the collider is inside the art. The direction comes from `contact − centre` rather than `contact.normal` because it is true at once for a flat wall, a corner and a rotated box, and it does not depend on which way physics happens to point its normal.
**Cost:** one number that has to be re-measured whenever the art or the hitbox changes, and that is wrong in a specific way: it is a single clearance for poses that are not one width, so against the `1514` px side profile the dash floats ~17 px off the body. It is also **a compensation, not a cure** — `Docs/ISSUES.md` #4 is the real defect (the character is drawn wider than it collides), and closing that one sets this clearance to zero. Offsetting outward by 0.24 m additionally puts the dash *inside* a `0.4` m wall, which is invisible against an opaque wall and would show through a thin prop.
**Rules:**
- Placement lives in `TouchMarks`. `Player` reports where it was hit and passes its own centre; it does not compute offsets, and the presentation numbers stay off the balance profile.
- A mark flashed without a body to clear (`bodyCenter` equal to the contact point) is placed where it was asked for. `Vector2.normalized` of a zero vector is zero, and that is the documented behaviour the `AMark_LiesAlongTheSurfaceItTouched` case relies on.
- The claim is locked by measurement, not by reading the code: `TouchMarkPlacementTests` walks the rig player into `Wall_West` in PlayMode and asserts the mark is ≥ `0.44` m from the centre, on the far side of the contact and still inside the wall's own thickness. `PlayerPoseImportTests` is what notices when the art's footprint changes underneath it.
- `ClosestPoint` is not to be re-proposed as the fix. It is the same point, and `Docs/ISSUES.md` #1 records why.

## 2026-10-29 — A pose is held until the heading has committed to leaving it

**Decision:** three gates sit between the resolver's answer and the sprite that gets drawn, all of them in `PlayerVisual`: the drawn pose may not change until the heading has cleared the current pose's sector edge by `boundaryDegrees = 10`; it may not change more than once every `dwellSeconds = 0.1`; and while the player is standing still with the pointer closer than `aimNoiseRadius = 0.27` m to the body, the aim is not treated as an input at all and the pose is held. `PlayerFacing` stayed a pure, boundary-deterministic map and gained only the vocabulary the gates need — `ResolveDirection` (the sector map from a heading, without the movement/aim choice), `PoseCentre` (the heading each pose is most right for) and `SectorHalfWidthDegrees`. `Player` gained one more read-only getter, `AimDistance`. Easing a pose *change* (a fade, a cross-fade, a turn animation) is rejected, as it was when the issue was opened.
**Reason:** `Docs/ISSUES.md` #3 measured four ways the same character quarter-turns itself every few frames, and none of them are a wrong rule — they are a right rule being asked to answer noise. A held WASD diagonal is *exactly* `|vx| = |vy|`, so the tie is not a corner of the number line but half the directions a player can walk in, and the resolver has to break it again every frame; a pointer 10 px off the body turns one pixel of shake into ~5.7° of aim, and parking the cursor on the character is the normal resting state; and the movement/aim handover has the velocity crossing `MoveDeadzoneSqr` constantly, where the *source* of the heading changes without any heading crossing a boundary — which hysteresis alone cannot see, because the sector did not move. The gates go in the drawing layer because flicker is a property of drawing: the compass answer at 45° is well-defined and testable, and what a player should be *shown* at 45° while the number wobbles is not. Keeping the pure function free of them is what the 2026-10-28 entry promised ("hysteresis, if it is ever needed, goes in `PlayerVisual`") and it is the reason the 1° sweep still means anything. A fade is impossible rather than merely unwelcome: there is one still per pose, so there is no in-between frame to show.
**Cost:** a real one, and it is behaviour rather than memory. **A diagonal now shows the pose you were already in when you entered it** — the tie sits exactly 45° off a profile, which is inside the band by construction — so the drawing layer and the pure rule disagree for a quarter-rotation of input, and anyone reading `PlayerFacing.Resolve` alone will describe a character that does not exist. 10° of band plus 100 ms of dwell is also latency spent on every genuine turn, which is the price of a pose that stops being read as a jitterbug, and it is one of the two numbers issue 2 has to halve: eight poses move the edge to 22.5° and this band to 5°, because twice the boundaries is twice the places to stick. Three more serialized numbers means three more that go stale if the art or the walk speed moves, and `AimDistance` is a second case of `Player` carrying API only the visual asks for.
**Corrected the same day, by issue 2 shipping.** The first half of that cost no longer exists: a WASD diagonal is now the dead centre of its own sector rather than a tie sitting 45° off a profile, so the drawing layer and the resolver agree on every direction a keyboard can produce, and the "the character you see is not the character `PlayerFacing` describes" warning in this entry's opening is retired. The band is the 5° this paragraph predicted, which also means it absorbs a smaller cursor wobble than it did — one pixel at the closest distance the aim is still trusted is 2.4°, and that is what the acceptance case now measures.
**Rules:**
- The gates live in `PlayerVisual`. `PlayerFacing.Resolve` and `ResolveDirection` stay boundary-deterministic and free of time, state and hysteresis, so the sector map remains testable as a table.
- Anything measuring the band uses `PoseCentre` and `SectorHalfWidthDegrees`, never a literal 45: `PlayerFacingTests` pins the helpers against the map they are measured from, so widening the fan moves the hysteresis with it instead of desynchronising the two.
- The order of the gates is part of the design: cursor radius, then dwell, then band. Checking the band first would resolve a heading the radius has already rejected.
- A gate must ship with the case that proves it is not a freeze. `AHeadingThatReallyLeftItsSector_TakesTheNewPose` is that case for all three, and the two acceptance cases for the flicker are named for what they promise rather than for the mechanism they touch.
- Easing a pose change stays rejected while there is one still per pose. If it is ever revisited, it is revisited as an art request, not as a coroutine.

## 2026-10-29 — Eight poses from the five files in the repo

**Decision:** the fan goes from four sectors to eight, built entirely out of what was already committed — no art requested, no file added. `PlayerPose` gains `DiagonalDownRight`, `DiagonalDownLeft`, `DiagonalUpRight`, `DiagonalUpLeft`; `PlayerFacing.ResolveDirection` is rewritten as a slope test against `tan(22.5°)` instead of an `|y| ≥ |x|` tie-break; `SectorHalfWidthDegrees` goes 45 → 22.5 and `boundaryDegrees` with it, 10 → 5; `Player_34_front` and `Player_34_back` become the down- and up-diagonals and each reaches its mirror with `flipX`, so **five textures draw eight poses**; `SceneKit` requires all five and falls back to the placeholder dot if any is missing. The convention for the set is that **the light sits on the side the character faces**, which makes the profile an authored right-facing file and the two 3/4 stills authored left-facing ones, so the flip is the disagreement between a pose's facing and a file's facing rather than a property of either.
**Reason:** the manual echo is a **64° cone** steered by the same number that picks the pose (`EchoTuningProfile.coneHalfAngleDeg = 32`, `player.AimRadians`), so with four 90° sectors the drawn face could sit up to **45° off the axis the sound actually travels on** while being correct for its sector — the character was the worst available indicator of the thing the character was doing, and a player reading direction off the body was systematically misled near sector corners. Eight 45° sectors cut the worst case to **22.5°**, inside the cone. The rewrite into slopes is not a style swap: with eight sectors the exact WASD diagonal stops being a boundary and becomes a sector *centre*, so the `1e-6f` epsilon and the "vertical wins ties" rule have nothing left to decide, and the edges that remain sit at 22.5° — a number no keyboard can produce. `Vector2.Angle` hysteresis and the 1° sweep both keep meaning something because `PoseCentre` and `SectorHalfWidthDegrees` moved *with* the map rather than being re-typed.
**Cost:** a real one, and it is about the art rather than the code. **The two `Player_34_*` files change meaning:** they were imported as a 3/4 *camera*'s poses, that camera does not exist, and the discussion now has to start from scratch because the files it was going to reuse are load-bearing for the top-down view. **They are not mirrors of each other** — each differs from its own mirror in ~25 % of the sampled pixels of its content box, three times `Player_front`'s 8 %, because the artist keeps the rim light and the speculars on a fixed side — so flipping a diagonal moves the light, and what ships is the same body lit from the opposite side. Which half of each pair keeps the authored lighting **can** be read — but from the light, not the silhouette (the body outline is symmetric to within 26 of 4096 px in both files), and the first cut did not read it: it fixed a convention instead and shipped both diagonals mirrored the wrong way round until the lead saw it in play. See the correction below. Two more `[SerializeField]` slots, a stricter `Awake` guard, and `SceneKit` writing a presentation number into a scene for the first time.
**Corrected the same day, in play, by the lead's eye — the diagonals shipped mirrored the wrong way round.** The version that compiled said "every file is authored facing screen right" and answered both questions with one list. That sentence was a convention, not a measurement, and the measurement was available: comparing the cyan rim and eye centroid to each file's own bounding-box centre gives **+197 px** for `Player_side` — the one file whose facing is independently proven, because issue 5 established it is a bit-exact mirror of its own flipped copy — and **−121 px** for both `Player_34_front` and `Player_34_back`. In this set the light sits on the side the character faces, so the two 3/4 stills are authored facing screen **left**, and the pose that needed the mirror was the *rightward* one, not the leftward one. The fix is not a swapped boolean: `PlayerVisual` now asks two questions (`LooksLeft`, `IsAuthoredLeftward`) and takes the disagreement, which is the shape that stays right when a third asymmetric file arrives. The four diagonal PlayMode cases moved with it, the four captures were re-taken, and the silhouette-versus-light distinction is recorded in `TUNING.md`, because this is the second time this project conflated a drawing's facing with a pose's facing and both times the thing that settled it was a centroid comparison rather than a reading of the code.
**Rules:**
- The band is a fraction of a sector, so it moves with the pose count and never independently. `PlayerFacingTests.TheSectorHalfWidth_IsWhatTheSectorsAreActuallyWorth` asserts `SectorHalfWidthDegrees * 2 == 360 / poses`, and the bootstrap re-asserts `boundaryDegrees` — a scene generated for four poses otherwise keeps a band tuned against geometry that no longer exists.
- A pose's facing and a file's authored facing are **two lists, not one**, and `sprite.flipX` is their XOR: `PlayerVisual.LooksLeft(pose)` and `PlayerVisual.IsAuthoredLeftward(file)`. Conflating them is what made the first cut wrong, and the separation is what makes the next asymmetric file one entry rather than a boolean swap that misnames itself. A pose count that grows again adds sectors to `PlayerFacing`, and — if the art is asymmetric — a *file* to the second list.
- The dot fallback is all-or-nothing on the five files. A half-wired set disables `PlayerVisual` silently (the builder already warned) rather than drawing cardinals correctly and going blank on the first diagonal: `PlayerVisualTests.APlayerVisualFromTheFourPoseEra_TurnsItselfOffSilently`.
- `Player_34_*` are not available to a 3/4 camera any more. If that camera happens, its stills are an art request, not these two files.

## 2026-10-29 — Radian angles get one easing function, because `Mathf`'s are degrees APIs

**Decision:** `PlayerFacing.SmoothTowards(currentRad, targetRad, amount)` is the project's only radian-angle
ease, and `Player.UpdateAim` calls it. The line it replaces was `Mathf.LerpAngle(aimRad, Atan2(…), smoothing)`
— correct in shape, wrong in units. No gate, no band, no tuning number moved, and `PlayerVisual` was not
touched.

**Reason:** the flicker `Docs/ISSUES.md` #6 records was this line and not the hysteresis. `LerpAngle`,
`DeltaAngle` and `SmoothDampAngle` are all documented in **degrees**, and the aim is kept in radians; the wrap
correction inside them fires on a raw difference over 180, which two radian values never reach, so crossing the
±π seam interpolated the long way round — measured, five pose changes for two degrees of cursor. It could not
be fixed where the flicker was being looked for: the band, the dwell and the cursor radius all assume the
heading is a real measurement of where the player points, and here the heading genuinely did leave the sector,
seven times over, so a wider band would only have made the character deaf. It could not stay inline in `Player`
either, because a private method on a MonoBehaviour has no EditMode reach, and this is the second time the seam
has needed a test in `PlayerFacing` (`BothEndsOfTheAtan2Range_AreTheSameHeading` being the first).

**Cost:** a public function on a class that is otherwise the pose map, and a units round-trip per frame per
aim — three multiplies, no `Atan2`, nothing measurable. The real cost is the one this entry exists for: the
defect was invisible for a full week because the code *looked* right. `Mathf.LerpAngle` reads like "lerp an
angle", the field it was called on is called `aimRad`, and nothing in the line says which unit either side of
it is in. The name `SmoothTowards` is a grep, not a proof.

**Rules:**
- Any radian angle that eases goes through `SmoothTowards`. A new radian field does not get `LerpAngle`,
  `DeltaAngle` or `SmoothDampAngle` handed its values directly — those three take degrees, and the type system
  says nothing about it.
- Seam cases are asserted by **pose**, not by angle. `ACursorCrossingDueLeft_KeepsFacingLeftAllTheWayAcross`
  fails when the character moves, whatever the arithmetic returns; `ATurnAcrossTheSeam_TakesTheShortWayRound`
  asserts both that the ease stayed on the short arc *and* that it arrived, because a function that simply
  refused to move would pass the first half.
- The ease rate (`18`/s) is documented in `TUNING.md` as `aimEase` rather than left as a literal, because the
  same eased number steers the echo cone: it is a gameplay lag, not just a presentation curve.

## 2026-10-29 — The run is one chapter until the hunt exists

**Decision:** `GameFlow.chapterScenes` is `{ Level01_Maze }` for this milestone. `Level02_Corridor` stays in
the build and in the editor, but it is not a chapter a run can enter. The maze exit therefore ends the run
and shows the victory panel.

**Reason:** the stage's goal was the **shape** of the game — menu, a real level, a victory — not a second
room with no ending. The corridor is a week-2 blockout with the Crawler's suspicion still a stub; chaining it
after the maze would make "finish the game" mean walking through a room whose rules are not built yet. A run
that ends at the maze is honest about what exists, and the victory panel reads the chapter list, so adding
the corridor later makes it say "2 rooms" with no panel change.

**Cost:** one line of the level list is a **scope promise**, not a permanent architecture. The corridor must
re-enter the list in the same change that removes the hearing stub and gives it a loop, a hollow and a wake
trigger (M2). Until then, a tester who wants to see the Crawler opens `Level02_Corridor` from the editor.
The level file, the door, the sanctuary and the checkpoint remain valid chapter-one content.

**Rule:** a chapter is in `chapterScenes` only when a player can finish it **and** its systems are the ones
the GDD describes. A preview room is a scene, not a chapter.

## 2026-10-30 — Windows and Linux are both delivery targets

**Decision:** the game ships as a Linux x86_64 player **and** a Windows x86_64 Mono player, both built from the Linux editor. This reverses the "Windows is dropped" half of the 2026-10-28 entry; Linux stays the platform the team develops and measures on.
**Reason:** the game must be playable for people outside the team, and most of them are on Windows.
**Cost:** the editor needs the **Windows Build Support (Mono)** module (not installed on the lead's machine as of 2026-10-30); `PlayerBuild` must grow a Windows target; and nobody on the team has a Windows machine, so the Windows microphone path (device names, the `Monitor of …` rule in `MicDeviceChoice`, latency) stays unmeasured until someone outside the team runs the build. `MIC_TEST_MATRIX.md` needs Windows rows for that run.

## 2026-10-30 — The Crawler roars

**Decision:** every Crawler roars every 10–15 s (`roarIntervalMin`/`roarIntervalMax`, drawn on its own seeded `System.Random`, separate from the patrol-pause stream, reset by `ResetRun`). A roar draws a warm (`creatureArcColor`) full-circle ring that grows to `roarWaveRadius` and fades over `roarWaveSeconds` (`EchoDirector.EmitRipple`), and plays a 3D `AudioSource` with linear rolloff out to `roarHearDistance`. The `AudioListener` moves to the Player, because the camera sits at z = -10 and would hear everything from at least ten metres.
**Reason:** a deliberate addition requested by the team (product owner): the player should know roughly where the creature is without pinging. The ripple is a **location cue**, not information about walls — it casts no rays, takes no slot in the ping pool and no cooldown, so the rule "only the returning wave carries geometry" still holds. It is **not part of the noise stream**: it never goes through `ReportNoise`/`NoiseEmitted`, so nothing hears it and the week-3 NoiseBoard does not have to filter it.
**Cost:** the creature is easier to track, which loosens the tension `rho` is tuned for; the interval is the knob if playtests say it is too generous. The roar clip `Assets/Audio/Crawler_roar.wav` is "CC0 Deep Monster Roar" by trazzz123 (https://opengameart.org/content/cc0-deep-monster-roar, CC0), trimmed to 3.2 s, mono, with a 0.8 s fade-out; the week-5 audio pass may replace it. This is the first sound the game plays, so it is also the first time the listener placement matters.

## 2026-10-30 — The Crawler stomps on patrol and hurries on a hunt

**Decision:** the Crawler has two gaits. Patrolling it walks at `crawlerPatrolSpeedMultiplier = 0.4` × `walkSpeed` (1.0 m/s) and takes one step every `crawlerStepInterval = 1.5` s; hunting it walks at `crawlerChaseSpeedMultiplier = 0.8` (2.0 m/s) with the same stride, so the interval shrinks to `1.5 × 0.4 / 0.8 = 0.75` s (`Crawler.StepInterval`). Every step still goes to `director.ReportNoise` at `footstepLoudness`, and now also plays `Assets/Audio/Crawler_step.wav` through the roar's `AudioSource` at `crawlerStepVolume`. "Hunt" here is the week-2 `Investigate` stub ("heard a noise, walk to it"), which stands in for Chase until `EnemyBrain` lands. The single `crawlerSpeedMultiplier` (0.65) and the Crawler's `stepRhythm` field are gone.
**Reason:** requested by the product owner: a slow, loud, giant patrol the player can track by ear, and a hunt that is faster but not as fast as the player. Keeping the stride constant means the tempo of the stomps is itself the alarm. `OnValidate` enforces `patrol <= chase <= 0.95` (TUNING.md rule 5), so no inspector tweak can make the creature outwalk the player or slow down when it hears you.
**Cost:** the hunt's margin over the player is now `0.5` m/s instead of `0.9` m/s at the old 0.65, so a hunt is harder to escape and there is still no pathfinding to make it fair; a constant stomp also makes the creature easier to locate, which, like the roar, loosens the tension `rho` is tuned for. No per-step pitch variation: `PlayOneShot` has no pitch of its own and bending `source.pitch` would also bend a roar in flight. Steps share the roar's `maxDistance` (`roarHearDistance`). The clip is "Mech Stomp" by hc (https://opengameart.org/content/mech-stomp-step-sound, **CC0**), mono, 0.87 s, pitched down ×0.68 with a bass boost to sound giant; the week-5 audio pass may replace it.

## 2026-10-30 — Walls the player touched are remembered as segments, never re-read from colliders

**Decision:** every wall contact (`Player.OnCollisionEnter2D` and `OnCollisionStay2D`, through `TouchMarks.Remember`) stores a world-space segment of `wallMemorySegmentLength` (1.0 m) centred on the contact point along the contact tangent in a `WallMemory`: a fixed ring of `wallMemoryCapacity` (1500) segments. A touch within `wallMemoryMergeDistance` (0.15 m) of a remembered line and roughly collinear extends that line instead of adding one (`WallMemory.TryMerge`, pure and tested), so leaning on a wall spends one slot and sliding along it grows one line. When full the oldest is overwritten and `OverflowCount` is incremented. Lines are thin neon (`baseThickness × 0.6`, alpha 0.45, a bluer cyan than live arcs), fade in over 0.25 s and never fade out, drawn by their own mesh under the arcs. Enemy contacts (Enemy layer, or `EchoSurface` kind Crawler/Spike) are not remembered. The store lives on a scene object: it survives dying and retrying, and is gone when a chapter scene loads.
**Reason:** the product owner: "when you hit a wall, a thin neon line for it stays forever. Why: the lines come from the PLAYER'S MEMORY, not the actual walls. Later a door may open and the wall has an entrance, but the player can't find it because they only see the remembered walls, not the real geometry." So the segments are never re-read from colliders: a `Door` switching its colliders off must leave the remembered wall exactly where it was.
**Cost:** echo hits (pings) do **not** add to the memory yet; that is the obvious next extension (the owner agreed to start with touch only). `GDD.md` "Memory trace" (a 10-15 s faint trace of echo hits) is a different, still-unbuilt feature. Merging is greedy (first matching line) and never joins two lines a new touch bridges. A corner or a curved pillar gives a straight 1 m chord along the contact tangent, which can stand off the surface (about 0.27 m at the ends on a 0.6 m radius pillar). A merged extension appears without its own fade-in. A remembered line is thinner (about 2.7 px) than rule 4's 4 px floor and leans on the glow; verify by eye.

## 2026-10-30 — A four-second footprint trail

**Decision:** the player leaves a faint warm dot (`trailColor`, alpha `trailMaxAlpha` 0.35 fading linearly to 0) every `trailStepDistance` (0.35 m) walked, and each dot is dropped after `trailSeconds` (4 s). `TrailBuffer` is a ring with capacity `ceil(trailSeconds × walkSpeed / trailStepDistance) + 4` = 33, computed once; expiry pops the tail. Own mesh, rebuilt per frame while there are dots. Shift-walking leaves prints too (the owner did not say otherwise, so the simpler rule). The Crawler leaves none, and prints within 0.4 m of the player are skipped so they are not drawn over the body.
**Reason:** the owner: "so I know if I've been here before — but the trail should vanish at some point (does erasing every trail in ~4 s make the game heavy?)". It does not: at most 33 points and 33 quads, O(1) expiry, no allocation.
**Cost:** measured rebuild cost: see `Docs/TUNING.md` ("Wall memory and the footprint trail"); `TrailBufferTests.RebuildingAFullTrail_CostMeasured` logs it (not yet run: the Unity editor was not launched when this was written). A trail this short cannot answer "was I here a minute ago"; that is the wall memory's job.

## 2026-10-30 — The first death before a sanctuary goes back to the spawn

**Decision:** `LevelSession.Checkpoint` is the spawn captured in `Awake` until a sanctuary moves it. It no longer falls back to the player's live position.
**Reason:** the fallback read where the body was, which on a retry is where it died (ISSUES #9). `MazeLevel` (execution order -20) places the player before the session's `Awake`, and Level 2 is serialised on its spawn, so `Awake` is the right moment and no lazy capture is needed.
**Cost:** none in code. A level that moves the player *after* the session's `Awake` would have to call `SetCheckpoint` itself; none does today.

## 2026-10-30 — The death has a snarl

**Decision:** the Crawler's kill plays `Assets/Audio/Death_snarl.wav` flat (2D) through a `DeathSnarl` on the player, once per `Player.Died` (which `Die()` raises only once, and `Revive` never), at `deathSoundVolume`.
**Reason:** requested by the product owner: a sound that rhymes with the roar and ends it. Built from `monster_06.ogg` of the CC0 pack "80 CC0 creature SFX" (https://opengameart.org/content/80-cc0-creature-sfx, **CC0**): of the ten roar/monster clips, it was the one whose length after a 30 % pitch drop (`asetrate` 0.7, 0.83 s to 1.19 s) lands on the 1.2 s target and the loudest sustained one (RMS -15.6 dB, peak -2.5 dB), where `roar_02` came out 1.29 s and 3.6 dB quieter, `roar_01` is silent. Then a 5 ms attack, a 55 Hz decaying sine sting mixed under it, a 0.4 s fade-out, a limiter and a two-pass `loudnorm`: mono, 44.1 kHz, 16-bit, **1.200 s, -12.0 LUFS integrated, true peak -3.0 dB** (a -2 dB ceiling cannot reach -12 LUFS with the raw sting on top, hence the limiter). Played 2D, not at the Crawler: the death should be heard in full wherever it happened.
**Cost:** one more `AudioSource` (on the player, beside the listener). The test counts the trigger (`DeathSnarl.PlayCount`), not audible output, because batch mode has no audio device; the sound itself has to be heard by a person. The week-5 audio pass may replace the clip.

## 2026-10-30 — A hollow is the first real safe spot

**Decision:** `Hollow` (a box, no collider, no drawing) makes quiet sounds made inside it carry `hollowHearingMultiplier = 0.15` of their normal radius to the Crawler, **unless** the sound is louder than `hollowBreaksAbove = 0.25` (a ping, a shout, a door being closed) **or** the Crawler is already hunting. The HUD says "You are hidden" on entering and "You are exposed" on leaving (`LevelSession.Notice`). `Level02_Corridor` has two: the south alcove (the spawn) and a new pocket in the north wall on the far side of the room (opening x -3.1..-1.9, inside 1.2 x 1.2 m, 2.9 m from the Crawler's far patrol point, clear of the pillar by the body's width). The north wall is split to make it; the builder deletes the old single `Wall_North` it replaces.
**Reason:** the player walked along walls, bumped the Crawler and was always caught, and the GDD says a hollow is the answer. The patrol never enters either pocket, so the safe property is the one the GDD states: it does not hear you.
**Cost:** it is a stub in the stub. The rule lives in `Crawler.OnNoise` (one query, `Hollow.AnyContains`) and the pure `Hollow.HearingScale`; `NoiseBoard` / `HeardBy` replace both in week 3 and take `HearingScale` and its tests with them. With the shipped numbers a footstep carries only 2.3 m even in the open, so cover mostly protects against the F pulse, bumps and doors, and a ping in cover still wakes the whole room: that is the rule, not a bug. The Crawler can still walk into a hollow while hunting and kill a player there.

## 2026-10-30 — Chapter one teaches the controls on the HUD

**Decision:** five one-shot lines on the HUD's existing notice label, only in `Level01_Maze` (`LevelHints.hintsEnabled`, turned on by `Level01MazeSceneSetup`): the Space charge at the start, F after the first wave, the wall memory on the first touch mark, Shift after 3 s of noisy walking, E when a door is in reach. Each is shown once per run for 4 s (plus the HUD's 1 s fade), queued so two never overlap (`HintQueue`, pure, EditMode-tested). The wording is one static class, `HintTexts`. They travel through `LevelSession.Hint(text, seconds)` into `HudView.Notice(text, seconds)`.
**Reason:** the tutorial is the one room where a player can experiment for free, but nothing said which keys exist. A second text system would have been a second thing to keep dim, thin and in step with the first.
**Cost:** a hint shares the notice label, so a checkpoint line can replace a hint mid-display (the hint is not re-shown: once is once). The wall-touch hint reads `TouchMarks.ActiveCount > 0`, so it fires with the existing mark and does not depend on the wall-memory work, but it will also fire from any future effect that lights a touch mark.

## Under decision

- **Does a human read wall / pillar / corner / hollow / Crawler from the arcs alone, at these thresholds?** Everything above is counted in samples; none of it is seen in eyes. This is the week-2 go/no-go and only the readability session on Linux can close it.

## Resolved in week 2

- **The sign of `gainSign`: `+1`, kept.** With `+1` the nearest hit lands exactly on the carrier circle `R_k`, so the wave swallows a room from the far wall inward and the closest thing is the last thing standing — the "returning" the GDD asks for. `ArcMathTests.NearestSurfaceSitsOnTheCarrierCircle` locks it and `GainSign_FlipsDeformation` locks that the knob still means what it says. `−1` remains a 5-second diagnostic setting, nothing more.
- **The corner hot-spot is emergent; no vertex-proximity boost is added.** `SurfaceReadTests` orders wall < pillar < corner, and the blockout measurement says the ordering survives contact with real geometry *once* the step/kink split above removed the crossings that were masquerading as corners. An explicit proximity boost would have been a second, non-geometric brightness source in a design that just deleted its first one (materials). What is still open is only whether a person sees it — the bullet above.

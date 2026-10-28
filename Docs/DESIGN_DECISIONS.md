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
- Hysteresis against boundary flicker, if it is ever needed, goes in `PlayerVisual`. The pure `PlayerFacing.Resolve` stays boundary-deterministic so it stays testable.
- The `Player_34_*` poses stay unwired until the camera that needs them exists. They are imported with the same settings as the rest so nobody's first refresh commits PPU-100 defaults for them.
- Motion is measured off the `Visuals` transform. A number read from the root is not a measurement of the animation.

**Corrected 2026-10-29, by the 4096 re-delivery — the entry above stands as a record of a decision whose facts moved.** Two of its load-bearing measurements were true of the 1024 originals and are false of the files now in the repo. The profiles are **not** bit-exact mirrors: `flip(Player_side)` differs from the delivered `Player_side_left` in 35 % of sampled pixels, in the eye, the rim arc and the speculars, because the artist keeps the light fixed instead of mirroring it — so `flipX` now draws a character lit from the opposite side to the one delivered, and `Player_side_left` is a live texture rather than a redundant one (fixed the same day: `Docs/ISSUES.md` #5). And the `Player_34_*` poses are no longer waiting on a camera: the lead has assigned them the top-down diagonals, which is what issue #2 now builds on, with the same lighting caveat. The PPU changed with the canvas (640 → 2560) for the reason in `TUNING.md`: a sprite's world size is source pixels over PPU, and `maxTextureSize` does not move it.

## 2026-10-28 — A touch mark is placed where the drawn body meets the wall

**Decision:** `TouchMarks.Flash` takes the contact point, the surface normal **and the body centre**, and pushes the dash outward from the body by `silhouetteClearance = 0.24` m along `normalize(contactPoint − bodyCenter)`. `Player` supplies the centre and keeps no opinion about geometry. The dash is therefore no longer at the point physics reports; it is at the point the *character* touches.
**Reason:** the hitbox is a `0.25` m circle and the drawn figure reaches `0.447` m sideways (`2289` of a `4096` canvas at PPU 2560), so the reported contact sits 18–22 screen pixels **inside the torso**, and because marks live on the `Echo` layer — above `Default`, where `Visuals` is, which is what lets the arcs read over the character — the dash is composited on top of the body it belongs to. The first candidate, `collision.collider.ClosestPoint(body.position)`, was written down as the fix and **measured to be a no-op**: for a circle against a flat box face the contact point *is* the nearest point on the wall, so both candidates are the same pixel. The fault is not which surface point is chosen, it is that a surface point chosen for the collider is inside the art. The direction comes from `contact − centre` rather than `contact.normal` because it is true at once for a flat wall, a corner and a rotated box, and it does not depend on which way physics happens to point its normal.
**Cost:** one number that has to be re-measured whenever the art or the hitbox changes, and that is wrong in a specific way: it is a single clearance for poses that are not one width, so against the `1514` px side profile the dash floats ~17 px off the body. It is also **a compensation, not a cure** — `Docs/ISSUES.md` #4 is the real defect (the character is drawn wider than it collides), and closing that one sets this clearance to zero. Offsetting outward by 0.24 m additionally puts the dash *inside* a `0.4` m wall, which is invisible against an opaque wall and would show through a thin prop.
**Rules:**
- Placement lives in `TouchMarks`. `Player` reports where it was hit and passes its own centre; it does not compute offsets, and the presentation numbers stay off the balance profile.
- A mark flashed without a body to clear (`bodyCenter` equal to the contact point) is placed where it was asked for. `Vector2.normalized` of a zero vector is zero, and that is the documented behaviour the `AMark_LiesAlongTheSurfaceItTouched` case relies on.
- The claim is locked by measurement, not by reading the code: `TouchMarkPlacementTests` walks the rig player into `Wall_West` in PlayMode and asserts the mark is ≥ `0.44` m from the centre, on the far side of the contact and still inside the wall's own thickness. `PlayerPoseImportTests` is what notices when the art's footprint changes underneath it.
- `ClosestPoint` is not to be re-proposed as the fix. It is the same point, and `Docs/ISSUES.md` #1 records why.

## Under decision

- **Does a human read wall / pillar / corner / hollow / Crawler from the arcs alone, at these thresholds?** Everything above is counted in samples; none of it is seen in eyes. This is the week-2 go/no-go and only the readability session on Linux can close it.

## Resolved in week 2

- **The sign of `gainSign`: `+1`, kept.** With `+1` the nearest hit lands exactly on the carrier circle `R_k`, so the wave swallows a room from the far wall inward and the closest thing is the last thing standing — the "returning" the GDD asks for. `ArcMathTests.NearestSurfaceSitsOnTheCarrierCircle` locks it and `GainSign_FlipsDeformation` locks that the knob still means what it says. `−1` remains a 5-second diagnostic setting, nothing more.
- **The corner hot-spot is emergent; no vertex-proximity boost is added.** `SurfaceReadTests` orders wall < pillar < corner, and the blockout measurement says the ordering survives contact with real geometry *once* the step/kink split above removed the crossings that were masquerading as corners. An explicit proximity boost would have been a second, non-geometric brightness source in a design that just deleted its first one (materials). What is still open is only whether a person sees it — the bullet above.

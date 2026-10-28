# 5-week development plan (+1 week buffer)

**Start:** 19 October 2026 — **delivery:** 20 November 2026 — **buffer:** until 27 November
**Plan version:** 2.0 — rewritten against [GDD.md](../GDD.md) v3.0 (root). This file replaces the plan written against GDD 1.1.
**Scope being delivered:** echo core (mic, cone wave, returning arc train, brightness-from-geometry, touch marks, weak pulse) + silence (Listen mode, Shift) + hiding (hollows, sanctuary checkpoints, doors that hold) + orientation (landmarks, memory trace, exit hum) + 3 enemies (Crawler, Spike, Absorber) with the suspicion meter + decoy marble + noise floors and noise zones + 3 levels, level 1 being the enemy-free maze + calibration inside the dark + minimal HUD and menus + full audio + Linux build + the bot simulation that feeds the report.
**Out of scope:** [SCOPE_CUTS.md](SCOPE_CUTS.md) — read it before adding any feature. **It still describes the GDD 1.1 cut list; reconciling it against v3.0 is week 2 day 1.**

This file is the **living project history**: at the end of each day the ticked items and the week's status are recorded and committed here, and at the end of each week that same commit is tagged `v0.x`. Technical decisions: [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md). Balancing parameters: [TUNING.md](TUNING.md).

## What changed in the plan, and why

The plan used to be organised around the arc being *readable*. GDD v3.0 says the arc is only the entry point to a stealth game, so the schedule moved from "prove the visual language, then build a game" to "prove the visual language **inside a playable room as early as possible**". Consequences:

| GDD 3.0 decision | Effect on this plan |
|---|---|
| Surface materials are gone; brightness comes from the curvature of the distance profile | Week 2 replaces the per-surface `reflectivity` lookup with the curvature term. The old week 2 exit criterion ("name metal vs sponge from the arcs") is void; it becomes "name pillar, wall, corner, Crawler" |
| Levels are 3 + an enemy-free tutorial maze, not 4 | Level work is redistributed: a blockout room in week 2, the full maze in week 3, The Corridor in week 4, The Sump in week 5 |
| One-hit death + sanctuary checkpoints | Death/retry/checkpoint logic is gameplay, not polish — it lands in week 2 (retry) and week 3 (sanctuaries) |
| Enemy hearing is a suspicion meter fed by one `NoiseBoard` | `NoiseBoard` + `HeardBy` move to week 3, with the Crawler running on a documented proximity stub in week 2 |
| Microphone is the primary input and the whole team is on Linux | The real `MicrophoneEchoSource` moves from week 3 to **week 2**. Measured 2026-10-28: the Linux editor exposes capture devices and returns live samples, so **Linux is the microphone platform** — the editor here is the reference rig and the Linux player build is what has to be verified. Windows is not a test target; nobody on the team has a Windows machine |
| The game theory report needs bot data | The scripted bot becomes a named deliverable (week 4 v1, week 5 data), not an idea |

## The rule for every week: something new on screen

Each week must end with a change a tester can *see* without reading a commit log. The ladder:

| Week | What is on the screen that was not there last week |
|---|---|
| 1 | Black screen; Space emits one cyan arc that dents around a pillar, stays flat on a wall, breaks between objects |
| 2 | **A room.** You speak into a microphone and an enclosed level appears around you — walls, a pillar, a corner that burns bright, an alcove. A Crawler with two humps patrols it and shimmers. Touching a wall leaves a dim mark; `F` gives a small 360° ring. Touch it and you die: black screen, instant retry |
| 3 | **The dark answers back.** The screen is black until you make a sound, and now ripples exist that are not yours: warm moving ones from enemies in Listen mode, cool steady ones from landmark sounds. Faint wall traces hang in the air for ten seconds. Doors open with `E`, a hollow hides you, a sanctuary room saves a checkpoint, and there is a HUD ring for cooldown, a main menu, a calibration page — and a whole maze (level 1) you can finish with your voice |
| 4 | **A hunt.** Enemies freeze for half a second and snarl before they chase, then follow the place you last sounded rather than you. Doors splinter under the Spike and stay splintered. A thrown marble blooms into an arc train across the room and something goes after it. The arcs themselves start to jitter as the nearest suspicious enemy closes in |
| 5 | **A hole in the sound.** A patch of arcs simply goes missing and the thing that ate it is slow and patient. A noise zone halves your reach and statics the arcs — safe but blind. Level 3 ends at the only light in the game, with a full audio pass behind it and the credits after that |
| 6 | Nothing new by design: bugs fixed, readability re-tuned, the demo recorded |

## Rules that never change

1. At the end of every week `main` must **open and be playable with editor 6000.6.3f1**. If something is left half-done, it is switched off behind a `bool` in a ScriptableObject — the project is never left unrunnable.
2. Each week's success criterion is a "testable play session", not a feature count.
3. At least one compiling commit per day.
4. Never rename a `.cs` file or class outside the editor (broken references = an unrunnable scene).
5. `ProjectSettings/**` files are only edited in a separate, batched commit.
6. **Every parameter that a designer might argue about lives in `EchoTuningProfile` or a level ScriptableObject, never in C#.** GDD 3.0 is written so balance can change without touching code; the plan keeps that promise or the sound designer and level designer are blocked on the programmer's schedule.
7. A chase space is not "done" until it passes the GDD level design checklist (loop, a hiding option within 20–30 s of travel, two doors if a Spike lives there, a chosen wake trigger, a landmark per room, one tool that answers the problem, a bot-provable escape route).

## Phase table

| Week | Dates | Output | Exit criterion (playable test) | Tag |
|---|---|---|---|---|
| 1 | 19–25 Oct | Skeleton + the first arc | WASD moves, Space emits a cyan arc that dents at the pillar, stays flat on the wall, breaks between objects; 60 fps; zero console errors | `v0.1-sandbox` |
| 2 | 26 Oct–1 Nov | **A room, your voice, a crawling thing** — mic + arc train + curvature brightness + touch marks + weak pulse + Crawler + blockout level + death/retry | A tester on **their own Linux machine, with a real microphone and headphones**, walks into a blockout room, pings by voice, names **3 of {wall, pillar, corner, hollow, Crawler}** from the arcs alone, and dies at least once and retries — **the project's go/no-go point** | `v0.2-first-room` |
| 3 | 2–8 Nov | **The maze tutorial complete** — NoiseBoard + suspicion + Listen mode + doors/hollows/sanctuaries + memory trace + calibration + HUD/menus + level 1 | Start from the main menu, calibrate inside the dark, finish level 1 (enemy-free maze) using voice only, use `E` on a door, hide in a hollow, get a checkpoint in a sanctuary, die and return to it; first tagged Linux build | `v0.3-maze` |
| 4 | 9–15 Nov | **Level 2 + the hunt** — Spike + doors that break + decoy marble + alert/give-up cues + fear jitter + landmarks + bot v1 | In level 2, hear the Crawler before seeing it, read its signature, open a door to wake it, escape along the loop, throw a marble and reach the exit; the bot completes the scripted escape route headless | `v0.4-corridor` |
| 5 | 16–20 Nov | **Level 3 + Absorber + noise zones + audio + delivery** | All 3 levels start to finish on Linux with a real microphone at 60 fps; the Absorber's gap is noticed by a tester without being told; a teammate brings the project up from a fresh clone per README | `v0.5-submission` |
| 6 | 21–27 Nov (buffer) | Bug burn-down, readability and ρ re-tune, level select, demo recording | Final presentation build; optional experiment (The Listener) only if everything above is green | `v0.6-final` |

## Pre-agreed cut list (if a week falls behind)

- **Week 2:** drop the weak pulse, then the fracture mask (raw `d` only). **Never drop:** the mic path, the mesh builder, the tuning ScriptableObject, the `IEchoSource` seam, or the readability test itself.
- **Week 3:** drop CREDITS and the voice-loudness indicator; drop the memory trace to 5 s. **Never drop calibration, Listen mode, or the checkpoint/retry loop** — without them the fear and the one-hit death both stop working.
- **Week 4:** the Spike becomes the Crawler with a sawtooth collider and no door-breaking; the proximity warning stays text-only; the marble is a noise event without its own arc train.
- **Week 5:** in this order: fewer obstacles in level 3 ← the Absorber becomes a hole in the arcs without the occlusion pass ← the ending becomes a text screen ← the bot report data drops to a single ρ value. **The commitment line is 3 levels + the maze's tutorial beats — polish the cut, not the levels.**

## Open defects carried into week 2

Found while closing week 1, all of them on the path that week 2 tooling takes:

1. `EchoDirector.AcquireRecord` throws `ArgumentOutOfRangeException` when the pool is empty — every `ForcePing` outside Play Mode, and every profile with `maxLivePings = 0`. Guard it before `EchoDebugWindow` lands.
2. `Player.director` is `{fileID: 0}` in `Sandbox.unity:729`; the player reaches the director only through the `FindAnyObjectByType` fallback. Wire the reference or delete the field.
3. `PlayerMock → Player` rewrite: `PlayerMock.Gameplay` reads `Keyboard/Pointer.current` directly and bypasses `GameInput`. `.inputactions` stays untouched until week 3 (see `DESIGN_DECISIONS.md`).
4. Multi-arc train is unexercised: `EchoDirector.AdvancedArcs` always calls `AddArc(..., arcIndex 0, ...)`, and `innerDecay = 1`.
5. `Assets/Tests/PlayMode/` holds only an `.asmdef` — zero test files. Week 2 puts the first scene-validation tests there.
6. ~~`EchoTuningProfile.SurfaceKind` still carries `Concrete / Metal / Sponge`.~~ Done 2026-10-27: `Metal` and `Sponge` are deleted (their numeric slots stay empty so nothing already serialised re-tags), `Concrete` became `Plain`, and the enum's only job is naming the **signature** a collider casts. The remaining job is week 2 day 2: delete the per-object amplitude lookup itself and derive brightness from curvature. The scene objects keep the fossil names `Pillar_Concrete` / `Wall_Concrete` because `GitSetupValidator`'s merge probe matches on that literal — rename them in the editor and fix the probe in the same commit.
7. `Assets/Prefabs/` is empty. Week 2 creates the first real prefabs; every prefab referenced in a GDD table gets made in the week its system lands, not "in the audio week".

---

# Week 1 — daily checklist (complete, kept as the record)

The only goal was: one cyan ring that collapses around pillar/wall/box using the GDD formula, at 60fps with zero errors, and a repository the team can open.
**Deliberately not built (so the scope did not slip):** arc train, fracture mask, materials, microphone, HUD, menus, enemies, levels.

### Day 1 — infrastructure and documents

- [x] `git lfs install --local` and the merge driver registered — **this was ticked on day 1 and was false until the wrapper landed.** The recipe in `TEAM_SETUP.md` pointed git straight at `UnityYAMLMerge`, which cannot parse git's extension-less temp files: every scene merge either errored into a marker-less conflict or, worse, exited 0 having kept only our side. `Tools/unity-yaml-merge.sh` plus `Register Unity Merge Driver` is what made it real, and `Validate Git Setup` now proves it by merging an actual scene rather than reading three config lines.
- [x] `.gitattributes`: `text eol=lf` for `*.md/*.json/*.cs/*.shader/*.hlsl` and the defensive comment "Unity YAML is never put in LFS"
- [x] `Docs/`: GDD, PLAN, SCOPE_CUTS, GIT, TEAM_SETUP, root README
- [x] Template cleanup: delete `Assets/Welcome`, remove `com.unity.learn.iet-framework` and its Settings file, rename `SampleScene` → `Sandbox`
- [x] Folder structure and four `.asmdef` files (Runtime / Editor / EditMode / PlayMode)

### Day 2 — project settings and scene

- [x] `Project Settings > Editor`: turn off Enter Play Mode Options (Domain Reload comes back)
- [x] Layers: `World, EchoTarget, Enemy, Absorber, Player, Trigger` — Sorting: `Default ← Echo ← HUD`
- [x] `Sandbox` scene: orthographic camera at size 6, black background, Post Processing ticked, Global Light 2D removed
- [x] Global Volume + `EchoVolumeProfile.asset` with only a Bloom override
- [x] Three test objects on the `EchoTarget` layer (pillar / wall / box) and an `EchoField` GameObject
- [x] `PlayerMock`: WASD movement, angle from the mouse pointer, Space → ping

### Day 3 — input and rays

- [x] `EchoTuningProfile` + a sample `Data/Echo/EchoTuning.asset` (with a fallback if the asset is missing)
- [x] `IEchoSource` + `SimulatedEchoSource` + `ScriptedEchoSource`
- [x] `PingRecord` + `RayFan` using `Physics2D.Raycast` from an event-based `ContactFilter2D` (the `*NonAlloc` family was removed in Unity 6)

### Day 4 — geometry and rendering

- [x] `ArcMath` (a pure static class, formula `r_k(θ) = R_k + A_k (d(θ) - d_min)`)
- [x] `EchoRenderer` with a fixed-capacity Mesh + `IEchoRenderer`
- [x] `EchoDirector`: a pooled set of three pings, `ForcePing`, arc death at `R <= rKill`
- [x] `EchoArc.shader` (additive, `_Hdr`, soft skirt) + the `Arc_Cyan` material

### Day 5 — tests and closing the week

- [x] `Tests/EditMode/ArcMathTests.cs`: `Formula_MatchesGdd`, `MonotonicInDistance`, `CollapsesToOrigin_AsAgeGrows`, `AllMisses_ProduceNoSamples_AndNoNaN`, `GainSign_FlipsDeformation` (+ seven more: inner train, alpha, sample angles, ray capacity, `PingRecord` reuse, `EmissionPulse_NeverOvertakesTheReturningCarrier`)
- [x] Running the tests from the command line with `-runTests -testPlatform EditMode` → 16 of 16 green (`Logs/editmode.xml`)
- [x] `Tests/EditMode/ArcReadabilityTests.cs`: four readability claims expressed in pixels (the pillar dent, two different radii for the pillar and the wall behind it, the pillar centre as the innermost point of the arc, ribbon thickness) — because the claim "nothing is visible" has to be answered with a number, not an opinion
- [x] The week 1 rebalance (`gain0 = 1`, `baseThickness = 0.05`, `maxRadius = 6.5`) and the outgoing wave ring (`emissionPulseSeconds`); reason and measurement in `Docs/TUNING.md` and `Docs/DESIGN_DECISIONS.md`
- [x] `Docs/TUNING.md` with the parameter table
- [x] The commit ritual ([GIT.md](GIT.md))

### Week 1 exit criterion

- [ ] A fresh clone opens with 6000.6.3f1 and `Sandbox` Plays without errors ← the clone half is measured: a `git clone` over SSH gives 144 files, `ProjectSettings/ProjectVersion.txt` is 6000.6.3f1, the LFS sprite smudges into a real 16×16 PNG, `Tools/unity-yaml-merge.sh` keeps mode 755, and every `m_Script` and material GUID in `Sandbox.unity` resolves (three of them into the URP package cache, none missing). The merge driver is **absent** in the clone on purpose ← it lives in local config, so `Docs/TEAM_SETUP.md` §3 is the first step on any new machine. Still open: opening the clone in the editor and pressing Play ← **closes on a teammate's Linux machine in week 2, day 1.**
- [ ] The cyan ring dents at the pillar, is flat on the wall, has a bright point at the box corner ← two claims have a capture now. Pillar: the pillar direction is the innermost point of the arc and its tips fall back on the wall behind. Wall: 141 of 201 rays hit and the profile spans only 0.40 m, which reads as one flat sheet. Corner: **not met yet** ← all three sandbox surfaces carry `reflectivity = 1`, so brightness is uniform and the corner box reads as an *angle* in the ribbon instead of a bright point. **Under GDD 3.0 the fix is not a material lookup: it is the curvature term, and it is week 2's first job.** Waiting on the designer's eyes.
  - Recipe for the three shots (the captures themselves stay out of git, see `.gitignore`): enter Play Mode, `Time.timeScale = 1`, clear `alive` on every record in the pool, `SetPose(playerPosition, aim)` + `ForcePing(1f)` with aim 0° for the pillar, 90° for the wall and 148° for the box, then set that record's `emitTime = Time.time - 0.5f` (carrier at 3.0 m) and `Time.timeScale = 0.0015f` and screenshot immediately. Outside Play Mode this produces nothing: `Awake` never ran and there is no pool.
- [x] Mesh construction in `EchoRenderer.AddArc`/`EndFrame` under 3ms in the Profiler; zero errors in the Console ← 300 repetitions on a live ping with 141 of 201 rays hitting: `AddArc` alone 0.012 ms, `BeginFrame` + `AddArc` + `EndFrame` 0.054 ms for 140 quads, 0.117 ms for three arcs (420 quads, `OverflowCount` 0). The live loop with three real pings drew 227 quads with zero overflow, and the whole main thread frame was 1.37 ms. The Console held zero entries of any type through startup, twelve pings and the pool-overflow path. ← the first attempt at this measured 0.001 ms with `LastQuadCount` 0: it ran in **edit mode**, where `Awake` never filled the pool and `AddArc` builds nothing. Any measurement of this system must assert a quad count above zero before it believes a millisecond.
- [x] `git lfs ls-files -n` lists the binaries (`Assets/Art/Sprites/Player_dot.png`); `merge.unityyamlmerge.driver` is registered
- [x] Tag `v0.1-sandbox` and push with `--follow-tags` ← `e6b55ab`, on `origin` (`git ls-remote --tags` lists `refs/tags/v0.1-sandbox`)
- [ ] If the ring was not visible, debug in this order: the camera's Post Processing tick ← the `EchoTarget` layer and the ray mask ← `sortingLayer` and the material ← `_Hdr` against the Bloom `threshold`

Two things visible in the editor Console that are not errors: the "assembly has no scripts" warning for `EchoMaze.Tests.PlayMode.asmdef` (filled by the PlayMode tests in week 2) and the `EchoDirector has no EchoTuningProfile` warning, which only appears when the scene was not rebuilt and the `profile` field on `Echo System` is empty.

---

# Week 2 — a room, your voice, a crawling thing (26 Oct – 1 Nov)

**Goal:** the first screen that looks like the game. A tester speaks, a room appears, and something in it is alive. Also: the go/no-go on readability, on the microphone on **Linux**, and on whether `ρ` can be tuned at all.
**Visible change:** from "one arc in an empty black space" to "an enclosed room you can move through, with a patrolling creature and a death".

**Deliberately not built this week:** Listen mode, suspicion meter, doors, hollow-as-a-hiding-rule (its geometry exists, the hearing exemption lands in week 3), sanctuaries, the marble, landmark audio, menus, HUD, the input-action asset.

### Day 1 — reconcile the documents, then the machine

- [x] Rewrite [SCOPE_CUTS.md](SCOPE_CUTS.md) against GDD 3.0: materials, the 4-level structure, the BREATH bar, the health bar, Mimic/Spider/Frequency Hunters, non-story modes — done 2026-10-27, and it now separates "removed from the design" from "cut for time"
- [x] Update the root [README.md](../README.md): the GDD moved to the repo root, 3 levels + the tutorial maze, one-hit death, the microphone is week 2 not week 3 — done 2026-10-27
- [x] `Docs/GDD.md` (the v1.1 copy) is banner-marked **superseded — history only**, and the README points at the root document. Deleting it is also fine (`git rm Docs/GDD.md`); it stays because it explains why `TUNING.md` and this plan are shaped the way they are — see the 2026-10-27 entries in [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md)
- [x] `Docs/QA_CHECKLIST.md`, `Docs/PLAYTEST_REPORT.md`, `Docs/TUNING.md`, `Docs/MIC_TEST_MATRIX.md`, `Docs/TEAM_SETUP.md` reconciled with v3.0 (no metal/sponge rows, no Persian/RTL row, no "level 4", the mic rows are scheduled in week 2) — done 2026-10-27
- [x] `Docs/DESIGN_DECISIONS.md`: "2026-10-27 — GDD 3.0 is the design; the live document sits at the repo root" and "2026-10-27 — `SurfaceKind` loses Metal and Sponge and keeps its numbers", plus amendments on the four entries v3.0 invalidated (mic week, one-collider-one-material, four levels, the BREATH bar)
- [x] `Docs/DESIGN_DECISIONS.md`: add "2026-10-28 — brightness from geometry replaces the material lookup", with the reason (GDD 3.0) and the cost (a hot-spot must be tuned via the curvature terms, not assigned per object) — done 2026-10-28 with the code
- [ ] Pull the project onto a **teammate's Linux machine**, run `Docs/TEAM_SETUP.md` §3 (merge driver), open the clone in 6000.6.3f1 and press Play → **closes the open half of the week 1 exit criterion** ← the machine that was going to do this does not exist; the team is all Linux
- [x] ~~Run `Mic Probe` on the Windows machine and start `MIC_TEST_MATRIX.md` entries~~ → **void**: the microphone is measured on Linux. Rows 1, 11 and 14 of [MIC_TEST_MATRIX.md](MIC_TEST_MATRIX.md) are filled from the lead's machine, 2026-10-28

### Day 2 — the arc train, and brightness from geometry

- [x] `EchoDirector`: real multi-arc train — `arcsPerPing`, `arcInterval`, `A_k = gain0 · innerDecay^k`, arc death at `R <= rKill`; remove the hard-coded `arcIndex 0` — done 2026-10-28. `AdvanceArcs` `break`s on an unborn arc and `continue`s on a dead one, because arc k is older than arc k+1
- [x] Per-ping fracture mask from `PingRecord.seed` + `brokenAt[]`, every arc of that ping reads the same mask (locked decision), plus a few dropped distances on flat runs so a wall always looks incomplete — done: `SurfaceRead.Compute` draws the mask once at emit time with `System.Random(ping.seed)`, only over locally flat runs
- [x] Curvature brightness: `b(i) = base + k · clamp(|d[i+1] − 2·d[i] + d[i−1]| / dCurv)` fed to the existing per-vertex colour/thickness in `EchoRenderer`. New fields in `EchoTuningProfile`: `brightnessBase`, `brightnessGain`, `distanceCurvature` — done, with one change of name: the divisor is **`d[i] · Δθ²`**, which makes a flat plane measure exactly 1.0 whatever the distance and whatever the ray count, so `curvatureFloor = 2` and `curvatureRange = 6` are absolute numbers instead of per-ping normalisations. Without the `Δθ²` the metric was distance-dependent by ~10⁴ and nothing crossed the floor
- [x] `EchoSurface`: keep it as the signature tag (`Plain / Absorber / Crawler / Spike` — the enum half is already done), and stop letting `reflectivity` pick an amplitude per object. Absorbers keep `0`; everything else is `1` and reads its brightness from geometry — done with the day-4 rebuild: both scenes store `reflectivity: 1` on every ordinary body and `SceneKit` writes it, so a rebuilt scene cannot drift. The honest residue: the field is still read (`RayFan` → `PingRecord.amp` → `EchoRenderer`), because that is exactly how an `Absorber` erases its samples (`ArcMath` requires `amp > 0`). A designer can still type `0.5` and get a dim wall back; retiring the knob completely is week 3's `NoiseBoard` decision
- [x] Guard `AcquireRecord` against an empty pool (defect 1) — done, one-shot warning instead of a silent null; fix `Player.director` wiring (defect 2) — **closed 2026-10-28** with the rebuild: `Sandbox.unity` line 729 is now `director: {fileID: 243327850}`, and `SceneKit.EnsureDirector` hands the reference to `Player` and `Crawler` on both scenes, so there is nothing left for a fallback to hide
- [x] Tests: `BreakupTests` asserts the mask is identical across the arcs of one ping; `CurvatureBrightnessTests` asserts corner > pillar > wall in brightness for the same ping — done as `ArcTrainTests` (7) and `SurfaceReadTests` (9), **in model values, not pixels**: the pixel half is deferred to the day-5 debug window, because asserting on a screenshot in CI is how a test starts losing

### Day 3 — the microphone, for real

- [x] `MicrophoneEchoSource : IEchoSource` using `Microphone.Start` + `GetData`; RMS over a small buffer; `RawLoudness` out of the gate, `Loudness` after — done 2026-10-28 and **measured working on Linux**. Two API facts the training data gets wrong: `Microphone.Start` returns the `AudioClip` it records into and its length argument is an `int` of seconds; there is **no** `Microphone.GetData` — samples come from `AudioClip.GetData(Span<float>, int offsetSamples)`. Two facts only a real device gives: the ring is **uninitialised memory** for the first window (0.10 RMS of garbage against a 0.003 room floor, enough to open the gate by itself → `WarmupSeconds`), and `GetPosition` advances in blocks, so a frame with zero new samples must **not** feed the gate a zero (that fake silence closes the gate mid-shout)
- [x] `NoiseGate` + `LoudnessFollower`: threshold with hysteresis, **one ping per rising vocal event** (the week 1 note is explicit: the automatic gate must stop firing every `minPingInterval`), cooldown `≈ 0.6 s + 1.4 s · L` — done: `NoiseGate` is a plain class with immediate attack and held release; `minPingInterval` is **deleted** from the profile and the asset; `cooldownBase`/`cooldownPerLoudness` replace it
- [x] Loudness drives reach and ray count (`minRadius → maxRadius`, `minRays → maxRays`) — already in `ArcMath`, now fed by a live signal for the first time ← verified live: two real microphone events at different loudness produced 153-ray and 111-ray fans
- [x] `EchoTuningProfile.echoSourceMode` gains `Microphone`; **the fallback is `Simulated` with a Console warning, never a crash**. `ManualOnly` (Space) stays available everywhere, so nobody is blocked by a mic. `Sandbox.unity` now saves `sourceMode: 2` — the microphone is the default path on this team's machines, not the exception
- [x] `ScriptedEchoSource` unchanged in spirit — it is the bot's input in week 4 — but it now runs its levels through the same `NoiseGate`, so the scripted, simulated and real sources all report `NewVocalEvent` the same way and one gate implementation is the one being tested
- [x] PlayMode test (first real files in `Assets/Tests/PlayMode/`): a scripted loudness sequence produces exactly N pings, no barrage — `VocalEventPingTests`: 30 loud frames = 1 ping, two separated events = 2 pings, a whisper and a shout differ in reach **and** ray count, a second event inside the cooldown = still 1 ping. 4/4 green, and 39/39 EditMode

### Day 4 — a room you can walk around in

- [x] New `Player` (replaces `PlayerMock`, kept as the reference until deleted): WASD/arrows, mouse aim, `Shift` = slow silent step, `Space` = charged manual echo, `F` = weak pulse. `GameInput` and `.inputactions` still wait for week 3 — say so in a comment — done 2026-10-28: `PlayerMock` is deleted, `Sandbox` was rebuilt onto `Player`, and the class comment names week 3 as the rewrite. The charge is `manualEchoLoudness → 1.0` over `manualChargeSeconds` (`ArcMath.ManualLoudness`), and a shout whose cooldown expired while the key was held is dropped, not queued
- [x] Touch marks: on collider contact, a dim segment of that wall flashes at the contact point and fades in ≈0.5 s; a bump also emits a very small noise — done: `TouchMarks` is a recycled `SpriteRenderer` pool of short quads rotated to the contact **tangent**, so a mark lies along the wall it touched instead of pointing at the player (`TouchMarkTests.AMark_LiesAlongTheSurfaceItTouched`). `Player` only reports the noise when the collision had real speed behind it, so leaning on a wall to listen is silent
- [x] 360° weak pulse: reuse the ray pipeline with `coneHalfAngleDeg = 180`, reach ≈1.5 m, its own cooldown, very low noise — done as `weakPulseRadius` / `weakPulseRays` / `weakPulseCooldown` in the profile and `EchoDirector.TryWeakPulse`, which pays its own cooldown and never the loud one — that separation is the whole reason to press `F`, and `NoiseStreamTests.TheWeakPulse_IsTheQuietestEventOnTheStream` locks it
- [x] `Level02_Corridor.unity` **blockout**: one enclosed room (4–8 m), flat walls, one pillar, one box-corner, one alcove shape (the future hollow), one exit tile. `Sandbox` keeps existing as the math playground; the blockout is where gameplay gets tuned — done: `Level02SceneSetup` (menu `Tools/Echo Maze/Week 2 Bootstrap`) generates a 7 × 5 interior with five wall runs, `Pillar`, `Box_Corner`, a south-wall alcove (three slabs, a real pocket in the wave, not a box in front of a wall), an `Exit_Tile` trigger the fan must not measure, and the player spawned inside the alcove. The room is 7 × 5 rather than the GDD's 4–8 top end on purpose: `maxRadius = 6.5` has to stay a *shout* measurement of the far wall from anywhere inside. `Sandbox` still exists and was rebuilt with every reference wired
- [x] The Crawler: two `CircleCollider2D` with a gap (its signature is the collider shape, no special code), slower than the player, a fixed waypoint patrol with a **seeded** random pause, footstep ripples as noise events — done: `crawlerSpeedMultiplier = 0.65` against `walkSpeed`, `System.Random(seed)` for the pause so a run replays exactly, and `EmitStep` puts its dragging footsteps on the same `ReportNoise` stream as the player's. Nothing hears them yet; that is week 4's landmark audio
- [x] Detection stub, documented as temporary: kill on contact, plus "heard a ping inside `ρ · R(L)`" → walk to the ping origin. Replaced in week 3 by `HeardBy` + the suspicion meter; note it in `DESIGN_DECISIONS.md` so the stub does not quietly become permanent — done: "2026-10-28 — Week 2 enemy hearing is a deliberate stub on a permanent seam", with the rules week 3 must not break, and `huntSeconds` gives an unreachable hunt a deadline instead of a wall-pressing loop
- [x] Living things shimmer: arc samples whose `kind` is a creature pulse at a heartbeat rate; walls stay steady — done 2026-10-28 with the arc-train work (`SurfaceRead.Shimmer`, `heartbeatHz`, `shimmer`); the numbers still need the Crawler on screen to be tuned by eye
- [x] One-hit death: contact → black screen + "YOU WERE HEARD" + instant retry (<2 s) to the room start (the sanctuary checkpoint comes in week 3) — done: `Player.Die` (no health, no knockback) → `RunController` unscaled `retryDelay = 1` → revive at the captured room start **and** `Crawler.ResetRun`, which rewinds the spawn, the first leg and the seeded RNG so the retry patrols identically. Found a real bug: the crawlers were cached at `Awake`, so the week-3 section reset would reach into destroyed objects — they are looked up at retry time now, and `RunControllerTests` covers it

### Day 5 — the readability gate and closing the week

- [x] `EchoDebugWindow` (Editor): Force Ping, draw raw rays in the Scene view, `d[i]` and `amp[i]` and now `b[i]` plots, arc freeze, ping timer + allocation readout. Everything week 2's tuning needs, so balance is measured and not argued about — done 2026-10-28 (`Tools/Echo Maze/Echo Debug Window`). The freeze is not a pause on `Time`: `EchoDirector` grows an editor-only frozen clock that `Emit`, `AdvanceArcs` and the shimmer all read, so a ping fired *while frozen* is born at the frozen instant and ages in steps instead of jumping. It rebinds after a domain reload by searching for the director, and it subscribes to `NoiseEmitted`, which is the only way to see whether a sound the Crawler heard ever drew a wave. Every number quoted in `TUNING.md`'s brightness rows and in the step/kink decision came out of this window.
- [x] Resolve the two `DESIGN_DECISIONS.md` "under decision" items with the debug window and a slider: the sign of `gainSign`, and whether the corner hot-spot is emergent or needs an explicit vertex-proximity boost. Record the answer here and in `PLAYTEST_REPORT.md` — done: `+1` kept (locked by `NearestSurfaceSitsOnTheCarrierCircle`, and it is what makes the nearest surface the last thing standing), hot-spot **emergent, no proximity boost**. Both answers moved into "Resolved in week 2" in `DESIGN_DECISIONS.md`, and the template now carries them under "Recorded before the session" so a tester confirms them in eyes instead of re-deciding them. **What the window actually found on the way is the bigger item:** the curvature metric was measuring *silhouette crossings* as corners (12670 at the `Box_Corner` → `Wall_North` edge), which is fixed by the new `edgeJumpFraction` step/kink split and locked by `EdgeBreakTests` (4) — see the 2026-10-28 decision entry.
- [x] `TUNING.md`: the arc-train block (`arcsPerPing`, `arcInterval`, `innerDecay`), the brightness block (`brightnessBase`, `brightnessGain`, `distanceCurvature`), the mic block (gate threshold/hysteresis/`ρ = 0.8` starting point), each with "if you raise it / if you lower it" — done, and the brightness block is no longer guesses: `curvatureFloor`, `curvatureRange` and the new `edgeJumpFraction` rows carry measurements from `Level02_Corridor`, plus a "Measured, still open" section for the one defect the split did **not** fix (oblique incidence inflates a flat plane to `2/cos²ψ − 1`) and for this week's only fake bug report: **Enter Play Mode Options had silently drifted back on** on the lead's machine, which is what was nulling `Crawler.pauseRandom` between recompiles. `EditorSettings.asset` is back to `0`; nothing validates that file yet, and week 3 should.
- [ ] First **real readability session**: 3 people who have never seen the game, headphones + mic on **Linux**, name 3 of {wall, pillar, corner, hollow, Crawler} from arcs alone. This is the go/no-go ← **blocked on people, not on code.** No percentage-of-cone in this repo can express whether a shape is *seen*; the tooling, the numbers and the questions are ready (`PLAYTEST_REPORT.md`).
- [ ] Freeze the week's numbers into `EchoTuning.asset`, run the full suite from the command line, commit with the ritual, tag `v0.2-first-room`, push with `--follow-tags` ← the asset already carries every week-2 number and the suites are green in the editor (51 EditMode + 18 PlayMode, 2026-10-28). Waiting on the session above: freezing numbers before anyone has looked at them is how a bad default becomes a regression lock. The command-line run and the tag come after.

### Week 2 exit criterion

- [ ] **Go/no-go:** a tester names at least 3 of {wall, pillar, corner, hollow shape, Crawler} from the arcs alone, with the Crawler never labelled on screen. If this fails, week 3 is cancelled and goes entirely to readability — gameplay does not start on an unreadable wave
- [ ] A shout and a whisper give visibly different reach on a real microphone, and one vocal event makes exactly one ping
- [ ] You can walk the blockout room blind, find the exit by pinging, and get killed by the Crawler at least once; retry returns you in under 2 s
- [ ] Corner reads as a bright point (the open week 1 claim, now closed by the curvature term, not by a material)
- [ ] 60 fps with the arc train live: quad count and frame time measured in Play Mode with a non-zero `LastQuadCount` (the week 1 lesson), `OverflowCount` 0
- [ ] All PlayMode + EditMode tests green from the command line; `Logs/` captures committed as evidence, captures themselves stay ignored
- [ ] Tag `v0.2-first-room` on `origin`

---

# Week 3 — the maze tutorial, silence as a move (2 – 8 Nov)

**Goal:** level 1 finishes as a game, not a demo: black screen, your voice, doors, a hiding place, a checkpoint, a menu, and a calibration that measures itself inside the dark.
**Visible change:** the dark becomes *populated* — ripples that are not yours, faint traces of walls you already saw, a HUD ring, and menus around it.
**Work:** `NoiseBoard` (one stream: ping / weak pulse / walking / Shift = nothing / wall touch / noisy floor / doors) + the pure, unit-tested `HeardBy(distance, loudness, walls, closed doors, noise zones, hollows)`; `EnemyBrain` with the suspicion meter and Dormant / Patrol / Curious / Investigate / Chase / Lost (seeded pauses, deterministic); the Crawler stub removed and wired to `EnemyBrain`; Listen mode (hold Ctrl) with warm enemy ripples that vanish when an enemy stands still, and cool steady landmark ripples; hollows (hearing × ≈0.15 except against a chaser; pinging inside breaks cover); doors as Open/Closed/Broken with `E`, closed = a wall to rays and to hearing; sanctuary rooms = checkpoint (player position + section state) + enemies give up at the threshold; section state reset on retry (defect-level prerequisite: every static holder gets a `ResetStaticState()`); memory trace (10–15 s, walls only, never enemies); noisy floors (glass/gravel/puddle trigger zones); touch-mark noise cost; calibration inside the dark (≈5 s of enforced silence at the start of level 1, measures the noise floor with the ambience playing) + the manual calibration page (live waveform, sensitivity, threshold, TEST) reachable from menu and pause; minimal HUD (cooldown ring bottom-right, "Make a sound to listen" that fades after the first ping, faint pause corner); `GameFlow` + Boot scene + main menu (PLAY / CALIBRATE MIC / CREDITS / QUIT) + pause + win/lose panels; `GameInput` on the rewritten `InputSystem_Actions.inputactions` and `PlayerMock` deleted; `Level01_Maze` completed to the GDD beats (dead ends, loops, pillars, boxes, a door, glass shards, one hollow, one sanctuary, a dripping-pipe landmark, per-room landmark sounds, the exit hum, the audio-only growl near the exit); **first tagged Linux build.**
**Tests:** `HeardBy` as a pure function gets the heaviest EditMode coverage in the project (occlusion by a wall, blocked by a closed door, halved reach and masked hearing inside a noise zone, hollow multiplier, the Chase exemption, suspicion drain). PlayMode: a level-1 smoke test that walks a scripted route to the exit headless.
**Cut list:** CREDITS → voice-loudness indicator → memory trace 5 s. Never calibration, Listen mode, or checkpoint/retry.
**Criterion:** a new player goes from `PLAY` to finishing level 1 with their voice on Linux, opens a door, hides in a hollow, dies once and returns to the sanctuary with the section reset — and nobody has explained a single control to them. Tag `v0.3-maze`.

---

# Week 4 — the hunt, the Spike, the marble (9 – 15 Nov)

**Goal:** the loop from the GDD (Survey → Wake → Escape → Reprieve) is playable and provable, and fear has a shape on screen.
**Visible change:** enemies telegraph and commit — a half-second freeze plus a snarl when a chase starts, a fading growl when it ends; doors splinter and stay splintered; a thrown marble lights up the far side of the room and something walks toward it; the arcs themselves jitter and flicker as the nearest suspicious enemy closes in.
**Work:** the Spike (sawtooth polygon collider = its signature, faster than the player, weak hearing, breaks a door in ≈3 s loudly, broken doors persist until section reset; only placed in spaces with ≥2 usable doors); the alert telegraph and the give-up cue; fear feedback as one shader parameter driven by nearest-suspicious-enemy distance + the faint "SOMETHING IS CLOSE" text; the decoy marble (right click / Q, lobbed, loud noise event at the landing point, its own arc train from that origin, ≈3 per level in a ScriptableObject); sound landmarks proper (`LandmarkEmitter`: panned 2D audio + the steady Listen-mode ripple; per-room identity, exit hum that gets slightly louder during a chase); `Level02_Corridor` completed with its loop, hollows, doors, sanctuary and the Crawler's wake trigger being a door the player must open; the geometry of `Level03_Sump` blocked out; **bot v1** — `ScriptedEchoSource` + a scripted route + fixed seeds, headless and sped up, run from the command line, proving level 2's escape route and printing survival against ping rate.
**Tests:** suspicion thresholds (0.3 / 0.6 / 1.0) as pure-function cases; the GDD level design checklist becomes an editor validator (`Validate Level`) that flags a chase space with no loop, or no hiding option within 20–30 s of travel, or a Spike room with <2 doors.
**Cut list:** Spike door-breaking (it becomes a fast Crawler with a sawtooth silhouette) → marble arc train (noise event only) → proximity warning stays text-only.
**Criterion:** in level 2 a tester hears the Crawler before seeing it, reads its two-bump signature, opens the door that wakes it, survives using the loop or the marble, and reaches the exit — and the bot finishes the same route headless with a fixed seed twice in a row, identically. Tag `v0.4-corridor`.

---

# Week 5 — the Absorber, the sump, the delivery (16 – 20 Nov)

**Goal:** the third enemy, the third level, the sound design pass, and a build the course can be handed.
**Visible change:** a patch of the arc field simply goes empty and stays empty as you approach; a noise zone halves your reach and fills the arcs with static — the first place in the game that is *safe but blind*; and the final exit is the only light anyone has seen.
**Work:** the Sound Absorber (slowest, strong hearing, cannot open doors, `Absorber` layer so rays return nothing → the gap; the arc must visibly empty out, which means the fracture mask and the gap must not be confusable — measure it); noise zones (reach ×0.5, enemy hearing ×0.3, Listen mode and memory trace masked, marked by sound only); `Level03_Sump` finished, including the passage where the marble is mandatory and the several sanctuaries; the exit light + short ending text; the full audio pass (landmark loops, footstep pan per enemy rhythm — the Crawler drags, the Spike clicks —, alert and give-up sounds, chase heartbeat, ambient bed, door and break sounds, defeat/UI clicks, the delayed echo per arc) with an AudioMixer and a pause Snapshot; quality tiers (ray count / Bloom / MSAA) behind a ScriptableObject; balancing from ≥3 playtest sessions plus bot sweeps over `ρ`, written into `TUNING.md` and the report; `QA_CHECKLIST.md` walked for all 3 levels; Linux build + zip, `v0.5-submission`, a 60-second demo recording.
**Cut list:** fewer obstacles in level 3 → Absorber as a pure hole without the occlusion pass → ending becomes text → report data drops to a single `ρ` measurement. The commitment line is 3 levels.
**Criterion:** all 3 levels start to finish on Linux with a real microphone and headphones at 60 fps; a tester mentions the Absorber's missing patch without being told to look for it; a teammate brings the project up from a fresh clone using only the README. Tag `v0.5-submission`.

---

# Week 6 — buffer (21 – 27 Nov)

Burn-down of the testers' bug list, readability and `ρ` re-tune from the last bot data, a debug level select, `Enter Play Mode Options` switched back on once every static holder really has a `ResetStaticState()`, presentation build. The only optional feature is **The Listener** (it reuses the ray pipeline for a 360° map-drawing pulse) and it starts only if weeks 2–5 are all green, behind a `bool`, never on `main`'s critical path.

## Standing obligations, every week

- The daily commit ritual ([GIT.md](GIT.md)); the week's status written into this file before the tag.
- Any new parameter goes into `EchoTuningProfile` / a level ScriptableObject and gets a row in `TUNING.md` in the same commit.
- Any decision that closes a path gets a dated entry in `DESIGN_DECISIONS.md` with its reason and its accepted cost.
- Playtest claims get a number (pixels, milliseconds, seconds, counts) or they are not recorded as verified — the week 1 lesson: a measurement with `LastQuadCount == 0` measured nothing.
- Audio arrives incrementally: placeholders the week the system lands, a full pass in week 5.

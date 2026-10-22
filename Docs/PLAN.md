# 5-week development plan (+1 week buffer)

**Start:** 19 October 2026 — **delivery:** 20 November 2026 — **buffer:** until 27 November
**Final scope:** echo core + 3 surface materials + 3 enemies + decoy marble and weak pulse + 4 levels + HUD and menus + microphone calibration + complete audio + Windows build.
**Out of scope:** [SCOPE_CUTS.md](SCOPE_CUTS.md) — read this list before adding any new feature.

This file is the **living project history**: at the end of each day the ticked items and the week's status are recorded and committed here, and at the end of each week that same commit is tagged `v0.x`. For technical decisions: [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md). For balancing parameters: [TUNING.md](TUNING.md).

## Rules that never change

1. At the end of every week `main` must **open and be playable with editor 6000.6.3f1**. If something is left half-done, it is switched off behind a `bool` in a ScriptableObject — the project is never left unrunnable.
2. Each week's success criterion is a "testable play session", not a feature count.
3. At least one compiling commit per day.
4. Never rename a `.cs` file or class outside the editor (broken references = an unrunnable scene).
5. `ProjectSettings/**` files are only edited in a separate, batched commit.

## Phase table

| Week | Dates | Output | Exit criterion (playable test) | Tag |
|---|---|---|---|---|
| 1 | 19–25 Oct | Skeleton + the first arc | With WASD you move and with Space a cyan ring is emitted that dents around the pillar, stays flat on the wall and gives a bright point at the box corner; 60fps, zero errors | `v0.1-sandbox` |
| 2 | 26 Oct–1 Nov | Arc train + materials + debug tooling | A tester names **at least 3** of {pillar, wall, box, metal, sponge} from the arcs alone — **the project's go/no-go point** | `v0.2-arc-train` |
| 3 | 2–8 Nov | Audio loop + menus + level 1 | Start from the menu, calibrate the microphone on Windows, clear level 1 with sound only (and on Linux with Space only), die once and Retry, first Windows build | `v0.3-mic-calibration` |
| 4 | 9–15 Nov | Readable enemies + decoy marble + levels 2 and 3 | In level 3, say from the arc alone whether it is a Crawler or a Spike, distract one with the marble and reach the exit | `v0.4-enemies` |
| 5 | 16–20 Nov | Absorber + level 4 + audio + delivery | All 4 levels start to finish on Windows with a real microphone at 60fps; a teammate brings the project up from a fresh clone per README | `v0.5-submission` |
| 6 | 21–27 Nov (buffer) | Bug burn-down, readability re-tune, demo recording | Final presentation build | `v0.6-final` |

## Pre-agreed cut list (if any week falls behind)

- **Week 1:** drop the fracture mask and the material lookup (raw `d` only). **Never drop:** the mesh builder, the parameters ScriptableObject, the `IEchoSource` seam.
- **Week 2:** drop sponge and land on two materials. If the readability test fails, week 3 goes entirely to readability and gameplay does not start.
- **Week 3:** drop CREDITS and the ending text; drop the voice-loudness indicator (the noise gate stays). **Calibration is never dropped.**
- **Week 4:** Spike = the Crawler with a simpler signature and behaviour; the proximity warning is text only.
- **Week 5:** in this order: fewer obstacles in level 3 ← the Absorber is only a "hole" without the occlusion pass ← the ending becomes a text screen. **The commitment line is 4 levels — polish the cut, not the levels.**

---

# Week 1 — daily checklist

The only goal: one cyan ring that collapses around pillar/wall/box using the GDD formula, at 60fps with zero errors, and a repository the team can open.
**Deliberately not built (so the scope does not slip):** arc train, fracture mask, metal/sponge materials, microphone, HUD, menus, enemies, levels.

### Day 1 — infrastructure and documents

- [x] Register the merge driver and `git lfs install --local`, verified with `git config --get-regexp merge\.`
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

- [ ] A fresh clone opens with 6000.6.3f1 and `Sandbox` Plays without errors ← these three items can only be confirmed with the editor open; headless runs have no Play
- [ ] The cyan ring dents at the pillar, is flat on the wall, has a bright point at the box corner
- [ ] Mesh construction in `EchoRenderer.AddArc`/`EndFrame` under 3ms in the Profiler; zero errors in the Console
- [x] `git lfs ls-files -n` lists the binaries (`Assets/Art/Sprites/Player_dot.png`); `merge.unityyamlmerge.driver` is registered
- [ ] Tag `v0.1-sandbox` and push with `--follow-tags`
- [ ] If the ring was not visible, debug in this order: the camera's Post Processing tick ← the `EchoTarget` layer and the ray mask ← `sortingLayer` and the material ← `_Hdr` against the Bloom `threshold`

Two things visible in the editor Console that are not errors: the "assembly has no scripts" warning for `EchoMaze.Tests.PlayMode.asmdef` (filled by the PlayMode tests in week 2) and the `EchoDirector has no EchoTuningProfile` warning, which only appears when the scene was not rebuilt and the `profile` field on `Echo System` is empty.

---

# Week 2 — arc train and the readability test

Work: the arc train (`arcInterval` / `arcsPerPing` / `A_k` with `innerDecay` / `fadeCurve`), a stable per-ping fracture mask + seeded gaps, three material ScriptableObjects, validating the "corner point", `EchoDebugWindow` (Force Ping, drawing raw rays in the Scene view, `d[i]` and `amp[i]` plots, arc freeze, timer and allocation), EditMode tests for the math and the breakup, a gallery of objects in `Sandbox`, `Docs/TUNING.md`.
Criterion: the readability go/no-go. If it fails, everything stops and the time goes to readability.

# Week 3 — audio loop, calibration, menus, level 1

Work: `MicrophoneEchoSource` + `LoudnessFollower` + `NoiseGate`, a calibration page with a live waveform, Options with PlayerPrefs, rewriting `InputSystem_Actions.inputactions` and `GameInput` (replacing `PlayerMock`), Shift for silent movement, manual echo + cooldown ring, full HUD, `GameFlow` + Pause, win/lose panels, `Boot.unity` and `Level01_QuietRoom`, **the first Windows Mono build and the first serious microphone test on Windows** (`Docs/MIC_TEST_MATRIX.md`).

# Week 4 — enemies and the decoy marble, levels 2 and 3

Work: `NoiseBoard`, the pure `HeardBy` function with a line-of-sight penalty + tests, `EnemyBrain` (Patrol/Investigate/Chase/Lost), the Crawler and Spike wave signatures as ScriptableObjects, the proximity warning, the decoy marble pinging from its own origin, the weak pulse, `Interactable/Door/Exit`, a complete `Level02_Corridor`, the geometry of `Level03_Arena`.

# Week 5 — Absorber, level 4, audio, delivery

Work: the Absorber (trigger + occlusion pass + indifferent to traps), `Level04_Finale` with a "marble is required" passage, quality tiers (ray count/Bloom/MSAA), complete sound and music with an AudioMixer and a pause Snapshot, balancing from at least three test sessions, `Docs/QA_CHECKLIST.md` for all 4 levels, Windows build + zip, tag `v0.5-submission`, recording a 60-second demo.

# Week 6 — buffer

Burn-down of the testers' bug list, readability re-tune, a debug Level Select; any optional experiment (arcs on the GPU) stays behind `#if` so `main` remains deliverable.

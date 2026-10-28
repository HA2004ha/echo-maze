# Echo — `echo-maze`

A 2D top-down stealth/horror game where the screen is completely black and you see the world through **the echo of your own sound**: you make a noise, a cone-shaped wave travels forward, hits walls and creatures, and comes back as a train of returning arcs; you have to read the shape of each arc. Every sound you make is also heard by the things in the dark — they cannot be killed, only escaped, and one touch kills. This is a project for the Game Theory course at Iran University of Science and Technology.

Three levels: an enemy-free maze that teaches the language, The Corridor, The Sump. English only, **Linux PC** with a microphone and headphones.

- Design document: [GDD.md](GDD.md) (version 3.0, at the repo root)
- 5-week plan and current status: [Docs/PLAN.md](Docs/PLAN.md)
- Balancing parameters: [Docs/TUNING.md](Docs/TUNING.md) · technical decisions: [Docs/DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md)
- Things deliberately not being built: [Docs/SCOPE_CUTS.md](Docs/SCOPE_CUTS.md)
- Seen in play, not yet fixed: [Docs/ISSUES.md](Docs/ISSUES.md)
- Working with git/LFS: [Docs/GIT.md](Docs/GIT.md) · setting up your machine: [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md)
- [`Docs/GDD.md`](Docs/GDD.md) is the superseded v1.1 design — history only, not a requirement

## Editor version

**Unity 6000.6.3f1** — exactly this version. Opening the project with a newer editor upgrades the files one way. Required modules and details in [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md).

## Opening and running

```
Unity Hub ▸ Add project from disk ▸ this folder
Assets/Scenes/Sandbox.unity ▸ Play
```

`Sandbox` is the mechanical test scene for the echo mechanic (move with WASD, aim with the mouse, ping with Space). Once week 2 lands, `Assets/Scenes/Level02_Corridor.unity` is the first real room: geometry, a patrolling Crawler, and death. The build is produced with the platform set to **Linux64**.

## Sound input and platform limits

All sound input sits behind one abstraction layer called `IEchoSource`. **`Sandbox` now runs with `sourceMode = Microphone`**: the scene opens the capture device and pings on your voice. Measured on Linux (`6000.6.3f1`, PipeWire) on 2026-10-28: `Microphone.devices` returns the real inputs, `Microphone.Start` records, and the room's noise floor sits at ~0.003 RMS against a `gateOpenRms` of 0.02. Two things the first run taught us: the ring buffer is uninitialised for its first window (so the source discards 0.25 s of capture before it trusts the level), and `GetPosition` advances in blocks, so a frame with no new audio must not be read as silence.

If a machine has no usable input, the director logs one warning and falls back to `Simulated` (a constant `simulatedLoudness` on a timer), and `ManualOnly` + Space remain available everywhere — nobody is blocked by a missing microphone. Left empty, `EchoTuningProfile.microphoneDevice` takes the first device whose name is not a `Monitor of …`, because a monitor is your own speaker output and the game would answer its own sound effects. Pasting a name into that asset is only ever a stopgap: the device is meant to be **chosen in-game** from the live list on the week-3 calibration page, saved as a preference, and never pinned in a committed file (see the 2026-10-28 entry in [Docs/DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md)). The Linux **player build** now enumerates and opens capture devices (row 2 of [Docs/MIC_TEST_MATRIX.md](Docs/MIC_TEST_MATRIX.md) — 4 devices, one opened, zero errors); what still needs a person is whether the gate opens on a real voice. Details in [Docs/DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md).

## Folder layout

```
Assets/Scenes            Sandbox (mechanic test) · Boot (menu, week 3) · Level01_Maze · Level02_Corridor · Level03_Sump
Assets/Scripts/Runtime   Core (echo) · Audio (sound input) · Gameplay · Data · UI · Debug
Assets/Scripts/Editor    Editor tooling (echo debug window, level validation, Validate Git Setup)
Assets/Tests             EditMode (arc math / readability / hearing) and PlayMode (scene validation)
Assets/Data              Balancing ScriptableObjects: echo parameters, enemies, levels
Assets/Art               Arc shader, materials, fonts
Docs                     PLAN, SCOPE_CUTS, GIT, TEAM_SETUP, TUNING, DESIGN_DECISIONS, QA and test docs (the live GDD is at the repo root)
Tools                    unity-yaml-merge.sh — the git merge driver wrapper (see Docs/TEAM_SETUP.md)
```

## Team

Hossein Askari (lead programmer — echo core, microphone input, rendering) · Mahziar Mirazimi (UI, AI, levels, testing) · Seyed Saeed Nourian (game and sound design, balancing).

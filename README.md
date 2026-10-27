# Echo — `echo-maze`

A 2D top-down stealth/horror game where the screen is completely black and you see the world through **the echo of your own sound**: you make a noise, a cone-shaped wave travels forward, hits walls and creatures, and comes back as a train of returning arcs; you have to read the shape of each arc. Every sound you make is also heard by the things in the dark — they cannot be killed, only escaped, and one touch kills. This is a project for the Game Theory course at Iran University of Science and Technology.

Three levels: an enemy-free maze that teaches the language, The Corridor, The Sump. English only, Windows PC with a microphone and headphones.

- Design document: [GDD.md](GDD.md) (version 3.0, at the repo root)
- 5-week plan and current status: [Docs/PLAN.md](Docs/PLAN.md)
- Balancing parameters: [Docs/TUNING.md](Docs/TUNING.md) · technical decisions: [Docs/DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md)
- Things deliberately not being built: [Docs/SCOPE_CUTS.md](Docs/SCOPE_CUTS.md)
- Working with git/LFS: [Docs/GIT.md](Docs/GIT.md) · setting up your machine: [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md)
- [`Docs/GDD.md`](Docs/GDD.md) is the superseded v1.1 design — history only, not a requirement

## Editor version

**Unity 6000.6.3f1** — exactly this version. Opening the project with a newer editor upgrades the files one way. Required modules and details in [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md).

## Opening and running

```
Unity Hub ▸ Add project from disk ▸ this folder
Assets/Scenes/Sandbox.unity ▸ Play
```

`Sandbox` is the mechanical test scene for the echo mechanic (move with WASD, aim with the mouse, ping with Space). Once week 2 lands, `Assets/Scenes/Level02_Corridor.unity` is the first real room: geometry, a patrolling Crawler, and death. The Windows build is produced with the platform set to **Windows ▸ Mono**.

## Sound input and platform limits

All sound input sits behind one abstraction layer called `IEchoSource`. **Right now** the `Sandbox` scene runs with `sourceMode = ManualOnly`: no sound source is created at all, and `Space` is the only way to ping. Set `sourceMode` to `Simulated` if you want to see the automatic loudness gate without a microphone — it then reads a constant level from `simulatedLoudness`. A real microphone is a **week 2** deliverable: `MicrophoneEchoSource` (the `Microphone.Start` path with the noise gate, one ping per vocal event) is measured per machine with `Tools ▸ Echo ▸ Mic Probe`. `Microphone` support in the Unity editor on Linux has never been verified here, so the microphone is developed and tested on Windows, and the Linux path keeps `Simulated` / `ManualOnly` as its fallback — `Space` must stay a complete way to play. The matrix is in [Docs/MIC_TEST_MATRIX.md](Docs/MIC_TEST_MATRIX.md). Details in [Docs/DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md).

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

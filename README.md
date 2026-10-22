# Echo — `echo-maze`

A 2D top-down stealth/horror game where the screen is completely black and you see the world through **the echo of your own sound**: you make a noise, a cone-shaped wave travels forward, hits walls and creatures, and comes back as a train of returning arcs; you have to read the shape of each arc. This is a project for the Game Theory course at Iran University of Science and Technology.

- Design document: [Docs/GDD.md](Docs/GDD.md)
- 5-week plan and current status: [Docs/PLAN.md](Docs/PLAN.md)
- Things deliberately not being built: [Docs/SCOPE_CUTS.md](Docs/SCOPE_CUTS.md)
- Working with git/LFS: [Docs/GIT.md](Docs/GIT.md) · setting up your machine: [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md)

## Editor version

**Unity 6000.6.3f1** — exactly this version. Opening the project with a newer editor upgrades the files one way. Required modules and details in [Docs/TEAM_SETUP.md](Docs/TEAM_SETUP.md).

## Opening and running

```
Unity Hub ▸ Add project from disk ▸ this folder
Assets/Scenes/Sandbox.unity ▸ Play
```

`Sandbox` is the mechanical test scene for the echo mechanic (move with WASD, aim with the mouse, ping with Space). The Windows build is produced with the platform set to **Windows ▸ Mono**.

## Sound input and platform limits

All sound input sits behind one abstraction layer called `IEchoSource`. **Right now** the `Sandbox` scene runs with `sourceMode = ManualOnly`: no sound source is created at all, and `Space` is the only way to ping. Set `sourceMode` to `Simulated` if you want to see the automatic loudness gate without a microphone — it then reads a constant level from `simulatedLoudness`. A real microphone is the target on both Windows and Linux, but `Microphone` support in the Unity editor on Linux has never been verified here; that path is measured with a Windows build according to [Docs/MIC_TEST_MATRIX.md](Docs/MIC_TEST_MATRIX.md). Details in [Docs/DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md).

## Folder layout

```
Assets/Scenes       Scenes: Sandbox (mechanic test), Boot (menu), Level01..Level04
Assets/Scripts/Runtime   Core (echo) · Audio (sound input) · Gameplay · Data · UI · Debug
Assets/Scripts/Editor    Editor tooling (echo debug window, level validation, Validate Git Setup)
Assets/Tests             EditMode (arc math / readability) and PlayMode (scene validation)
Assets/Data              Balancing ScriptableObjects: echo parameters, surfaces, enemies
Assets/Art               Arc shader, materials, fonts
Docs                     GDD, PLAN, SCOPE_CUTS, GIT, TEAM_SETUP, TUNING, DESIGN_DECISIONS, test docs
Tools                    unity-yaml-merge.sh — the git merge driver wrapper (see Docs/TEAM_SETUP.md)
```

## Team

Hossein Askari (lead programmer — echo core, microphone input, rendering) · Mahziar Mirazimi (UI, AI, levels, testing) · Seyed Saeed Nourian (game and sound design, balancing).

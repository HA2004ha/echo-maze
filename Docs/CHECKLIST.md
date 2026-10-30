# Checklist — what is left (2026-10-30)

Short working list derived from [PLAN.md](PLAN.md), [STATUS.md](STATUS.md) and [../GDD.md](../GDD.md).
Tick items here as they land; the reasoning stays in PLAN.md.

## Levels today

- **Level 1 — The Maze** (`Level01_Maze`, in the run). 21 × 15 cells at 2 m, built from
  `Assets/Data/Levels/Level01_Maze.txt`. Spawn bottom-left, exit top-right, one closed door in the middle
  band (the only way through), a sanctuary checkpoint just before it, a 6 m glass run on the spawn row, one
  pillar and one box corner. No enemies.
- **Level 2 — The Corridor** (`Level02_Corridor`, editor only, not in the run). A 7 × 5 m blockout room:
  walls, a pillar, a box corner, two alcoves (both hollows), an exit tile, and one Crawler on a vertical
  patrol using the week-2 placeholder hearing.
- **Level 3 — The Sump**: not built.
- `Sandbox`: echo test scene, not a level.

## 0. Platform

- [ ] Install **Windows Build Support (Mono)** for editor 6000.6.3f1 in Unity Hub
- [ ] `PlayerBuild`: add a Windows x86_64 target next to Linux
- [ ] Build both players and smoke-test the Windows build (Wine or a friend's PC)
- [ ] Windows microphone rows in `MIC_TEST_MATRIX.md` (device names, monitor rule, gate on voice)

## 1. M2 — the hunt (next)

- [x] Crawler roars every 10–15 s: warm ripple (not a ping, not a noise) + 3D roar audio, listener on the Player (CC0 monster roar from OpenGameArt)
- [x] Crawler gaits: patrol 1.0 m/s with one giant stomp every 1.5 s (CC0 "Mech Stomp" from OpenGameArt, pitched down), hunt 2.0 m/s stepping every 0.75 s, always slower than the player (`OnValidate` clamps chase to 0.95 × walk)
- [x] Respawn fix: dying before the first sanctuary returns to the spawn, not the death point (`LevelSession.Checkpoint`, ISSUES #9, `RespawnTests`)
- [x] Death snarl: `Death_snarl.wav` (CC0, from the monster clips, pitched down) played flat at the moment of death (`DeathSnarl`, `deathSoundVolume`)
- [x] First safe spot: `Hollow` (quiet sounds ×0.15, loud ones and a chasing Crawler ignore it) with "You are hidden" / "You are exposed" on the HUD; two hollows in `Level02_Corridor` (the spawn alcove and a north-west pocket). Stub hearing only — the real rule moves into `HeardBy`
- [x] Level 1 on-screen hints (`LevelHints` / `HintQueue` / `HintTexts`): wave, F pulse, wall touch, Shift, doors; once each, queued, Level 1 only
- [ ] `NoiseBoard` + pure `HeardBy` (distance, loudness, walls, closed doors, noise zones, hollows) + tests
- [ ] Route every sound through it: ping, weak pulse, footsteps, wall bump, glass, doors
- [ ] `EnemyBrain`: suspicion meter, Dormant / Patrol / Curious / Investigate / Chase / Lost, seeded pauses
- [ ] Alert telegraph (0.5 s freeze + sound) and give-up cue
- [ ] Crawler on `EnemyBrain`; delete the week-2 hearing stub
- [ ] Listen mode (hold Ctrl): warm enemy ripples, cool landmark ripples, slow movement
- [ ] Hollows: hearing × 0.15 except against a chaser; pinging inside breaks cover
- [ ] Sanctuary: enemies stop and give up at the threshold
- [ ] Memory trace: wall hits stay faint for 10–15 s, walls only
- [x] Wall memory: touched walls stay as permanent thin neon lines from the player's memory, never re-read from colliders (echo hits do not feed it yet) (2026-10-30)
- [x] Footprint trail that vanishes after 4 s (ring buffer, own mesh) (2026-10-30)
- [ ] Full section reset on death (doors, enemies, marbles)
- [ ] Calibration silence (≈5 s) at the start of level 1
- [ ] Level 2 finished: loop, hollow, doors, wake trigger door, sanctuary; add to `GameFlow.chapterScenes`

## 2. Week 4 — Spike, marble, fear

- [ ] Spike: sawtooth collider, faster than the player, breaks doors in ≈3 s, broken doors stay broken
- [ ] Decoy marble (right click / Q), ≈3 per level, loud noise at the landing point
- [ ] Fear feedback: arcs jitter near a suspicious enemy + "SOMETHING IS CLOSE"
- [ ] Sound landmarks (`LandmarkEmitter`) per room + exit hum
- [ ] `Validate Level` editor check (loop, hiding spot within 20–30 s, 2 doors if a Spike)
- [ ] Bot v1: scripted route, fixed seed, headless, proves level 2's escape route

## 3. Week 5 — Absorber, level 3, delivery

- [ ] Sound Absorber: rays return nothing, slow, strong hearing
- [ ] Noise zones: reach × 0.5, hearing × 0.3, Listen mode and trace masked
- [ ] Level 3 — The Sump: Crawler, Spike, Absorber, mandatory marble section, several sanctuaries, exit light
- [ ] Full audio pass (landmarks, enemy footsteps, alert/give-up, heartbeat, doors, UI, echo sound)
- [ ] Bot sweeps over ρ for the report
- [ ] Credits screen, short ending
- [ ] Linux + Windows builds zipped, tag `v0.5-submission`, 60 s demo video

## 4. Needs people, not code

- [ ] Readability session: 3 new players name 3 of {wall, pillar, corner, hollow, Crawler} from arcs alone
- [ ] Mic voice tests: whisper vs shout, latency, noisy room (matrix rows 3, 5–9)
- [ ] Decide `ISSUES.md` #4: art wider than hitbox (rescale art / grow hitbox / accept)
- [ ] Fresh-clone check by a teammate using only the README

# Game Design Document (GDD)

**Project name:** Echo — **code name:** `echo-maze`
**Document version:** 3.0 (complete rewrite; supersedes 1.0, 1.1 and 2.0)
**Engine:** Unity 2D (pinned version: `6000.6.3f1` — see [TEAM_SETUP.md](Docs/TEAM_SETUP.md))
**Target platform:** Windows and Linux (PC) with a microphone and headphones *(amended 2026-10-30: Windows is a delivery target again, next to Linux — see the 2026-10-30 entry in [Docs/DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md))* *(earlier amendment 2026-10-28: the team has no Windows machine, and the microphone was measured working in the Linux editor — see [Docs/MIC_TEST_MATRIX.md](Docs/MIC_TEST_MATRIX.md) row 1)*
**Language:** English only

> This file is the open-source version of the document the team wrote initially. The reference images of the original document are not in the repo and have been dropped. Technical decisions are in [DESIGN_DECISIONS.md](Docs/DESIGN_DECISIONS.md), balancing parameters in [TUNING.md](Docs/TUNING.md), the schedule in [PLAN.md](Docs/PLAN.md), and the list of things deliberately not built in [SCOPE_CUTS.md](Docs/SCOPE_CUTS.md).
>
> **What changed from 1.1:** surface materials were removed; the game is built around one trade-off and one loop (explore, hide, exploit); enemies cannot be killed, only escaped; enemy hearing is a suspicion meter; the world has noise, doors, hollows and sanctuary rooms; the health bar was replaced by one-hit death with checkpoints; the game is 3 levels with an enemy-free maze as the tutorial; and the document now contains the game theory framing and the verification plan.

---

## The idea in one sentence

Everything is dark and the screen is completely black. You make a sound, your sound becomes a wave that spreads in front of you, hits walls and bad creatures and comes back. From the shape of the returning wave you figure out what is there. But every sound you make, the creatures of the dark hear too.

## Design pillars

1. **Every sound is a trade.** A sound shows you the world and shows you to the world. A feature that does not deepen this trade is decoration.
2. **What you learned is what saves you.** Explore, then hide, then exploit. The player's memory of the level is the real resource; there is no map.
3. **Silence is a move, not a pause.** Not pinging must be an active, informative choice (see Listen mode).
4. **Readable, not realistic.** The arcs are a visual language. Clarity beats physical accuracy every time.
5. **Fear comes from absence and uncertainty.** The Absorber's empty gap, an arc that trembles, a footstep ripple that suddenly stops. No jump scares.
6. **Few things, done deep.** Prefer one more behaviour for an existing enemy over a new enemy type.
7. **Escape, never fight.** The player is unarmed and enemies cannot be killed. Every chase must have an answer the player can find.

## The loop of every level

Each area of a level follows the same four beats. This is the structure the level designer works from.

1. **Survey.** The area is calm; its enemy is dormant or far away. The player finds the landmarks, the loop, the doors, the hollows and the noisy floors.
2. **Wake.** Something wakes the enemy: a door the player must open, a loud floor, a sound the player chose to make. It is always the player's choice or the player's fault, never a surprise from nowhere.
3. **Escape.** The player uses what they learned: close a door, reach a hollow, run the loop, throw the marble, cross a noise zone.
4. **Reprieve.** A sanctuary room. The next area repeats the pattern with a twist: a new enemy or a new tool.

Three things make this loop real:

- **Patrol routes are fixed, with random pauses.** In Listen mode the player can learn a route and its timing, so scouting pays off and a plan can be specific.
- **Levels are handcrafted, never random.** Dying and retrying keeps what the player learned, so a retry is a smarter attempt, not a repeat.
- **Tool to problem.** Every level introduces a problem and makes sure one of these tools answers it:

| Tool | What it solves |
|---|---|
| Closing a door | Blocks hearing and blocks the Crawler and the Absorber; delays the Spike |
| Hollow | Hides you from every enemy except one that is already chasing you |
| Sanctuary room | A guaranteed safe place and a checkpoint |
| Decoy marble | Lures an enemy away from your path |
| Noise zone | A safe but blind passage |
| Loop | Somewhere to run in circles |
| Sound landmark | Knowing where you are while fleeing |
| Memory trace | Running along what you just saw |

## Project goal

This game is our Game Theory course project. Our main goal is to make a stealth and horror game whose core mechanic is "seeing by hearing". The player never sees the environment — they only see the shape of the returning wave and must understand from it whether there is a wall, a pillar or an enemy in front of them.

Our second goal is for the game to be simple and minimal so it finishes within one term, while the idea is distinctive enough to keep working on and publish later.

### Why this idea?

- Low production cost: no 3D models and no textures needed.
- Not tied to a language, so it can be released internationally.
- Great for streaming and reaction videos, because the player's own voice is part of the game.
- A learning opportunity for us: microphone input, raycasting and shader writing.
- It is a clean, playable example of the theory we study.

### Audience

Ages 15 to 35, no gender restriction. People who like horror, stealth and experimental games. Streamers and content creators.

## Game theory framing

| Concept | Where it lives in Echo |
|---|---|
| **Costly signalling** (information vs exposure) | Every ping buys information and pays with detection risk. |
| **Hider–seeker (search) game** | Enemies investigate the *last place a sound was heard*, not where the player is now. This produces the core rhythm: **ping, then move**. |
| **Mixed strategies / unpredictability** | A player who pings from the same spot at the same interval gets caught. Enemy patrols include randomised pauses, so they cannot be memorised to the second either. |
| **Deception / bluffing** | The decoy marble (and, later, the Mimic). |
| **Asymmetric, imperfect information** | The player sees only arcs; the enemy knows only loudness and distance. Neither knows the other's exact position. |
| **Exploiting a predictable opponent** | Enemy hearing is deterministic, so a prepared player can lure, delay and hide. The level is the payoff matrix. |
| **Payoff tuning knob** | `ρ = enemy hearing radius / visible echo reach` at the same loudness. If ρ is too high, pinging is never worth it; if too low, there is no tension. |

**Results for the report** come from two sources: playtest metrics (pings per minute, time between pings, detections, how often the player moves right after a ping) and the bot simulation (see Verification and simulation), which gives survival rate against ping rate and against ρ.

## Team and responsibilities

| Name | Role | Duties |
|---|---|---|
| Hossein Askari | Programmer 1 | The game core: microphone input, conical raycast, wave and reflection system |
| Mahziar Mirazimi | Programmer 2 | UI, enemy AI, levels and menus |
| Seyed Saeed Nourian | Game and sound designer | Level design, enemies, balancing, the sound palette (including every landmark sound) and the feeling of fear |

> In practice for this term, **programmer 1 writes all the code** and the other two do level design, balancing, sound collection and testing. The documentation and parameters are shaped so they can be changed without touching code.

## Project boundaries: what we build now, what comes later

| Area | Now (this term's delivery version) | Stretch | Later / cut |
|---|---|---|---|
| Core mechanic | Microphone input, cone wave, returning arcs, charged manual echo, brightness from geometry, touch marks, 360° weak pulse | — | Loudness-driven cone width is a tuning option only |
| Silence | Listen mode, Shift silent movement, footstep noise | — | — |
| Hiding and safety | Hollows, sanctuary rooms with checkpoints, doors that hold | — | — |
| Orientation | Sound landmarks, short memory trace, exit hum | — | — |
| Enemies | Crawler, Spike, Sound Absorber, suspicion meter, alert and give-up cues | The Listener | Mimic, Spider, Frequency Hunters |
| Tools | Decoy marble | — | Ultrasonic whistle, recorder |
| World | Noisy floors, noise zones, dripping pipe | — | Resonant switch, frequency doors, echo-note corridors, echo halls |
| Levels | 3 levels + a conventional main menu | — | More levels |
| Modes | Story | — | Blind mode, Whisper mode, Ghost Echoes, co-op, survival |
| Setup | Calibration inside the dark, manual calibration screen | — | — |
| Verification | Bot simulation using the scripted source | — | — |
| Rest | Minimal HUD, pause, instant retry, fear feedback | — | Streamer mode, Android, Bestiary, post-mortem map |

**Surface materials (metal, sponge, glass) are not in the game.** Everything is "ordinary", and surfaces look different only through geometry.

## Core mechanic: how the wave works

This is the most important part of the game and everything is built on it.

1. **The player is silent, nothing is visible.** The screen is black. The player emits no light. Walls, pillars and enemies are all there but invisible. Only a faint line of text: "Make a sound to listen".
2. **The player makes a sound, the outgoing wave travels.** The microphone captures the sound and a cone-shaped wave (not a full circle) spreads in the direction the player is looking. This outgoing wave is visible.
3. **The wave hits something and returns.** The returning wave comes back as a set of nested arcs (from large and new to small and old) toward the player. The shape of each arc is built from the shape of the surface it hit:
   - Round surface (pillar): a soft, smooth arc
   - Flat surface (wall): a flat, elongated arc
   - Corner (box): one bright point
   - Sharp or serrated surface (Spike): a serrated arc
   - **Important:** the player never sees the complete shape of an object. The arcs are broken and only the part of the surface facing the player is read. The returning wave must feel like it is coming back, i.e. moving from the object toward the player, not the other way round.
4. **The player reads the shape.** Every object and every enemy has its own "wave signature".

### Loudness is a dial, not a switch

The microphone's loudness `L` (0 to 1, after the noise gate) sets reach and ray density. The same loudness sets how far enemies can hear you, and the cooldown grows with it, so shouting continuously is never free.

*Tuning option:* the cone half-angle can also follow loudness (about 20° for a whisper to about 45° for a shout, with 32° at mid loudness). If playtests show it does not help, it stays a constant 32°.

### Brightness comes from geometry

Brightness of an arc sample follows how sharply the hit distance changes between neighbouring rays: roughly `b(i) = base + k · clamp(|d[i+1] − 2·d[i] + d[i−1]| / dCurv)`. A flat wall has almost no change and reads dim and even. A pillar is a smooth curve. A corner is a sudden jump and becomes a bright point. No per-surface data is needed.

### The returning arc formula

For a ray at angle θ whose hit distance is `d(θ)`, arc `k` is drawn as:

```
r_k(θ) = R_k + A_k * (d(θ) - d_min)
```

- `R_k`: the arc's base radius, which decreases over time — the arc travels toward the player.
- `A_k`: the deformation amplitude, reduced for the inner (older) arcs, i.e. they fade out.
- `d_min`: the closest hit distance in that ping.

So wherever an object is nearer to the player, the arc reaches that point sooner and closer to the player, and the arc dents toward the player (a U shape). With this sign the arc genuinely feels like it is returning. **Version 1.0 of the document had the sign reversed, and the arcs looked like outgoing waves.**

A new arc is created at each fixed time interval after the sound (a train of nested arcs). Breakup: wherever the difference in hit distance between two adjacent rays exceeds a limit, the line breaks. On a flat surface we also drop a few random distances so it always looks incomplete. Because the shape comes from real distances, every object at every position yields a different profile, so there is no need to force symmetry by hand.

### Living things shimmer

Arcs that come from creatures pulse faintly at a heartbeat rate. Walls and pillars stay perfectly steady. At a glance, "alive or not" is readable, and it is unsettling.

### Fear feedback

There is no breath bar. The arcs jitter and flicker more as the nearest suspicious enemy gets closer. The faint HUD text "SOMETHING IS CLOSE" appears. It is one shader parameter and no extra UI.

## Feeling the walls without seeing them

Bumping into things in the dark must not feel bad, and the player must not be forced to ping for every step. Two small tools solve this without giving free light:

- **Touch marks.** When the player touches a wall, a tiny dim segment of that wall flashes at the contact point and fades in about half a second. A bump also makes a very small noise, so careless walking has a cost.
- **360° weak pulse.** A small all-around ring (about 1.5 m reach) with a short cooldown and very low noise. It is the "look around me a bit" tool: a little information for a little risk.

There is deliberately **no permanent light around the player**: free information at close range would break the core trade and would show the edges of the Absorber's gap.

## Noise, hearing and suspicion

### Noise is one system

Everything that makes sound produces a **noise event** with a position and an intensity, and every enemy listens to the same stream:

| Source | Intensity |
|---|---|
| Your ping | `L` (the loudness) |
| Weak pulse | very low |
| Walking normally | small, short range |
| Walking with Shift, or standing still | none |
| Touching a wall | very low |
| Stepping on a noisy floor (glass, gravel, puddles) without Shift | medium |
| Closing a door / opening a door | medium / low |
| A Spike breaking a door | high |
| Decoy marble landing | high |

### Enemy hearing and suspicion

An enemy hears a noise event if the event lies within its hearing radius `H = ρ · R(L) · enemyHearing`, reduced by walls and closed doors between them and by noise zones. Hearing does not switch an enemy straight to "chase". Each enemy has a **suspicion meter** that fills from noise and drains over time:

| Suspicion | State | Behaviour |
|---|---|---|
| None | Dormant | Standing or sleeping. Ignores everything except a strong noise or its designed wake trigger. Makes no footstep ripples. |
| Low | Patrol | Walks its fixed route with randomised pauses. |
| About 0.3 | Curious | Stops and turns toward the sound. |
| About 0.6 | Investigate | Walks to **where the sound was heard**, not where you are now. |
| 1.0 | Chase | Runs at you. |
| — | Lost | After a few seconds without hearing anything it searches the last known point, then returns to Patrol. |

Enemies are **permanent**: with no killing, they never leave. Patrols give each enemy a territory, and Lost always returns to Patrol. A quiet ping barely moves the meter; sustained shouting fills it fast. This makes enemy behaviour readable and forgives microphone noise: a stray spike does not cause a chase.

### Alert and give-up cues

With one-hit death the player must always know when the hunt starts and when it ends:

- **Alert telegraph.** When an enemy enters Chase it freezes for about half a second and makes a distinct sound. The player gets a moment to react and knows exactly when it began.
- **Give-up cue.** When an enemy loses the player, its growl fades and its footsteps change. The player knows it is safe to move.

### Listen mode (silence is a move)

Hold Ctrl: you move slowly and make no ping, so you cannot see walls. But within a short radius you perceive the world's sounds as faint ripples:

- **Enemy ripples:** dim and warm-coloured (a different tone from your cyan arcs, so "their sound" never looks like "your echo"). A ripple shows only a position and a rough direction. **If an enemy stands still, its ripple disappears** — which is exactly when you should be most afraid. A ripple pulses faster and brighter as that enemy's suspicion rises, so the player reads its state with no UI.
- **Landmark ripples:** steady, non-moving ripples in a third, cool grey tone, for stationary sound sources (see Sound landmarks).

Listen mode does not use the microphone, and it is masked inside noise zones. It is also how the player learns patrol routes and timing.

## Escape and safety

### Hollows

Small alcoves. Inside a hollow you become nearly unhearable to every enemy **except one that is already in Chase**, which follows you in. So the real puzzle is "break the chase first, then hide". **Pinging inside a hollow breaks your cover** (it makes a noise event as usual). Listen mode works inside. Hollows are the frequent, risky tool.

### Sanctuary rooms (checkpoints)

A small soundproof room with a door. Entering one saves a **checkpoint**. Enemies stop at its threshold and give up, and the player can ping freely inside. Dying returns the player to the last sanctuary and **resets that section's state**, including broken doors. Sanctuaries are the rare, safe tool, and they mark the Reprieve beat of each area.

### Doors that hold

The player opens and closes doors with E. A closed door blocks enemy hearing, blocks the player's rays (it is a wall) and stops enemies physically:

| Enemy | Behaviour at a closed door |
|---|---|
| Crawler | Cannot open doors at all |
| Absorber | Cannot open doors at all |
| Spike | Breaks a door in about 3 seconds, loudly. **A broken door stays broken until the section is reset**, so doors are a limited resource the player spends on a route |

### Noisy floors and noise zones

- **Noisy floors:** glass shards, gravel and puddles placed by the level designer. Stepping on them without Shift makes a noise event.
- **Noise zones:** areas with loud ambience (a waterfall, machinery). Inside a zone your ping reach is halved and the arcs get static, enemies hear you much less (a hiding place), Listen mode and the memory trace are masked. The result is a place that is **safe but blind**. Zones are marked by sound only.

### Sound landmarks

Every room or junction has its own constant sound: a ticking clock, a dripping pipe, a humming generator, a creaking fan. The player builds a map in their ears ("the generator is on the left, the clock is the corridor to the exit"). The sounds are panned in stereo, are kept quiet, and are collected by the sound designer. In Listen mode each stationary source also shows as a steady ripple, so landmarks still work without good headphones. The **exit hum** is a landmark too: panned, and slightly louder while the player is being chased.

### Memory trace

After each ping, the wall points that were hit stay visible as a very faint trace for about 10 to 15 seconds, far fainter and longer-lived than the arcs. The player can run along what they just saw. It shows **walls only, never enemies**, and is suppressed in noise zones. It must stay faint, or it weakens both the maze and the fear.

*Note: the permanent touch memory (2026-10-30) is separate from this 10–15 s ping trace.*

### The escape rules (level design contract)

1. **Every area where a chase can happen has a loop** the player can circle around, so there is always somewhere to run.
2. **A hollow, a door, or a sanctuary room appears at roughly every 20 to 30 seconds of travel** at walking speed (about 50 to 75 m at the starting speed).
3. **The Spike is introduced only in spaces with doors** (at least two usable doors in any area that contains one).

## Characters

**Player:** a small, dim creature seen from above. It gives off no light and is nearly invisible. **One hit kills.** It has no attack.

### Enemies (now)

| Name | Wave signature | Speed | Hearing | Behaviour |
|---|---|---|---|---|
| Crawler | Two round bumps with a gap between them | Slower than the player | Medium | Can be outrun. Cannot open doors |
| Spike | Serrated, like a saw | Faster than the player | Weak | Cannot be outrun; can only be escaped with a door, a hollow, a sanctuary or the marble. Breaks doors |
| Sound Absorber | No wave returns at all. A black empty patch in the middle of the arcs | Slowest | Strong | Swallows sound. When you get close the arcs suddenly empty out, and that is the scary part. Cannot open doors |

Enemies are introduced in three steps: you **hear** it first (footsteps in the dark), then you see its **arc**, and only then do you learn its **signature**.

### Enemies and set pieces (stretch / later)

- **The Listener (stretch finale):** a large creature that pings the world itself. Every few seconds it emits a 360° pulse that draws the whole map for about 1 second; anyone in its line of sight is seen. A low rumble warns 1.5 s before. The player must be behind cover on each pulse, then move. It cannot be killed. It reuses the ray pipeline.
- **Later:** Mimic (plays back the player's recorded sound), Spider, and Frequency Hunters (low-sound and high-sound hunters using a coarse two-band split, not note detection).

### Obstacles and objects

Pillar: soft round arc — Box: straight line with a bright point at the corners — Wall: straight, long line, but broken — Door: two line fragments with a gap between them (closed: a wall).

### Tools

**Now:** the decoy marble, which you throw and which makes a loud noise event where it lands, drawing the enemy there. The number of marbles is limited per level (about 3, tuned in playtests) so each throw is a decision.
**Later:** ultrasonic whistle, recorder.

## Controls

| Input | Action |
|---|---|
| Microphone | Creating an echo. Loudness sets reach and ray density |
| W A S D or arrows | Movement (makes a small noise) |
| Mouse | Aim direction (the direction of the echo cone) |
| Shift | Slow, silent movement |
| Hold Ctrl | Listen mode |
| Space | Manual echo: hold to charge loudness, release to ping |
| F (key to be confirmed) | 360° weak pulse |
| E | Open or close a door |
| Right click or Q | Throw the decoy marble |
| Esc | Pause menu |

There is no run. The manual echo is a real alternative to the microphone, not a downgrade: its maximum loudness is a little lower and its cooldown a little longer. It also makes the game playable in a noisy room.

## Win and lose rules

- Reaching the level exit means winning.
- An enemy touching the player means losing. There is no health bar: **one hit kills**. Lose screen: black with "YOU WERE HEARD", then an instant retry (under 2 seconds) from the last sanctuary with that section's state reset.

## Levels (now)

| Level | Content | Difficulty |
|---|---|---|
| 1. The Maze (tutorial) | A maze with **no enemies**; the goal is to find the way out. Opens with the calibration silence (see Setup). Dead ends, loops, pillars, boxes and a door teach echo and shape reading: a dead end, a corner and an open corridor each look different. Teaches touch marks, the weak pulse and Listen mode (the dripping pipe is a landmark with a steady ripple: "a sound is not always an enemy"). Glass shards teach noisy floors. One hollow and one sanctuary appear so the player knows their shape. Each room has its own landmark sound, and the exit hums. Near the exit, an **audio-only growl** in the distance: nothing appears, but the player learns that the world has something in it | Easy |
| 2. The Corridor | A Crawler. Introduces the full loop: Survey, Wake (a door the player must open), Escape, Reprieve. The player finds the marble. Doors, a loop, hollows, a sanctuary. Listen mode is needed to hear the Crawler behind a door | Easy to medium |
| 3. The Sump (finale) | Crawler, Spike (only in door spaces), and the Absorber. A noise zone as a safe but blind passage, and a section where the **marble is mandatory**. Several sanctuaries. The exit has the only light in the game. Short ending | Hard |

Playtime: roughly 15 to 20 minutes. If The Listener is built, it patrols the final hall after the exit opens.

### Level design checklist

Every chase space must pass all of these before it is considered done:

- [ ] It has a loop.
- [ ] A hollow, a door, or a sanctuary is within about 20 to 30 seconds of travel from anywhere in it.
- [ ] If it contains a Spike, it has at least two usable doors.
- [ ] Its wake trigger is a deliberate player choice or fault.
- [ ] Every room has a distinct landmark sound.
- [ ] At least one tool from the tool-to-problem table answers its problem.
- [ ] The bot has a scripted escape route that completes it.

## Modes

**Story (now):** 3 levels in sequence. Everything else (Blind mode, Whisper mode, survival, co-op) is not planned for this term.

## Story (short)

A researcher wakes up in the basement of an abandoned facility. The power is out and nothing is visible. They can only hear their own voice. The place is full of creatures that react to sound. They must find the exit without being detected. At the end it emerges that the facility was built to study these very "echoes".

## Setup: calibration inside the dark

Level 1 opens with about five seconds of enforced silence ("the room breathes"). During that time the game measures the ambient noise floor and sets the noise gate automatically (noise floor plus a margin). It reads as atmosphere and removes a setup step for most players. The ambient landmark loops play while it measures, so the gate accounts for speaker bleed. The manual calibration screen stays available from the main menu and the pause menu.

## Look and mood

2D, top-down view, camera locked to the player. Pure black background, thin bright lines in cyan (about `#7fe9f0`) with a Bloom glow. Enemy sounds (footstep ripples) use a dim warm tone, about `#ffb56b`; stationary landmark ripples use a cool grey, about `#8aa0b8`. The HUD uses the same thin lines but dimmer than the waves, so it never becomes a visible light source itself. A monospace font such as JetBrains Mono or Share Tech Mono. The one deliberate break in the darkness is the light at the final exit.

## Screens and menus

### In-game HUD

Kept to a minimum. Bottom right: ECHO COOLDOWN ring — bottom centre: the text "Make a sound to listen" (fades after the first ping) — top centre (faint): the proximity warning "SOMETHING IS CLOSE" — top right: pause. The voice level meter and sound range are shown in the calibration screen only.

### Menus

- **Main menu:** a conventional menu: PLAY, CALIBRATE MIC, CREDITS, QUIT. The background is an echo arc drifting slowly.
- **Pause:** RESUME, CALIBRATE MIC, QUIT and a TIP box.
- **Microphone calibration:** live waveform, sensitivity and noise-threshold sliders, TEST button. This is the only settings screen.
- **Victory:** a short text.
- **Defeat:** black screen with "YOU WERE HEARD" and instant retry.
- **Credits.**

## Technical section (Unity)

| Part | How |
|---|---|
| Microphone input | `Microphone.Start` and reading samples with `GetData`. Loudness computed as RMS, then noise filter, sensitivity threshold and calibration. Behind an abstract `IEchoSource` |
| Conical raycast | With `Physics2D.Raycast`. When sound passes the threshold, 100 to 200 rays are fired inside a cone in the mouse direction. The rays are invisible; only each hit distance is stored |
| Outgoing wave | A bright arc that spreads from the player at constant speed up to the sound's reach |
| Reflected wave | Every object has a Collider. The returning arc is built from the ray hit distances, with brightness from the curvature of the distance profile |
| Drawing the arcs | A fixed-capacity Mesh and one hand-written shader (per-sample thickness, colour and break) |
| Noise | A `NoiseBoard`: every sound is an event with position and intensity. A pure `HeardBy` function computes hearing (distance, loudness, walls, closed doors, noise zones, hollows) and is unit-tested |
| AI | `EnemyBrain`: suspicion meter plus Dormant / Patrol / Curious / Investigate / Chase / Lost states, with alert and give-up cues |
| Enemy signatures | Mostly **collider shapes**: Crawler is two circle colliders with a gap, Spike is a sawtooth polygon collider. The existing ray pipeline draws them with no special code |
| Sound Absorber | A collider on the `Absorber` layer: rays that hit it return no reflection, so the arc keeps an empty gap there |
| Listen mode | Enemies emit a ring at their position while moving; landmark emitters emit steady rings; both drawn only inside the listen radius and hidden in noise zones |
| Doors | A door has three states: Open, Closed, Broken. Enemy logic reads the state; the Spike's break timer is the only way to Broken |
| Hollows and noise zones | Trigger volumes with multipliers for enemy hearing (the hollow ignores them for an enemy in Chase) |
| Sanctuaries | A trigger volume that saves a checkpoint (player position plus the state of that section) and tells enemies to give up |
| Memory trace | A fixed-capacity buffer of recent wall hit points with a long fade |
| Landmarks | A `LandmarkEmitter` component: an `AudioSource` with a stereo-panned 2D position, plus a steady ring for Listen mode |
| Calibration | A silence-measurement step before level 1, writing the gate into the same settings the manual page uses |

### Verification and simulation

The `ScriptedEchoSource` can drive a **bot** that pings at a chosen rate and follows a scripted route, while enemies run normally. Runs are headless, sped up, and use fixed random seeds so they repeat. The bot serves two purposes:

1. **Level validation:** every chase space has a scripted escape route; the bot proves it can complete it.
2. **Balancing and report data:** hundreds of runs give survival against ping rate and against ρ, which feeds the tuning and the game theory section of the report.

### Starting values (all live in the `EchoTuningProfile` ScriptableObject)

| Parameter | Starting value |
|---|---|
| Walk speed / Shift speed / Listen speed | about 2.5 m/s / 1.0 m/s / 1.0 m/s |
| Enemy speeds | Crawler about 0.4× walk on patrol and 0.8× on a hunt (amended 2026-10-30; was 0.65×), Spike about 1.3× walk, Absorber about 0.4× walk |
| Reach `R(L)` | from about 2 m (whisper) to 6.5 m (shout) |
| Cone half-angle | 32° (optionally 20° to 45° with loudness) |
| Rays per ping | 100 to 200 |
| Cooldown | about 0.6 s + 1.4 s × L |
| `ρ` (hearing radius over reach) | 0.8 |
| Suspicion thresholds | 0.3 / 0.6 / 1.0, drain about 0.25 per second |
| Listen radius | about 4 m |
| Weak pulse | about 1.5 m reach, very low noise |
| Touch mark | fades in about 0.5 s |
| Memory trace | about 10 to 15 s |
| Alert freeze | about 0.5 s |
| Spike door break | about 3 s |
| Hollow | enemy hearing × about 0.15 (not against Chase) |
| Noise zone | reach × 0.5, enemy hearing × 0.3 |
| Calibration silence | about 5 s |
| Marbles per level | about 3 |

All values are guesses to be replaced by playtest and simulation data.

### Risks

| Risk | Mitigation |
|---|---|
| Ambient noise and microphone hiss ruin the game | Calibration inside the dark plus the manual page; the suspicion meter tolerates stray spikes; charged manual echo with Space |
| Landmark sounds from the speakers feed back into the microphone | Headphones required, quiet landmark volume, calibration measures with the loops playing, gate hysteresis |
| Stereo landmarks do not work on poor speakers | Steady Listen-mode ripples give landmarks a visual backup |
| Microphone latency | Small buffer, and testing across several microphones |
| The microphone has to be measured on Linux, where Unity's player build is unverified | `IEchoSource` with simulated and scripted sources keeps every path playable without a device; the Linux **editor** path is measured (2026-10-28: real devices, live samples, 0.003 RMS room floor), the Linux **player build** is the open row in `Docs/MIC_TEST_MATRIX.md` |
| One-hit death is frustrating | Sanctuary checkpoints, the alert telegraph, the give-up cue, and the escape rules |
| A chase has no answer | The escape rules, the level design checklist, and a bot-verified escape route per chase space |
| Hearing feels unfair | Suspicion meter, the Curious warning state, and the single tuning knob `ρ` |
| The player does not understand the wave | The enemy-free maze tutorial, and early playtests with people who have never seen the game |
| Performance with many rays and arcs | Ray and arc counts are configurable, old arcs are removed early |
| Not enough time | The stretch list has only one item (The Listener); feature freeze before the last week |

### Audio

Audio is part of gameplay, not decoration. Landmark loops per room, the exit hum, enemy footsteps panned in stereo (each enemy with its own rhythm: the Crawler drags, the Spike clicks), the alert sound and the give-up change, a heartbeat in a chase, dripping pipes, a very quiet ambient bed, player footsteps, door sounds, hit and defeat sounds, UI clicks, and the reflected echo sound (a short delayed sound per arc). Collected and made by the sound designer from the start of the project, and dropped in as the code is ready.

---

## Implementation appendix

These are the decisions taken during planning, layered on this document, and live in the current code and docs:

1. **Cone angle** lives in a ScriptableObject and can change without code. 64° total at mid loudness is the default; the loudness-driven range is a tuning option and falls back to a constant if playtests prefer it.
2. **The BREATH bar was cut**; footstep noise and fear feedback replace it.
3. **Aiming uses the absolute pointer position**, not delta. If the screen edges feel sticky, a delta mode with a sensitivity slider goes into the calibration screen.
4. **Sound input comes from an abstract layer, `IEchoSource`**, with three implementations: real microphone, simulated, and scripted (for tests and the bot). Reason: Unity editor microphone support on Linux cannot be relied on.
5. **Arcs are drawn with a fixed-capacity Mesh and one hand-written shader**, not LineRenderer: per-sample thickness and colour are required, and each sample must be able to break the line.
6. **The fracture mask is computed once per ping** and every arc of that same ping reads from it, so the pattern of segments stays coherent while it collapses toward the player.
7. **The build is a Linux x86_64 build from the Linux editor** (Linux Build Support module; the whole team develops and tests on Linux). *(Amended 2026-10-30: a Windows x86_64 Mono build from the same editor is a delivery build too, not an optional extra.)*
8. **Audio arrives incrementally**: placeholder sounds early, a full pass in the last week.
9. **Surface materials were removed** and brightness comes from geometry.
10. **Pitch detection is not used anywhere in this term's scope**: it is unreliable across people and microphones. Any pitch-based idea is limited to a coarse low/high split and stays in "later".
11. **Section state is resettable**: doors, enemies and the marble count can be restored to a checkpoint, which both the retry and the bot rely on.
12. **The simulation must be deterministic**: enemy randomness (patrol pauses) uses seeded generators.

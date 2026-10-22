# Game Design Document (GDD)

**Project name:** Echo — **code name:** `echo-maze`
**Document version:** 1.1 (rewrite of version 1.0)
**Engine:** Unity 2D (pinned version: `6000.6.3f1` — see [TEAM_SETUP.md](TEAM_SETUP.md))
**Target platform:** Windows (PC) with a microphone

> This file is the open-source version of the document the team wrote initially. The reference images (images 1 to 4) are not in the repo; they were only ever cited as "image n of the original document" and have been dropped. The implementation decisions layered on top of this document are in [the implementation appendix](#implementation-appendix) and [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md). The list of things deliberately not built is in [SCOPE_CUTS.md](SCOPE_CUTS.md).

---

## The idea in one sentence

Everything is dark and the screen is completely black. You make a sound, your sound becomes a wave that spreads in front of you, hits walls and bad creatures and comes back. From the shape of the returning wave you figure out what is there. But every sound you make, the creatures of the dark hear too.

## Project goal

This game is our Game Theory course project. Our main goal is to make a stealth and horror game whose core mechanic is "seeing by hearing". The player never sees the environment — they only see the shape of the returning wave and must understand from it whether there is a wall, a pillar or an enemy in front of them.

Our second goal is for the game to be simple and minimal so it finishes within one term, while the idea is distinctive enough to keep working on and publish later.

### Why this idea?

- Low production cost: no 3D models and no textures needed.
- Not tied to a language, so it can be released internationally.
- Great for streaming and reaction videos, because the player's own voice is part of the game.
- A learning opportunity for us: microphone input, raycasting and shader writing.

### Audience

Ages 15 to 35, no gender restriction. People who like horror, stealth and experimental games. Streamers and content creators.

## Team and responsibilities

| Name | Role | Duties |
|---|---|---|
| Hossein Askari | Programmer 1 | The game core: microphone input, conical raycast, wave and reflection system |
| Mahziar Mirazimi | Programmer 2 | UI, enemy AI, levels and menus |
| Seyed Saeed Nourian | Game and sound designer | Level design, enemies, balancing and the feeling of fear |

> In practice for this term, **programmer 1 writes all the code** and the other two do level design, balancing and testing. That is why the phasing in [PLAN.md](PLAN.md) is laid out for one person — but the documentation and parameters are shaped so they can be changed without touching code.

## Project boundaries: what we build now, what comes later

We have many ideas, but the term is short. So everything is split into two groups. The "now" group is finished completely and cleanly first; only if time remains do we get to "later".

| Area | Now (this term's delivery version) | Later (if time allows, or after the term) |
|---|---|---|
| Core mechanic | Microphone input, outgoing conical wave, reflected wave, manual echo with Space | Ghost Echoes, Deaf Zones |
| Surface material | Three kinds: metallic (strong reflection), spongy (absorbs sound), ordinary | Glass (sound passes through), angled acoustic mirror |
| Enemies | Crawler, Spike, sound Absorber | The Silent, Spider, Mimic, Frequency Hunter |
| Tools | Decoy marble, weak pulse | Ultrasonic whistle, audio recording, frequency tuner, other power-ups |
| Levels | 4 levels | More levels, a final boss (the unstable-echo room) |
| Modes | Short story, Silent Mode | Survival, Blind, two-player co-op |
| Rest | HUD, menus, microphone calibration, creature notebook | Streamer mode, heartbeat, full audio-horror pass, Android |

**Why this "now" set:** we took the three priority proposals from the idea file (two surface materials, one simple audio tool, an Absorber enemy), because they are both cheap to implement and change the feel of the game a great deal.

## Core mechanic: how the wave works

This is the most important part of the game and everything is built on it.

1. **The player is silent, nothing is visible** (original document, image 1). The screen is black. The player emits no light. Walls, pillars and enemies are all there but invisible. Only a faint line of text: "Make a sound to listen".
2. **The player makes a sound, the outgoing wave travels** (image 2). The microphone captures the sound and a cone-shaped wave (not a full circle) spreads in the direction the player is looking. This outgoing wave is visible.
3. **The wave hits something and returns** (image 3). The returning wave comes back as a set of nested arcs (from large and new to small and old) toward the player. The shape of each arc is built from the shape of the surface it hit:
   - Round surface (pillar): a soft, smooth arc
   - Flat surface (wall): a flat, elongated arc
   - Corner (box): one bright point
   - Sharp or serrated surface (Spike): a serrated arc
   - **Important:** the player never sees the complete shape of an object. The arcs are broken and only the part of the surface facing the player is read. The returning wave must feel like it is coming back, i.e. moving from the object toward the player, not the other way round.
4. **The player reads the shape** (image 4). Every object and every enemy has its own "wave signature".

### Core rules

- Loudness determines reach and ray density. Louder means more visibility.
- The enemies hear sound too. Loud sound means an enemy comes toward you.
- After each echo there is a cooldown, shown in the HUD.
- If the player has no microphone or does not want to speak, Space gives a manual echo (longer cooldown, shorter reach).
- Shift is slow, silent movement.

### Surface materials (now)

| Material | Wave behaviour | Shape in game |
|---|---|---|
| Ordinary (concrete, wall) | Medium reflection | Arc with normal brightness |
| Metallic | Strong reflection, longer reach | Very bright, sharp arc |
| Spongy (cloth, mud) | Absorbs sound | Dark, short, faint arc. The room becomes hard to read |

## Characters

**Player:** a small, dim creature seen from above. It gives off no light and is nearly invisible.

### Enemies (now)

| Name | Wave signature | Behaviour |
|---|---|---|
| Crawler | Two round bumps with a gap between them | Slow, medium hearing |
| Spike | Serrated, like a saw | Fast, but weak hearing |
| Sound Absorber | No wave returns at all. A black empty patch in the middle of the arcs | Swallows sound. When you get close the arcs suddenly empty out, and that is the scary part |

### Enemies (later)

The Silent (very quiet, reacts to the smallest sound), Spider (fast, irregular path), Mimic (plays back the player's own recorded sound), Frequency Hunter (reacts only to low or high pitch).

### Obstacles and objects

Pillar: soft round arc — Box: straight line with a bright point at the corners — Wall: straight, long line, but broken — Door: two line fragments with a gap between them.

### Tools and items

**Now:** the decoy marble, which you throw and which makes a sound where it lands, drawing the enemy there. The weak pulse, which gives a very short, quiet echo that the enemies hear much less.
**Later:** echo amplifier, soft shoes, microphone battery, ultrasonic whistle, audio recording, frequency tuner.

## Controls

| Input | Action |
|---|---|
| Microphone | Creating an echo. Loudness sets reach and ray density |
| W A S D or arrows | Movement |
| Mouse | Aim direction (the direction of the echo cone) |
| Shift | Slow, silent movement |
| Space | Manual echo without a microphone |
| Right click or Q | Throw the decoy marble |
| Esc | Pause menu |

## Win and lose rules

- Reaching the level exit means winning.
- An enemy reaching the player, or health running out, means losing. Lose screen: "YOU WERE HEARD".
- **BREATH bar:** running and fast movement deplete breath; with low breath the player's breathing gets louder and the enemies hear it. This part is **cut from this term's scope** (see [SCOPE_CUTS.md](SCOPE_CUTS.md)).

## Levels (now)

| Level | Content | Difficulty |
|---|---|---|
| 1 (tutorial) | Simple room with pillars and boxes, no enemies. Learning the echo and reading wave shapes. One sponge wall as well, so they see the difference between materials | Easy |
| 2 | Corridor and door, Crawler enemy | Easy to medium |
| 3 | Open space with many obstacles, Spike and Crawler enemies, metal and sponge walls | Medium |
| 4 (finale) | All enemies plus the Absorber, using the decoy marble is mandatory. Short story ending | Hard |

Playtime: roughly 30 to 60 minutes for the short story mode.

## Modes

**Story (now):** 4 levels in sequence. **Silent (in scope in the document, cut in the implementation):** only a limited number of sounds. **Later:** survival, blind, co-op.

## Story (short)

A researcher wakes up in the basement of an abandoned facility. The power is out and nothing is visible. They can only hear their own voice. The place is full of creatures that react to sound. They must find the exit without being detected. At the end it emerges that the facility was built to study these very "echoes".

## Look and mood

2D, top-down view, camera locked to the player. Pure black background, thin bright lines in cyan (about `#7fe9f0`) with a Bloom glow. The HUD uses the same thin cyan lines but dimmer than the waves, so it never becomes a visible light source itself. A monospace font such as JetBrains Mono or Share Tech Mono, plus a suitable Persian font.

## Screens and menus

### In-game HUD

Top left: HEALTH bar — top centre: VOICE INPUT level and SOUND RANGE — top right: pause and settings — bottom left: breath bar (cut) — bottom right: ECHO COOLDOWN ring — bottom centre: the text "Make a sound to listen" — top centre (faint): the proximity warning "SOMETHING IS CLOSE".

### Menus

- **Main menu:** PLAY, OPTIONS, CREDITS, QUIT. Background is an echo arc drifting slowly.
- **Pause:** RESUME, SETTINGS, CALIBRATE MIC, QUIT and a TIP box.
- **Settings:** microphone selection, sensitivity, volume, effect quality.
- **Microphone calibration:** live waveform, sensitivity and noise-threshold sliders, TEST button.
- **Victory:** stars based on quietness, health and time (cut in the implementation).
- **Defeat:** black screen with "YOU WERE HEARD" and a RETRY button.
- **Bestiary:** the wave signature of everything discovered (cut in the implementation).
- **Credits.**

## Technical section (Unity)

| Part | How |
|---|---|
| Microphone input | `Microphone.Start` and reading samples with `GetData`. Loudness computed as RMS, then noise filter, sensitivity threshold and calibration |
| Conical raycast | With `Physics2D.Raycast`. When sound passes the threshold, 100 to 200 rays are fired inside a cone in the mouse direction. The rays are invisible; only each hit distance is stored |
| Outgoing wave | A bright arc that spreads from the player at constant speed up to the sound's reach |
| Reflected wave | Every object has a Collider and a material. The returning arc is built from the ray hit distances |
| Drawing the arcs | LineRenderer or Mesh with a custom shader for the glow (in the implementation: a fixed-capacity Mesh plus a hand-written shader) |
| AI | Hearing based on loudness and distance, moving toward the sound source, patrol/investigate/chase states (a simple state machine) |
| Sound Absorber | A ray that hits it returns no reflection at all, so the arc keeps an empty gap there |

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

### Risks

| Risk | Mitigation |
|---|---|
| Ambient noise and microphone hiss ruin the game | Microphone calibration page and noise threshold from day one, plus a manual echo with Space |
| Microphone latency | Small buffer, and testing across several microphones |
| Performance with many rays and arcs | Ray and arc counts are configurable, old arcs are removed early |
| The player does not understand the wave | Tutorial level, testing with people who have never seen the game (the week 2 go/no-go point) |
| Not enough time | Everything in "later" is optional; the weekly cut list is in [PLAN.md](PLAN.md) |

### Audio

Very quiet, scary ambient music, player footsteps, breathing and heartbeat as an enemy approaches, a sound per enemy, hit and damage effects, victory and defeat, UI clicks, and the reflected echo sound (a short delayed sound per arc).

---

## Implementation appendix

These are the decisions taken while planning the 5-week schedule, layered on this document, and live in the current code and docs:

1. **Cone angle:** default 64 degrees (`coneHalfAngleDeg = 32`); the number lives in a ScriptableObject and can change without code. This closes the document's open question.
2. **The BREATH bar was cut** so that 4 levels and 3 enemies could land on time; the "moving faster makes more noise" mechanic is compensated by the `loudness` pulse in Shift mode.
3. **Aiming uses the absolute pointer position** (per the GDD), not delta. If the screen edges feel sticky, a delta mode with a sensitivity slider goes into Options.
4. **Sound input comes from an abstract layer, `IEchoSource`**, with three implementations: real microphone, simulated, and scripted (for tests). Reason: Unity editor microphone support on Linux cannot be relied on, and the lead programmer develops on Linux.
5. **Arcs are drawn with a fixed-capacity Mesh and one hand-written shader**, not LineRenderer: per-sample thickness and colour are required, and each sample must be able to break the line.
6. **The fracture mask is computed once per ping** and every arc of that same ping reads from it — so the pattern of segments stays coherent while it collapses toward the player and the eye can read several arcs as one object.
7. **The Windows build is a Mono build from the Linux editor** (the Windows Build Support (Mono) module is installed; Windows IL2CPP is not available on Linux).
8. **Full sound and music arrive in week 5**; the risk of speaker sound returning into the microphone is mitigated by calibration, hysteresis and headphone advice.

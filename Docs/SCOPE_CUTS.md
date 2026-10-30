# Things deliberately not built (out-of-scope)

This file exists so nothing gets lost: every idea left out of this term's delivery version is recorded here, with the reason and "where we pick it up later". Before adding any new feature to the game, read this list and the "now / stretch / later" table in [GDD.md](../GDD.md).

**Revised 2026-10-27 against GDD v3.0.** The previous version of this file cut things from a game that no longer exists: it promised three surface materials, four levels, a BREATH bar and a health bar. All four of those are gone from the design, so they moved from "polish we skipped" to "features that were removed", below.

## Removed from the design itself (not cuts — deletions)

| Item | Why it is gone | What replaced it |
|---|---|---|
| Surface materials (concrete / metal / sponge) | Every surface sounding different meant per-surface data, a material lookup, and a readability test nobody could pass by ear. Geometry alone already carries the information | Brightness from the curvature of the hit-distance profile: `b(i) = base + k · clamp(\|d[i+1] − 2·d[i] + d[i−1]\| / dCurv)`. A corner is bright because it is a discontinuity, not because it is metal |
| The BREATH bar | It modelled a resource the game does not have: sound is not stamina | Footstep noise (walking is audible, `Shift` is silent) plus fear feedback — the arcs jitter as the nearest suspicious enemy closes in |
| The health bar | Three hits of slack turned one-hit death into attrition, and stealth tension needs the threat to be lethal | One hit kills, with sanctuary checkpoints, an alert telegraph before every chase, a give-up cue, and the escape rules that guarantee an answer to every chase |
| Level 4 | Four levels in five weeks meant none of them finished | Three levels: the enemy-free maze (tutorial), The Corridor, The Sump |

## Final cuts for the delivery version (Autumn 2026)

| Item | Why it was cut | Where it continues |
|---|---|---|
| The Listener | The only stretch item we keep. It needs a full enemy plus a map-drawing pulse on top of a shipped game | Week 6 buffer, only if weeks 2–5 are green; it reuses the ray pipeline, so it is data and one AI state |
| Mimic, Spider, Frequency Hunters | Each is a behaviour plus a wave signature; the Mimic needs recorded audio and the Frequency Hunters need a spectrum split | `Data/Enemies/*.asset`; the two-band low/high split is the only pitch-ish idea allowed (GDD appendix 10) |
| Ultrasonic whistle, audio recorder, frequency tuner | Each needs its own sound-input/recording subsystem | After the term; the recorder rides on the same `IEchoSource` |
| Resonant switch, frequency doors, echo-note corridors, echo halls | All of them are variations on "surface type matters", which v3.0 removed | After the term, if geometry-only reading still leaves the world too plain |
| Ghost Echoes, Deaf Zones, second-order bounces | They only make sense once the first-order arc is proven readable, and they reopen `RayFan` for multiple bounces | After the term |
| End-of-level stars, XP and score | Needed numeric balancing that cannot be measured in five weeks | After the term; its inputs (`NoiseBoard` accumulated loudness, time) are already recorded |
| Bestiary | A whole UI with state collection and saving, for something that adds no decision to the loop | After the term; its signatures are already data on the colliders |
| Blind mode, Whisper mode, survival, two-player co-op | All of them change the level/scene architecture, not a parameter | After the term; "Blind mode" is nearly one flag in `EchoTuningProfile` |
| Streamer mode (chat hooked to sound) | An external dependency, untestable in this time | After the term — even though the player's own voice makes this the game's natural marketing channel |
| Android | Microphone input and mobile performance; the GDD target is Windows and Linux PC with headphones | Never in this project; a separate version if it happens |
| Localisation (Persian and any other language) | GDD v3.0 is English-only; RTL text and a second font pipeline are not worth a week | After the term, through `LocTable` |
| CI on GitHub Actions | Activating a Unity licence on a runner is itself a week of work | Tests run locally from the command line; the command is in [QA_CHECKLIST.md](QA_CHECKLIST.md) |
| Loudness-driven cone width | Kept as a tuning option only: the half-angle may follow loudness from 20° to 45° | If playtests say it does not help, `coneHalfAngleDeg` stays a constant 32° |

## What was **not** cut and is a commitment

The GDD v3.0 "now" column is the contract: microphone input behind `IEchoSource` with calibration inside the dark and a manual page; the charged `Space` echo as a real alternative; the cone wave, the returning arc train, brightness from geometry, fracture, touch marks, the 360° weak pulse, the shimmer of living things; Listen mode, `Shift` silent movement, footstep noise; hollows, sanctuary checkpoints, doors that hold; sound landmarks, the memory trace, the exit hum; the Crawler, the Spike and the Sound Absorber with the suspicion meter and the alert/give-up cues; the decoy marble; noisy floors and noise zones; three levels with the enemy-free maze as the tutorial; a conventional main menu, pause, instant retry; the bot simulation; the full sound palette; and two builds, Linux x86_64 and Windows x86_64 Mono (amended 2026-10-30: both platforms are delivery targets).

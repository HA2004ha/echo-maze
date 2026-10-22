# Things deliberately not built (out-of-scope)

This file exists so nothing gets lost: every idea left out of this term's delivery version is recorded here, with the reason and "where we pick it up later". Before adding any new feature to the game, read this list and the "now/later" table in [GDD.md](GDD.md).

## Final cuts for the delivery version (Autumn 2026)

| Item | Why it was cut | Where it continues |
|---|---|---|
| Bestiary | Would have needed a complete UI with state collection and saving; neither plays any role in the core mechanic | After the term; its wave-signature data reads from the same `EchoSignature` assets |
| Silent Mode | It was only a counter, but it doubled the balancing test load | After the term, on the same `GameFlow` with one limit inside `EchoDirector` |
| End-of-level stars, XP and score | Needed numeric balancing that could not be tested in 5 weeks | After the term; its input is free from `NoiseBoard` (accumulated loudness) and time |
| BREATH bar | The GDD itself said "cut this if time runs short"; it overlapped with the `loudness` pulse in Shift mode | After the term, or converted into "fatigue" on that same `loudness` pulse |
| Glass material and angled acoustic mirror | Glass means sound passing through and the path bending; a mirror means vector reflection — both require rewriting the ray physics | The ray physics in `RayFan` reopens for multiple bounces |
| Ghost Echoes and Deaf Zones | They only make sense after arc readability is proven, otherwise they make an unreadable game more confusing | Week 6 and later; a `secondOrderBounce` in `ArcMath` |
| The Silent, Spider, Mimic and Frequency Hunter enemies | Four behaviours plus four new wave signatures = two weeks; the Frequency Hunter specifically needs a frequency analyser | `Data/Enemies/*.asset` and `EchoSignature` were built for these, so they only need data |
| Ultrasonic whistle, audio recording, frequency tuner | Each needs its own sound input/recording subsystem | After the term; audio recording rides on the same `IEchoSource` |
| Final boss (the unstable-echo room) | A global rendering pass plus full balancing | After the term |
| Streamer mode (chat hooked to sound) | External dependency and untestable in this time | After the term |
| Survival, Blind and two-player co-op modes | All three require opening up the scene/level architecture | After the term; "Blind" is only one flag in `EchoTuningProfile` |
| Android | Microphone input and mobile performance; the GDD target was Windows PC | Never in this project; a separate version if it happens |
| CI on GitHub Actions | Activating a Unity editor license on a runner is itself a week of work | Running the tests locally from the command line; the command is in [QA_CHECKLIST.md](QA_CHECKLIST.md) |

## What was **not** cut from the GDD's "now" scope and is a commitment

Microphone input with a noise gate and calibration, the outgoing cone wave and the reflected arc train, three surface materials (concrete/metal/sponge), three enemies (Crawler/Spike/Absorber), the decoy marble and the weak pulse, four levels, the HUD, the main/pause/settings/calibration/win/lose menus, the short story ending, sound and music, and a Windows build.

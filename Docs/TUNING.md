# Wave settings and balancing (TUNING)

Every number in this table is live in `Assets/Data/Echo/EchoTuning.asset`, i.e. `Assets/Scripts/Runtime/Data/EchoTuningProfile.cs`. None of them are hard-coded in C#, so changing the balance needs no compile and can be tested in the editor with Play. The character's motion is the one deliberate exception: those numbers are `[SerializeField]` on `PlayerVisual` in the scene and are listed under "The player's art and motion" at the end of this file. The profile is the balance brain — `OnValidate` enforces its invariants and `MovementAndHearingTests` asserts them — and a breathing rate is not balance.

The target these numbers have to hit is [GDD.md](../GDD.md) v3.0, whose tuning knob is **`ρ = enemy hearing radius / visible echo reach` at the same loudness**, starting at `0.8`. If `ρ` is too high, pinging is never worth it; if too low, there is no tension.

## What is missing from this table (and lands when)

GDD 3.0 is a stealth game with one hit and a suspicion meter, and this profile only knows about waves. The rest gets added to this file in the same commit as the field:

| Group | Parameters | Week |
|---|---|---|
| Arc train | `arcsPerPing`, `arcInterval`, `fractureFlatBelow`, `flatDropChance` | 2 — **landed**, rows below |
| Brightness (replaces the deleted material lookup) | `brightnessBase`, `brightnessGain`, `curvatureFloor`, `curvatureRange` | 2 — **landed**, rows below |
| Microphone | `gateOpenRms`, `gateCloseRms`, `gateFullRms`, `loudnessHold`, `microphoneDevice` | 2 — **landed**, rows below |
| Echo cost | `cooldownBase`, `cooldownPerLoudness` (they replace `minPingInterval`) | 2 — **landed**, rows below |
| Silent fallbacks | `weakPulseRadius`, `weakPulseRays`, `weakPulseCooldown` | 2 — **landed**, rows below |
| Living things | `heartbeatHz`, `shimmer` | 2 — **landed**, needs the Crawler in the scene to tune by eye |
| Movement and `ρ` | walk `2.5` / Shift `1.0` / listen `1.0` m/s, enemy speeds (`0.65×`, `1.3×`, `0.4×`), `rho` | 2 — **landed**, rows below. The player half is `Player`; the `1.3×` and `0.4×` enemies (Chaser, Lurker) arrive with `EnemyBrain` in week 3 |
| Hearing | suspicion thresholds `0.3 / 0.6 / 1.0`, drain `≈0.25/s`, alert freeze `0.5 s`, give-up search time | 3 — the Crawler already listens through `H = ρ·R(L)` and gives up after `huntSeconds`, which is the week-2 stub, not the suspicion meter |
| Safety | listen radius `4 m`, weak pulse reach `1.5 m`, touch-mark fade `0.5 s`, memory trace `10–15 s`, hollow `×0.15`, noise zone `reach ×0.5 / hearing ×0.3` | 3 |
| Doors and tools | Spike break time `3 s`, marbles per level `3` | 4 |
| Level pacing | the 20–30 s hiding-option distance the level validator measures | 4 |

## Non-negotiable rules

1. `waveSpeed >= 1.6 * walkSpeed` — if the arc travels slower than 1.6 times the player's speed, the returning arc can never overtake the player and the "returning" feeling dies. `walkSpeed` is `2.5`, so the floor is `4.0`; the default `7` sits with a wide margin. This one is not left to memory: `EchoTuningProfile.OnValidate` rewrites `waveSpeed` up to the floor the moment a designer raises `walkSpeed` in the inspector, so the rule cannot be broken by a typo.
2. The fracture mask is computed once per ping and every arc of that ping reads the same mask. This is a readability decision, not an optimisation (read `Docs/DESIGN_DECISIONS.md`).
3. A ray that hits nothing means "there is no information", not "a distant surface". `d = +inf` and `amp = 0`, and the line breaks right there.
4. Balance is judged in pixels, not in world units. The sandbox camera is `orthographicSize = 6`, so on the short side of the screen 12 world units map onto 1080 pixels and each world unit is roughly 90 pixels. Any number in this table that ends up below 4 pixels, or below 25 pixels of shape change across the wave's lifetime, effectively does not exist in the game.

## The week 1 rebalance (date: 2026-10-17)

The player said in Play "the wave just arrives, nothing comes back from the objects". Measurement instead of guessing: with the GDD defaults (`gain0 = 0.28`, `baseThickness = 0.022`) the pillar's dent on the arc came to `0.119` world units — `10` pixels on screen, `2` pixels on the ribbon. The eye sees a circle there, not a pillar. Now:

| Parameter | Before | After | Reason |
|---|---|---|---|
| `gain0` | `0.28` | `1` | The dent reaches `38` pixels, so the arc actually reflects the object's own distance |
| `baseThickness` | `0.022` | `0.05` | The ribbon goes from `2` to `4.5` pixels; below this it breaks into dashes on a 1080p monitor |
| `maxRadius` | `9` | `6.5` | A ring larger than the room reads as a circle that came from nowhere; a shout's reach is `5.47` units, which covers the wall (`4.85`) and the box (`3.31` to `4.97`) |
| `minRadius` | `2.5` | `2` | A whisper should show something but not as much as a shout |

**Regression lock:** `Assets/Tests/EditMode/ArcReadabilityTests.cs` translates these same four claims into pixels (pillar dent ≥ 25 pixels, pillar vs the wall behind it ≥ 90 pixels apart in radius, the pillar centre as the innermost point of the arc, ribbon ≥ 4 pixels). If the tuning gets flattened again, this goes red — not the playtest.

**The same playtest, second complaint:** "sound only comes toward the player, nothing leaves him". A returning arc is inherently a ring collapsing from outside and shows nothing of the outbound part. For that same cause-and-effect feeling, `emissionPulseSeconds` opens a plain line in *the same cone* as the ping outward from the origin for 0.16 seconds. Deliberately shapeless: it contains no measurement, so the rule "only the returning wave carries information" is not broken. Its radius is always clamped below the returning carrier so the two rings cannot cross. **Lock:** `EmissionPulse_NeverOvertakesTheReturningCarrier` in `ArcMathTests.cs`.

## Parameters

| Field | Default | Meaning | Safe range | If you raise it | If you lower it |
|---|---|---|---|---|---|
| `coneHalfAngleDeg` | `32` | Half the cone angle; `64` degrees total per the GDD | `5..90` | More peripheral vision, enemies see you from the side | Narrow cone, the player is blind behind themselves |
| `minRays` | `80` | Ray count at the quietest audible ping | `16..512` | The Spike's teeth show even on low-quality profiles | Expensive core |
| `maxRays` | `200` | Ray count at a shout | `minRays..512` | Cleaner straight line, higher cost | The box corner may fall between samples |
| `minRadius` | `2` | Wave reach at zero loudness | `>=0.5` | A whisper reveals the whole room | A resting breath is nearly useless |
| `maxRadius` | `6.5` | Wave reach at a shout | `>= minRadius+0.5` | The whole room in one ping | A large room cannot be read in one ping |
| `radiusLoudnessExponent` | `0.75` | Curvature of reach against loudness | `0.1..3` | Even quiet sounds give good reach (easier game) | Only shouting helps (harder, noisier game) |
| `waveSpeed` | `7` | How fast the arc collapses toward the origin, units/second | `>=5.12` | Sharper feel, less readability | Slow wave, more time to read |
| `rKill` | `0.35` | The radius at which the arc dies | `0.05..2` | The arc fades sooner, the origin stays clean | The arc lands on the player and eats readability |
| `emissionPulseSeconds` | `0.16` | Lifetime of the outbound ring in the same cone as the ping; it carries no geometry information | `0..0.5` | The two rings collapse into each other and cause and effect are lost | At `0` it is removed and the player only sees the returning wave |
| `gain0` | `1` | `A_0` in the formula: how much the hit distance deforms the arc | `0..2` | Object shapes are exaggerated and read faster, the ring is no longer a circle | The arc stays nearly circular and the shape is lost |
| `gainSign` | `+1` | Sign of the deformation | `+1 / -1` | `-1` makes the wave look like emission rather than return; only for a 5-second test | — |
| `alphaCurve` | `1 → 0.75 → 0` | Opacity against normalised arc age | — | Long tail, cluttered | Early fade, the next ping feels lonely |
| `baseThickness` | `0.05` | Ribbon width in world units | `0.002..0.3` | Lines merge into each other, shapes are lost | Below `0.04` it disappears at the 1080p target |
| `arcColor` | `#7fe9f0` | Base cyan colour, before `_Hdr` | — | — | The HUD uses the same colour; tune wave-vs-interface contrast here |
| `creatureArcColor` | `#ffb56b` | The GDD's second tone: colour of the arc segments whose sample came back off a living thing (`SurfaceRead.IsCreature`), so a warm fragment inside a cyan ring says "that part of the wave came back alive" before the player decodes any shape. Only the tint changes — distance, deformation and the shimmer pulse still come from the same geometry | — | Every arc glows warm and the room is full of false creatures | A creature reads exactly like a pillar, which is the failure this colour exists to prevent |
| `loudnessThreshold` | `0.18` | Gated loudness an automatic ping needs; it sits **on top of** the microphone gate, so raising it makes the player be clearly loud, not merely audible | `0..1` | Every cough makes a ping | The player has to shout |
| `cooldownBase` | `0.6` | Seconds of recovery every ping pays | `0..3` | Silence feels expensive, the pace drops | The signal is nearly free and the game becomes a shout fest |
| `cooldownPerLoudness` | `1.4` | Extra seconds per unit of loudness: a shout costs `0.6 + 1.4 = 2.0 s`, a whisper `~0.7 s` | `0..4` | Being loud is a real risk, which is the game theory | Loud and quiet pings cost the same, so nobody whispers |
| `manualEchoLoudness` | `0.65` | Loudness of a quick `Space` tap, and the floor the hold starts from — the fallback that must stay playable on a machine with no microphone | `0..1` | `Space` makes the microphone pointless | A player without a microphone loses |
| `manualChargeSeconds` | `1.2` | Seconds of holding `Space` that take the manual echo from `manualEchoLoudness` to `1.0` (`ArcMath.ManualLoudness`) | `0.2..4` | A panic tap is safer, a deliberate shout takes real time in the open | The charge is over before the player decides whether to be loud |
| `maxLivePings` | `3` | Simultaneous pings in the pool | `1..4` | Stacked arcs, unreadable shapes | A large room cannot be gathered in one ping |
| `walkSpeed` | `2.5` | Metres per second, the normal pace. Every other speed in the game is measured against it, and rule 1 is enforced from it | `0.5..8` | Rooms empty faster, the returning arc has less time to be read, `waveSpeed` is pushed up with it | The player crawls and the arc waits |
| `silentSpeed` | `1.0` | Metres per second while Shift is held. `Player` emits **no** footstep at this speed, which is the only cheap move the game offers | `0.1..walkSpeed` | Silence stops being a trade | Silent walking is as slow as standing still, so nobody uses it |
| `footstepInterval` | `0.55` | Seconds between footstep noise events while walking normally | `0.1..3` | A fast walk is a drumbeat the Crawler can track | Footsteps become continuous and stop being a rhythm |
| `footstepLoudness` | `0.12` | Loudness of one footstep. Through `H = ρ · R(L)` this puts a normal walker's hearing range a couple of metres behind the player | `0..1` | Walking is nearly a shout and Shift becomes mandatory everywhere | A Crawler two rooms away never hears you walk |
| `bumpLoudness` | `0.04` | Loudness of the body pressing a wall, emitted with the touch mark. `Player` only emits it when the collision had real speed behind it (`relativeVelocity² > 0.25`) | `0..1` | Leaning on a wall to listen becomes a risk | Careless walking is free |
| `rho` | `0.8` | The tuning knob from the GDD: enemy hearing radius / visible echo reach at the same loudness (`ArcMath.HearingRadius`) | `0.05..3` | Pinging is never worth it — you are heard before you see | No tension; the enemy is effectively deaf |
| `crawlerSpeedMultiplier` | `0.65` | Crawler speed as a fraction of `walkSpeed`, i.e. `1.6` m/s. Below 1 by design: you can outrun it, you must not outrun the Chaser | `0.1..2` | The Crawler stops being an obstacle and becomes a chase | The creature is furniture |
| `arcsPerPing` | `3` | Arcs in one train; one is a pulse, three is a wave with a front | `1..4` | More to read, more clutter, more quads | The wave stops looking like it is travelling |
| `arcInterval` | `0.12` | Seconds between the arcs of one train | `0..0.5` | The arcs separate and read as three objects | They merge into one thick band |
| `innerDecay` | `0.72` | Damping of `A_k` in the arc train (`A_k = gain0 * innerDecay^k`). Was `1`, which made all three arcs identical and the train read as one wide line | `0.1..1` | Inner arcs lose the shape | All arcs have the same shape and the train looks flat |
| `brightnessBase` | `0.4` | Opacity of a locally flat run — a plane measures curvature 1.0 and sits under `curvatureFloor`, so walls stay dim by construction | `0..1` | Everything glows and the features stop standing out | The room is invisible and only the features exist |
| `brightnessGain` | `0.6` | Extra opacity a sample can earn from curvature (`brightnessBase + gain` is the ceiling; `OnValidate` clamps the sum to 1) | `0..1−base` | Corners flare | Features and walls look the same |
| `curvatureFloor` | `2` | Normalised curvature that counts as flat. A plane is exactly 1.0 head-on and ~1.8 at the edge of a 32° cone, so 2 leaves the whole flat band below it. Beyond that band the metric is incidence-dependent, not shape-dependent: a plane hit at angle ψ measures `2/cos²ψ − 1` (7 at 60°, 29 at 75°), which is the known residue listed under "Measured, still open" below | `0..8` | Only sharp corners light up | A wall at an angle starts glowing |
| `curvatureRange` | `6` | How much curvature saturates a sample to full brightness. Measured in `Level02_Corridor` from the alcove at a shout: every flat wall run in the cone sits at 1.0–2.0, the `Pillar` (r = 0.6 at 5.7 m) at ~10. **`floor + range = 8` is therefore the line between "architecture" and "feature"** — the pillar and the Crawler's bodies cross it, the room does not. Checked at the other end by script, above the inspector ceiling of `40`: at `60` the pillar's saturation drops to 3.3% of the cone ≈ 6 samples ≈ 18 px, under rule 4's 25-pixel threshold, so the flare stops being visible at all | `0.5..40` | Small features never brighten; at 60 the whole room flattens to base | The cone is a sheet of white and nothing reads as a feature |
| `edgeJumpFraction` | `0.12` | A distance change between neighbouring rays bigger than this fraction of the distance itself is a **step** between two surfaces, not a bend in one. Steps get zero brightness and break the line; a continuous kink (a real corner) keeps the flare. Measured on the `Wall_North` cone: the peak normalised curvature in the fan drops from `12670` to `236`, and the box-corner cone from `12684` to `638`, with the saturated sample count essentially unchanged because what saturates there is the pillar, legitimately | `0.02..0.6` | Real shallow corners start being suppressed as if they were crossings | Grazing runs along a receding wall get chopped into spokes |
| `flatDropChance` | `0.18` | Probability that a segment of a locally flat run is dropped from the fracture mask | `0..0.6` | The wall dissolves into dashes | A wall reads as a perfect sheet, which is what the GDD calls too clean |
| `fractureFlatBelow` | `2.4` | Curvature under this counts as "flat" for the fracture mask, so gaps never eat a feature | `0..8` | Features start losing segments | Only the flattest runs break |
| `heartbeatHz` | `1.1` | Rate at which a creature's arc samples pulse | `0..4` | A creature strobes and reads as a fault | The pulse is invisible |
| `shimmer` | `0.35` | Amplitude of that pulse on brightness | `0..1` | Living things are obvious — which may be too obvious | The creature is indistinguishable from a wall |
| `microphoneDevice` | `""` | Exact `Microphone.devices` string to open; empty takes the first device that is not a `Monitor of …`. Keep it empty in the committed asset: from week 3 the player chooses the device **in-game** and this field is only where that choice is stored, so a pinned name here is wrong on every other machine by construction (2026-10-28 decision entry) | — | — | — |
| `gateOpenRms` | `0.02` | RMS that opens the microphone gate. Measured floor of a quiet room on Linux: **0.002–0.003**, so this is ~7× the hiss | `0.001..0.2` | Breathing and hiss make pings | The player must shout to be believed |
| `gateCloseRms` | `0.012` | RMS that closes it again; the band between the two is the hysteresis that stops one syllable becoming three pings | `0.001..0.2` | The gate chatters open and shut | A two-syllable word becomes two vocal events |
| `gateFullRms` | `0.12` | RMS that maps to loudness 1.0 | `0.01..1` | Everything is a shout | Normal speech maxes the meter and reach stops discriminating |
| `loudnessHold` | `0.25` | Seconds of level held after the voice drops below `gateCloseRms` | `0..2` | One long word blurs into the next | Brief dips inside one word close the gate |
| `weakPulseRadius` | `1.5` | Reach of the 360° `F` pulse — the "is something standing behind me" move | `0.25..4` | It replaces a real ping and the voice becomes pointless | It cannot reach past your own feet |
| `weakPulseRays` | `96` | Rays in that ring (odd; `| 1` keeps it odd) | `24..512` | Smoother circle, more cost | A close creature reads as a few dots |
| `weakPulseCooldown` | `1.2` | Its own cooldown, independent of the loud one — that separation is the whole reason to press `F` | `0.1..5` | Spamming a pulse stops being safe | Spamming a pulse costs nothing |
| `simulatedLoudness` | `0.7` | Constant loudness of `Simulated` mode; only has an effect when `EchoDirector.sourceMode` is `Simulated`, which is the **fallback** after a microphone fails to open. The scene default is `Microphone` | `0..1` | — | — |
| `simulatedEventInterval` | `1.6` | Seconds between invented vocal events in `Simulated` mode, so the fallback still respects "one event, one ping" | `0.2..8` | — | — |

## `_Hdr` and Bloom

The glow comes from two places: the `_Hdr` multiplier in the material `Assets/Art/Materials/Arc_Cyan.mat` (shader `Assets/Art/Shaders/EchoArc.shader`) and Bloom's `threshold` in `Assets/Settings/EchoVolumeProfile.asset`. For the cyan line to actually produce light you need:

```
arcColor(channel max) * _Hdr * shape(≈1)  >  bloom.threshold
```

With `arcColor ≈ 0.914` and `_Hdr = 2.5` the output is about `2.28`, which passes the threshold of `0.55`. If the line looks dark and unlit, do not raise `_Hdr` first: check the camera's Post Processing tick and the `Echo` layer first (see `Docs/PLAN.md`, "if the ring was not visible").

The warm creature tone is close to the same ceiling: `creatureArcColor` peaks at `1.0` on red, so a fully bright warm segment plus Bloom saturates toward cream rather than orange. It still reads as a different family from cyan in `Level02_Corridor` (verified on a frozen arc: warm and cyan vertices side by side in one mesh, distinct in the capture). If a future playtest says the two tones are hard to tell apart, lower `creatureArcColor` toward `#ff8a3d` before touching Bloom — the hue distance is the thing being tested, and Bloom's threshold is shared by both.

## The week 1 test scene

`Sandbox.unity` has three test bodies, all on the `EchoTarget` layer and with no SpriteRenderer: the pillar (`CircleCollider2D` r `0.6` at `(3, 0)`), the flat wall (`BoxCollider2D` `4×0.3` at `(0, 5)`) and the box for the "corner point" test (`BoxCollider2D` `1.2×1.2` at `(-3.5, 2.2)`). Ping with Space and ask the player what they see from the arcs alone.

The `_Concrete` suffix in the first two names is a fossil of the material system GDD 3.0 deleted — every body in the scene is ordinary geometry with `kind = Plain`. The names stay because `GitSetupValidator`'s scene-merge probe matches on `Pillar_Concrete`; rename them inside the editor and fix the probe in the same commit.

## The week 2 blockout

`Assets/Scenes/Level02_Corridor.unity` is generated by `Level02SceneSetup` (menu `Tools/Echo Maze/Week 2 Bootstrap`) and is not hand-built: the sizes, the patrol route and every scene reference come from that script, so the room can be re-made from the same numbers after a bad merge. The interior is `7 × 5` m — the GDD's 4–8 m room at the top of the range — and the south wall is split to leave a real pocket rather than a box standing in front of a wall. The player starts inside that pocket at `(2, -3.15)`, and the geometry the wave is supposed to distinguish is: `Wall_North/West/East/SouthWest/SouthEast` (straight runs), `Box_Corner` (the L), `Pillar` (the curve), the three `Alcove_*` slabs (the concave shape that becomes the week-4 hollow) and `Exit_Tile` (a trigger, excluded from the fan, so the wave must *not* measure it). One `Crawler` walks the two-waypoint leg between `(-2.2, -0.6)` and `(2.2, -0.6)`, which is far enough from the furniture that a patrolling thing never jams on a prop.

The player-vs-audio numbers that were picked here rather than guessed, and where each one is locked:

| Claim | Number | Lock |
|---|---|---|
| A ping is itself a noise at the player's own ears | `loudness` at `transform.position` | `NoiseStreamTests.AVisiblePing_IsAlsoANoiseAtItsOwnOrigin` |
| A bare noise draws nothing and costs nothing | no ping, no cooldown | `NoiseStreamTests.ABareNoise_IsHeardButDrawsNothingAndCostsNothing` |
| The `F` pulse is the quietest thing the player can do that still costs a cooldown | `weakPulse` before `cooldownBase` is paid | `NoiseStreamTests.TheWeakPulse_IsTheQuietestEventOnTheStream` |
| Noise grows in the order the game teaches it | `bump < footstep < manualEcho < 1` | `NoiseStreamTests.TheNoisesThePlayerMakes_AreOrderedFromQuietToLoud` |
| The Crawler hears inside `H = ρ·R(L)` and is deaf outside it | `rho 0.8` | `CrawlerHearingTests` (both directions) |
| Its own footsteps do not startle it | `distance < arrivalRadius` | `CrawlerHearingTests` |
| A hunt with no pathfinding is abandoned | `huntSeconds` | `CrawlerHearingTests.AHunt_IsGivenUpWhenNothingWasFound` |
| One hit kills and the room restarts in under two seconds | `retryDelay 1` | `RunControllerTests.OneHit_PutsYouBackAtTheRoomStartUnderTwoSeconds` |
| The retry resets the creatures, not just the player | same spawn, same seed | `RunControllerTests` |

Nothing in this scene was tuned by eye: the walk/Shift speeds, `rho` and the crawler's pace are the GDD's starting values. The brightness rows above are the first numbers taken out of the room itself rather than off a sandbox rig.

### How those numbers were taken (and one trap)

Through `Tools/Echo Maze/Echo Debug Window` in the editor: freeze the arc train so a frame can be studied instead of flown past, and read the pool's `d[i]` / `b[i]` / `broken[i]` plots. The same figures above were reproduced by re-running the fan on the live scene twice with identical results, which is what `PingRecord.seed` and the emitted-at frozen clock are for — determinism is the point, not convenience.

The trap, learned the expensive way: this week's only "bug" — a `NullReferenceException` from `Crawler.FixedUpdate`, where the seeded `pauseRandom` was gone while the serialized patrol data had survived — was not a game bug. **Enter Play Mode Options had drifted back on** on the lead's machine, against the setting week 1 turned off on purpose: with the reloads switched off, a script recompile restores serialized fields and leaves every non-`[SerializeField]` private field null. `ProjectSettings/EditorSettings.asset` is back to `m_EnterPlayModeOptionsEnabled: 0` — the fourth time that file has had to be reverted, twice within 2026-10-28 alone — and the general rule is that a measurement taken in a domain-reload-disabled editor is not a measurement of the game. `Validate Git Setup` proves the merge driver; nothing yet proves this file, and a check that does is worth five minutes in week 3 — four drifts in two weeks is the argument for it.

### Measured, still open

- **The metric is incidence-dependent, not only distance-dependent.** `NormalisedCurvature` divides by `d[i] · Δθ²`, which makes a flat plane measure exactly 1.0 *at normal incidence*; at grazing incidence the same plane measures `2/cos²ψ − 1`, i.e. 7 at 60° and 29 at 75°. In this 7 × 5 room it does not bite yet — every saturated sample in the `Wall_North` cone belongs to the `Pillar` or the `Crawler`, and none to a flat run — but looking *along* a wall rather than at it will eventually flare the wall. The fix is on record for week 3: store the hit normal's cosine per sample at emit time and divide by the plane's expected value, which costs one array and no extra raycasts.
- **Percentage-of-cone cannot express shape.** Everything above is a count; whether a human separates "wall" from "pillar" from "corner" by brightness alone is the readability session, and no number in this file answers it.

## The player's art and motion

The character is four stills from `Assets/Art/Sprites/Player/` and a motion computed in `PlayerVisual`. The knobs below are `[SerializeField]` on that component (the `TouchMarks` pattern), not fields of the profile. Pixels are at the 1080p target, where one world unit is 90 px.

| Knob | Value | What it is | Too high | Too low |
|---|---|---|---|---|
| `spritePixelsPerUnit` (importer) | `640` | The 632 px-tall drawing becomes `0.99` world units = **89 px** on screen — the same footprint the 16 px placeholder dot had at PPU 16, so no room, camera or readability number in this file had to be re-read | The player towers over the furniture it is hiding behind | The character is a sticker and the poses stop being legible |
| `breatheAmplitude` | `0.012` | Idle vertical scale swing: ±1 px. Deliberately under rule 4's 4-pixel floor — the breath is there so a still frame is not frozen, and it must never compete with the wave for the eye | The body pumps, and the player watches themselves instead of the room | Indistinguishable from a static sprite, so the knob is a lie |
| `breatheHz` | `0.4` | One breath every 2.5 s | Hyperventilating | Reads as a slow slide rather than a breath |
| `bobHeight` | `0.03` | Hop at the top of a stride: 2.7 px | The player bounces, which reads as cartoon and hides the lean | The step is only audible, not visible |
| `landSquash` | `0.02` | How far the body dips on the frame a footstep lands (1.8 px), so the hop has a bottom to it | The character collapses | The step lands silently in the picture |
| `leanDegrees` | `3` | Tip into the direction of travel, at `walkSpeed` | The player is skiing | No direction of travel in the silhouette |
| `leanReferenceSpeed` | `2.5` | The `walkSpeed` this lean was tuned against; it saturates there, so a future speed change cannot tip the character further. Re-tune if `walkSpeed` moves | — | — |
| `calmSeconds` | `0.2` | Seconds of holding Shift for **all four** motion terms to reach zero together. This is the animation saying "you are being quiet", which is the thing the game teaches, so it is locked by `PlayerVisualTests.HoldingShift_FlattensEveryMotionWithinTheCalmWindow` rather than by taste | The character keeps bouncing while silent, and Shift has no visual meaning | It snaps, and reads as a glitch rather than a decision |
| `MoveDeadzoneSqr` | `0.01` (0.1 m/s) | Below this the pose comes from the mouse aim, not the velocity. Same threshold `Player.UpdateFootsteps` uses to decide whether the step timer runs at all; `PlayerFacingTests.TheMoveDeadzone_IsTheFootstepThreshold` pins the two | Walking repositions the camera-facing pose too eagerly | The character flickers between poses on a key release |

What the assets themselves are, measured rather than assumed: the drawn figure occupies `572 × 632` px of a 1024 px canvas whose centre lands at pixel `(512, 503)`, so a **centre pivot needs no transform offset**; `Player_side_left` and `Player_side` are **exact pixel mirrors** (a flop-compare returns 0 differing pixels), so facing left is one `flipX` rather than a second texture (2.67 MB each at runtime: 1024² DXT5, 11 mip levels); and `Player_34_front` / `Player_34_back` differ from `front`/`back` by ~22% of their content, which is why they are imported with the correct settings but **unwired** — they belong to a 3/4 camera the game does not have yet.

The pose rule and the step phase are pure (`PlayerFacing`), which is what makes them testable at all: 12 EditMode cases cover the four sectors, every degree of the sweep, the ±45°/±135° edges, the `Atan2` wrap and the four `StepPhase` states; 14 PlayMode cases cover sprite selection, the mirror convention, the Shift flattening, the bounce being locked to `Player`'s own footstep clock, and the guarantee that a player with no `PlayerVisual` at all still runs a clean frame.

The motion was verified in `Level02_Corridor` in Play mode rather than only in the suite: walking right holds `localRotation.z = -0.0262` (the 3° lean) with `localScale = (1.010, 0.980)` (the landing dip), and the same frame with Shift held reads exactly `(0, 0, 0)` / identity / `(1, 1, 1)` while the pose still tracks the walk. The arcs draw over the body — `Visuals` is on `Default` (id 0) and `EchoField` on `Echo` (id 1).

**Open, and only a playtest closes it:** the dot was a flat teal disc at alpha 0.35; the character is a near-black body with a cyan rim and an eye at full brightness. The silhouette is the same size by construction, but the bright area is far smaller and the dark body is a different class of object from the faint cyan geometry the room is drawn with. "Is the player now too readable, or not readable enough?" is not answerable from these numbers.


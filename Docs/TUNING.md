# Wave settings and balancing (TUNING)

Every number in this table is live in `Assets/Data/Echo/EchoTuning.asset`, i.e. `Assets/Scripts/Runtime/Data/EchoTuningProfile.cs`. None of them are hard-coded in C#, so changing the balance needs no compile and can be tested in the editor with Play.

## Non-negotiable rules

1. `waveSpeed >= 1.6 * maxPlayerSpeed` — if the arc travels slower than 1.6 times the player's speed, the returning arc can never overtake the player and the "returning" feeling dies. With the default player speed `3.2` in `PlayerMock`, the floor for `waveSpeed` becomes `5.12`; the default `7` sits with a safety margin.
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
| `gain0` | `1` | `A_0` in the formula: how much the hit distance deforms the arc | `0..2` | Object shapes are exaggerated and read faster, the ring is no longer a circle | The arc stays nearly circular and material/shape are lost |
| `gainSign` | `+1` | Sign of the deformation | `+1 / -1` | `-1` makes the wave look like emission rather than return; only for a 5-second test | — |
| `innerDecay` | `1` | Damping of `A_k` in the arc train (`A_k = gain0 * innerDecay^k`) | `0.1..1` | Inner arcs lose the shape | All arcs have the same shape and the train looks flat |
| `alphaCurve` | `1 → 0.75 → 0` | Opacity against normalised arc age | — | Long tail, cluttered | Early fade, the next ping feels lonely |
| `baseThickness` | `0.05` | Ribbon width in world units | `0.002..0.3` | Lines merge into each other, shapes are lost | Below `0.04` it disappears at the 1080p target |
| `arcColor` | `#7fe9f0` | Base cyan colour, before `_Hdr` | — | — | The HUD uses the same colour; tune wave-vs-interface contrast here |
| `loudnessThreshold` | `0.18` | Noise-gate threshold for an automatic ping | `0..1` | Every cough makes a ping | The player has to shout |
| `minPingInterval` | `0.35` | Minimum seconds between automatic pings | `>=0.05` | Shouting = a ping barrage and a blinded field | Shouting only makes one ping |
| `manualEchoLoudness` | `0.65` | Loudness of the manual `Space` echo (the only ping input until the microphone lands) | `0..1` | `Space` makes the microphone pointless | A player without a microphone loses |
| `maxLivePings` | `3` | Simultaneous pings in the pool | `1..4` | Stacked arcs, unreadable shapes | A large room cannot be gathered in one ping |
| `simulatedLoudness` | `0.7` | Constant loudness of the `Simulated` mode; only has an effect when `EchoDirector.sourceMode` is `Simulated`. The scene default is `ManualOnly`, where no sound source is created at all | `0..1` | — | — |

## `_Hdr` and Bloom

The glow comes from two places: the `_Hdr` multiplier in the material `Assets/Art/Materials/Arc_Cyan.mat` (shader `Assets/Art/Shaders/EchoArc.shader`) and Bloom's `threshold` in `Assets/Settings/EchoVolumeProfile.asset`. For the cyan line to actually produce light you need:

```
arcColor(channel max) * _Hdr * shape(≈1)  >  bloom.threshold
```

With `arcColor ≈ 0.914` and `_Hdr = 2.5` the output is about `2.28`, which passes the threshold of `0.55`. If the line looks dark and unlit, do not raise `_Hdr` first: check the camera's Post Processing tick and the `Echo` layer first (see `Docs/PLAN.md`, "if the ring was not visible").

## The week 1 test scene

`Sandbox.unity` has three test bodies, all on the `EchoTarget` layer and with no SpriteRenderer: the pillar (`CircleCollider2D` r `0.6` at `(3, 0)`), the flat wall (`BoxCollider2D` `4×0.3` at `(0, 5)`) and the box for the "corner point" test (`BoxCollider2D` `1.2×1.2` at `(-3.5, 2.2)`). Ping with Space and ask the player what they see from the arcs alone.

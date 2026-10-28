# Seen issues (ISSUES)

Defects and design gaps observed **in play**, in this editor or by a tester, that are not yet fixed.
This file is the queue: `Docs/PLAN.md` schedules weeks, `Docs/QA_CHECKLIST.md` is what a tester walks, and
`Docs/PLAYTEST_REPORT.md` records what was observed. An issue enters here with a number attached, and it
leaves only into a commit that says so. Anything here without a pixel, a millisecond or a count is a
impression and gets moved back to the report.

| # | Issue | Severity | Status | Lands |
|---|---|---|---|---|
| 1 | A touch mark is drawn **inside the character** instead of where the body meets the wall | High — it is the game's primary "you touched something" cue and it currently points at the player | open, fix is decided but not built | week 3, before any further polish |
| 2 | Four poses are too coarse: the face can be a quarter-turn off the direction the echo actually travels | Medium-high — art-team lead time, so the request must go out now | open, needs a decision from the art team | week 3 if the stills arrive, week 4 otherwise |
| 3 | The pose **flickers** on a sector boundary, with no hysteresis and no dwell anywhere | Medium — cosmetic, but it is the thing a player notices first and it doubles with issue 2 | open | week 3, and it is a *prerequisite* of issue 2 |

---

## 1 — The touch mark lands inside the character, not on the wall

**Seen:** 2026-10-28, in `Level02_Corridor`, by the lead, walking into a wall. The dim dash that is
supposed to say "here is a surface, you are touching it" appears on top of the character's body.

**Mechanism, and it is two faults stacked:**

1. **The mark is placed at the collider's surface, and the collider is about half the size of the drawn
   body.** `Player.OnCollisionEnter2D` flashes at `collision.GetContact(0).point`
   (`Assets/Scripts/Runtime/Gameplay/Player.cs:214-217`), which for a `CircleCollider2D` of radius `0.25`
   is exactly `0.25` m from the player centre. The art is `572 × 632` px at `spritePixelsToUnits = 640`,
   i.e. `0.89 × 0.99` world units, so its silhouette reaches `0.447` m sideways and `0.494` m up-down from
   the centre. The mark's centre therefore sits **0.197–0.244 m inside the visible body — 18 to 22 screen
   pixels at the 1080p target**, which is not a rounding error by any rule in `TUNING.md`.
2. **The mark is drawn on the layer that sorts above the body.** `TouchMarks` puts every dash on `Echo`
   (`Assets/Scripts/Runtime/Gameplay/TouchMarks.cs:51`), and `Echo` (id 1) sorts above `Default` (id 0),
   which is where `Visuals` sits — verified the same day, because it is what lets the arcs read over the
   character. So the dash is not merely close to the body, it is *composited on top of it*. The dash is
   also pivot-centred and `0.7 × 0.09` units (`TouchMarks.cs:21`), so half of its 63-pixel length extends
   along the wall through the body as well.

**Why it was never reported before today:** the offset is not new — the `0.25` collider has always been
smaller than the drawn figure, which used to be a 16 px cyan disc at alpha `0.35`. A translucent glow with
no silhouette cannot contain a thing, so a dim dash landing inside it read as "a soft glow at the wall".
The opaque character turned a latent mismatch into a visible bug. That is worth stating because the
mismatch itself is the real defect, not the art: **the hitbox is `0.5` m across and the character is
drawn `0.9` m tall.**

**Fix — the mark must be placed on the wall, not on the hitbox.** `Collider2D.ClosestPoint(Vector2)`
exists in this Unity version (confirmed by reflection over `UnityEngine.Collider2D`) and returns the
nearest point on the *other* collider to a supplied point, so `collision.collider.ClosestPoint(body.position)`
lands on the wall face. The candidate, in order of preference:

| | Change | Cost |
|---|---|---|
| **a (recommended)** | Flash at the closest point **on the wall's collider**, from the player's body centre. No per-pose number, nothing to re-measure when the art changes, and it matches the GDD's semantics — a touch mark belongs to a place on a wall, not to the player | One extra physics query per bump, on contact only. It also makes the mark sit *behind* the silhouette at the near edge, which is correct: the wall is where the wall is |
| b | Offset `contact.point` outward by `silhouetteHalfWidth − 0.25` along the normal | Needs a per-pose half-width constant (a profile is thin, a front is wide), so it silently goes wrong every time the pose rule or the art changes |
| c | Move the dash pivot to its inner edge so it extends into the wall instead of straddling the contact | Half-fixes it: the near end still starts `0.2` m inside the body |
| d | Drop marks to the `Default` layer so the body hides them | Hides the symptom and hides the mark whenever the player stands against the wall, which is exactly when it is needed. Reject |
| e | Grow the collider to match the art | This is a **balance change, not a fix**: the hitbox feeds the room's gaps, the alcove pocket and the crawler's `ρ`-based hearing geometry. Reject for this issue; raise it separately if the hitbox ever needs to be honest |

**Acceptance:** a PlayMode case that walks the rig player into `Wall_West` and asserts the active mark's
distance from the player centre is **≥ 0.44 m** (the smaller of the two silhouette half-extents, `572/2/640`),
plus one capture against a wall in which the dash visibly sits on the wall face and no part of it falls
inside the character's torso. Rule 4 of `TUNING.md` applies: the mark's offset has to be worth more than
4 pixels or it is not a fix, it is a commit.

---

## 2 — Four poses cannot aim a 64° cone

**Seen:** 2026-10-28, in play, and it is a design consequence rather than a bug. The manual echo is a
**64° cone** steered by the mouse (`EchoTuningProfile.coneHalfAngleDeg = 32`, and `PlayerVisual` reads
`player.AimRadians`, which is the same smoothed number that aims it — `Player.cs:107`). Four poses cover
four 90° sectors, so the face can point up to **45° away from the axis the sound will actually travel on**
while being "correct" for its sector. The character is therefore the worst available indicator of the thing
the character is doing, and a player who reads direction off the body is systematically misled near sector
corners.

**Decision wanted: eight poses (four cardinals + four diagonals), sectors 45° wide, worst-case error ±22.5°.**

What that costs, in the order that matters:

- **Art: two new drawings, not four — and this has to be checked with the art team before it is promised.**
  The profiles arrived as **exact pixel mirrors** (a flop-compare between `Player_side_left.png` and
  `Player_side.png` returns **0 differing pixels**), so if the same symmetry holds for the diagonals,
  `up-right` mirrors to `up-left` and `down-right` mirrors to `down-left`: two stills, four runtime poses
  and one `flipX`. That symmetry is an assumption about art that does not exist yet, so the request must
  name it. It also buys back the standing limitation: if the team ever draws the two sides *differently*
  (a shield, a lantern), `flipX` stops being free and becomes a code change.
- **Not to be confused with the two `Player_34_*` files already imported.** Those are ~22 % different from
  `front`/`back` and belong to a 3/4 *camera*, not to a top-down diagonal. They must not be handed over as
  "the diagonals". They stay unwired until that camera exists.
- **Code:** `PlayerPose` grows from 4 to 8; `PlayerFacing.Resolve` gets eight sectors with the boundaries at
  ±22.5° and ±67.5° instead of ±45°/±135°; `PlayerVisual` gets **two** new sprite slots (the up-diagonal and
  the down-diagonal) and reaches the other two by the same `flipX` the side profile already uses;
  `SceneKit` wires them and must keep falling back to the dot when any is missing — the rule that a scene
  without the new art still boots; and the whole `PlayerFacingTests` sector table, including the 1° sweep,
  is rewritten for eight. Expect roughly double the pose cases, not four new ones.
- **It makes issue 3 worse if issue 3 is not fixed first:** eight sectors is eight boundaries, so twice as
  many places for a pose to flicker. **Issue 3 is a prerequisite.**

**Acceptance:** the worst-case angle between the drawn face and the cone axis is **≤ 22.5°**, provable as a
pure-function case over the same 1° sweep that already exists (`TheFourCardinals…` family), and one capture
per diagonal pose. If the art arrives *without* the mirror symmetry, the acceptance adds four textures and
the memory line goes into `TUNING.md`.

---

## 3 — The pose flickers on a boundary

**Seen:** 2026-10-28, in play, walking along a diagonal and standing still with the cursor near the body.

**Mechanism — four things, none of them individually surprising:**

1. **No hysteresis anywhere.** `PlayerFacing.Resolve` compares `|dir.y|` to `|dir.x|` on a single line, so
   a heading of exactly 45° is a coin toss on the number line, and the `1e-6f` tolerance makes the tie go
   vertical rather than making the boundary sticky. The `DESIGN_DECISIONS.md` entry that chose this said
   hysteresis belongs in `PlayerVisual` and never in the pure function, so that it stays testable. That
   promise has not been kept because no hysteresis has been written.
2. **A WASD diagonal is *exactly* a tie**, `|vx| = |vy|`, by construction. This is not an edge case of the
   input space, it is half of the directions a player can walk in, and every such step lands on the
   boundary the resolver has to break.
3. **The cursor near the player amplifies jitter into rotations.** `aimRad = atan2(dir.y, dir.x)` of
   `cursor − player`, so at 10 px of separation one pixel of cursor movement is ~5.7°, and at 3 px it is
   ~19°. Standing still with the pointer parked on the character — the normal resting state — the pose is
   driven by sub-pixel noise.
4. **The movement/aim handover is instant and has no dwell.** The pose switches the frame
   `body.linearVelocity.sqrMagnitude` crosses `MoveDeadzoneSqr = 0.01` (0.1 m/s), which key release,
   collider damping and the walk/Shift speed change all cross constantly. `calm` already eases the
   *motion* terms over `calmSeconds = 0.2`; nothing eases the *pose*.

**Fix, all of it inside `PlayerVisual` (the pure function stays boundary-deterministic):**

| | Change | Notes |
|---|---|---|
| **a** | **Angular dead band.** Once a pose is chosen, the heading must clear it by ~5–10° before a neighbour is allowed. A 4-sector map with ±10° hysteresis leaves 20° of overlap at each boundary, which is a sane amount of stickiness; for 8 poses, halve it to ~5° | Two thresholds per edge instead of one; the test is "a boundary walker stops flicking", not "the resolver is clever" |
| **b** | **Minimum dwell.** A pose may not change more often than every ~0.1 s | Kills the class wholesale, including the cursor-noise case, at the cost of 100 ms of response. Tunable, and it is presentation, so it lives on the component and not in `EchoTuningProfile` |
| **c** | **Ignore aim inside a small cursor radius.** Below roughly 24 px (~0.27 m) of pointer-to-body separation, keep the last pose instead of resolving the aim | Targets cause 3 specifically, which a and b only damp. It is a *new number* and needs its own row in `TUNING.md` |
| d | Fade or ease the pose change | Rejected: with mirrored stills there is no in-between frame to show, so this reads as a glitch, not a turn |

**Acceptance:** a PlayMode case that parks the aim exactly on a sector boundary, jitters it by ±1 px for 30
frames and asserts the pose changed **at most once**; and a case that walks a perfect WASD diagonal for 2 s
and asserts the pose is **stable after the first frame**. Until both exist, "eight poses" (issue 2) stays
blocked — it doubles the boundary count, and shipping it first makes the visible defect worse.

---

## How an issue in this file gets closed

Name it in the commit subject, replace the status row with the commit hash, move the numbers that changed
into `TUNING.md`, and if the fix closed a path, add the dated entry to `DESIGN_DECISIONS.md` with the cost
it accepted. An issue is not closed by a code change alone — issues 1 and 3 close on their captures, issue 2
closes on eight poses visible in the room.

# Seen issues (ISSUES)

Defects and design gaps observed **in play**, in this editor or by a tester, that are not yet fixed.
This file is the queue: `Docs/PLAN.md` schedules weeks, `Docs/QA_CHECKLIST.md` is what a tester walks, and
`Docs/PLAYTEST_REPORT.md` records what was observed. An issue enters here with a number attached, and it
leaves only into a commit that says so. Anything here without a pixel, a millisecond or a count is a
impression and gets moved back to the report.

| # | Issue | Severity | Status | Lands |
|---|---|---|---|---|
| 1 | A touch mark is drawn **inside the character** instead of where the body meets the wall | High — it is the game's primary "you touched something" cue and it currently points at the player | **closed** — mark flashes 0.486 m from the body centre, `Captures/markfix_wall_west.png` | week 3, done 2026-10-28 |
| 2 | Four poses are too coarse: the face can be a quarter-turn off the direction the echo actually travels | Medium-high — it is the aim indicator the player reads direction off | open, **decided 2026-10-29**: eight poses from the six files in the repo, no new art | week 3, after issue 3 |
| 3 | The pose **flickers** on a sector boundary, with no hysteresis and no dwell anywhere | Medium — cosmetic, but it is the thing a player notices first and it doubles with issue 2 | open | week 3, and it is a *prerequisite* of issue 2 |
| 4 | The character is **drawn wider than it collides**, so it visibly sinks into walls, and the touch mark and the echo arc disagree about where the surface is | Medium — it is the root cause issue 1 worked around, and it decides whether the alcove pocket and the corridor gaps read honestly | open, needs a decision (art scale, hitbox growth, or accept) | week 3 as a decision, week 4 as a change |
| 5 | The new 4096 art **is not mirror-symmetric**, so the `flipX` that faces the character left draws it lit from the wrong side | Medium — the left-facing pose is visibly inconsistent with the file the art team delivered for it, and issue 2's eight-pose plan was priced on the old symmetry | **closed** — the delivered left profile is wired and drawn unflipped, `Captures/issue5_left_profile.png` | week 3, done 2026-10-29 |

---

## 1 — The touch mark lands inside the character, not on the wall

**Seen:** 2026-10-28, in `Level02_Corridor`, by the lead, walking into a wall. The dim dash that is
supposed to say "here is a surface, you are touching it" appears on top of the character's body.

**Mechanism, and it is two faults stacked:**

1. **The mark is placed at the collider's surface, and the collider is about half the size of the drawn
   body.** `Player.OnCollisionEnter2D` flashed at `collision.GetContact(0).point`
   (`Assets/Scripts/Runtime/Gameplay/Player.cs:214-217`), which for a `CircleCollider2D` of radius `0.25`
   is exactly `0.25` m from the player centre. The art is `2289 × 2528` drawn pixels of a 4096 canvas at
   `spritePixelsPerUnit = 2560`, i.e. `0.89 × 0.99` world units, so its silhouette reaches `0.447` m
   sideways and `0.494` m up-down from the centre. The mark's centre therefore sits **0.197–0.244 m inside
   the visible body — 18 to 22 screen pixels at the 1080p target**, which is not a rounding error by any
   rule in `TUNING.md`.
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

**Fix — the mark must be placed on the wall, not on the hitbox.** The first candidate written here was
`collision.collider.ClosestPoint(body.position)`, on the theory that it lands somewhere better than the
contact point. **It does not, and the measurement says so:** for a `CircleCollider2D` against a flat box
face, the contact point *is* the nearest point on the wall, 0.25 m from the body centre, so `ClosestPoint`
returns the same pixel and the "fix" is a no-op with a physics query attached. The fault is not which
surface point is chosen — both candidates are the same point — it is that the surface point is inside the
drawn figure.

What shipped is the offset form, with the test as the guard that the original note wanted none of:

| | Change | Cost |
|---|---|---|
| **b (shipped)** | `TouchMarks.silhouetteClearance = 0.24` m, applied along the line from the body centre through the contact point, so the dash's near edge lands on the silhouette rather than on the chest | One number that has to be re-measured when the art or the hitbox changes — which is exactly what `TouchMarkPlacementTests` and `PlayerPoseImportTests` now exist to catch. The direction comes from `contactPoint − bodyCenter`, not from `contact.normal`, because that is true for a flat wall, a corner and a rotated box at once, and it does not depend on which way the physics normal happens to point |
| a | `ClosestPoint` from the body centre | **No-op**, as measured above |
| c | Move the dash pivot to its inner edge so it extends into the wall instead of straddling the contact | Half-fixes it: the near end still starts `0.2` m inside the body |
| d | Drop marks to the `Default` layer so the body hides them | Hides the symptom and hides the mark whenever the player stands against the wall, which is exactly when it is needed. Reject |
| e | Grow the collider to match the art | This is a **balance change, not a fix**: the hitbox feeds the room's gaps, the alcove pocket and the crawler's `ρ`-based hearing geometry. Reject for this issue; it is now **issue 4** instead, where the mismatch itself is the thing to decide on |

**Acceptance:** a PlayMode case that walks the rig player into a wall and asserts the active mark's
distance from the player centre is **≥ 0.44 m** (the smaller of the two silhouette half-extents), plus one
capture in which the dash visibly sits clear of the character's torso. Rule 4 of `TUNING.md` applies: the
mark's offset has to be worth more than 4 pixels or it is not a fix, it is a commit.

**Closed 2026-10-28** (`fix(marks): put the touch mark where the drawn body meets the wall`, see
`Docs/TUNING.md` for the number). Measured, not asserted: the rig player touching a `Wall_West`-sized face
flashes its dash **0.486 m** from the body centre — the torso edge is 0.447 m, so the near edge of the dash
lands within 4 px of the silhouette and no part of it is on the chest. `Captures/markfix_wall_west.png`
shows both placements in one frame: the collision's own mark outside the body, and the old raw-contact
placement running through the torso. What the fix does *not* settle is why the two positions differ at all
— that is issue 4.

---

## 2 — Four poses cannot aim a 64° cone

**Seen:** 2026-10-28, in play, and it is a design consequence rather than a bug. The manual echo is a
**64° cone** steered by the mouse (`EchoTuningProfile.coneHalfAngleDeg = 32`, and `PlayerVisual` reads
`player.AimRadians`, which is the same smoothed number that aims it — `Player.cs:107`). Four poses cover
four 90° sectors, so the face can point up to **45° away from the axis the sound will actually travel on**
while being "correct" for its sector. The character is therefore the worst available indicator of the thing
the character is doing, and a player who reads direction off the body is systematically misled near sector
corners.

**Decision (2026-10-29, the lead): eight poses — four cardinals + four diagonals — built from the files already
in the repo, with no new art requested.** `Player_front`, `Player_back` and `Player_side` stay the cardinals,
and the two `Player_34_*` stills become the diagonals, each reaching its mirror with `flipX`: `34_front` for
the two downward diagonals, `34_back` for the two upward ones. Sectors go to 45° wide, worst-case error ±22.5°.

What that costs, in the order that matters:

- **The lighting does not mirror, and that is now measured rather than assumed.** This section used to price
  the diagonals on the fact that the two delivered profiles were **bit-exact mirrors** (0 differing pixels),
  which bought four poses from two stills. **The 4096 re-delivery broke it** — see issue 5: the artist keeps
  the rim light and the eye on a fixed side instead of mirroring them with the body, so a flop-compare of the
  new pair leaves 35 % of pixels differing by more than 8 per channel. The same fixed-lighting habit shows in
  the 3/4 stills: each is **25 % different from its own mirror**. So `flipX` here is not a free bit-exact
  reproduction of a delivered image, it is a pose lit from the opposite side to the rest of the set. Accepted
  deliberately, because the alternative is no diagonals this week, and recorded here so that when the art team
  sees it they know which two files to redraw rather than being handed a bug report about a boolean.
- **The two `Player_34_*` files change meaning.** They were imported as a 3/4 *camera*'s poses and left
  unwired; they are now the top-down diagonals. That is a real reading of the drawings — a 3/4 front *is*
  "facing down-right" — but it means the 3/4 camera discussion has to start from scratch when it happens,
  because the files it was going to reuse are now load-bearing for something else.
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
per diagonal pose. The memory line goes into `TUNING.md`: eight runtime poses from six 1024² DXT5 mip-chained
textures, ~2.67 MB each.

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

## 4 — The character is drawn wider than it collides

**Seen:** 2026-10-28, while measuring issue 1. It is not a new defect — it is the thing issue 1 was a
symptom of — and it is listed separately because fixing it properly is a balance decision, not a
presentation patch, and because the fix for issue 1 has to be *un*-written if this one is ever closed.

**The measurement.** `CircleCollider2D` radius `0.25` ⇒ the hitbox is `0.5` m across. The pose art is
`2289 × 2528` drawn pixels at PPU `2560` ⇒ the figure is `0.89 × 0.99` world units. Standing against a wall
the body centre stops `0.25` m from the surface while the drawn shoulders reach `0.447` m, so **the
character is embedded `0.197` m — 18 screen pixels at the 1080p target — into every wall it touches.**

**What that costs, in the order it shows up:**

- **The two statements about a surface disagree.** An echo arc is drawn where the *collider* is; issue 1's
  fix moved the touch mark to where the *art* is. Both are cyan, both appear on the same wall, and they sit
  `0.24` m apart. `Captures/markfix_wall_west.png` is the evidence: the dash and the arc line are
  unambiguously two different surfaces.
- **Gaps lie.** The corridor, the alcove pocket and the corner box were sized for a `0.5` m body, so the
  player can slip through spaces the drawing says are too narrow, and the silhouette overlaps props it is
  not touching.
- **The crawler's `ρ` thresholds are centre-based**, so this one is *not* affected by the mismatch — worth
  saying, because it is the number people expect to break.

**The three ways out, with their price:**

| | Change | Price |
|---|---|---|
| f | Redraw or rescale the art to the hitbox (PPU up, figure down) | The character goes from 89 to ~50 screen pixels tall and the eyes — the only thing that reads at distance — go with it. This is a readability change on the game's silhouette, so it needs the playtest, not the editor |
| g | Grow the collider toward `0.447` | A **balance change**: every gap in both blockouts was measured against `0.5`, and `TouchMarks.silhouetteClearance` goes to zero. Needs a pass through `Docs/TUNING.md` rule 4 on each room |
| h | Accept it, as most 2D games do | Free, but then the *arc* should be the one that moves to agree with the mark, and the mark's clearance should be re-checked against wall thickness: at `0.24` m the dash already sits inside a `0.4` m wall, and a thinner prop would show it through the far side |

**Acceptance:** whichever way is chosen, one capture of a body against `Wall_West` with an echo in flight
in which the arc and the touch mark are on the same line, and `Docs/TUNING.md` updated so the hitbox, the
silhouette and the clearance are three numbers that add up instead of three that are argued about.

---

## 5 — The new art is not a mirror, so `flipX` faces a character lit the wrong way

**Seen:** 2026-10-29, while re-measuring the pose files after the 4096 re-delivery, to check whether the
number issue 2 was priced on still held. It does not.

**The measurement.** `Player_side_left.png` is compared against `flip(Player_side.png)` over the content box
(`1514 × 2610`, sampled every 4th pixel, max over the four channels): **35.1 % of samples differ by more than
8, mean difference 31.9**. The 1024 originals were bit-exact — 0 differing pixels — which is the fact
`DESIGN_DECISIONS.md`'s 2026-10-28 entry and the old `TUNING.md` line were written against.

It is not a precision artefact. The difference image is structured: the **eye glow**, the **top rim arc** and
the **two small speculars** are all on the opposite side in the delivered file from where a mirror puts them.
Confirmed independently by self-mirror asymmetry per file — `Player_front` differs from its own mirror by
8.1 % of samples, `Player_34_front` and `Player_34_back` by 25 %, `Player_side` by 35 %. A symmetric drawing
would score near zero; the profiles scoring highest is the signature of **fixed lighting**: the artist keeps
the light where it is in the room and redraws the body, rather than producing a mirror.

**What is wrong in the game right now:** `PlayerVisual.ApplyPose` renders the left-facing pose as
`sideSprite` with `flipX = true` (`Assets/Scripts/Runtime/Gameplay/PlayerVisual.cs:134-137`) and
`Player_side_left.png` is imported, configured and **unwired**. So walking left shows a character whose eye
and rim light are on the side the art team decided they are not on. It is one texture load away from correct,
and the 2.67 MB the mirror was saving is already spent — the file is in the repo and imported either way.

**Fix:** a fourth `Sprite` slot on `PlayerVisual` for the delivered left profile, drawn with `flipX` false,
`SceneKit` wiring it, and the `PlayerVisualTests` mirror case rewritten to assert the opposite — that facing
left shows its own texture and does not flip. The slot is optional rather than required: a scene built
without the file falls back to the flip, because losing the character is worse than a wrongly-lit one, and
that fallback is itself a test case. The `Player_side_left` file stops being dead weight and the "if the team
ever draws the two sides differently, `flipX` becomes a code change" clause in the 2026-10-28 decision entry
is now the live case.

**What this does *not* fix:** issue 2's diagonals still ship on `flipX`, because there is no other way to get
eight poses from six files. That is accepted with its eyes open in issue 2's text, and the honest ask to the
art team — whenever they next touch this character — is two more 3/4 stills, mirrored in *lighting* rather
than in shape.

**Acceptance:** one capture of the character walking left, next to a capture of the delivered
`Player_side_left.png`, in which the eye is on the same side in both.

**Closed 2026-10-29.** `PlayerVisual` gained a `sideLeftSprite` slot, `SceneKit` wires
`Assets/Art/Sprites/Player/Player_side_left.png` into it, and both blockouts serialise the reference. The
left-facing pose now draws the delivered texture with `flipX = false` — verified in Play mode in
`Level02_Corridor` (`pose=ProfileLeft sprite=Player_side_left flipX=False`) and in
`Captures/issue5_left_profile.png`, where the in-game body and the source file agree on which side the eye
and the rim light are on. The slot is **optional**: a scene wired without the file falls back to the old
flip rather than losing its character, which is the same rule that keeps the placeholder dot alive, and
`WithoutTheLeftProfile_FacingLeft_FallsBackToTheFlippedSide` is the case for it.

---

## How an issue in this file gets closed

Name it in the commit subject, replace the status row with the commit hash, move the numbers that changed
into `TUNING.md`, and if the fix closed a path, add the dated entry to `DESIGN_DECISIONS.md` with the cost
it accepted. An issue is not closed by a code change alone — issues 1 and 3 close on their captures, issue 2
closes on eight poses visible in the room.

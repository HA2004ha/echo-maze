# Seen issues (ISSUES)

Defects and design gaps observed **in play**, in this editor or by a tester, that are not yet fixed.
This file is the queue: `Docs/PLAN.md` schedules weeks, `Docs/QA_CHECKLIST.md` is what a tester walks, and
`Docs/PLAYTEST_REPORT.md` records what was observed. An issue enters here with a number attached, and it
leaves only into a commit that says so. Anything here without a pixel, a millisecond or a count is a
impression and gets moved back to the report.

| # | Issue | Severity | Status | Lands |
|---|---|---|---|---|
| 1 | A touch mark is drawn **inside the character** instead of where the body meets the wall | High — it is the game's primary "you touched something" cue and it currently points at the player | **closed** — mark flashes 0.486 m from the body centre, `Captures/markfix_wall_west.png` | week 3, done 2026-10-28 |
| 2 | Four poses are too coarse: the face can be a quarter-turn off the direction the echo actually travels | Medium-high — it is the aim indicator the player reads direction off | **closed** — eight poses from the five files in the repo, worst-case face-to-cone error **22.5°**, the 1° sweep rewritten for eight sectors, four diagonal captures | week 3, done 2026-10-29 |
| 3 | The pose **flickers** on a sector boundary, with no hysteresis and no dwell anywhere | Medium — cosmetic, but it is the thing a player notices first and it doubles with issue 2 | **closed** — three gates in `PlayerVisual`: a 10° band, a 0.1 s dwell and a 0.27 m cursor radius, each with its own case | week 3, done 2026-10-29, and it unblocks issue 2 |
| 4 | The character is **drawn wider than it collides**, so it visibly sinks into walls, and the touch mark and the echo arc disagree about where the surface is | Medium — it is the root cause issue 1 worked around, and it decides whether the alcove pocket and the corridor gaps read honestly | open, needs a decision (art scale, hitbox growth, or accept) | week 3 as a decision, week 4 as a change |
| 5 | The new 4096 art **is not mirror-symmetric**, so the `flipX` that faces the character left draws it lit from the wrong side | Medium — the left-facing pose is visibly inconsistent with the file the art team delivered for it, and issue 2's eight-pose plan was priced on the old symmetry | **closed** — turned out to be an export mistake, not a design decision: the lead re-exported the left profile and the pair is **bit-exact again (0 differing pixels)**, so the slot that had shipped for a day is reverted, `Player_side_left.png` is deleted, and `flipX` stands | week 3, done 2026-10-29 |

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

- **The profiles mirror; the 3/4 stills do not, and that is now measured rather than assumed.** The side
  profile is a free second pose — the flop-compare of `flip(Player_side)` against the left file the art team
  delivered returns **0 differing pixels** (issue 5, closed: the version that did not mirror was an export
  mistake, corrected by hand on 2026-10-29). The `Player_34_*` pair is a different case: each still differs
  from **its own mirror** in **~25 % of the pixels of its content box**, three times the front pose's 8 %,
  because the artist keeps the rim light and the eye on a fixed side instead of mirroring them with the body.
  So `flipX` on the diagonals is not a bit-exact reproduction of a delivered image — it is the same body
  lit from the opposite side. Accepted deliberately, because the alternative is no diagonals this week, and
  recorded here so that when the art team sees it they know which two files to redraw rather than being
  handed a bug report about a boolean.
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
per diagonal pose. The memory line goes into `TUNING.md`: eight runtime poses from five 1024² DXT5
mip-chained textures, ~2.67 MB each, three of them carrying a flipped half.

**Closed 2026-10-29 exactly as decided: eight poses, five files, no art requested.** The sector map is written
as a slope comparison against `tan(22.5°)` — `y <= x * limit`, then `x <= y * limit`, then the two signs —
rather than an `Atan2` and a modulo, so it is exactly scale-invariant and never has to meet the ±π wrap. The
consequence nobody predicted when the issue was written: **a held WASD diagonal is now a sector *centre***,
not a boundary. The "vertical wins ties" rule and its `1e-6f` epsilon have nothing left to decide, and the
only remaining edges sit at 22.5°, 67.5°, 112.5° and 157.5°, which no keyboard combination can produce. An
edge that *is* hit exactly — only a mouse or an analog stick can — stays with the **cardinal**, the pose whose
file was drawn straight-on rather than one that is only ever seen flipped.

The ≤ 22.5° claim is measured, not asserted. `EveryDegree_MatchesTheSectorTable` walks 360 samples across
eight cells; `TheSectorEdges_SitAtTwentyTwoAndSixtySevenDegrees` puts a point either side of all four edges;
`EveryPose_OwnsTwentyTwoDegreesEitherSideOfItsOwnCentre` checks each pose from its own centre instead of from
a literal; `TheEightCentres_AreFortyFiveDegreesApartAndEachIsItsOwnNeighbour` proves the fan is regular; and
`AnEdgeExactlyOnTheSlope_BelongsToTheCardinal` pins the ownership rule with the only two vectors that are
float-exact on the line — `(1, tan 22.5°)` and `(tan 22.5°, 1)` — because the reciprocal form `(1, 2 + tan)`
would be asserting a coin toss.

**Issue 3's stated cost is retired by this.** The band went from 10° to 5° because `SectorHalfWidthDegrees`
went 45 → 22.5, which keeps the total dead angle where it was: a switch window of 27.5° off the current pose's
centre, 17.5° of responsive angle either side. It is a function of the pose count, not a taste, and the
bootstrap now *writes* it (`SceneKit.SetFloat` on `boundaryDegrees`) — a scene generated while the fan had
four poses still has 10 serialised in it, and `EnsureComponent<T>` returns the existing component rather than
a fresh one, so only the builder re-asserting the number can correct an old scene. The cursor-jitter case moved
onto a real edge (22.5°, ±2.4° = one pixel of shake at `aimNoiseRadius`) and still passes with at most one
change in 30 frames; the diagonal-walk case now settles on `DiagonalUpRight` and holds it, which is the whole
point of the issue.

`PlayerVisual` gained `diagonalDownSprite` and `diagonalUpSprite`, one `SpriteFor` switch, and one
`FacesLeft` list, so the set has a single convention — **every file is authored facing screen right** — and the
day the art team draws the turn properly it is one boolean per pose to revisit. The fallback is
all-or-nothing: five sprites or the dot, never a half-dressed character that freezes on a diagonal, guarded by
`APlayerVisualFromTheFourPoseEra_TurnsItselfOffSilently`. Four PlayMode cases assert pose, sprite *identity*
and `flipX` per diagonal, and the four captures (`Captures/pose_zoom_diag_*.png`) show them in the picture:
the mirrored diagonals do move the rim light and the eye, which is the accepted cost above rather than a
surprise. Eight poses now load from five files — ~13 MB on disk, ~13.4 MB at runtime, every one of them
resident in any scene that has a player. 77 EditMode and 44 PlayMode cases green.

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

**Closed 2026-10-29, with a, b and c shipped as written and d still rejected.** Three `[SerializeField]`
knobs on `PlayerVisual` — `boundaryDegrees = 10`, `dwellSeconds = 0.1`, `aimNoiseRadius = 0.27` m — applied in
the order the causes demand: the cursor radius is checked before anything is resolved (a heading read off a
pointer sitting on the character is not a measurement), the dwell before the band (it is the cheaper test, and
it also has to gate the handover, where the *source* of the heading changes without crossing anybody's
boundary). The pure resolver did not move: `PlayerFacing` gained `ResolveDirection` and `PoseCentre` so the
sticky layer can ask the same question about a heading it has already chosen and measure the band as an angle
between two vectors rather than as arithmetic on an `Atan2` result that has to handle the ±π wrap by hand.
`Player.AimDistance` is the second read-only getter on that file for the same reason the step clock ones are:
the visual needs a number, not a copy of the logic that made it.

Both acceptance cases exist, named for what they promise:
`ParkedOnASectorBoundary_CursorJitterForThirtyFrames_ChangesThePoseAtMostOnce` (±5.7° of jitter — the one
pixel the cause-3 measurement describes — held to at most one change in 30 frames) and
`WalkingASteadyDiagonal_ForTwoSeconds_HoldsOnePose` (120 frames, zero changes after the first). Two more
guard the gates from over-correcting: `AHeadingThatReallyLeftItsSector_TakesTheNewPose` proves a half-turn is
still drawn, so none of this is a character that has stopped turning, and `Dwell_BlocksASecondPoseInsideItsWindow`
proves the window is a real gate rather than a coincidence of frame rate. `PlayerFacingTests` pins the two new
helpers against the sector map they are measured from, so moving a boundary moves the band with it instead of
quietly desynchronising the two.

**The consequence, said out loud because it is a change of behaviour and not only a fix:** a held WASD diagonal
now shows **the pose you were already in** when you entered it, because the tie is exactly 45° off — inside the
band by construction. `PlayerFacing` still answers the tie with the vertical, and the drawing layer declines to
act on that answer until the heading has clearly committed. That is the whole point of hysteresis, but it does
mean the character disagrees with the pure rule for a quarter-rotation of input. Issue 2 retires the
disagreement rather than tuning it: eight poses put a real drawing at 45°, which moves the boundary to 22.5°
and this band to 5°, and the diagonal stops being a tie at all.

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

## 5 — The left profile arrived lit from the wrong side

**Seen:** 2026-10-29, while re-measuring the pose files after the 4096 re-delivery, to check whether the
number issue 2 was priced on still held. It did not hold — and the answer turned out to be an export
mistake rather than a design decision, which is the useful part of this record.

**The measurement.** `Player_side_left.png` compared against `flip(Player_side.png)` over the content box
(`1514 × 2610`, sampled every 4th pixel, max over the four channels): **35.1 % of samples differed by more
than 8, mean difference 31.9**. The 1024 originals had been bit-exact — 0 differing pixels — which is the
fact `DESIGN_DECISIONS.md`'s 2026-10-28 entry and the old `TUNING.md` line were written against.

It was not a precision artefact. The difference image was structured: the **eye glow**, the **top rim arc**
and the **two small speculars** were all on the opposite side from where a mirror puts them. Confirmed
independently by self-mirror asymmetry per file — `Player_front` differs from its own mirror by 8.1 % of
samples, `Player_34_front` and `Player_34_back` by ~25 %, `Player_side` by 35 %. A symmetric drawing scores
near zero; the profiles scoring highest looked like **fixed lighting**: light kept where it is in the room,
body redrawn, rather than a mirror produced.

**What was wrong in the game:** `PlayerVisual.ApplyPose` renders the left-facing pose as `sideSprite` with
`flipX = true`, so walking left showed a character whose eye and rim light were on the side the delivered
left file says they are not — while that file sat imported, configured and unwired, its 2.67 MB already spent.

**First fix, shipped and then withdrawn** (`eb7fd95`, `720b74d`, `0d96be1`): a fourth `Sprite` slot,
`sideLeftSprite`, drawn unflipped, wired by `SceneKit` into both blockouts, with the mirror test rewritten to
assert the opposite and an optional-slot fallback so a scene without the file still showed a body. Verified in
Play mode (`pose=ProfileLeft sprite=Player_side_left flipX=False`) and in `Captures/issue5_left_profile.png`.

**Closed 2026-10-29, by the file, not by the code.** The lead re-exported the correct drawing over it. The
version now in the tree is **bit-exact against `flip(Player_side)` — a difference image over the full canvas
returns no bounding box at all, 0 differing pixels** — so the second texture is redundant again and the flip
reproduces the drawing exactly. The slot is reverted, `sideLeftSprite` is gone from `PlayerVisual` and from
both scenes, and `Player_side_left.png` with its `.meta` are deleted: one profile file, two poses, 2.67 MB of
runtime texture and 3.6 MB of repo back. Suite green on the reversal: 71/71 EditMode, 34/34 PlayMode.

**The condition that reopens this, and where to change it:** the mirror is free only while the two sides are
drawn the same. A shield, a lantern, or a light that stays fixed while the body turns ends it — this issue is
exactly that case arriving by accident. `WalkingLeft_ReusesTheSameTextureFlipped` in
`Assets/Tests/PlayMode/PlayerVisualTests.cs` is the seam: it asserts the flip, so the day the art stops
mirroring, that case is where the second texture comes back.

**What this does *not* settle:** issue 2's diagonals still ship on `flipX`, and the 3/4 stills are **not**
symmetric — each differs from its own mirror in ~25 % of the pixels of its content box, three times the front
pose's 8 %. So the diagonal poses will be lit from the opposite side in their flipped half. That is accepted
with its eyes open in issue 2's text, because there is no other way to get eight poses from the files that
exist; the honest ask to the art team, whenever they next touch this character, is two more stills mirrored in
*lighting* rather than only in shape.

**Acceptance:** met by the measurement above — the delivered pair is bit-exact, so the flipped in-game body
*is* the delivered drawing, and `Captures/issue5_left_profile.png` stands as the record of the version that
was not.

---

## How an issue in this file gets closed

Name it in the commit subject, replace the status row with the commit hash, move the numbers that changed
into `TUNING.md`, and if the fix closed a path, add the dated entry to `DESIGN_DECISIONS.md` with the cost
it accepted. An issue is not closed by a code change alone — issues 1 and 3 close on their captures, issue 2
closes on eight poses visible in the room.

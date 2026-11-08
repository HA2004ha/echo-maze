# UX pass — the first ten minutes, read as a player

**Date:** 2026-11-08
**Scope:** the menu, the chapters page's copy, and the in-game hint voice. The rooms themselves are
[LEVELS.md](LEVELS.md); the systems are [STATUS.md](STATUS.md).

This is a review written from the build rather than from the code: the player build was run, the menu and the
chapters page were photographed at 1920×1080, and every claim below has a coordinate in one of those frames.

## What a new player actually meets

```
Boot ──▶ chapter I ──▶ (death → sanctuary → retry) ──▶ exit ──▶ victory ──▶ Boot ──▶ chapters
```

The order is right: the menu's first button is the run, the story page is the second, and the guide is one
press away from both. What was wrong was not the shape but the reading.

## Findings, worst first

### 1. The menu's buttons sat on the diorama, not on a page (fixed)

The button column is 520 px wide on a screen whose whole middle is a lit 3D maze on a table. The panel's wash
is full-screen at α0.55, which is correct for a page with nothing behind it and wrong for this page: at
`y ≈ 380–700` the table's far wall and its rim ran straight through `begin`, `chapters` and `how to play`.
Every label was legible only because of its own brightness.

**Fixed:** a narrow plate (820 × 650, black α0.74) under the button column only, above the wash and below
every button. The title stays on the diorama on purpose — that is the one image the page is built around —
and the subtitle, which crosses the table's brightest rim, now carries the same drop shadow the controls
legend already needed.

### 2. The status line was truncated into meaninglessness (fixed)

`mic:Default Input Devi...` — the microphone line, which exists so a player can decide whether to shout or to
open the calibration page, cut the device name at eighteen characters. A truncated name reads like a failure.

**Fixed:** the short name now keeps 34 characters, and the label is set inside the plate's width at 21 pt
instead of running across the whole frame at 24 pt.

### 3. The tutorial hints shouted (fixed)

Chapter one is the only room that teaches, and it did it in capitals with a full sentence about aiming:
`SPACE: hold, aim with the mouse, release to send a wave in that direction`. Every other line in the game is
lower case and whispers; a hint that shouts is a different interface interrupting this one.

**Fixed:** key first, consequence second, one glance each, in the game's own voice:

```
space: hold to charge, aim, release
f: a quiet circle all around you
walls you touch stay in your memory
shift walks silently — steps make noise
e: open or close a door
```

### 4. The controls legend fights the wave (mitigated, not solved)

The legend sits at the bottom of the frame, on a band at α0.75, and the diorama's emissive trim passes under
it. The band and the per-glyph shadow together make it readable in every capture so far, but it is the one
line on the page that has to win against a moving light. The band went to α0.82; anything further is a
redesign of the bottom strip, not a tweak.

## Judged and left alone

- **The chapters page.** The rail reads the whole story at once, sealed rooms included, and the dossier says
  what each node is before the player commits. After the numeral size and the dossier's position were fixed
  in this pass, nothing on that page asks the player to guess.
- **`begin` / `continue` / `walk it again`.** One button that says what it will do beats three buttons that
  can disagree. It is already read from the save, not from a flag.
- **The five-second chapter title.** It leaves before the first wave, which is the point of it.
- **Hints in later chapters.** They are off by design: chapters II–X are the test, not the lesson.
- **A settings page.** There is one setting that matters (the microphone) and it already has a screen with a
  live meter and a device picker. A settings menu would be a second place to look for it.

## What is still missing, in the order a player would notice

1. **No listen mode.** The guide tells the player that a creature's roar is a warm ring, and nothing in the
   rooms after chapter two lets them stand still and hear one. This is M2 work and the largest single gap in
   the experience.
2. **The exit is not audible.** The GDD wants a hum at the far wall; today the way out is found by walking a
   wall and pressing `E`. Ten rooms of that is a lot of the same search.
3. **Nothing on the chapters page suggests what a sealed room will feel like** beyond one line of threat.
   A map thumbnail per chapter would make the descent look like a descent; it is a cosmetic step, not a
   systems one.
4. **A death is cheap but silent in the story.** The black screen says `YOU WERE HEARD` and the room comes
   back in two seconds. Nothing tells the player which sound gave them away, which is the one lesson the
   game is built to teach.

## Verification

- `Tools ▸ Echo Maze ▸ Build Everything`, then `PlayerBuild.Build`, then captures from the player:
  `Screenshots/menu_ux_pass.png` and `Screenshots/chapters_rail_progress.png`.
- EditMode suite green after the copy and layout changes.

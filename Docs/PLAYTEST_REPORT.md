# Playtest report (session)

Testers copy this file for each session (`PLAYTEST_REPORT — 2026-11-05 — tester name.md`) and record findings as an Issue or a PR against **that tag**. Do not judge the code on `main`; tags only.

## Session details

| Item | Value |
|---|---|
| Tag / build version | |
| Platform (Windows / Linux) | |
| Sound input device (laptop mic / headset / no microphone) | |
| Calibration performed? | |
| Session length | |
| Had they seen the game before? | |
| Headphones used? (required — speaker bleed moves the gate) | |

## Metrics for the report (count, do not estimate)

The game theory section needs these from playtests, and the same curve from the bot: pings per minute, average seconds between pings, detections per session, and how often the player **moved right after pinging** (the hider–seeker tell).

| Metric | Value |
|---|---|
| Pings per minute | |
| Mean interval between pings | |
| Detections (chases started) | |
| Pings followed by movement (%) | |
| Deaths / retries | |

## Readability test (the week 2 go/no-go criterion)

The tester only sees the arcs and has to say what is in front of them. At least 3 correct from the list below? There are no surface materials in this game: brightness comes from geometry, so a corner is bright because it is a discontinuity.

| Object | Correct? | Notes |
|---|---|---|
| Pillar (round) | | |
| Wall (flat and long, broken) | | |
| Box / corner (hot-spot) | | |
| Hollow / alcove | | |
| Door (two fragments and a gap; closed = a wall) | | |
| Crawler enemy (two bumps with a gap, shimmering) | | |
| Spike enemy (saw teeth) — from week 4 | | |
| Absorber (a hole in the arcs) — from week 5 | | |

## The loop test (from week 4)

- [ ] The tester hid, rather than ran, at least once (a hollow, a door, a sanctuary)
- [ ] The tester used Listen mode to learn a patrol route before crossing it
- [ ] A chase started because of something the **tester** did, and they could say what
- [ ] After dying, the retry was a smarter attempt, not a repeat (they used what they learned)

## Open questions we need answers to

1. What is the first reaction to the black screen? (uncomfortable, curious, frustrated?)
2. Did they understand Space (the manual echo), or did they wait to make a sound?
3. How many times did they shout/make noise for no reason and drew an enemy? (false-trigger rate)
4. Which level stalled and why?
5. Was the wave felt as "returning", or did it look like an outgoing wave? (the sign of `gainSign` — see [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md))
6. Did one-hit death feel fair? Did they see the alert telegraph before the chase, and notice when the enemy gave up?
7. Did a stray microphone noise (a chair, a cough) fill the suspicion meter and cost them the level?
8. Did they ever feel lost with no information — i.e. did the landmark sounds and the memory trace orient them?

## Bugs

| # | Level | Description | Reproducible? | Serious? |
|---|---|---|---|---|
| 1 | | | | |

## Decision after this session

- [ ] Continue as planned
- [ ] Spend next week's time on readability
- [ ] Cut this from scope: …

## Recorded before the session (developer, 2026-10-28)

Two questions that were "under decision" are settled in code and in numbers. The session is asked to confirm them **in eyes**, not to re-open them:

- **`gainSign = +1`, so the wave reads as returning**: the far wall is swallowed first and the nearest surface is the last thing standing. Locked by `ArcMathTests.NearestSurfaceSitsOnTheCarrierCircle`; question 5 above is the human half of that check.
- **The corner hot-spot is emergent from curvature** — no per-object brightness was added, which is what GDD 3.0 deleted. Measured in the blockout at a shout: every flat wall run sits at 1.0–2.0 normalised curvature, the 0.6 m pillar at 5.7 m at ~10, and a crossing between two surfaces (a silhouette edge) now counts as nothing at all instead of ~10⁴.

If the readability table above comes back with fewer than 3 of 6 named, the tuning numbers are not the suspect — the ordering in the second bullet is.

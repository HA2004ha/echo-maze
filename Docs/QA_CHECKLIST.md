# QA checklist and running the tests

## Before each weekly tag

```
# EditMode tests from the command line (needs an active license on this machine)
# Find your editor: ls "$HOME"/Unity*/Editor/Unity "$HOME"/Unity/Hub/Editor/*/Editor/Unity
U=$HOME/Unity/6000.6.3f1/Editor/Unity
"$U" -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults ./Logs/editmode.xml -logFile ./Logs/editmode.log

echo "exit=$?"        # zero = all green
grep -o 'result="[A-Za-z]*"' ./Logs/editmode.xml | sort | uniq -c
```

License activation: `Help ▸ Licensing`, once, in the graphical editor. We do not go to CI for this (the reason is in `SCOPE_CUTS.md`).

## Commit ritual

`git status --short` ← `git add <file...>` ← `git lfs ls-files --summarize` ← `git commit` ← `git push origin main --follow-tags`. Details: [GIT.md](GIT.md).

## Opening from a fresh clone (once a week, in this order)

- [ ] Clone into a temporary folder, open with Unity 6000.6.3f1
- [ ] Import finishes without errors; Console has zero red
- [ ] `Assets/Scenes/Sandbox.unity` ▸ Play ▸ the mechanic works
- [ ] No "missing script" and no package override in Package Manager

Before opening Unity, the clone can be checked from the shell: `cat ProjectSettings/ProjectVersion.txt` (must be 6000.6.3f1), `git lfs ls-files` (the `*` means the object really downloaded), `ls -l Tools/unity-yaml-merge.sh` (must stay executable), and comparing every `guid:` referenced in `Assets/Scenes` against the `guid:` lines of all `.meta` files plus `Library/PackageCache` — a reference that resolves in neither is a missing script. `merge.unityyamlmerge.driver` is expected to be **missing** here: it is local config, so [TEAM_SETUP.md](TEAM_SETUP.md) §3 runs first on every machine.

## In-game checklist (from week 3)

- [ ] Microphone calibration: the live waveform moves, the TEST button fires a real ping, settings survive a restart
- [ ] Calibration inside the dark: the ≈5 s silence at the start of level 1 sets a gate that neither a quiet room nor normal speech breaks
- [ ] Pause: sound stops, microphone closes, the menu works with both keyboard and mouse (EventSystem on `InputSystemUIInputModule`)
- [ ] Death: one touch kills, "YOU WERE HEARD" appears, retry returns in under 2 s to the last sanctuary with **that section's state reset** (doors, enemies, marble count)
- [ ] A closed door blocks a ray, blocks enemy hearing and stops a Crawler; the Spike breaks it in ≈3 s and it stays broken until the section resets
- [ ] A hollow hides you from a patrolling enemy but not from one already in Chase; pinging inside breaks cover
- [ ] A noise zone halves your reach, statics the arcs, masks Listen mode and the memory trace, and makes you much harder to hear
- [ ] The alert telegraph fires before every chase (≈0.5 s freeze + a distinct sound) and the give-up cue fires when an enemy loses you
- [ ] Win: reaching the exit, advancing to the next level, and on level 3 the exit light and the ending screen
- [ ] All text is English and renders without missing glyphs (GDD 3.0 is English-only; there is no RTL requirement this term)
- [ ] The HUD is dimmer than the waves and Bloom does not make it glow
- [ ] Performance: 60fps on the target machine with three live pings and 200 rays; zero stable per-frame allocation

## Per-level checklist

- [ ] Start to exit without getting stuck (invisible wall, blocked path)
- [ ] Every enemy in the level is identifiable from its arc
- [ ] The GDD level design checklist holds: a loop, a hiding option within 20–30 s of travel from anywhere, two usable doors in any space containing a Spike, a wake trigger that is the player's choice, a distinct landmark sound per room, one tool that answers the problem
- [ ] The no-microphone path (`Space` only) completes the level — the game must stay playable in a noisy room
- [ ] A quiet route exists for every level (the stealth reward)
- [ ] The tutorial/guide text is read and understood
- [ ] The bot has a scripted escape route through every chase space and completes it headless with a fixed seed

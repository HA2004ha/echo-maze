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

## In-game checklist (from week 3)

- [ ] Microphone calibration: the live waveform moves, the TEST button fires a real ping, settings survive a restart
- [ ] Pause: sound stops, microphone closes, the menu works with both keyboard and mouse (EventSystem on `InputSystemUIInputModule`)
- [ ] Retry after death: the scene reloads and enemy/HUD state starts clean (not continuing from the previous run)
- [ ] Win: reaching the exit, advancing to the next level, and on level 4 the ending screen
- [ ] Persian text renders correctly RTL in TMP and is not garbled
- [ ] The HUD is dimmer than the waves and Bloom does not make it glow
- [ ] Performance: 60fps on the target machine with three live pings and 200 rays; zero stable per-frame allocation

## Per-level checklist

- [ ] Start to exit without getting stuck (invisible wall, blocked path)
- [ ] Every enemy in the level is identifiable from its arc
- [ ] The silent mode (Space only) is possible for every level
- [ ] A "quiet route" exists for every level (the stealth reward)
- [ ] The tutorial/guide text is read and understood

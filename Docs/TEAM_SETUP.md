# Setting up your machine to open this project

## 1) Editor version: exactly Unity 6000.6.3f1

The project is pinned to `6000.6.3f1` (`ProjectSettings/ProjectVersion.txt`). This is **a signal, not a coercion**: if you open the project with a newer editor, the editor upgrades the YAML files one way and older versions can no longer open them. So:

- Unity Hub ▸ **Install Unity** ▸ version **6000.6.3f1**
- Module checkboxes: on Linux **Linux Build Support** + **Windows Build Support (Mono)**; on Windows **Windows Build Support (Mono)** (installed by default)
- Do **not** use editors 6000.7, a different 6000.6.x patch, or 2022.x for this repo. If you must test with another version, work in a separate scratch clone and **do not push**.
- After the first open: import takes a few minutes. Do not click anywhere until it finishes, and do not press any button in `Window ▸ Package Manager ▸ Updates`.
- **Input System (new) is the only active mode** (`activeInputHandler: 1`). Old code written against `Input.GetAxis` or `Input.mousePosition` will not compile/run in this project.

## 2) Git and LFS

```
git lfs install --local
```

If you already cloned and files look like pointers: `git lfs pull`.

## 3) Register the YAML merge driver

Without this, merges of `.unity` and `.prefab` are silently corrupted.

First find the tool on your machine — it lives inside the editor install, not in the repo:

```
ls "$HOME"/Unity*/Editor/Data/Tools/UnityYAMLMerge \
   "$HOME"/Unity/Hub/Editor/*/Editor/Data/Tools/UnityYAMLMerge 2>/dev/null
```

On **Linux**, using the path you just found (note that Hub installs and manually-extracted installs differ — `~/Unity/6000.6.3f1/...` vs `~/Unity/Hub/Editor/6000.6.3f1-x86_64/...`):

```
git config merge.unityyamlmerge.name      "Unity Smart Merge"
git config merge.unityyamlmerge.driver    "$HOME/Unity/6000.6.3f1/Editor/Data/Tools/UnityYAMLMerge merge -p %O %A %B %P"
git config merge.unityyamlmerge.recursive binary
```

On **Windows** (only if the file exists):

```
git config merge.unityyamlmerge.name      "Unity Smart Merge"
git config merge.unityyamlmerge.driver    "\"%PROGRAMFILES%\Unity\Hub\Editor\6000.6.3f1-x86_64\Editor\Data\Tools\UnitySmartMerge.exe\" merge -p %O %A %B %P"
git config merge.unityyamlmerge.recursive binary
```

On **macOS**: the `UnitySmartMerge` tool is inside the editor bundle; locate it with Finder and register it in the same shape.

Verification:

```
git config --get-regexp 'merge\.'      # the three lines must be there
```

Or inside the editor: the menu `Tools ▸ Echo Maze ▸ Validate Git Setup`.

## 4) Opening and running

1. Unity Hub ▸ Add project from disk ▸ the repo root folder (the one containing `Assets/` and `ProjectSettings/`).
2. Open the scene `Assets/Scenes/Sandbox.unity` (the mechanic test level), or `Assets/Scenes/Boot.unity` once it exists (menu and settings).
3. Play. Controls: WASD to move, mouse for aim direction, Space for the manual echo.
4. **Do not expect a working microphone yet.** It is the goal on both Windows and Linux, but `Microphone` support in the Unity editor on Linux has never been verified here. That is why sound input sits behind the `IEchoSource` layer. Today `Sandbox` runs with `sourceMode = ManualOnly` (no source at all, Space only); switching to `Simulated` feeds a constant `simulatedLoudness` through the automatic gate so the mic path can be developed and demoed without a mic. Real microphone testing currently requires a Windows build.

## 4a) The editor from the command line (without opening a window)

To compile and test without a GUI:

```
U=<path-to-editor>/Editor/Unity
"$U" -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/editmode.xml" -logFile "$PWD/Logs/editmode.log"
```

Exit code `0` means every test is green; the per-test result is in `Logs/editmode.xml`. The three editor tools can be invoked the same way: `EchoMaze.EditorTools.SandboxSceneSetup.Bootstrap` (builds the scene and the assets), `.ApplyProjectSettings` (layers and sorting), `EchoMaze.EditorTools.GitSetupValidator.Validate`.

Three traps we hit on this machine:

- The editor finishes its work and writes the "done" message to the log, but **does not exit itself** and keeps eating CPU. The result has already been written; invoke it with `timeout 400` and `kill -TERM` the process once you see the success message. Note that whatever the editor would flush to disk on exit (such as `ProjectSettings/EditorSettings.asset`) does not get written in that state; edit that file by hand with the editor closed.
- **PlayMode tests cannot be run in batchmode on this machine** (neither with `-nographics` nor with `DISPLAY=:0`): the process exits immediately with code `1` and writes no result `xml`. PlayMode tests are only runnable inside the editor (Test Runner tab). Whatever needs to be green in CI is written as EditMode.
- If, after creating an asset with an editor script, some field in the scene ends up as `objectReference: {fileID: 0}`: the asset was probably loaded **before** `EditorSceneManager.OpenScene(...)`. Scene loading flushes asset references that have no scene reference, and your local variable points at a destroyed object. Correct order: `OpenScene` first, then `EnsureX()`. `SandboxSceneSetup` now has an explicit `LogError` if this happens again.

## 5) Windows build from the Linux editor

`File ▸ Build Settings ▸ Platform: Windows ▸ Mono` (not IL2CPP — the Windows IL2CPP module does not exist on Linux). Put the output in `Builds/`, which is ignored. The first Windows build is scheduled for week 3.

## 6) Team roles

- Code: Hossein Askari (lead programmer) — `feat/*` branches merged into `main`.
- Level/enemy design, balancing and testing: Seyed Saeed Nourian — parameters live in `Assets/Data/**` and `Docs/TUNING.md`; no code required.
- Testing and review: Mahziar Mirazimi — plays only the `v0.x` tags and records findings in the form of `Docs/PLAYTEST_REPORT.md`.

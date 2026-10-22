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

**Do not register `UnityYAMLMerge` directly — that recipe does not work.** Measured on this project: git passes a merge driver three temporary, **extension-less** files (`.merge_file_XXXXXX`), and the tool chooses how to parse its input **from the file extension**. Called by git it prints `Couldn't locate merge tool to handle extension` and gives up. Worse, whenever the inputs *do* carry an extension it writes its result to `%P` (a path git ignores) and exits `0`, so git records a successful merge containing **only our side** — the other branch's scene edits vanish with no warning.

`Tools/unity-yaml-merge.sh` exists to fix exactly this: it re-attaches the extension from `%P`, calls the tool, and copies the merged document into `%A`, which is the file git actually reads. Non-zero exit leaves `%A` untouched so git reports an honest conflict instead of a half-merged scene.

The shortest way to register it is the menu — **`Tools ▸ Echo Maze ▸ Register Unity Merge Driver`** — which finds the tool inside your editor install, writes the three config keys and then runs the verification probe. By hand on **Linux/macOS** (substitute your own tool path, and keep the repo path absolute):

```
git config merge.unityyamlmerge.name      "Unity Smart Merge"
git config merge.unityyamlmerge.driver    "UNITY_YAML_MERGE='$HOME/Unity/6000.6.3f1/Editor/Data/Tools/UnityYAMLMerge' '$PWD/Tools/unity-yaml-merge.sh' %O %A %B %P"
git config merge.unityyamlmerge.recursive binary
```

Find your tool first if that path does not exist — Hub installs and manually-extracted installs differ (`~/Unity/6000.6.3f1/Editor/Data/Tools/` vs `~/Unity/Hub/Editor/<version>/Editor/Data/Tools/`):

```
ls "$HOME"/Unity*/Editor/Data/Tools/UnityYAMLMerge \
   "$HOME"/Unity/Hub/Editor/*/Editor/Data/Tools/UnityYAMLMerge 2>/dev/null
```

On **Windows** the wrapper is a POSIX shell script, so it needs Git Bash. The tool there is `UnitySmartMerge.exe`. Whether the Windows build has the same extension dependency is **unverified** — nobody on this team has run a real scene merge on Windows yet. Register the plain form, then prove it with the probe below before you trust a merge:

```
git config merge.unityyamlmerge.name      "Unity Smart Merge"
git config merge.unityyamlmerge.driver    "\"%PROGRAMFILES%\Unity\Hub\Editor\6000.6.3f1\Editor\Data\Tools\UnitySmartMerge.exe\" merge -p %O %A %B %P"
git config merge.unityyamlmerge.recursive binary
```

Verification — `git config --get-regexp 'merge\.'` only proves three lines exist, which is precisely the check that passed while merging nothing. Use the editor probe instead: **`Tools ▸ Echo Maze ▸ Validate Git Setup`** performs a real three-way merge on a copy of `Sandbox.unity`, with two branches editing *adjacent lines* of the same GameObject (the case a text merge conflicts on), and requires both changes in the result. It reports `PASS probe merge resolved both sides of a real scene`.


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

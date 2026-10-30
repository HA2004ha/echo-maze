# Setting up your machine to open this project

## 1) Editor version: exactly Unity 6000.6.3f1

The project is pinned to `6000.6.3f1` (`ProjectSettings/ProjectVersion.txt`). This is **a signal, not a coercion**: if you open the project with a newer editor, the editor upgrades the YAML files one way and older versions can no longer open them. So:

- Unity Hub ▸ **Install Unity** ▸ version **6000.6.3f1**
- Module checkboxes: on Linux **Linux Build Support** (this is the delivery target — the whole team develops and tests on Linux); **Windows Build Support (Mono)** too (amended 2026-10-30: Windows is a delivery target)
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
2. Open the scene `Assets/Scenes/Level02_Corridor.unity` — from week 2 that is the room the game is actually about: an enclosed `7 × 5` blockout with a patrolling Crawler in it, and it is the scene a build boots into. `Assets/Scenes/Sandbox.unity` stays as the math playground (three test objects, no gameplay), and `Assets/Scenes/Boot.unity` comes when the menu and options land in week 3.
3. Play. Controls: WASD to move, mouse for aim direction, `Space` for the manual echo (hold it to charge), `Shift` for silent movement, `F` for the 360° weak pulse. Touch a wall and it marks; the Crawler kills on contact and the room restarts in about a second.
4. **The microphone works on Linux, in the editor.** `Level02_Corridor` and `Sandbox` both save `sourceMode = Microphone`, and on 2026-10-28 the editor on this team's machine listed three devices, captured real samples and opened the gate on voice — the result is row 1 of [MIC_TEST_MATRIX.md](MIC_TEST_MATRIX.md). If your machine has no usable input, or its default input is a `Monitor of …` alias the code cannot recognise, `EchoDirector` falls back to the simulated source with a Console warning and the game stays playable on `Space`. Measure your own machine with **`Tools ▸ Echo ▸ Mic Probe`** and record it in the matrix. What is *not* yet proven is the **player build's** audio backend (row 2), which no editor session can answer.

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

- **Close the GUI editor first.** A project can only be open in one Unity process; `batchmode` on a project another editor instance holds either fails or silently works on stale assets, and it re-imports the whole library while it runs. The command-line suite is therefore a *headless* step: quit the editor, run it, reopen. That is also why the week's "all tests green from the command line" evidence is taken at the freeze and not continuously.

- The editor finishes its work and writes the "done" message to the log, but **does not exit itself** and keeps eating CPU. The result has already been written; invoke it with `timeout 400` and `kill -TERM` the process once you see the success message. Note that whatever the editor would flush to disk on exit (such as `ProjectSettings/EditorSettings.asset`) does not get written in that state; edit that file by hand with the editor closed.
- **PlayMode tests cannot be run in batchmode on this machine** (neither with `-nographics` nor with `DISPLAY=:0`): the process exits immediately with code `1` and writes no result `xml`. PlayMode tests are only runnable inside the editor (Test Runner tab). Whatever needs to be green in CI is written as EditMode.
- If, after creating an asset with an editor script, some field in the scene ends up as `objectReference: {fileID: 0}`: the asset was probably loaded **before** `EditorSceneManager.OpenScene(...)`. Scene loading flushes asset references that have no scene reference, and your local variable points at a destroyed object. Correct order: `OpenScene` first, then `EnsureX()`. `SandboxSceneSetup` now has an explicit `LogError` if this happens again.

## 5) Linux build, from the Linux editor

`File ▸ Build Settings ▸ Platform: Linux ▸ x86_64`. Put the output in `Builds/`, which is ignored. **This is the platform the team runs and tests on**: the whole team is on Linux, so the microphone is measured on Linux per [MIC_TEST_MATRIX.md](MIC_TEST_MATRIX.md) — the editor path is proven (row 1, 2026-10-28) and the player build is the open row (row 2). A Windows build is only worth making if someone outside the team needs to play it; the module for it is **Windows Build Support (Mono)**, which does not exist as IL2CPP on Linux.

The build scenes list is `Level02_Corridor` first, then `Sandbox` (committed in `ProjectSettings/EditorBuildSettings.asset`), so a built player boots into the walkable room and not into the math playground. Build it as a **Development** build: the mic and framerate questions in row 2 and row 9 are answered from the log file, and a release build does not write one. Then:

```
cd Builds/Linux && ./EchoMaze.x86_64
```

Logs land next to the binary, in `~/.config/unity3d/<company>/<product>/Logs/` and in `Player.log` under the same folder; pass `-logFile <path>` to put one where you can read it back. The first line to look for is `Microphone open in the player: mic:<device>. N capture device(s): …` — `EchoDirector` prints it once at startup, and it is row 2 of the matrix: on 2026-10-28 the player on this machine listed four devices and opened one, so the Linux *player* audio path works. What that run cannot answer is everything that needs a voice: rows 3, 6, 7, 9 and 11's player half — does the gate open on real speech, do a whisper and a shout differ on screen, how long from speaking to arc, and does the mic light go off when the window closes. One of those can fail in the player while working perfectly in the editor — that is the reason the rows exist.

## 6) Team roles

- Code: Hossein Askari (lead programmer) — `feat/*` branches merged into `main`.
- Level/enemy design, balancing and testing: Seyed Saeed Nourian — parameters live in `Assets/Data/**` and `Docs/TUNING.md`; no code required.
- Testing and review: Mahziar Mirazimi — plays only the `v0.x` tags and records findings in the form of `Docs/PLAYTEST_REPORT.md`.

# Working with git in this repo (Unity + LFS)

This file is the git contract for the Echo project. Three things have to be done correctly: commit the right things, let LFS carry the binary files, and merge editor YAML files with the correct driver.

## What gets committed and what does not

**Always committed:**

- `Assets/**` together with **every `.meta` file** (without `.meta` files the GUIDs are lost and references break inside scenes)
- `Packages/manifest.json` and `Packages/packages-lock.json` — **with one deliberate exception:** the lead's machine carries `com.coplaydev.unity-mcp` (an editor-only tool that drives Unity from an external agent) as a git-URL dependency, and it is **never committed**. It is not part of the game, it points at a moving `#main` ref, and a fresh clone should not need a third-party GitHub repo to resolve. So those two files are permanently dirty on the lead's machine; a teammate's clone of `main` will not show them modified, and if yours does, you have the package in your local `manifest.json` too.
- `ProjectSettings/**` (including `ProjectSettings/Packages/**`)
- `.gitignore`, `.gitattributes`, `Docs/**`

**Never committed** (all of these are already in `.gitignore`; just check that nothing was force-added with `git add -f`):

`Library/`, `Temp/`, `Obj/`, `Logs/` (except `Logs/editmode.xml` and `Logs/playmode.xml`, which a week's exit criterion commits as evidence that the suites were run from the command line and not only inside the editor), `UserSettings/`, `Builds/`, `MemoryCaptures/`, `Recordings/`, `*.csproj`, `*.sln`, `*.slnx`, `*.user`, `sysinfo.txt`, `InitTestScene*.unity*`

The final game build does **not** go into the repo; the delivery zip is sent separately.

## Commit ritual (every time, no exceptions)

```
git status --short                    # 1. what actually changed?
git add <path> <path>                 # 2. file by file, not git add -A
git lfs ls-files -n                   # 3. every new png/ttf/otf/wav/ogg must be listed here
git commit -m "..."                   # 4
git push origin main --follow-tags    # 5 (on your own branches only, without --follow-tags)
```

Step 3 is critical: LFS **silently failed to run** once in this repo and a raw 816,464-byte PNG landed in commit history. If a file is missing from step 3 but should be there:

```
git rm --cached <file>
git lfs install --local
git add <file>
```

Commit messages: `feat:` for features, `fix:` for bugs, `chore:` for infrastructure, `test:` for tests, `docs:` for documentation. Scoped prefixes are allowed too: `chore(project-settings):`, `feat(sandbox):`.

## Branches and tags

- `main` **always opens and plays with editor 6000.6.3f1.** Testers and reviewers take only `main` or a tag, never a working branch.
- Work happens on `feat/echo-mesh`, `feat/enemy-crawler`, … with a maximum lifetime of **2 days**; merged as a squash.
- One tag at the end of each week: `v0.1-sandbox`, `v0.2-arc-train`, `v0.3-mic-calibration`, `v0.4-enemies`, `v0.5-submission`, `v0.6-final`.
- `main` is never force-pushed. After the team's first clone, `git lfs migrate` and any form of history rewriting is **forbidden**.
- Edits to `ProjectSettings/**` (layers, quality, input handler, enter-play-mode) come in a separate, batched commit — never in the middle of a feature branch.
- Each branch edits at most **one scene**. Scenes are the biggest source of conflicts.

## The YAML merge driver (Unity Smart Merge)

`.gitattributes` requests `merge=unityyamlmerge` for every YAML file (`*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, …). If this driver is **not registered** in your machine's git config, git silently falls back to a plain text merge and **ruins** the scene merge. The tool ships inside the editor (it has not been removed in Unity 6), but it cannot be registered as the bare binary: git gives merge drivers extension-less temp files and the tool picks its parser from the extension. `Tools/unity-yaml-merge.sh` adapts it — full explanation and registration commands in [TEAM_SETUP.md](TEAM_SETUP.md).

Verification — three config lines is **not** proof. A driver can be registered, correctly named, and still resolve nothing (that is how this repo shipped for a week). The only meaningful check is a real merge:

```
git config --get-regexp 'merge\.'      # step 1: the three unityyamlmerge lines exist
```

then `Tools ▸ Echo Maze ▸ Validate Git Setup`, which performs an actual three-way merge on a copy of `Sandbox.unity` where two branches edit adjacent lines of the same object, and confirms both changes survive.

**When the probe reports a conflict, there are no `<<<<<<<` markers to fix.** That is how git behaves with an external merge driver: on a non-zero exit it keeps the driver's output as-is and only records the stages. So never resolve a scene conflict by eyeballing the file or by `git add -A` — read the three stages and pick deliberately:

```
git show :1:Assets/Scenes/Sandbox.unity    # base
git show :2:Assets/Scenes/Sandbox.unity    # ours
git show :3:Assets/Scenes/Sandbox.unity    # theirs
```

## LFS for binaries only

The rule: `*.asset`, `*.unity`, `*.prefab`, `*.meta` **never** go to LFS (there is a defensive comment saying so in `.gitattributes` too). These are text YAML; if they become pointers, the editor on any machine without `git-lfs` reads them as assets and the project breaks — and the smart merge driver does not work on pointers either.

Keep the GitHub LFS limit in mind (the free plan is ≈1 GB of storage/bandwidth): our game is vector lines and small sprites, so it fits easily — but nobody should drop a large PSD or a raw recorded WAV into `Assets/` without thinking about it.

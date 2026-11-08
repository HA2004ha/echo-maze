# Levels — the ten chapters, and how to add the eleventh

The run is **ten chapters**, from a room with nothing in it to the sump. Every room is a text file; the scene
that plays it is generated. This document is the part that has to survive a half-finished branch: what is
written by hand, what is generated, and the two commands that rebuild everything.

- The story, the order, the difficulty and the map of each chapter: `Assets/Scripts/Runtime/Gameplay/ChapterStory.cs`
- The maps: `Assets/Data/Levels/LevelNN_*.txt`
- The generator for a first draft of a map: `Tools/level_forge.py`
- The scene builder: `Assets/Scripts/Editor/ChapterSceneSetup.cs`

## The ladder

| # | scene | room | grid | size | camera | shares it | grade |
|---|-------|------|------|------|--------|-----------|-------|
| I | `Level01_Maze` | the maze | `Level01_Maze.txt` | — | 6 | nothing | tutorial |
| II | `Level02_Corridor` | the corridor | hand-built (`Level02SceneSetup`) | — | — | one crawler | easy |
| III | `Level03_FloodedWing` | the flooded wing | `Level03_FloodedWing.txt` | 21×13 | 6 | glass | easy |
| IV | `Level04_ListeningRoom` | the listening room | `Level04_ListeningRoom.txt` | 21×15 | 6 | crawler, hollow, glass | medium |
| V | `Level05_SumpStairs` | the sump stairs | `Level05_SumpStairs.txt` | 23×15 | 6 | crawler, 2 doors | medium |
| VI | `Level06_BoilerRun` | the boiler run | `Level06_BoilerRun.txt` | 25×17 | 6 | crawler, 2 sanctuaries, glass | medium |
| VII | `Level07_DryWell` | the dry well | `Level07_DryWell.txt` | 25×17 | 6 | crawler, 2 hollows | hard |
| VIII | `Level08_ColdStore` | the cold store | `Level08_ColdStore.txt` | 27×19 | 6 | crawler, 3 doors | hard |
| IX | `Level09_Floodgate` | the floodgate | `Level09_Floodgate.txt` | 29×19 | 6 | crawler, 3 doors, glass floor | hard |
| X | `Level10_Sump` | the sump | `Level10_Sump.txt` | 31×21 | 6 | crawler, 2 hollows, 3 doors | brutal |

Difficulty climbs in one direction only; `ChapterLevelTests.TheLadder_OnlyEverGetsHarder` fails the suite if a
new chapter is inserted out of order.

## Adding a chapter — three edits

1. **Make the map.** Either copy a `.txt` beside the others and draw it, or ask the forge for a first draft:

   ```
   python3 Tools/level_forge.py --out Assets/Data/Levels/Level11_Cistern.txt \
       --size 31x21 --seed 11 --doors 3 --glass 6 --sanctuaries 1 --hollows 2 --crawler
   ```

   It carves a perfect maze, opens it into loops, puts the exit behind a door, places the threats, and then
   *validates* its own output: it refuses to write a map the player cannot finish. What it writes is a first
   draft by design — open it and move one wall.

2. **Add one entry** to `chapters` in `ChapterStory.cs`, in the position the chapter is played:

   ```csharp
   new ChapterStory(
       "Level11_Cistern", "XI", "the cistern", "one line about the room",
       "the line the HUD whispers when the chapter opens",
       "the sentence that leads into the next room",
       "what the room leaves behind once it has been walked",
       ChapterDifficulty.Hard, "one line naming what shares the room",
       "Assets/Data/Levels/Level11_Cistern.txt"),
   ```

   Nothing else needs to know the chapter exists: the flow's order, the build scene list, the story page's
   rail node, the guide, and the scene itself all come from this entry.

3. **Rebuild.**

   ```
   "/home/<you>/Unity/Hub/Editor/6000.6.3f1-x86_64/Editor/Unity" -batchmode -quit -projectPath . \
       -executeMethod EchoMaze.EditorTools.GameBootstrap.BuildEverything -logFile Logs/bootstrap.log
   ```

   or, in the editor, `Tools ▸ Echo Maze ▸ Build Everything`. That is the whole loop. `Build Chapter Scenes`
   alone rebuilds only the grid chapters, which is faster while iterating on one room.

## The legend

The header of every map repeats this. `;` starts a comment line.

```
#  wall            .  floor           P  player spawn (exactly one)
E  exit            D  door            S  sanctuary / checkpoint
G  glass           o  pillar          b  wooden box
C  crawler spawn   c  crawler waypoint    h  hollow (hides a quiet player)
```

`C`, `c` and `h` are placed by the scene builder rather than by the level's runtime code, because a creature
carries audio and serialized tuning that a runtime `AddComponent` cannot author: `ChapterSceneSetup` reads the
map, puts one crawler at each `C` walking every `c` in file order, and one hollow at each `h`. The grid parser
treats all three as floor, so the walls, the wave and the player see nothing different.

Rules the tests hold a map to (`Assets/Tests/EditMode/ChapterLevelTests.cs`):

- exactly one `P`, one `E`, and both reachable with the doors open;
- **if the map has a door, the exit must be unreachable with the doors closed** — a door beside the route, not
  on it, teaches nothing;
- every `C`, `c`, `h`, `S` and `G` is on floor the player can also reach, so nothing is authored into a sealed
  pocket;
- the map parses, the spawn is not inside a wall, and the chapter's scene exists on disk.

## The story page

`Boot` carries the chapters page: one rail node per catalogue entry and a dossier for the selected room, with
the selected node's best run stamped on it. It is drawn by `UiKit.EnsureChapterSelect` (layout, generated from
`ChapterCatalog.Count`) and written at runtime by `ChapterSelectPanel` (state), and the light that walks down
the rail is `ChapterRailPulse`. Adding a chapter needs no work on that page.

## If this work stops halfway

Everything committed is committed in stages so a different machine can continue:

1. `feat(story)` — the chapters page and the saved run, with two chapters.
2. `feat(levels)` — the catalogue, the ten maps, the generic chapter scene builder, the new markers.
3. `feat(ui)` — the rail-and-dossier page, the pulse, the guide's new card.
4. `docs(levels)` — this file, the README and STATUS.

If you inherit a branch after (2) and before (3): the catalogue and the maps are complete and the scenes build,
but the menu still draws the old three-row page, which references fields the new panel no longer has. Re-run
`Build Everything` after taking the UI in (3) — the page is generated, so there is nothing to repair by hand.

If the maps are there and the scenes are not, `Build Everything` makes them. If a map is missing, the builder
logs the file name and leaves the scene empty rather than shipping a room with no floor.

## Checks

```
# scenes, build list, player settings
"-executeMethod" EchoMaze.EditorTools.GameBootstrap.BuildEverything

# the ten maps, the story, the page's wiring
"-runTests" "-testPlatform" EditMode "-testResults" Logs/editmode.xml

# the real thing, with the story page captured
./Builds/echo-maze.x86_64 -echo-shot out.png -echo-chapters -echo-quit
```

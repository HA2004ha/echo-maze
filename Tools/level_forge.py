#!/usr/bin/env python3
"""Level forge: write a playable Echo Maze text level from a seed.

The levels are authored files, not random rooms: this tool makes the first draft of
one — a looped maze, an exit gated by a door, a sanctuary on the route, glass, an
optional crawler patrol — and the designer then edits the .txt by hand to put the
beats where the story wants them. The output is committed as text, so what ships is
the file, not the seed.

Everything it emits already satisfies the rules ChapterLevelTests asserts: one spawn,
one exit, the exit unreachable while the doors are shut, and every walkable cell
reachable once they are open. If a seed cannot satisfy them the forge says so instead
of writing a broken level.

Usage examples (from the repository root):

    python3 Tools/level_forge.py --out Assets/Data/Levels/Level03_FloodedWing.txt \
        --size 21x13 --seed 3 --doors 1 --glass 4 --sanctuaries 1 --hollows 1

    python3 Tools/level_forge.py --out Assets/Data/Levels/Level10_Sump.txt \
        --size 31x21 --seed 10 --doors 3 --glass 6 --sanctuaries 1 --hollows 2 --crawler

After generating a level, add its entry to ChapterCatalog and run
Tools > Echo Maze > Build Everything; the EditMode suite then proves the room is
solvable before anybody plays it.
"""

import argparse
import random

WALL = "#"
FLOOR = "."
SPAWN = "P"
EXIT = "E"
DOOR = "D"
SANCTUARY = "S"
GLASS = "G"
PILLAR = "o"
BOX = "b"
CRAWLER = "C"
PATROL = "c"
HOLLOW = "h"

WALKABLE = {FLOOR, SPAWN, EXIT, SANCTUARY, GLASS, PILLAR, BOX, CRAWLER, PATROL, HOLLOW}
BLOCKING = {WALL, PILLAR, BOX}

LEGEND = """; Echo Maze — first draft written by Tools/level_forge.py, then edited by hand.
; One character per cell, 2.0 m per cell, the first grid row below is the TOP of the map.
; Lines starting with ';' and blank lines are ignored by MazeGrid.Parse.
;
;   #  wall            .  floor           P  player spawn     E  level exit
;   D  door (closed at the start; the route to E runs through one of them)
;   S  sanctuary (checkpoint)            G  glass shards (a noisy floor)
;   o  pillar (round signature)          b  box (the bright corner)
;   C  crawler spawn                     c  crawler patrol waypoint
;   h  hollow (cover, before you are found)
;
; Every cell that is not '#' must be reachable from P with the doors open, and E must be
; unreachable with them closed — locked by ChapterLevelTests.
"""


def carve(width, height, rng, braid):
    """A perfect maze of cells, then a fraction of its dead ends opened into loops."""
    grid = [[WALL for _ in range(width)] for _ in range(height)]

    def cell(cx, cy):
        return 2 * cx + 1, 2 * cy + 1

    cw, ch = (width - 1) // 2, (height - 1) // 2
    visited = {(0, 0)}
    stack = [(0, 0)]
    grid[1][1] = FLOOR

    while stack:
        cx, cy = stack[-1]
        options = []
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = cx + dx, cy + dy
            if 0 <= nx < cw and 0 <= ny < ch and (nx, ny) not in visited:
                options.append((nx, ny, dx, dy))

        if not options:
            stack.pop()
            continue

        nx, ny, dx, dy = rng.choice(options)
        wx, wy = cell(cx, cy)
        tx, ty = cell(nx, ny)
        grid[wy + dy][wx + dx] = FLOOR
        grid[ty][tx] = FLOOR
        visited.add((nx, ny))
        stack.append((nx, ny))

    dead_ends = []
    for cy in range(ch):
        for cx in range(cw):
            gx, gy = cell(cx, cy)
            if grid[gy][gx] != FLOOR:
                continue
            open_sides = sum(1 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                             if grid[gy + dy][gx + dx] == FLOOR)
            if open_sides == 1:
                dead_ends.append((gx, gy))

    rng.shuffle(dead_ends)
    for gx, gy in dead_ends[: max(0, int(len(dead_ends) * braid))]:
        closed = [(gx + dx, gy + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                  if 0 <= gx + dx < width and 0 <= gy + dy < height and grid[gy + dy][gx + dx] == WALL]
        if closed:
            bx, by = rng.choice(closed)
            grid[by][bx] = FLOOR

    return grid


def walkable(grid, x, y, doors_open):
    ch = grid[y][x]
    if ch == DOOR:
        return doors_open
    return ch in WALKABLE and ch not in BLOCKING


def reachable(grid, start, doors_open):
    height, width = len(grid), len(grid[0])
    seen = {start}
    queue = [start]
    while queue:
        x, y = queue.pop(0)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if not (0 <= nx < width and 0 <= ny < height):
                continue
            if (nx, ny) in seen or not walkable(grid, nx, ny, doors_open):
                continue
            seen.add((nx, ny))
            queue.append((nx, ny))
    return seen


def find(grid, marker):
    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            if ch == marker:
                return (x, y)
    return None


def validate(grid):
    """The same three rules ChapterLevelTests asserts, checked before the file is written."""
    spawn = find(grid, SPAWN)
    exit_cell = find(grid, EXIT)
    if spawn is None or exit_cell is None:
        return "needs one spawn and one exit"

    if exit_cell in reachable(grid, spawn, doors_open=False):
        return "the exit is reachable with every door shut"

    open_cells = reachable(grid, spawn, doors_open=True)
    if exit_cell not in open_cells:
        return "the exit is not reachable even with the doors open"

    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            if ch in WALKABLE and ch not in BLOCKING and (x, y) not in open_cells:
                return f"cell ({x},{y}) '{ch}' is walled off from the route"

    return None


def gate_the_exit(grid, rng):
    """Puts the exit in a pocket whose only way in is a door. Returns False if it cannot."""
    height, width = len(grid), len(grid[0])
    candidates = [(1, height - 2), (width - 2, height - 2), (1, 1), (width - 2, 1)]
    rng.shuffle(candidates)

    for ex, ey in candidates:
        if grid[ey][ex] != FLOOR:
            continue

        sides = [(dx, dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                 if 0 < ex + dx < width - 1 and 0 < ey + dy < height - 1 and grid[ey + dy][ex + dx] == FLOOR]
        if not sides:
            continue

        grid[ey][ex] = EXIT
        door_dx, door_dy = sides[0]
        grid[ey + door_dy][ex + door_dx] = DOOR

        # Every other side of the pocket becomes wall: the door is the whole entrance.
        for dx, dy in sides[1:]:
            grid[ey + dy][ex + dx] = WALL

        if validate(grid) is None:
            return True

        # Put the room back before trying the next corner.
        grid[ey][ex] = FLOOR
        for dx, dy in sides[1:]:
            grid[ey + dy][ex + dx] = FLOOR
        grid[ey + door_dy][ex + door_dx] = FLOOR

    return False


def path_from(grid, start, goal):
    height, width = len(grid), len(grid[0])
    seen = {start: None}
    queue = [start]
    while queue:
        node = queue.pop(0)
        if node == goal:
            break
        x, y = node
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            nxt = (nx, ny)
            if nxt in seen or not (0 <= nx < width and 0 <= ny < height):
                continue
            if not walkable(grid, nx, ny, doors_open=True):
                continue
            seen[nxt] = node
            queue.append(nxt)

    if goal not in seen:
        return []
    route = []
    node = goal
    while node is not None:
        route.append(node)
        node = seen[node]
    return list(reversed(route))


def decorate(grid, rng, doors, glass, sanctuaries, hollows, crawler):
    spawn = find(grid, SPAWN)
    exit_cell = find(grid, EXIT)
    door = find(grid, DOOR)
    taken = {spawn, exit_cell, door}

    def free_floors():
        return [(x, y) for y, row in enumerate(grid) for x, ch in enumerate(row)
                if ch == FLOOR and (x, y) not in taken]

    # Extra doors: a corridor cell turned into a second thing to spend. The gate at the exit
    # is already in place, so these are the level's own choices, not its lock.
    extra = max(0, doors - 1)
    for _ in range(extra * 20):
        if extra == 0:
            break
        candidates = free_floors()
        if not candidates:
            break
        x, y = rng.choice(candidates)
        horizontal = grid[y][x - 1] == FLOOR and grid[y][x + 1] == FLOOR
        vertical = grid[y - 1][x] == FLOOR and grid[y + 1][x] == FLOOR
        if not (horizontal ^ vertical):
            continue
        grid[y][x] = DOOR
        taken.add((x, y))
        extra -= 1

    route = path_from(grid, spawn, exit_cell)
    for _ in range(sanctuaries):
        spot = None
        for step in (route[len(route) // 2], route[len(route) // 3], route[-3] if len(route) > 4 else None):
            if step and grid[step[1]][step[0]] == FLOOR:
                spot = step
                break
        if spot is None:
            candidates = free_floors()
            if not candidates:
                break
            spot = rng.choice(candidates)
        grid[spot[1]][spot[0]] = SANCTUARY
        taken.add(spot)

    for _ in range(glass):
        candidates = free_floors()
        if not candidates:
            break
        x, y = rng.choice(candidates)
        grid[y][x] = GLASS
        taken.add((x, y))

    for _ in range(hollows):
        candidates = free_floors()
        if not candidates:
            break
        x, y = rng.choice(candidates)
        grid[y][x] = HOLLOW
        taken.add((x, y))

    if crawler:
        candidates = free_floors()
        if candidates:
            sx, sy = candidates[len(candidates) // 2]
            grid[sy][sx] = CRAWLER
            taken.add((sx, sy))
            for wx, wy in (spawn, (2, 2), (2, len(grid) - 3), (len(grid[0]) - 3, 2)):
                if not (0 <= wx < len(grid[0]) and 0 <= wy < len(grid)):
                    continue
                if grid[wy][wx] != FLOOR or (wx, wy) in taken:
                    continue
                grid[wy][wx] = PATROL
                taken.add((wx, wy))


def forge(width, height, seed, doors, glass, sanctuaries, hollows, crawler, braid):
    for attempt in range(120):
        rng = random.Random(seed * 1000 + attempt)
        grid = carve(width, height, rng, braid)
        spawn = find(grid, FLOOR)
        grid[spawn[1]][spawn[0]] = SPAWN

        if not gate_the_exit(grid, rng):
            continue

        decorate(grid, rng, doors, glass, sanctuaries, hollows, crawler)

        problem = validate(grid)
        if problem is None:
            return grid, attempt
        print(f"  seed {seed} attempt {attempt}: {problem}")

    raise SystemExit(f"could not forge a playable {width}x{height} room from seed {seed}")


def render(grid):
    # MazeGrid.Parse reads the file top line as the highest row, so the bottom-up grid is
    # printed upside down: what the designer sees in the file is the room the player walks.
    return LEGEND + "\n".join("".join(row) for row in reversed(grid)) + "\n"


def main():
    parser = argparse.ArgumentParser(description="Forge one Echo Maze level as text.")
    parser.add_argument("--out", required=True, help="path of the .txt to write")
    parser.add_argument("--size", default="21x15", help="WIDTHxHEIGHT in characters (odd numbers)")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--doors", type=int, default=1)
    parser.add_argument("--glass", type=int, default=3)
    parser.add_argument("--sanctuaries", type=int, default=1)
    parser.add_argument("--hollows", type=int, default=0)
    parser.add_argument("--crawler", action="store_true")
    parser.add_argument("--braid", type=float, default=0.35,
                        help="fraction of dead ends opened into loops")
    args = parser.parse_args()

    width, height = (int(part) for part in args.size.lower().split("x"))
    if width % 2 == 0 or height % 2 == 0:
        raise SystemExit("--size must be odd x odd: a maze of cells needs a wall on both sides")

    grid, attempt = forge(width, height, args.seed, args.doors, args.glass, args.sanctuaries,
                          args.hollows, args.crawler, args.braid)

    with open(args.out, "w", encoding="utf-8") as handle:
        handle.write(render(grid))

    counts = {}
    for row in grid:
        for ch in row:
            counts[ch] = counts.get(ch, 0) + 1
    print(f"{args.out}: {width}x{height} attempt {attempt} " +
          " ".join(f"{k}:{v}" for k, v in sorted(counts.items())))


if __name__ == "__main__":
    main()

"""Generate battle_arena and department_store map JSON files."""

import json
import os

# Tile IDs
GRASS = 1
PATH = 2
TREE = 3
WATER = 0
DOOR = 4
BRIDGE = 5
WALL = 6
ROCK = 8
FENCE = 18

# Overlay IDs
SIGN = 9
NPC = 10
SHOP = 11
HEAL = 12
ITEM = 13
KEY_ITEM = 14
TRAINER_DOWN = 21
GYM_LEADER = 24
VILLAIN_BOSS = 30
MINION_DOWN = 36
HIDDEN_ITEM = 40
PC = 41
POKEBALL = 42
STATUE = 49

# Building footprints (base tiles)
POKECENTER = [
    [3, 3, 3, 3],
    [3, 4, 4, 3],
    [3, 4, 4, 3],
    [6, 4, 4, 6],
]

POKEMART = [
    [3, 3, 3, 3],
    [3, 6, 6, 3],
    [3, 11, 6, 3],
    [6, 4, 4, 6],
]

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "exported")


def make_grid(width, height, fill):
    """Create a width x height grid filled with a value."""
    return [[fill for _ in range(width)] for _ in range(height)]


def place_building(base, footprint, col, row):
    """Place a building footprint into the base grid at (col, row) = top-left."""
    for dy, fp_row in enumerate(footprint):
        for dx, tile in enumerate(fp_row):
            base[row + dy][col + dx] = tile


def fill_rect(grid, col, row, w, h, tile):
    """Fill a rectangle in the grid."""
    for r in range(row, row + h):
        for c in range(col, col + w):
            if 0 <= r < len(grid) and 0 <= c < len(grid[0]):
                grid[r][c] = tile


def set_tile(grid, col, row, tile):
    """Set a single tile."""
    if 0 <= row < len(grid) and 0 <= col < len(grid[0]):
        grid[row][col] = tile


def hline(grid, row, c_start, c_end, tile):
    """Draw a horizontal line."""
    for c in range(c_start, c_end + 1):
        set_tile(grid, c, row, tile)


def vline(grid, col, r_start, r_end, tile):
    """Draw a vertical line."""
    for r in range(r_start, r_end + 1):
        set_tile(grid, col, r, tile)


def fence_box(grid, col, row, w, h):
    """Draw a fence border box. Interior is Path."""
    # Top and bottom horizontal fences
    hline(grid, row, col, col + w - 1, FENCE)
    hline(grid, row + h - 1, col, col + w - 1, FENCE)
    # Left and right vertical fences
    vline(grid, col, row, row + h - 1, FENCE)
    vline(grid, col + w - 1, row, row + h - 1, FENCE)
    # Fill interior with path
    fill_rect(grid, col + 1, row + 1, w - 2, h - 2, PATH)


def save_map(data, filename):
    """Save map data as JSON."""
    path = os.path.join(OUTPUT_DIR, filename)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
    print(f"Saved: {path}")
    # Validate dimensions
    assert len(data["baseTiles"]) == data["height"], \
        f"baseTiles has {len(data['baseTiles'])} rows, expected {data['height']}"
    assert len(data["overlayTiles"]) == data["height"], \
        f"overlayTiles has {len(data['overlayTiles'])} rows, expected {data['height']}"
    for i, row in enumerate(data["baseTiles"]):
        assert len(row) == data["width"], \
            f"baseTiles row {i} has {len(row)} cols, expected {data['width']}"
    for i, row in enumerate(data["overlayTiles"]):
        assert len(row) == data["width"], \
            f"overlayTiles row {i} has {len(row)} cols, expected {data['width']}"
    print(f"  Validated: {data['width']}x{data['height']}")


def generate_battle_arena():
    """Generate the Battle Arena map (36x28)."""
    W, H = 36, 28
    base = make_grid(W, H, GRASS)
    overlay = make_grid(W, H, None)

    # Tree border around all edges
    hline(base, 0, 0, W - 1, TREE)   # top
    hline(base, H - 1, 0, W - 1, TREE)  # bottom
    vline(base, 0, 0, H - 1, TREE)   # left
    vline(base, W - 1, 0, H - 1, TREE)  # right

    # Entry: col 1-4, row 0 (gap in top tree border -> Path)
    for c in range(1, 5):
        set_tile(base, c, 0, PATH)

    # === Main concourse (rows 1-11) ===

    # Entry path going down from row 0: col 1-4, rows 1-2
    fill_rect(base, 1, 1, 4, 2, PATH)

    # 3-wide avenue: rows 3-4, col 1-34
    fill_rect(base, 1, 3, 34, 2, PATH)

    # Vertical street: col 9-10, going down from row 3 to row 11
    fill_rect(base, 9, 3, 2, 9, PATH)

    # Pokecenter at col 3-6, rows 5-8
    place_building(base, POKECENTER, 3, 5)

    # Connect pokecenter door (row 8, col 4-5) to vertical street (col 9-10) with 2-wide path
    fill_rect(base, 5, 8, 5, 2, PATH)  # row 8-9, col 5-9

    # Pokemart at col 13-16, rows 5-8
    place_building(base, POKEMART, 13, 5)

    # Connect pokemart door (row 8, col 14-15) to vertical street (col 9-10) with 2-wide path
    fill_rect(base, 10, 8, 4, 2, PATH)  # row 8-9, col 10-13

    # Path around buildings: row 5, col 1-2 connecting avenue to buildings
    fill_rect(base, 1, 5, 2, 4, PATH)

    # === Arena entrance boulevard: rows 11-12, col 1-34 (3-wide, rows 11-12) ===
    fill_rect(base, 1, 11, 34, 2, PATH)

    # Vertical street connecting row 4 to row 11: col 9-10 (already done above to row 11)

    # === Round 1 (rows 13-15): 4 fenced battle lanes ===
    # Vertical connector from boulevard
    fill_rect(base, 9, 13, 2, 1, PATH)

    # Lane 1: col 3-5, rows 14-16
    fence_box(base, 3, 14, 3, 3)
    set_tile(overlay, 4, 15, TRAINER_DOWN)  # center

    # Lane 2: col 9-11, rows 14-16
    fence_box(base, 9, 14, 3, 3)
    set_tile(overlay, 10, 15, TRAINER_DOWN)

    # Lane 3: col 19-21, rows 14-16
    fence_box(base, 19, 14, 3, 3)
    set_tile(overlay, 20, 15, TRAINER_DOWN)

    # Lane 4: col 25-27, rows 14-16
    fence_box(base, 25, 14, 3, 3)
    set_tile(overlay, 26, 15, TRAINER_DOWN)

    # Central path (col 9-10) connects through rows 13
    fill_rect(base, 9, 13, 2, 1, PATH)

    # === Connecting path: row 16-17, col 1-34 (2-wide) ===
    fill_rect(base, 1, 17, 34, 2, PATH)

    # === Round 2 (rows 19-21): 2 fenced battle lanes ===
    # Lane 1: col 3-5
    fence_box(base, 3, 19, 3, 3)
    set_tile(overlay, 4, 20, TRAINER_DOWN)

    # Lane 2: col 9-11
    fence_box(base, 9, 19, 3, 3)
    set_tile(overlay, 10, 20, TRAINER_DOWN)

    # === Connecting path: row 22, col 1-34 (2-wide, rows 22-23) ===
    fill_rect(base, 1, 22, 34, 2, PATH)

    # Vertical connectors between rounds
    fill_rect(base, 9, 13, 2, 5, PATH)  # col 9-10 from row 13 down to row 17

    # === Final battle (rows 23-26): single large fenced arena ===
    # Fence border: col 10-25, rows 23-26
    fence_box(base, 10, 23, 16, 4)

    # Gym Leader (overlay 24) at col 17, row 24 on Path base
    set_tile(overlay, 17, 24, GYM_LEADER)

    return {
        "schemaVersion": 2,
        "mapId": "battle_arena",
        "displayName": "Battle Arena",
        "tileSize": 32,
        "width": W,
        "height": H,
        "baseTiles": base,
        "overlayTiles": overlay,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0",
    }


def generate_department_store():
    """Generate the Department Store Interior map (24x18)."""
    W, H = 24, 18
    base = make_grid(W, H, ROCK)
    overlay = make_grid(W, H, None)

    # Rock (8) border around all edges (already filled with ROCK)

    # === Entrance: double Door (4) at col 11-12, row 16-17 (bottom center) ===
    set_tile(base, 11, 16, DOOR)
    set_tile(base, 12, 16, DOOR)
    set_tile(base, 11, 17, DOOR)
    set_tile(base, 12, 17, DOOR)

    # === Main lobby corridor: row 14-15, col 1-22 (Path, full width inside walls) ===
    fill_rect(base, 1, 14, 22, 2, PATH)

    # === Two side corridors: col 1-2 and col 21-22, going up from lobby (Path, rows 7-15) ===
    fill_rect(base, 1, 7, 2, 9, PATH)
    fill_rect(base, 21, 7, 2, 9, PATH)

    # === Shop floor (rows 7-13): ===
    # Open Path floor from col 3-20, rows 7-13
    fill_rect(base, 3, 7, 18, 7, PATH)

    # Counter 1: Fence border at col 5-8, rows 8-10
    fence_box(base, 5, 8, 4, 3)
    # Shop entity (overlay 11) at col 6, row 9 on Path base
    set_tile(overlay, 6, 9, SHOP)

    # Counter 2: Fence border at col 14-17, rows 8-10
    fence_box(base, 14, 8, 4, 3)
    # Shop entity (overlay 11) at col 15, row 9 on Path base
    set_tile(overlay, 15, 9, SHOP)

    # NPCs (overlay 10): at col 5, row 12 and col 18, row 12
    set_tile(overlay, 5, 12, NPC)
    set_tile(overlay, 18, 12, NPC)

    # Sign (overlay 9) at col 11, row 12 (welcome sign)
    set_tile(overlay, 11, 12, SIGN)

    # === Cross corridor: row 6, col 1-22 (Path) ===
    fill_rect(base, 1, 6, 22, 1, PATH)

    # === Upper area (rows 1-5): ===
    # Path floor col 5-18, rows 1-5
    fill_rect(base, 5, 1, 14, 5, PATH)

    # Two vertical corridors: col 5-6 and col 17-18 connecting to cross corridor
    fill_rect(base, 5, 1, 2, 6, PATH)
    fill_rect(base, 17, 1, 2, 6, PATH)

    # Top corridor: row 1, col 5-18 (Path) - already covered by fill above

    # NPC (overlay 10) at col 3, row 3 on Path base
    set_tile(base, 3, 3, PATH)
    set_tile(overlay, 3, 3, NPC)

    # PC (overlay 41) at col 20, row 3 on Path base
    set_tile(base, 20, 3, PATH)
    set_tile(overlay, 20, 3, PC)

    # Key Item (overlay 14) at col 11, row 5 on Path base
    set_tile(overlay, 11, 5, KEY_ITEM)

    return {
        "schemaVersion": 2,
        "mapId": "department_store",
        "displayName": "Department Store Interior",
        "tileSize": 32,
        "width": W,
        "height": H,
        "baseTiles": base,
        "overlayTiles": overlay,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0",
    }


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    arena = generate_battle_arena()
    save_map(arena, "battle_arena.map.json")

    store = generate_department_store()
    save_map(store, "department_store.map.json")

    print("\nDone! Generated 2 map files.")


if __name__ == "__main__":
    main()

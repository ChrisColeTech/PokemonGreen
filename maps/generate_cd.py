"""Generate Map C (Evergreen Expanse) and Map D (Safari Zone) as JSON files."""

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
TALL_GRASS = 7
ROCK = 8
CAVE = 15
WARP = 16
WATER_EDGE = 17
FENCE = 18
FLOWER = 19
SURF_WATER = 25
STRENGTH_ROCK = 26
CUT_TREE = 27
RARE_GRASS = 28
LEGENDARY = 29

# Overlay IDs
SIGN = 9
NPC = 10
SHOP = 11
HEAL = 12
ITEM = 13
KEY_ITEM = 14
HIDDEN_ITEM = 40
PC = 41
POKEBALL = 42
STATUE = 49
TRAINER_DOWN = 21
VILLAIN_BOSS = 30
MINION_DOWN = 36

# Building footprints
BUILDINGS = {
    "pokecenter": [[3,3,3,3],[3,4,4,3],[3,4,4,3],[6,4,4,6]],
    "pokemart": [[3,3,3,3],[3,6,6,3],[3,11,6,3],[6,4,4,6]],
    "house_small": [[3,3,3],[3,4,3],[6,4,6]],
    "house_large": [[3,3,3,3],[3,6,6,3],[3,4,6,3],[6,4,6,6]],
    "cave_entrance": [[3,3,3],[15,15,15]],
    "pond": [[17,0,0,17],[0,0,0,0],[17,0,0,17]],
}


def make_grid(width, height, fill=GRASS):
    """Create a 2D grid filled with a value."""
    return [[fill for _ in range(width)] for _ in range(height)]


def make_overlay(width, height):
    """Create a 2D overlay grid filled with None."""
    return [[None for _ in range(width)] for _ in range(height)]


def fill_rect(grid, col, row, w, h, tile):
    """Fill a rectangle in the grid. col/row are 0-indexed."""
    for r in range(row, row + h):
        for c in range(col, col + w):
            if 0 <= r < len(grid) and 0 <= c < len(grid[0]):
                grid[r][c] = tile


def set_tile(grid, col, row, tile):
    """Set a single tile. col/row are 0-indexed."""
    if 0 <= row < len(grid) and 0 <= col < len(grid[0]):
        grid[row][col] = tile


def place_building(grid, col, row, building_name):
    """Place a building footprint at col, row (top-left corner, 0-indexed)."""
    footprint = BUILDINGS[building_name]
    for dr, brow in enumerate(footprint):
        for dc, tile in enumerate(brow):
            set_tile(grid, col + dc, row + dr, tile)


def draw_hline(grid, col_start, col_end, row, tile, width=1):
    """Draw a horizontal line (inclusive col_start to col_end)."""
    for w in range(width):
        for c in range(col_start, col_end + 1):
            set_tile(grid, c, row + w, tile)


def draw_vline(grid, row_start, row_end, col, tile, width=1):
    """Draw a vertical line (inclusive row_start to row_end)."""
    for w in range(width):
        for r in range(row_start, row_end + 1):
            set_tile(grid, col + w, r, tile)


def tree_border(grid, width, height):
    """Draw tree border around all edges (1 tile thick)."""
    for c in range(width):
        set_tile(grid, c, 0, TREE)
        set_tile(grid, c, height - 1, TREE)
    for r in range(height):
        set_tile(grid, 0, r, TREE)
        set_tile(grid, width - 1, r, TREE)


def save_map(data, filepath):
    """Save map data as JSON."""
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
    print(f"Saved: {filepath}")
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
    print(f"  Validated: {data['width']}x{data['height']} ({data['width'] * data['height']} cells)")


# =============================================================================
# MAP C: Evergreen Expanse (56 x 44)
# =============================================================================
def generate_evergreen_expanse():
    W, H = 56, 44
    base = make_grid(W, H, GRASS)
    overlay = make_overlay(W, H)

    # Tree border
    tree_border(base, W, H)

    # --- North entry: cols 1-4, row 0 (gap in tree border) ---
    for c in range(1, 5):
        set_tile(base, c, 0, PATH)

    # --- South exit: cols 52-54, row 43 (gap in tree border) ---
    for c in range(52, 55):
        set_tile(base, c, 43, PATH)

    # --- WEST TRAIL: starts at north entry, winds south along left side ---
    # Row 1-5: straight down from entry at cols 1-4
    fill_rect(base, 1, 1, 4, 5, PATH)  # cols 1-4, rows 1-5

    # Row 5-7: continue cols 1-4
    fill_rect(base, 1, 6, 4, 2, PATH)  # cols 1-4, rows 6-7

    # Row 8: shift right slightly, cols 1-3 -> path widens
    draw_hline(base, 1, 4, 8, PATH, 1)

    # Row 9-10: cols 2-4
    fill_rect(base, 2, 9, 3, 2, PATH)

    # Row 11: big east-west connector at top of loop
    draw_hline(base, 2, 42, 11, PATH, 1)

    # Row 12-20: west trail continues south cols 3-5
    fill_rect(base, 3, 12, 3, 9, PATH)

    # Row 21-22: shift slightly, cols 3-5
    fill_rect(base, 3, 21, 4, 2, PATH)

    # Row 22: big east-west connector at bottom of loop
    draw_hline(base, 3, 42, 22, PATH, 1)

    # Row 23-26: cols 2-4 continue south
    fill_rect(base, 2, 23, 3, 4, PATH)

    # Row 27-32: cols 1-4 along tall grass and cave
    fill_rect(base, 1, 27, 4, 6, PATH)

    # Row 33-35: cols 2-4
    fill_rect(base, 2, 33, 3, 3, PATH)

    # Row 36-38: cols 3-5
    fill_rect(base, 3, 36, 3, 3, PATH)

    # Row 39-40: cols 4-6
    fill_rect(base, 4, 39, 3, 2, PATH)

    # Row 41: cols 5-7
    fill_rect(base, 5, 41, 3, 1, PATH)

    # Row 42: cols 6-8 -> connect east
    draw_hline(base, 6, 25, 42, PATH, 1)

    # Row 43 connector to south exit
    draw_hline(base, 7, 54, 43, PATH, 1)  # will be overwritten by tree border partially
    # Restore tree border except south exit
    set_tile(base, 0, 43, TREE)
    for c in range(1, 52):
        # keep grass or path
        pass
    # Actually, re-establish tree border on row 43 except exit
    for c in range(W):
        if c < 52 or c > 54:
            set_tile(base, c, 43, TREE)

    # --- EAST TRAIL: runs along right side ---
    # East vertical trail: cols 43-45, rows 1-10
    fill_rect(base, 43, 1, 3, 10, PATH)

    # Connect to row 11 (already drawn)

    # East trail south: cols 43-45, rows 12-20
    fill_rect(base, 43, 12, 3, 9, PATH)

    # East trail continuation: cols 43-45, rows 21 already on row 22 connector
    fill_rect(base, 43, 21, 3, 1, PATH)

    # East trail south side: cols 43-45, rows 23-42
    fill_rect(base, 43, 23, 3, 11, PATH)

    # Connect east trail to south exit
    draw_hline(base, 43, 54, 34, PATH, 1)
    fill_rect(base, 43, 35, 3, 5, PATH)
    draw_hline(base, 45, 54, 39, PATH, 1)
    fill_rect(base, 50, 39, 3, 4, PATH)
    fill_rect(base, 52, 42, 3, 1, PATH)

    # --- N-S connector avenues ---
    # Middle north-south avenue: col 22-24
    fill_rect(base, 22, 1, 3, 10, PATH)  # rows 1-10

    # South connector from row 22 to row 33
    fill_rect(base, 22, 23, 3, 11, PATH)

    # --- Additional cross connectors ---
    draw_hline(base, 22, 43, 5, PATH, 1)  # mid-north cross

    # --- CENTRAL LAKE: Water Edge border around Water interior ---
    # Lake: col 18-31, rows 12-19 (14w x 8h)
    # Water Edge border
    fill_rect(base, 18, 12, 14, 8, WATER_EDGE)
    # Water interior
    fill_rect(base, 19, 13, 12, 6, WATER)
    # Surf Water patch in center: col 22-23, rows 15-16
    set_tile(base, 22, 15, SURF_WATER)
    set_tile(base, 23, 15, SURF_WATER)
    set_tile(base, 22, 16, SURF_WATER)
    set_tile(base, 23, 16, SURF_WATER)

    # --- CAVE AREA: Rock enclosure col 24-33, rows 26-31 ---
    # Rock border
    fill_rect(base, 24, 26, 10, 1, ROCK)  # top row
    fill_rect(base, 24, 31, 10, 1, ROCK)  # bottom row
    draw_vline(base, 26, 31, 24, ROCK)     # left wall
    draw_vline(base, 26, 31, 33, ROCK)     # right wall
    # Interior is grass (already default)
    fill_rect(base, 25, 27, 8, 4, GRASS)

    # Cave entrance building inside: col 27-29, rows 28-29
    place_building(base, 27, 28, "cave_entrance")

    # Strength Rock in rock wall at bottom: row 31, col 28
    set_tile(base, 28, 31, STRENGTH_ROCK)

    # --- TALL GRASS ZONES ---
    # NW zone: col 6-17, rows 1-3
    fill_rect(base, 6, 1, 12, 3, TALL_GRASS)

    # NE zone: col 38-46, rows 1-4
    fill_rect(base, 38, 1, 9, 4, TALL_GRASS)

    # East zone 1: col 46-50, rows 12-19
    fill_rect(base, 46, 12, 5, 8, TALL_GRASS)

    # East zone 2: col 47-51, rows 20-25
    fill_rect(base, 47, 20, 5, 6, TALL_GRASS)

    # SW zone: col 5-10, rows 26-31
    fill_rect(base, 5, 27, 6, 5, TALL_GRASS)

    # SE-ish zone: col 43-47, rows 35-38
    fill_rect(base, 43, 35, 5, 4, TALL_GRASS)

    # --- RARE GRASS ---
    # East meadow: col 47-48, rows 23-24
    set_tile(base, 47, 23, RARE_GRASS)
    set_tile(base, 48, 23, RARE_GRASS)
    set_tile(base, 47, 24, RARE_GRASS)
    set_tile(base, 48, 24, RARE_GRASS)

    # Behind cut tree: col 14-15, rows 38-40
    for r in range(38, 41):
        set_tile(base, 14, r, RARE_GRASS)
        set_tile(base, 15, r, RARE_GRASS)

    # --- CUT TREE at col 14, row 35 ---
    set_tile(base, 14, 35, CUT_TREE)

    # --- Tree wall around legendary area: col 12-16, rows 35-41 ---
    # Left wall
    draw_vline(base, 35, 41, 12, TREE)
    # Right wall
    draw_vline(base, 35, 41, 16, TREE)
    # Top wall
    draw_hline(base, 12, 16, 35, TREE, 1)
    # Bottom wall
    draw_hline(base, 12, 16, 41, TREE, 1)
    # Interior: cols 13-15, rows 36-40 are grass/rare grass (already set above for rare)
    fill_rect(base, 13, 36, 3, 2, GRASS)  # rows 36-37 grass
    # rows 38-40 cols 14-15 already rare grass, col 13 grass
    set_tile(base, 13, 38, GRASS)
    set_tile(base, 13, 39, GRASS)
    set_tile(base, 13, 40, GRASS)

    # Cut tree replaces one tree in the wall
    set_tile(base, 14, 35, CUT_TREE)

    # --- LEGENDARY tile at col 14, row 40 ---
    set_tile(base, 14, 40, LEGENDARY)

    # --- OVERLAY ENTITIES ---
    # Items (overlay 13): at col 15, row 8 and col 13, row 28
    set_tile(overlay, 15, 8, ITEM)
    set_tile(base, 15, 8, GRASS)  # ensure base is walkable
    set_tile(overlay, 13, 28, ITEM)
    set_tile(base, 13, 28, GRASS)

    # Hidden Item (overlay 40): at col 28, row 17
    set_tile(overlay, 28, 17, HIDDEN_ITEM)
    # base is water here from lake - set to grass near lake edge
    # Actually col 28 row 17 is inside the lake water. Let's place it just outside.
    # The template shows H at row 18 col 34 area. Let's follow the instruction literally.
    # col 28, row 17 is inside water. The instructions say to put it there.
    # For overlay, the base should be walkable. Let's set base to path there.
    set_tile(base, 28, 17, PATH)

    # NPC (overlay 10): at col 25, row 20
    set_tile(overlay, 25, 20, NPC)
    set_tile(base, 25, 20, PATH)

    # 2 Trainers (overlay 21): at col 14, row 5 and col 47, row 15
    set_tile(overlay, 14, 5, TRAINER_DOWN)
    set_tile(base, 14, 5, GRASS)
    set_tile(overlay, 47, 15, TRAINER_DOWN)
    set_tile(base, 47, 15, GRASS)

    return {
        "schemaVersion": 2,
        "mapId": "evergreen_expanse",
        "displayName": "Evergreen Expanse",
        "tileSize": 32,
        "width": W,
        "height": H,
        "baseTiles": base,
        "overlayTiles": overlay,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0",
    }


# =============================================================================
# MAP D: Safari Zone (50 x 40)
# =============================================================================
def generate_safari_zone():
    W, H = 50, 40
    base = make_grid(W, H, GRASS)
    overlay = make_overlay(W, H)

    # Tree border
    tree_border(base, W, H)

    # --- North entry: wide path col 4-24, rows 1-2 ---
    fill_rect(base, 4, 1, 21, 2, PATH)  # cols 4-24, rows 1-2

    # Rows 3-4: narrower path leading in
    fill_rect(base, 8, 3, 4, 2, PATH)   # left avenue starts
    fill_rect(base, 36, 3, 4, 2, PATH)  # right avenue starts

    # --- Two N-S path avenues ---
    # Left avenue: col 8-11, rows 1 to 37
    fill_rect(base, 8, 1, 4, 37, PATH)

    # Right avenue: col 36-39, rows 1 to 37
    fill_rect(base, 36, 1, 4, 37, PATH)

    # --- Connect entry to right avenue ---
    fill_rect(base, 4, 1, 36, 2, PATH)  # wide entry path rows 1-2

    # --- E-W streets connecting avenues to grass zones (2-wide) ---
    # Zone A connectors (rows 6-7)
    fill_rect(base, 12, 6, 3, 2, PATH)   # from left avenue to zone A
    fill_rect(base, 26, 6, 10, 2, PATH)  # from zone A to right avenue

    # Zone B connectors (rows 18-19)
    fill_rect(base, 12, 18, 3, 2, PATH)
    fill_rect(base, 26, 18, 10, 2, PATH)

    # Zone C connectors (rows 30-31)
    fill_rect(base, 12, 30, 3, 2, PATH)
    fill_rect(base, 24, 30, 12, 2, PATH)

    # --- Bottom path network ---
    # Row 37: connecting path
    draw_hline(base, 8, 39, 37, PATH, 1)

    # Zone D approach paths
    fill_rect(base, 22, 36, 4, 2, PATH)  # col 22-25 connecting down
    fill_rect(base, 36, 36, 4, 2, PATH)

    # Row 38: path to legendary area
    draw_hline(base, 22, 39, 38, PATH, 1)

    # --- Water Channel 1: rows 12-14 ---
    # Row 12 (top edge): Water Edge full width (inside borders)
    fill_rect(base, 1, 12, 48, 1, WATER_EDGE)
    # Row 13 (middle): Water full width
    fill_rect(base, 1, 13, 48, 1, WATER)
    # Row 14 (bottom edge): Water Edge full width
    fill_rect(base, 1, 14, 48, 1, WATER_EDGE)

    # Bridges at left avenue (col 8-11) over channel 1
    fill_rect(base, 8, 12, 4, 3, BRIDGE)

    # Bridges at right avenue (col 36-39) over channel 1
    fill_rect(base, 36, 12, 4, 3, BRIDGE)

    # --- Water Channel 2: rows 24-26 ---
    fill_rect(base, 1, 24, 48, 1, WATER_EDGE)
    fill_rect(base, 1, 25, 48, 1, WATER)
    fill_rect(base, 1, 26, 48, 1, WATER_EDGE)

    # Bridges at left avenue (col 8-11) over channel 2
    fill_rect(base, 8, 24, 4, 3, BRIDGE)

    # Bridges at right avenue (col 36-39) over channel 2
    fill_rect(base, 36, 24, 4, 3, BRIDGE)

    # --- Zone A (rows 5-10): Tall Grass patch col 15-25, rows 5-9 ---
    fill_rect(base, 15, 5, 11, 5, TALL_GRASS)

    # --- Zone B (rows 17-22): Tall Grass patch col 15-25, rows 17-21 ---
    fill_rect(base, 15, 17, 11, 5, TALL_GRASS)

    # --- Zone C (rows 29-33): Rare Grass patch col 15-23, rows 29-33 ---
    fill_rect(base, 15, 29, 9, 5, RARE_GRASS)

    # --- Zone D (rows 37-39): Flower patches ---
    # Flower patches at col 15-17 and col 20-22, rows 37-38 (0-indexed)
    fill_rect(base, 15, 37, 3, 2, FLOWER)
    fill_rect(base, 20, 37, 3, 2, FLOWER)

    # Legendary (29) at col 40, row 37 (0-indexed)
    set_tile(base, 40, 37, LEGENDARY)

    # --- OVERLAY ENTITIES ---
    # Item (overlay 13) at col 20, row 7 (Zone A)
    set_tile(overlay, 20, 7, ITEM)
    set_tile(base, 20, 7, TALL_GRASS)  # keep tall grass underneath

    # Pokeball (overlay 42) at col 20, row 20 (Zone B)
    set_tile(overlay, 20, 20, POKEBALL)
    set_tile(base, 20, 20, TALL_GRASS)

    # Hidden Item (overlay 40) at col 18, row 31 (Zone C)
    set_tile(overlay, 18, 31, HIDDEN_ITEM)
    set_tile(base, 18, 31, RARE_GRASS)

    # NPC (overlay 10) at col 22, row 3 near entry
    set_tile(overlay, 22, 3, NPC)
    set_tile(base, 22, 3, PATH)

    return {
        "schemaVersion": 2,
        "mapId": "safari_zone",
        "displayName": "Safari Zone",
        "tileSize": 32,
        "width": W,
        "height": H,
        "baseTiles": base,
        "overlayTiles": overlay,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0",
    }


# =============================================================================
# Main
# =============================================================================
if __name__ == "__main__":
    out_dir = os.path.join(os.path.dirname(__file__), "exported")

    map_c = generate_evergreen_expanse()
    save_map(map_c, os.path.join(out_dir, "evergreen_expanse.map.json"))

    map_d = generate_safari_zone()
    save_map(map_d, os.path.join(out_dir, "safari_zone.map.json"))

    print("\nDone! Generated 2 maps.")

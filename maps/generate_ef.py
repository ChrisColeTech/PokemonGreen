"""Generate Map E (Mt. Granite Cave) and Map F (Lake Serenity) as JSON files."""

import json
import os

# Tile IDs
WATER = 0
GRASS = 1
PATH = 2
TREE = 3
DOOR = 4
BRIDGE = 5
WALL = 6
TALL_GRASS = 7
ROCK = 8
SIGN = 9
NPC = 10
SHOP = 11
HEAL = 12
ITEM = 13
KEY_ITEM = 14
CAVE = 15
WARP = 16
WATER_EDGE = 17
FENCE = 18
FLOWER = 19
TRAINER_DOWN = 21
SURF_WATER = 25
STRENGTH_ROCK = 26
CUT_TREE = 27
RARE_GRASS = 28
LEGENDARY = 29
VILLAIN_BOSS = 30
MINION_DOWN = 36
HIDDEN_ITEM = 40
PC = 41
POKEBALL = 42
STATUE = 49

# Overlay tile IDs (these go on overlay layer with appropriate base underneath)
OVERLAY_IDS = {SIGN, NPC, SHOP, HEAL, ITEM, KEY_ITEM, HIDDEN_ITEM, PC, POKEBALL, STATUE,
               TRAINER_DOWN, VILLAIN_BOSS, MINION_DOWN}

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "exported")


def make_grid(width, height, fill):
    """Create a width x height grid filled with a single value."""
    return [[fill] * width for _ in range(height)]


def fill_rect(grid, col, row, w, h, tile_id):
    """Fill a rectangle in the grid. col/row are 0-indexed."""
    for r in range(row, row + h):
        for c in range(col, col + w):
            if 0 <= r < len(grid) and 0 <= c < len(grid[0]):
                grid[r][c] = tile_id


def hline(grid, row, col_start, col_end, tile_id):
    """Draw a horizontal line (inclusive on both ends). 0-indexed."""
    for c in range(col_start, col_end + 1):
        if 0 <= row < len(grid) and 0 <= c < len(grid[0]):
            grid[row][c] = tile_id


def vline(grid, col, row_start, row_end, tile_id):
    """Draw a vertical line (inclusive on both ends). 0-indexed."""
    for r in range(row_start, row_end + 1):
        if 0 <= r < len(grid) and 0 <= col < len(grid[0]):
            grid[r][col] = tile_id


def set_tile(grid, col, row, tile_id):
    """Set a single tile. 0-indexed."""
    if 0 <= row < len(grid) and 0 <= col < len(grid[0]):
        grid[row][col] = tile_id


def place_building(base, footprint, col, row):
    """Place a building footprint into baseTiles at (col, row) top-left. 0-indexed."""
    for dr, brow in enumerate(footprint):
        for dc, tile_id in enumerate(brow):
            set_tile(base, col + dc, row + dr, tile_id)


def place_overlay(base, overlay, col, row, overlay_id, base_id=PATH):
    """Place an overlay entity and set appropriate base tile underneath. 0-indexed."""
    set_tile(base, col, row, base_id)
    set_tile(overlay, col, row, overlay_id)


def tunnel_h(base, row, col_start, col_end, width=1):
    """Carve a horizontal tunnel (path) of given width."""
    for w in range(width):
        hline(base, row + w, col_start, col_end, PATH)


def tunnel_v(base, col, row_start, row_end, width=1):
    """Carve a vertical tunnel (path) of given width."""
    for w in range(width):
        vline(base, col + w, row_start, row_end, PATH)


def save_map(data, filename):
    filepath = os.path.join(OUTPUT_DIR, filename)
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
# MAP E: Mt. Granite Cave (42 x 34)
# =============================================================================
# ASCII template uses 1-indexed rows/cols. We convert to 0-indexed internally.
# Template row 1 = index 0, col 1 = index 0.

def generate_mt_granite_cave():
    W, H = 42, 34
    base = make_grid(W, H, ROCK)
    overlay = make_grid(W, H, None)

    # Cave entrance building at col 4-6, row 1-2 (0-indexed: col 3-5, row 0-1)
    # cave_entrance footprint: [[3,3,3],[15,15,15]]
    cave_entrance = [[TREE, TREE, TREE], [CAVE, CAVE, CAVE]]
    place_building(base, cave_entrance, 3, 0)  # col 4-6 -> idx 3-5, row 1-2 -> idx 0-1

    # Entry tunnel: col 4-6 (idx 3-5), rows 3-6 (idx 2-5), 3 wide
    tunnel_v(base, 3, 2, 5, width=3)

    # Upper tunnels
    # Path branches right at row 6 (idx 5): hline row 6, col 4 to col 30
    tunnel_h(base, 5, 3, 29, width=2)  # rows 6-7 (idx 5-6)

    # Branch down from col 10 (idx 9): vline rows 6-12 (idx 5-11)
    tunnel_v(base, 9, 5, 12, width=2)  # col 10-11

    # Branch right from col 10 at row 9: path col 14-20, rows 8-9 (idx 7-8, col 13-19)
    tunnel_h(base, 7, 9, 19, width=2)  # rows 8-9
    # Dead end continuation col 14-20 at rows 8-9
    tunnel_h(base, 8, 13, 19, width=1)

    # Item (overlay 13) at col 18, row 8 (idx 17, 7)
    place_overlay(base, overlay, 17, 7, ITEM, PATH)

    # Item at col 5, row 10 (idx 4, 9)
    # Need path there first - extend entry tunnel area
    tunnel_v(base, 3, 6, 11, width=2)  # col 4-5 going down
    place_overlay(base, overlay, 4, 9, ITEM, PATH)

    # Upper-right area: vertical path from col 30 (idx 29) down rows 5-12 (idx 4-11)
    tunnel_v(base, 29, 4, 11, width=2)  # col 30-31

    # Branch at rows 8-9 going left to col 14-20 area (connect from right side)
    tunnel_h(base, 7, 19, 29, width=2)  # rows 8-9 connecting east side

    # Item at col 26, row 8 (idx 25, 7)
    place_overlay(base, overlay, 25, 7, ITEM, PATH)

    # Main east-west tunnel: row 12 (idx 11), col 4-33 (idx 3-32), 2 wide
    # Actually template says row 13 (idx 12)
    tunnel_h(base, 12, 3, 32, width=1)

    # Lower section
    # West branch: col 4 (idx 3), rows 12-18 (idx 11-17)
    tunnel_v(base, 3, 11, 17, width=2)

    # then right to col 10 at row 18 (idx 17): hline
    tunnel_h(base, 17, 3, 10, width=2)  # rows 18-19

    # Strength Rock (26) at col 11, row 18 (idx 10, 17) blocking shortcut
    set_tile(base, 10, 18, STRENGTH_ROCK)

    # Vertical connector from strength rock area down
    tunnel_v(base, 10, 13, 17, width=2)  # col 11-12 area

    # East branches: col 27-30 going down from row 12 to row 22 (idx 26-29, 11-21)
    tunnel_v(base, 29, 11, 21, width=2)  # col 30-31
    tunnel_v(base, 26, 13, 21, width=1)  # col 27

    # Connector between east branches
    tunnel_h(base, 15, 26, 31, width=2)

    # Hidden Item (overlay 40) at col 21, row 20 (idx 20, 19)
    # Need path there
    tunnel_h(base, 19, 15, 26, width=2)  # connect to area
    place_overlay(base, overlay, 20, 20, HIDDEN_ITEM, PATH)

    # Deep tunnel: row 22 (idx 21), col 3-35 (idx 2-34)
    tunnel_h(base, 22, 2, 34, width=1)

    # Villain area (bottom)
    # Branch down from col 3-9 at row 22-26 (idx 2-8, 21-25)
    tunnel_v(base, 2, 21, 25, width=2)  # col 3-4
    tunnel_v(base, 5, 21, 25, width=2)  # col 6-7

    # Minion (overlay 36) at col 5, row 24 (idx 4, 23)
    place_overlay(base, overlay, 4, 24, MINION_DOWN, PATH)

    # Further branch col 10 down rows 26-30 (idx 9, 25-29)
    tunnel_v(base, 9, 22, 29, width=2)
    tunnel_h(base, 26, 2, 11, width=1)  # connect at row 27

    # Villain Boss (overlay 30) at col 12, row 28 (idx 11, 27)
    tunnel_v(base, 11, 26, 29, width=2)
    place_overlay(base, overlay, 11, 28, VILLAIN_BOSS, PATH)

    # Branch continues to col 18 at row 30 (idx 17, 29)
    tunnel_h(base, 30, 9, 17, width=1)

    # Right side: col 37-39 going down rows 22-33 (idx 36-38, 21-32)
    tunnel_v(base, 36, 21, 32, width=2)  # col 37-38
    # Connect to deep tunnel
    tunnel_h(base, 22, 34, 38, width=1)

    # Pokeball (overlay 42) at col 30, row 32 (idx 29, 31)
    # Need path to it
    tunnel_h(base, 31, 17, 36, width=1)
    place_overlay(base, overlay, 29, 31, POKEBALL, PATH)

    # Two Warp tiles (16) at row 33 (idx 32): one at col 20 (idx 19), one at col 34 (idx 33)
    # Need path connections
    tunnel_h(base, 33, 17, 38, width=1)  # row 34 (idx 33) path
    tunnel_v(base, 19, 29, 33, width=1)  # connect warp 1
    tunnel_v(base, 33, 29, 33, width=1)  # connect warp 2
    set_tile(base, 19, 33, WARP)
    set_tile(base, 33, 33, WARP)

    # Add some extra dead-end branches for maze feel
    # Small branch off main tunnel going up at col 20
    tunnel_v(base, 20, 8, 12, width=1)

    # Small branch off deep tunnel going down at col 15
    tunnel_v(base, 15, 22, 26, width=1)

    return {
        "schemaVersion": 2,
        "mapId": "mt_granite_cave",
        "displayName": "Mt. Granite Cave",
        "tileSize": 32,
        "width": W,
        "height": H,
        "baseTiles": base,
        "overlayTiles": overlay,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0",
    }


# =============================================================================
# MAP F: Lake Serenity (48 x 38)
# =============================================================================

def generate_lake_serenity():
    W, H = 48, 38
    base = make_grid(W, H, GRASS)
    overlay = make_grid(W, H, None)

    # Tree border around all edges, 1 tile thick
    # Top row
    hline(base, 0, 0, W - 1, TREE)
    # Bottom row
    hline(base, H - 1, 0, W - 1, TREE)
    # Left column
    vline(base, 0, 0, H - 1, TREE)
    # Right column
    vline(base, W - 1, 0, H - 1, TREE)

    # West entry: col 1-4, rows 1-5 (idx 0-3, 0-4) - path gap in tree border
    # Template: rows 2-6 (idx 1-5), col 1-4 (idx 0-3)
    fill_rect(base, 0, 1, 4, 5, PATH)  # col 1-4, rows 2-6

    # Shoreline path curving right and down
    # row 2-5: col 1-4 path (already done)
    # row 6 (idx 5): path shifts right
    fill_rect(base, 1, 5, 4, 1, PATH)
    # row 7-8 (idx 6-7): col 2-5
    fill_rect(base, 2, 6, 4, 2, PATH)
    # row 9-10 (idx 8-9): col 3-6
    fill_rect(base, 3, 8, 4, 2, PATH)
    # row 11-12 (idx 10-11): col 3-6
    fill_rect(base, 3, 10, 4, 2, PATH)
    # row 13-14 (idx 12-13): col 4-7
    fill_rect(base, 4, 12, 4, 2, PATH)
    # row 15-16 (idx 14-15): col 5-8
    fill_rect(base, 5, 14, 4, 2, PATH)
    # row 17 (idx 16): col 5-8
    fill_rect(base, 5, 16, 4, 1, PATH)
    # row 18 (idx 17): path connects to bridge col 6-13
    fill_rect(base, 6, 17, 8, 1, PATH)

    # East exit: col 44-47, rows 4-6 (idx 43-46, 3-5) - path gap on right side
    # Template: rows 5-7 (idx 4-6), col 45-48 (idx 44-47)
    fill_rect(base, 44, 4, 3, 3, PATH)
    set_tile(base, 47, 4, PATH)
    set_tile(base, 47, 5, PATH)
    set_tile(base, 47, 6, PATH)

    # SW exit: col 3-5, rows 34-36 (idx 2-4, 33-35)
    # Template: rows 35-37 (idx 34-36), col 3-5 (idx 2-4)
    fill_rect(base, 2, 34, 3, 3, PATH)
    # Clear tree border for exit
    set_tile(base, 2, 37, PATH)
    set_tile(base, 3, 37, PATH)
    set_tile(base, 4, 37, PATH)

    # SE exit: col 20-22, rows 34-36 (idx 19-21, 33-35)
    # Template: rows 35-37 (idx 34-36), col 20-22 (idx 19-21)
    fill_rect(base, 19, 34, 3, 3, PATH)
    set_tile(base, 19, 37, PATH)
    set_tile(base, 20, 37, PATH)
    set_tile(base, 21, 37, PATH)

    # South shoreline path: connects bridge landing to south exits
    # From bridge end (row 18, col 13-14) going south and then splitting
    # Path going south along west side of lake
    fill_rect(base, 2, 34, 20, 1, PATH)  # row 35 connector
    # Vertical path from row 18 area down to row 34
    tunnel_v(base, 4, 17, 34, width=2)  # col 5-6 going south
    # Branch east to SE exit
    tunnel_h(base, 34, 4, 21, width=1)

    # The Lake
    # Water Edge (17) border: rectangle from col 14-33, rows 1-33 (idx 13-32, 0-32)
    # Template shows: col 15-34 roughly (idx 14-33), rows 2-34 (idx 1-33)
    # Water Edge border
    hline(base, 1, 14, 33, WATER_EDGE)   # top edge
    hline(base, 33, 14, 33, WATER_EDGE)  # bottom edge
    vline(base, 14, 1, 33, WATER_EDGE)   # left edge
    vline(base, 33, 1, 33, WATER_EDGE)   # right edge

    # Water (0) fill inside: col 15-32, rows 2-32 (idx 15-32, 2-32)
    fill_rect(base, 15, 2, 18, 31, WATER)  # col 15-32, rows 2-32

    # Island 1 (NPC island): col 20-23, rows 7-10 (idx 19-22, 6-9)
    # Water Edge border around island
    hline(base, 6, 19, 22, WATER_EDGE)   # top
    hline(base, 11, 19, 22, WATER_EDGE)  # bottom (row 12 idx 11)
    vline(base, 19, 6, 11, WATER_EDGE)   # left
    vline(base, 22, 6, 11, WATER_EDGE)   # right
    # Grass inside
    fill_rect(base, 20, 7, 2, 4, GRASS)  # col 21-22, rows 8-11 (idx 20-21, 7-10)

    # NPC overlay at col 21, row 8 (idx 20, 7) -> but template says row 9 (idx 8)
    place_overlay(base, overlay, 20, 8, NPC, GRASS)

    # Item overlay at col 22, row 10 (idx 21, 9) -> template row 11 (idx 10)
    place_overlay(base, overlay, 21, 10, ITEM, GRASS)

    # Island 2 (Rare grass): col 20-23, rows 21-24 (idx 19-22, 20-23)
    hline(base, 20, 19, 22, WATER_EDGE)
    hline(base, 25, 19, 22, WATER_EDGE)  # bottom border (row 26, idx 25)
    vline(base, 19, 20, 25, WATER_EDGE)
    vline(base, 22, 20, 25, WATER_EDGE)  # right (col 23, idx 22) -- reuse
    # Inner grass
    fill_rect(base, 20, 21, 2, 4, GRASS)
    # Rare Grass (28) at col 21-22, rows 22-23 (idx 20-21, 21-22)
    set_tile(base, 20, 22, RARE_GRASS)
    set_tile(base, 21, 22, RARE_GRASS)
    set_tile(base, 20, 23, RARE_GRASS)
    set_tile(base, 21, 23, RARE_GRASS)

    # Island 3 (Legendary): col 26-28, rows 28-30 (idx 25-27, 27-29)
    # Water Edge border
    hline(base, 27, 25, 28, WATER_EDGE)  # top (use col 26-29 idx 25-28)
    hline(base, 31, 25, 28, WATER_EDGE)  # bottom
    vline(base, 25, 27, 31, WATER_EDGE)
    vline(base, 28, 27, 31, WATER_EDGE)
    # Grass inside
    fill_rect(base, 26, 28, 2, 3, GRASS)
    # Legendary (29) at col 27, row 29 (idx 26, 29) -> template row 30 (idx 29)
    set_tile(base, 26, 29, LEGENDARY)

    # Surf Water (25): band at col 17-28, rows 13-14 (idx 16-27, 12-13)
    fill_rect(base, 16, 13, 12, 2, SURF_WATER)  # col 17-28, rows 14-15

    # Bridge (5): horizontal at row 17 (idx 16... but template says row 18, idx 17)
    # Bridge row 18 (idx 17), col 7-14 (idx 6-13)
    hline(base, 17, 6, 13, BRIDGE)

    # Sign (overlay 9) at col 8, row 13 (idx 7, 12) -> template row 14 (idx 13)
    place_overlay(base, overlay, 7, 13, SIGN, PATH)

    # Tall Grass (7): patch at col 40-43, rows 18-20 (idx 39-42, 17-19)
    # Template: rows 19-21 (idx 18-20), col 41-44 (idx 40-43)
    fill_rect(base, 40, 18, 4, 3, TALL_GRASS)

    return {
        "schemaVersion": 2,
        "mapId": "lake_serenity",
        "displayName": "Lake Serenity",
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
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    cave = generate_mt_granite_cave()
    save_map(cave, "mt_granite_cave.map.json")

    lake = generate_lake_serenity()
    save_map(lake, "lake_serenity.map.json")

    print("\nDone. Generated 2 map files.")

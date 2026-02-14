"""Generate Map A (Greenleaf Metro 64x48) and Map B (Coastal Route 25x56)."""

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

# Building footprints
BUILDINGS = {
    "pokecenter": [[3,3,3,3],[3,4,4,3],[3,4,4,3],[6,4,4,6]],
    "pokemart":   [[3,3,3,3],[3,6,6,3],[3,11,6,3],[6,4,4,6]],
    "gym":        [[3,3,3,3,3],[3,6,6,6,3],[3,6,12,6,3],[3,6,4,6,3],[6,6,4,6,6]],
    "house_small":[[3,3,3],[3,4,3],[6,4,6]],
    "house_large":[[3,3,3,3],[3,6,6,3],[3,4,6,3],[6,4,6,6]],
    "lab":        [[3,3,3,3,3],[3,6,6,6,3],[3,4,41,4,3],[6,4,4,4,6]],
    "cave_entrance": [[3,3,3],[15,15,15]],
    "gate":       [[6,6,6,6],[6,16,16,6],[6,6,6,6]],
    "pond":       [[17,0,0,17],[0,0,0,0],[17,0,0,17]],
}


def make_grid(width, height, fill=GRASS):
    return [[fill] * width for _ in range(height)]


def make_overlay(width, height):
    return [[None] * width for _ in range(height)]


def fill_rect(grid, x, y, w, h, tile):
    for r in range(y, y + h):
        for c in range(x, x + w):
            if 0 <= r < len(grid) and 0 <= c < len(grid[0]):
                grid[r][c] = tile


def hline(grid, x, y, length, tile):
    for c in range(x, x + length):
        if 0 <= y < len(grid) and 0 <= c < len(grid[0]):
            grid[y][c] = tile


def vline(grid, x, y, length, tile):
    for r in range(y, y + length):
        if 0 <= r < len(grid) and 0 <= x < len(grid[0]):
            grid[r][x] = tile


def place_building(grid, x, y, name):
    footprint = BUILDINGS[name]
    for dr, row in enumerate(footprint):
        for dc, tile in enumerate(row):
            r, c = y + dr, x + dc
            if 0 <= r < len(grid) and 0 <= c < len(grid[0]):
                grid[r][c] = tile


def place_overlay(base, overlay, x, y, tile, base_under=PATH):
    if 0 <= y < len(base) and 0 <= x < len(base[0]):
        base[y][x] = base_under
        overlay[y][x] = tile


def save_map(data, filepath):
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    with open(filepath, "w") as f:
        json.dump(data, f, indent=2, separators=(",", ": "))
    print(f"Saved: {filepath}")


# ---------------------------------------------------------------------------
# MAP A: Greenleaf Metro (64 x 48)
# ---------------------------------------------------------------------------
def generate_greenleaf_metro():
    W, H = 64, 48
    base = make_grid(W, H, GRASS)
    over = make_overlay(W, H)

    # --- Tree border (rows 0, 47; cols 0, 63) ---
    hline(base, 0, 0, W, TREE)      # top
    hline(base, 0, H - 1, W, TREE)  # bottom
    for r in range(H):
        base[r][0] = TREE
        base[r][W - 1] = TREE

    # --- Exits (gaps in tree border) ---
    # North exit: cols 12-15 row 0
    for c in range(12, 16):
        base[0][c] = PATH

    # West exit: rows 5-7 col 0
    for r in range(5, 8):
        base[r][0] = PATH

    # East exit: rows 23-24 cols 60-63
    for r in range(23, 25):
        for c in range(60, 64):
            base[r][c] = PATH

    # South exit 1: cols 12-15 row 47
    for c in range(12, 16):
        base[H - 1][c] = PATH

    # South exit 2: cols 28-31 row 47
    for c in range(28, 32):
        base[H - 1][c] = PATH

    # --- Main Boulevard: rows 16-19 (4 wide), cols 1-40 ---
    fill_rect(base, 1, 16, 40, 4, PATH)

    # --- N-S Avenue 1: cols 11-14, rows 1-47 ---
    fill_rect(base, 12, 1, 4, H - 2, PATH)

    # --- N-S Avenue 2: cols 23-25, rows 3-46 ---
    fill_rect(base, 23, 3, 3, H - 5, PATH)

    # --- N-S Avenue 3: cols 35-37, rows 3-46 ---
    fill_rect(base, 35, 3, 4, H - 5, PATH)

    # --- N-S Avenue 4: cols 50-52 rows 3-18 (NE connector) ---
    fill_rect(base, 50, 3, 3, 16, PATH)

    # --- East-west connector row 3-4 (north boulevard) ---
    fill_rect(base, 12, 3, 40, 2, PATH)

    # --- East exit path connector rows 23-24 from avenue 3 to east edge ---
    fill_rect(base, 38, 23, 26, 2, PATH)

    # --- Streets connecting buildings ---
    # Pokecenter street row 23 from avenue 1 to pokecenter
    fill_rect(base, 16, 23, 7, 2, PATH)

    # Mart street row 29 connecting marts
    fill_rect(base, 16, 29, 18, 2, PATH)

    # Residential alley on west side cols 2-4
    fill_rect(base, 2, 5, 3, 3, PATH)  # west exit connector

    # Street from avenue1 south section to residential
    fill_rect(base, 5, 32, 7, 2, PATH)
    fill_rect(base, 5, 37, 7, 2, PATH)
    fill_rect(base, 5, 40, 7, 2, PATH)
    fill_rect(base, 5, 44, 7, 2, PATH)

    # --- Civic plaza path around gym ---
    fill_rect(base, 25, 13, 4, 3, PATH)  # plaza in front of gym
    fill_rect(base, 25, 10, 2, 3, PATH)  # side path

    # --- Campus connector from avenue 4 ---
    fill_rect(base, 50, 13, 3, 3, PATH)

    # --- Waterfront paths SE ---
    fill_rect(base, 40, 40, 2, 6, PATH)  # path to waterfront
    fill_rect(base, 42, 40, 8, 2, PATH)  # bridge approach

    # ===== BUILDINGS =====

    # NW: Gate (4x3) at col 3, row 9
    place_building(base, 3, 9, "gate")

    # N-Center: Gym (5x5) at col 20, row 9
    place_building(base, 20, 9, "gym")

    # NE: Lab (5x4) at col 45, row 9
    place_building(base, 45, 9, "lab")

    # Campus houses (3 small) near lab
    place_building(base, 46, 15, "house_small")
    place_building(base, 50, 15, "house_small")
    place_building(base, 54, 15, "house_small")

    # Center-W: Pokecenter (4x4) at col 14, row 20
    place_building(base, 14, 20, "pokecenter")

    # Center: 2 Pokemarts (4x4 each) at col 22, row 26 and col 28, row 26
    place_building(base, 22, 26, "pokemart")
    place_building(base, 28, 26, "pokemart")

    # E: Pond (4x3) at col 42, row 28
    place_building(base, 42, 28, "pond")

    # Flower garden near pond
    fill_rect(base, 47, 27, 3, 6, FLOWER)

    # Residential W: 3 large houses + 1 small house
    place_building(base, 2, 33, "house_large")
    place_building(base, 2, 38, "house_small")
    place_building(base, 2, 41, "house_large")
    place_building(base, 2, 45, "house_large")  # extra house shifted to fit

    # Fences around residential lots
    # Fence left side of houses
    vline(base, 1, 33, 4, FENCE)
    vline(base, 6, 33, 4, FENCE)
    vline(base, 1, 38, 3, FENCE)
    vline(base, 5, 38, 3, FENCE)
    vline(base, 1, 41, 4, FENCE)
    vline(base, 6, 41, 4, FENCE)

    # S-center: Tall grass zone (10x6) with rare grass inside
    fill_rect(base, 28, 33, 10, 6, TALL_GRASS)
    fill_rect(base, 30, 35, 5, 2, RARE_GRASS)

    # SE: Water body with water edge border, surf water, bridge
    # Water edge border
    fill_rect(base, 42, 41, 6, 1, WATER_EDGE)  # top
    fill_rect(base, 42, 45, 6, 1, WATER_EDGE)  # bottom
    vline(base, 42, 41, 5, WATER_EDGE)          # left
    vline(base, 47, 41, 5, WATER_EDGE)          # right
    # Inner water
    fill_rect(base, 43, 42, 4, 3, WATER)
    # Surf water inside
    fill_rect(base, 44, 43, 2, 1, SURF_WATER)
    # Bridge
    fill_rect(base, 48, 40, 5, 2, BRIDGE)

    # ===== OVERLAY ENTITIES =====

    # Statue on civic plaza
    place_overlay(base, over, 26, 11, STATUE, PATH)

    # NPC on civic plaza
    place_overlay(base, over, 27, 12, NPC, PATH)

    # NPC near pokecenter
    place_overlay(base, over, 18, 21, NPC, PATH)

    # Sign near north exit
    place_overlay(base, over, 11, 2, SIGN, PATH)

    # Sign near commercial strip
    place_overlay(base, over, 21, 24, SIGN, PATH)

    # Sign near south exit 1
    place_overlay(base, over, 11, 46, SIGN, PATH)

    # Sign near south exit 2
    place_overlay(base, over, 27, 46, SIGN, PATH)

    # NPC in park
    place_overlay(base, over, 48, 30, NPC, GRASS)

    # NPC near residential
    place_overlay(base, over, 7, 36, NPC, PATH)

    # Item in park
    place_overlay(base, over, 50, 27, ITEM, GRASS)

    # Item near waterfront
    place_overlay(base, over, 52, 44, ITEM, GRASS)

    # Hidden item on island near bridge
    place_overlay(base, over, 54, 43, HIDDEN_ITEM, GRASS)

    # NPC on campus
    place_overlay(base, over, 52, 13, NPC, PATH)

    return {
        "schemaVersion": 2,
        "mapId": "greenleaf_metro",
        "displayName": "Greenleaf Metro",
        "tileSize": 32,
        "width": W,
        "height": H,
        "baseTiles": base,
        "overlayTiles": over,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0",
    }


# ---------------------------------------------------------------------------
# MAP B: Coastal Route (25 x 56)
# ---------------------------------------------------------------------------
def generate_coastal_route():
    W, H = 25, 56
    base = make_grid(W, H, GRASS)
    over = make_overlay(W, H)

    # --- Tree border ---
    hline(base, 0, 0, W, TREE)
    hline(base, 0, H - 1, W, TREE)
    for r in range(H):
        base[r][0] = TREE
        base[r][W - 1] = TREE

    # --- Exits ---
    # North entry: cols 1-4 row 0
    for c in range(1, 5):
        base[0][c] = PATH

    # South exit: cols 10-13 row 55
    for c in range(10, 14):
        base[H - 1][c] = PATH

    # --- Winding path ---
    # Section 1: cols 1-4, rows 1-8 (straight down from north entry)
    fill_rect(base, 1, 1, 4, 8, PATH)

    # Curve right: rows 8-9, extend path east
    fill_rect(base, 2, 8, 4, 2, PATH)

    # Section 2: cols 3-6, rows 9-14
    fill_rect(base, 3, 9, 4, 6, PATH)

    # Curve right more: rows 14-15
    fill_rect(base, 4, 14, 4, 2, PATH)

    # Section 3: cols 4-7, rows 15-19
    fill_rect(base, 4, 15, 4, 5, PATH)

    # Curve left: rows 19-20
    fill_rect(base, 3, 19, 4, 2, PATH)

    # Section 4: cols 3-6, rows 20-28 (rest stop area)
    fill_rect(base, 3, 20, 4, 9, PATH)

    # Rest stop street from pokecenter: row 23-24
    fill_rect(base, 7, 23, 4, 2, PATH)

    # Rest house street: row 27
    fill_rect(base, 7, 25, 8, 2, PATH)

    # Curve to wider path: rows 28-29
    fill_rect(base, 3, 28, 10, 2, PATH)

    # Section 5: cols 10-15, rows 29-41 (meadow path, wider)
    fill_rect(base, 10, 29, 6, 13, PATH)

    # Curve left: rows 37-41 path shifts left
    fill_rect(base, 9, 37, 6, 1, PATH)
    fill_rect(base, 8, 38, 6, 1, PATH)
    fill_rect(base, 7, 39, 6, 1, PATH)
    fill_rect(base, 6, 40, 6, 1, PATH)
    fill_rect(base, 5, 41, 6, 5, PATH)

    # Curve right: rows 46-50
    fill_rect(base, 6, 46, 6, 1, PATH)
    fill_rect(base, 7, 47, 6, 1, PATH)
    fill_rect(base, 8, 48, 6, 1, PATH)
    fill_rect(base, 9, 49, 5, 1, PATH)
    fill_rect(base, 10, 50, 4, 6, PATH)  # final stretch to south exit

    # --- East coastline: Water Edge + Water, rows 4-18 ---
    for r in range(4, 19):
        base[r][18] = WATER_EDGE
        base[r][19] = WATER
        base[r][20] = WATER
        base[r][21] = WATER
        base[r][22] = WATER
        base[r][23] = WATER_EDGE

    # Surf spot rows 17-18
    base[17][20] = SURF_WATER
    base[17][21] = SURF_WATER
    base[18][20] = SURF_WATER
    base[18][21] = SURF_WATER

    # --- Rocky bluff rows 9-14 east side ---
    fill_rect(base, 15, 9, 3, 1, ROCK)   # top row
    fill_rect(base, 14, 10, 1, 4, ROCK)  # left side
    fill_rect(base, 17, 10, 1, 4, ROCK)  # right side
    fill_rect(base, 15, 13, 3, 1, ROCK)  # bottom row

    # --- Tall grass bands ---
    # Band 1: rows 4-7, cols 12-14
    fill_rect(base, 12, 4, 3, 4, TALL_GRASS)

    # Band 2: rows 31-35, cols 2-5
    fill_rect(base, 2, 31, 4, 5, TALL_GRASS)

    # Band 3: rows 47-49, cols 15-17
    fill_rect(base, 15, 47, 3, 3, TALL_GRASS)

    # --- Rest stop buildings ---
    # Pokecenter (4x4) at col 7, row 20
    place_building(base, 7, 20, "pokecenter")

    # House small at col 16, row 25
    place_building(base, 16, 25, "house_small")

    # --- Coastal pond rows 41-44 east side ---
    fill_rect(base, 17, 41, 1, 4, WATER_EDGE)
    fill_rect(base, 18, 41, 4, 1, WATER_EDGE)
    fill_rect(base, 18, 44, 4, 1, WATER_EDGE)
    fill_rect(base, 22, 41, 1, 4, WATER_EDGE)
    fill_rect(base, 18, 42, 4, 2, WATER)

    # ===== OVERLAY ENTITIES =====

    # 6 Trainers (Trainer Down = 21)
    place_overlay(base, over, 6, 6, TRAINER_DOWN, PATH)     # trainer 1
    place_overlay(base, over, 6, 14, TRAINER_DOWN, PATH)    # trainer 2
    place_overlay(base, over, 4, 33, TRAINER_DOWN, TALL_GRASS)  # trainer 3 in grass
    place_overlay(base, over, 4, 43, TRAINER_DOWN, PATH)    # trainer 4
    place_overlay(base, over, 9, 51, TRAINER_DOWN, PATH)    # trainer 5
    place_overlay(base, over, 16, 31, TRAINER_DOWN, GRASS)  # trainer 6

    # Items (2-3 off-path)
    place_overlay(base, over, 16, 14, ITEM, ROCK)   # item on rocky bluff (base stays rock)
    base[14][16] = GRASS  # fix - items should be reachable
    place_overlay(base, over, 16, 14, ITEM, GRASS)
    place_overlay(base, over, 1, 42, ITEM, GRASS)   # item off-path west side

    # Hidden item
    place_overlay(base, over, 20, 36, HIDDEN_ITEM, GRASS)

    # Signs
    place_overlay(base, over, 6, 11, SIGN, PATH)    # "Trainer's Alley"
    place_overlay(base, over, 16, 51, SIGN, GRASS)   # route info

    # NPC at meadow
    place_overlay(base, over, 18, 31, NPC, GRASS)

    return {
        "schemaVersion": 2,
        "mapId": "coastal_route",
        "displayName": "Coastal Route",
        "tileSize": 32,
        "width": W,
        "height": H,
        "baseTiles": base,
        "overlayTiles": over,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0",
    }


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "exported")

    metro = generate_greenleaf_metro()
    save_map(metro, os.path.join(out_dir, "greenleaf_metro.map.json"))

    coast = generate_coastal_route()
    save_map(coast, os.path.join(out_dir, "coastal_route.map.json"))

    # Quick validation
    for name, data in [("greenleaf_metro", metro), ("coastal_route", coast)]:
        h = data["height"]
        w = data["width"]
        assert len(data["baseTiles"]) == h, f"{name} baseTiles row count mismatch"
        assert len(data["overlayTiles"]) == h, f"{name} overlayTiles row count mismatch"
        for r in range(h):
            assert len(data["baseTiles"][r]) == w, f"{name} baseTiles row {r} col count"
            assert len(data["overlayTiles"][r]) == w, f"{name} overlayTiles row {r} col count"
        print(f"{name}: {w}x{h} OK ({w*h} cells)")

"""Generate 8 Nature/Water themed maps for PokemonGreen."""
import json, os, random

OUTPUT = r"D:\Projects\PokemonGreen\maps\exported"
os.makedirs(OUTPUT, exist_ok=True)

# --- Tile IDs ---
WATER=0; GRASS=1; PATH=2; TREE=3; DOOR=4; BRIDGE=5; WALL=6; TALLGRASS=7; ROCK=8
SIGN=9; NPC=10; SHOP=11; HEAL=12; ITEM=13; KEYITEM=14; CAVE=15; WARP=16
WEDGE=17; FENCE=18; FLOWER=19; TUP=20; TDOWN=21; TLEFT=22; TRIGHT=23
GYMLEADER=24; SURF=25; STRROCK=26; CUTTREE=27; RAREGRASS=28; LEGENDARY=29
VILLBOSS=30; MINIONDOWN=36; HIDDENITEM=40; PC=41; POKEBALL=42; STATUE=49

# --- Building templates ---
POKECENTER = [[3,3,3,3],[3,4,4,3],[3,4,4,3],[6,4,4,6]]
POKEMART   = [[3,3,3,3],[3,6,6,3],[3,11,6,3],[6,4,4,6]]
HOUSE_SM   = [[3,3,3],[3,4,3],[6,4,6]]
HOUSE_LG   = [[3,3,3,3],[3,6,6,3],[3,4,6,3],[6,4,6,6]]
LAB        = [[3,3,3,3,3],[3,6,6,6,3],[3,4,41,4,3],[6,4,4,4,6]]
CAVE_ENT   = [[3,3,3],[15,15,15]]

# --- Helpers ---
def grid(w, h, fill=1):
    return [[fill]*w for _ in range(h)]

def overlay(w, h):
    return [[None]*w for _ in range(h)]

def rect(g, x, y, w, h, t):
    for dy in range(h):
        for dx in range(w):
            if 0<=y+dy<len(g) and 0<=x+dx<len(g[0]):
                g[y+dy][x+dx] = t

def hline(g, y, x1, x2, t):
    for x in range(min(x1,x2), max(x1,x2)+1):
        if 0<=y<len(g) and 0<=x<len(g[0]):
            g[y][x] = t

def vline(g, x, y1, y2, t):
    for y in range(min(y1,y2), max(y1,y2)+1):
        if 0<=y<len(g) and 0<=x<len(g[0]):
            g[y][x] = t

def bldg(g, x, y, tiles):
    for dy, row in enumerate(tiles):
        for dx, t in enumerate(row):
            if 0<=y+dy<len(g) and 0<=x+dx<len(g[0]):
                g[y+dy][x+dx] = t

def ov(g, x, y, t):
    if 0<=y<len(g) and 0<=x<len(g[0]):
        g[y][x] = t

def water_body(base, x, y, w, h):
    rect(base, x, y, w, h, WEDGE)
    if w > 2 and h > 2:
        rect(base, x+1, y+1, w-2, h-2, WATER)

def island(base, cx, cy, w, h):
    rect(base, cx, cy, w, h, WEDGE)
    if w > 2 and h > 2:
        rect(base, cx+1, cy+1, w-2, h-2, GRASS)

def surf_channel(base, x, y, w, h):
    """Surf water rectangle"""
    rect(base, x, y, w, h, SURF)

def path_h(base, y, x1, x2):
    hline(base, y, x1, x2, PATH)
    hline(base, y+1, x1, x2, PATH)

def path_v(base, x, y1, y2):
    vline(base, x, y1, y2, PATH)
    vline(base, x+1, y1, y2, PATH)

def bridge_h(base, y, x1, x2):
    for x in range(x1, x2+1):
        if 0<=y<len(base) and 0<=x<len(base[0]):
            base[y][x] = BRIDGE

def bridge_v(base, x, y1, y2):
    for y in range(y1, y2+1):
        if 0<=y<len(base) and 0<=x<len(base[0]):
            base[y][x] = BRIDGE

def save(mid, name, w, h, b, o):
    data = {
        "schemaVersion": 2,
        "mapId": mid,
        "displayName": name,
        "tileSize": 32,
        "width": w,
        "height": h,
        "baseTiles": b,
        "overlayTiles": o,
        "registryId": "pokemon-green-default",
        "registryVersion": "1.0.0"
    }
    path = os.path.join(OUTPUT, f"{mid}.map.json")
    with open(path, 'w') as f:
        json.dump(data, f, indent=2)
    print(f"Wrote {mid}.map.json ({w}x{h})")


# ============================================================
# MAP 1: OPEN OCEAN (90 x 70)
# ============================================================
def gen_open_ocean():
    W, H = 90, 70
    b = grid(W, H, WATER)
    o = overlay(W, H)

    # Water Edge border around entire map
    hline(b, 0, 0, W-1, WEDGE)
    hline(b, H-1, 0, W-1, WEDGE)
    vline(b, 0, 0, H-1, WEDGE)
    vline(b, W-1, 0, H-1, WEDGE)

    # --- 4 edge exits as surf channels ---
    # North exit (center)
    rect(b, 43, 0, 4, 1, SURF)
    # South exit (center)
    rect(b, 43, H-1, 4, 1, SURF)
    # West exit (center)
    rect(b, 0, 33, 1, 4, SURF)
    # East exit (center)
    rect(b, W-1, 33, 1, 4, SURF)

    # Surf water channels connecting exits through the map
    rect(b, 43, 1, 4, 8, SURF)
    rect(b, 43, H-8, 4, 7, SURF)
    rect(b, 1, 33, 8, 4, SURF)
    rect(b, W-9, 33, 8, 4, SURF)

    # --- ISLAND 1: Main island (large, NW area) with pokecenter + house ---
    island(b, 10, 10, 16, 12)
    # Path loop on island
    rect(b, 12, 12, 12, 2, PATH)
    rect(b, 12, 18, 12, 2, PATH)
    vline(b, 12, 12, 19, PATH)
    vline(b, 13, 12, 19, PATH)
    vline(b, 22, 12, 19, PATH)
    vline(b, 23, 12, 19, PATH)
    # Pokecenter
    bldg(b, 15, 13, POKECENTER)
    # House
    bldg(b, 19, 14, HOUSE_SM)
    # Flowers
    rect(b, 14, 17, 3, 1, FLOWER)
    # Sign near pokecenter
    ov(o, 14, 16, SIGN)
    # NPC
    ov(o, 20, 18, NPC)

    # --- ISLAND 2: NE area (cave entrance to underwater dungeon) ---
    island(b, 60, 8, 12, 10)
    rect(b, 62, 10, 8, 6, PATH)
    bldg(b, 64, 11, CAVE_ENT)
    ov(o, 63, 14, SIGN)
    # Trainer
    ov(o, 68, 14, TDOWN)

    # --- ISLAND 3: SE area (rare grass + legendary) ---
    island(b, 62, 48, 14, 10)
    rect(b, 64, 50, 10, 6, PATH)
    rect(b, 66, 51, 4, 4, RAREGRASS)
    b[53][69] = LEGENDARY
    ov(o, 65, 55, SIGN)
    # Flowers
    rect(b, 64, 55, 3, 1, FLOWER)
    rect(b, 72, 50, 2, 1, FLOWER)

    # --- ISLAND 4: SW area (medium) ---
    island(b, 8, 45, 10, 8)
    rect(b, 10, 47, 6, 4, PATH)
    bldg(b, 11, 47, HOUSE_SM)
    ov(o, 14, 50, TDOWN)
    ov(o, 10, 49, ITEM)

    # --- ISLAND 5: Central-west (small) ---
    island(b, 30, 28, 8, 6)
    rect(b, 32, 30, 4, 2, PATH)
    ov(o, 33, 31, TRIGHT)
    ov(o, 35, 30, ITEM)

    # --- ISLAND 6: Central (medium) ---
    island(b, 38, 20, 10, 8)
    rect(b, 40, 22, 6, 4, PATH)
    rect(b, 42, 23, 2, 2, TALLGRASS)
    ov(o, 44, 24, TDOWN)

    # --- ISLAND 7: Central-east ---
    island(b, 55, 30, 6, 5)
    rect(b, 57, 32, 2, 1, PATH)
    ov(o, 57, 32, ITEM)

    # --- ISLAND 8: South-center ---
    island(b, 35, 50, 8, 6)
    rect(b, 37, 52, 4, 2, PATH)
    ov(o, 38, 52, TUP)
    ov(o, 40, 53, HIDDENITEM)

    # --- ISLAND 9: Far NW (small with flowers) ---
    island(b, 3, 3, 5, 4)
    rect(b, 4, 4, 3, 2, FLOWER)

    # --- ISLAND 10: Far east ---
    island(b, 75, 22, 8, 6)
    rect(b, 77, 24, 4, 2, PATH)
    ov(o, 78, 24, TLEFT)
    ov(o, 80, 25, ITEM)

    # --- Bridges connecting close islands ---
    # Bridge: Island 1 to Island 6 (east from main island)
    bridge_h(b, 15, 26, 38)
    bridge_h(b, 16, 26, 38)
    # Bridge: Island 5 to Island 6
    bridge_h(b, 29, 38, 40)
    # Bridge: Island 6 to Island 7
    bridge_h(b, 32, 48, 55)
    # Bridge: Island 8 to Island 4 (west)
    bridge_h(b, 52, 18, 35)

    # --- Additional Surf water channels ---
    # Surf channel from Island 2 area south toward Island 10
    rect(b, 70, 18, 3, 4, SURF)
    # Surf channel from island 3 area west
    rect(b, 50, 52, 12, 3, SURF)
    # Surf between island 7 and island 10
    rect(b, 61, 28, 14, 2, SURF)

    # --- Extra trainers ---
    ov(o, 30, 15, TDOWN)   # on bridge
    ov(o, 50, 32, TLEFT)   # on bridge
    ov(o, 77, 24, TRIGHT)  # island 10

    # --- Extra items ---
    ov(o, 4, 5, HIDDENITEM)   # island 9
    ov(o, 62, 55, ITEM)       # near legendary island

    save("open_ocean", "Open Ocean", W, H, b, o)


# ============================================================
# MAP 2: CORAL ARCHIPELAGO (80 x 65)
# ============================================================
def gen_coral_archipelago():
    W, H = 80, 65
    b = grid(W, H, WATER)
    o = overlay(W, H)

    # Water Edge border
    hline(b, 0, 0, W-1, WEDGE)
    hline(b, H-1, 0, W-1, WEDGE)
    vline(b, 0, 0, H-1, WEDGE)
    vline(b, W-1, 0, H-1, WEDGE)

    # Exits: NW and SE as surf channels
    rect(b, 0, 2, 1, 3, SURF)
    rect(b, 1, 2, 3, 3, SURF)
    rect(b, W-4, H-5, 3, 3, SURF)
    rect(b, W-1, H-5, 1, 3, SURF)

    # --- Island chain in curved arc from NW to SE ---
    # Island A (NW): 8x6, has house
    island(b, 5, 5, 8, 6)
    rect(b, 7, 7, 4, 2, PATH)
    bldg(b, 7, 7, HOUSE_SM)
    ov(o, 10, 9, SIGN)

    # Island B: 6x5
    island(b, 16, 10, 6, 5)
    rect(b, 18, 12, 2, 1, PATH)
    ov(o, 18, 12, TDOWN)

    # Island C: 5x4, item
    island(b, 26, 8, 5, 4)
    rect(b, 28, 9, 1, 2, PATH)
    ov(o, 28, 10, ITEM)

    # Island D: 7x5, tall grass
    island(b, 33, 14, 7, 5)
    rect(b, 35, 16, 3, 1, TALLGRASS)
    rect(b, 35, 15, 3, 1, PATH)

    # Island E: LARGE CENTRAL (16x12) — full town
    island(b, 28, 24, 16, 12)
    # Path grid on central island
    rect(b, 30, 26, 12, 2, PATH)  # main E-W
    rect(b, 30, 32, 12, 2, PATH)  # south E-W
    vline(b, 30, 26, 33, PATH)
    vline(b, 31, 26, 33, PATH)
    vline(b, 40, 26, 33, PATH)
    vline(b, 41, 26, 33, PATH)
    # Pokecenter
    bldg(b, 32, 27, POKECENTER)
    # Pokemart
    bldg(b, 36, 27, POKEMART)
    # Houses
    bldg(b, 32, 31, HOUSE_SM)
    bldg(b, 36, 31, HOUSE_SM)
    bldg(b, 32, 33, HOUSE_LG)
    # NPCs and signs
    ov(o, 34, 26, NPC)
    ov(o, 39, 26, NPC)
    ov(o, 35, 34, SIGN)

    # Island F: 6x4, trainer
    island(b, 48, 22, 6, 4)
    rect(b, 50, 23, 2, 2, PATH)
    ov(o, 50, 24, TLEFT)

    # Island G: 5x4, flowers
    island(b, 50, 30, 5, 4)
    rect(b, 52, 31, 1, 2, FLOWER)

    # Island H: 7x5, cave
    island(b, 55, 35, 7, 5)
    rect(b, 57, 37, 3, 1, PATH)
    bldg(b, 57, 36, CAVE_ENT)

    # Island I: 4x3
    island(b, 60, 42, 4, 3)
    ov(o, 61, 43, ITEM)

    # Island J: 6x5, trainer
    island(b, 58, 48, 6, 5)
    rect(b, 60, 50, 2, 1, PATH)
    ov(o, 60, 50, TDOWN)
    ov(o, 61, 51, HIDDENITEM)

    # Island K: 5x4
    island(b, 65, 53, 5, 4)
    rect(b, 67, 54, 1, 2, TALLGRASS)

    # Island L (SE): 8x6, rare grass
    island(b, 68, 56, 8, 6)
    rect(b, 70, 58, 4, 2, RAREGRASS)
    ov(o, 72, 59, ITEM)

    # --- Bridges connecting nearby islands ---
    bridge_h(b, 12, 13, 16)  # A to B
    bridge_h(b, 11, 22, 26)  # B to C
    bridge_h(b, 15, 31, 33)  # C/D to E (approx)
    bridge_h(b, 27, 44, 48)  # E to F
    bridge_v(b, 52, 34, 35)  # G to H area
    bridge_h(b, 44, 60, 62)  # I toward J area
    bridge_h(b, 55, 63, 68)  # K to L

    # --- Surf channels between distant groups ---
    rect(b, 20, 15, 3, 8, SURF)   # B/C area to E
    rect(b, 52, 26, 3, 4, SURF)   # F to G gap
    rect(b, 62, 45, 3, 3, SURF)   # I to J
    rect(b, 72, 50, 3, 3, SURF)   # toward L

    # Additional trainers
    ov(o, 29, 9, TDOWN)
    ov(o, 56, 38, TRIGHT)
    ov(o, 66, 55, TUP)

    save("coral_archipelago", "Coral Archipelago", W, H, b, o)


# ============================================================
# MAP 3: MYSTIC SWAMP (65 x 55)
# ============================================================
def gen_mystic_swamp():
    W, H = 65, 55
    b = grid(W, H, GRASS)
    o = overlay(W, H)

    # Tree border
    rect(b, 0, 0, W, 1, TREE)
    rect(b, 0, H-1, W, 1, TREE)
    vline(b, 0, 0, H-1, TREE)
    vline(b, W-1, 0, H-1, TREE)

    # Dense tree clusters throughout
    for tx, ty, tw, th in [
        (5,3,6,4), (15,2,4,5), (25,3,5,3), (40,2,6,4), (50,3,5,4),
        (3,12,4,5), (12,15,5,3), (22,12,3,6), (35,10,4,5), (48,12,5,4),
        (8,25,5,4), (20,22,4,5), (38,22,5,3), (50,20,4,6),
        (5,35,4,4), (18,38,5,3), (30,36,3,5), (45,35,6,4), (55,38,4,4),
        (10,45,5,3), (28,44,4,4), (42,45,5,3), (55,45,4,4),
        (58,10,4,5), (58,25,4,4), (2,20,3,5),
    ]:
        rect(b, tx, ty, tw, th, TREE)

    # Water pools scattered everywhere
    water_body(b, 14, 8, 6, 4)
    water_body(b, 30, 6, 5, 3)
    water_body(b, 44, 7, 4, 3)
    water_body(b, 8, 20, 5, 4)
    water_body(b, 35, 18, 6, 4)
    water_body(b, 52, 28, 5, 3)
    water_body(b, 15, 32, 4, 3)
    water_body(b, 42, 40, 5, 4)
    water_body(b, 25, 48, 6, 4)

    # Tall grass everywhere (encounter-heavy)
    for tx, ty, tw, th in [
        (10,5,4,3), (28,4,3,3), (45,5,3,3),
        (6,14,5,4), (26,14,4,4), (40,14,5,3),
        (14,26,5,5), (32,25,5,4), (48,24,4,5),
        (8,30,4,4), (24,32,4,4), (38,30,4,5),
        (10,40,4,4), (35,42,5,4), (52,40,4,4),
        (18,48,4,3), (40,48,5,3), (55,48,3,3),
    ]:
        rect(b, tx, ty, tw, th, TALLGRASS)

    # --- Winding path from south entrance to north ---
    # South entrance
    rect(b, 30, H-2, 4, 2, PATH)
    # Path winds north through the swamp
    path_v(b, 31, 48, 53)
    path_h(b, 48, 27, 32)
    path_v(b, 27, 43, 48)
    path_h(b, 43, 23, 28)
    path_v(b, 23, 38, 43)
    path_h(b, 38, 23, 30)
    path_v(b, 30, 33, 38)
    path_h(b, 33, 30, 38)
    path_v(b, 38, 28, 33)
    path_h(b, 28, 30, 39)
    # Central area (villain hideout)
    rect(b, 28, 26, 8, 2, PATH)
    # Continue north
    path_v(b, 28, 20, 26)
    path_h(b, 20, 24, 29)
    path_v(b, 24, 15, 20)
    path_h(b, 15, 24, 32)
    path_v(b, 32, 10, 15)
    path_h(b, 10, 32, 40)
    path_v(b, 40, 5, 10)
    # North exit
    rect(b, 40, 1, 3, 5, PATH)

    # Cave entrance hidden NE
    bldg(b, 55, 5, CAVE_ENT)
    path_h(b, 6, 42, 55)

    # Strength rocks blocking central villain hideout
    b[27][28] = STRROCK
    b[27][35] = STRROCK

    # Villain hideout
    ov(o, 31, 27, VILLBOSS)
    ov(o, 33, 27, MINIONDOWN)
    ov(o, 29, 27, MINIONDOWN)

    # Rare grass in deepest part
    rect(b, 5, 42, 4, 3, RAREGRASS)

    # Signs with lore
    ov(o, 32, 50, SIGN)
    ov(o, 25, 40, SIGN)
    ov(o, 30, 30, SIGN)
    ov(o, 28, 18, SIGN)
    ov(o, 38, 8, SIGN)

    # Few NPCs
    ov(o, 26, 44, NPC)
    ov(o, 34, 16, NPC)

    # Items in dead ends
    ov(o, 6, 43, ITEM)
    ov(o, 50, 30, HIDDENITEM)
    ov(o, 56, 7, ITEM)
    ov(o, 40, 42, ITEM)

    save("mystic_swamp", "Mystic Swamp", W, H, b, o)


# ============================================================
# MAP 4: VOLCANIC ISLAND (60 x 50)
# ============================================================
def gen_volcanic_island():
    W, H = 60, 50
    b = grid(W, H, WATER)
    o = overlay(W, H)

    # Water Edge border (ocean around island)
    hline(b, 0, 0, W-1, WEDGE)
    hline(b, H-1, 0, W-1, WEDGE)
    vline(b, 0, 0, H-1, WEDGE)
    vline(b, W-1, 0, H-1, WEDGE)

    # The island mass (centered, leaving ocean rim)
    island(b, 8, 8, 44, 36)
    # Make interior rocky
    rect(b, 10, 10, 40, 32, ROCK)
    # Grassy shoreline ring
    rect(b, 10, 10, 40, 3, GRASS)
    rect(b, 10, 39, 40, 3, GRASS)
    rect(b, 10, 10, 3, 32, GRASS)
    rect(b, 47, 10, 3, 32, GRASS)

    # Southern shore base camp
    rect(b, 18, 40, 24, 3, PATH)
    bldg(b, 22, 38, POKECENTER)
    bldg(b, 28, 38, HOUSE_SM)
    bldg(b, 33, 38, HOUSE_SM)
    ov(o, 26, 42, NPC)
    ov(o, 20, 42, SIGN)

    # Switchback path going up the mountain
    # Level 1: south to west
    path_h(b, 37, 18, 30)
    path_v(b, 18, 33, 37)
    # Level 2: west to north
    path_h(b, 33, 14, 19)
    path_v(b, 14, 28, 33)
    # Level 3: north side east
    path_h(b, 28, 14, 20)
    path_h(b, 28, 20, 40)
    # Level 4: east side down toward center
    path_v(b, 40, 23, 28)
    path_h(b, 23, 35, 41)
    # Level 5: approach crater
    path_v(b, 35, 18, 23)
    path_h(b, 18, 25, 36)

    # Volcano crater (ring of rocks with cave inside)
    rect(b, 24, 16, 12, 10, ROCK)
    rect(b, 26, 18, 8, 6, CAVE)
    # Legendary at center
    b[21][30] = LEGENDARY

    # Strength rocks blocking crater path
    b[18][26] = STRROCK
    b[18][33] = STRROCK

    # Cave entrances
    bldg(b, 24, 14, CAVE_ENT)
    bldg(b, 33, 14, CAVE_ENT)

    # Hot springs (small water bodies with flowers)
    water_body(b, 12, 35, 4, 3)
    rect(b, 12, 34, 4, 1, FLOWER)
    water_body(b, 42, 35, 4, 3)
    rect(b, 42, 34, 4, 1, FLOWER)

    # Lava pools (water with edge border)
    water_body(b, 28, 25, 4, 3)

    # Trainers along the path
    ov(o, 22, 36, TDOWN)
    ov(o, 16, 30, TRIGHT)
    ov(o, 30, 28, TLEFT)
    ov(o, 38, 25, TUP)
    ov(o, 32, 19, TDOWN)

    # Items
    ov(o, 12, 11, ITEM)
    ov(o, 48, 11, ITEM)
    ov(o, 30, 17, HIDDENITEM)

    # Tree patches on shore
    rect(b, 10, 10, 2, 2, TREE)
    rect(b, 47, 10, 2, 2, TREE)
    rect(b, 10, 40, 2, 2, TREE)
    rect(b, 47, 40, 2, 2, TREE)

    save("volcanic_island", "Volcanic Island", W, H, b, o)


# ============================================================
# MAP 5: CRYSTAL CAVERNS (55 x 45)
# ============================================================
def gen_crystal_caverns():
    W, H = 55, 45
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # --- Chamber 1: Entry (NW) ---
    rect(b, 4, 2, 12, 8, CAVE)
    # Entry from surface
    rect(b, 8, 1, 3, 2, PATH)
    b[1][8] = WARP; b[1][9] = WARP; b[1][10] = WARP
    # Tunnel east from chamber 1
    rect(b, 16, 5, 8, 2, CAVE)
    # Crystal formations (flowers)
    b[4][6] = FLOWER; b[4][7] = FLOWER; b[5][6] = FLOWER
    b[3][12] = FLOWER; b[3][13] = FLOWER

    # --- Chamber 2: NE ---
    rect(b, 24, 2, 14, 10, CAVE)
    # Crystal cluster
    for cx, cy in [(27,4),(28,4),(27,5),(29,5),(28,6)]:
        b[cy][cx] = FLOWER
    # Dead end room north
    rect(b, 38, 3, 6, 4, CAVE)
    rect(b, 36, 4, 3, 2, CAVE)  # tunnel
    ov(o, 41, 5, ITEM)

    # Tunnel south from chamber 2
    rect(b, 30, 12, 2, 6, CAVE)

    # --- Chamber 3: Central ---
    rect(b, 18, 16, 18, 10, CAVE)
    # Crystals
    for cx, cy in [(22,18),(23,18),(22,19),(24,19),(23,20)]:
        b[cy][cx] = FLOWER
    for cx, cy in [(31,18),(32,18),(31,19)]:
        b[cy][cx] = FLOWER

    # Tunnel west from central
    rect(b, 8, 18, 10, 2, CAVE)
    # Dead end west room
    rect(b, 2, 15, 8, 8, CAVE)
    ov(o, 4, 18, ITEM)
    ov(o, 7, 20, HIDDENITEM)

    # --- Tunnel south from central ---
    rect(b, 26, 26, 2, 6, CAVE)

    # --- Chamber 4: East (Underground Lake) ---
    rect(b, 38, 16, 14, 12, CAVE)
    water_body(b, 40, 18, 10, 8)
    # Surf area in lake
    rect(b, 42, 20, 6, 4, SURF)
    # Island in the lake with item
    b[22][45] = GRASS
    b[22][46] = GRASS
    ov(o, 45, 22, ITEM)

    # Tunnel from central to east chamber
    rect(b, 36, 20, 2, 2, CAVE)

    # --- Chamber 5: SW (Level 2 via warp) ---
    rect(b, 4, 28, 14, 10, CAVE)
    # Warp connection from central
    b[25][20] = WARP
    b[28][6] = WARP
    # Crystals
    for cx, cy in [(7,30),(8,30),(7,31),(9,31)]:
        b[cy][cx] = FLOWER
    ov(o, 12, 34, ITEM)
    # Dead end room
    rect(b, 2, 38, 8, 5, CAVE)
    rect(b, 8, 36, 2, 3, CAVE)  # tunnel
    ov(o, 4, 40, HIDDENITEM)

    # --- Chamber 6: SE (Deepest - Level 3, villain) ---
    rect(b, 24, 32, 16, 10, CAVE)
    # Tunnel from south
    rect(b, 26, 30, 2, 2, CAVE)
    # Minions
    ov(o, 28, 35, MINIONDOWN)
    ov(o, 32, 35, MINIONDOWN)
    # Villain boss
    ov(o, 30, 38, VILLBOSS)
    # Treasure
    ov(o, 36, 39, KEYITEM)
    # Warp out
    b[40][38] = WARP
    # Crystals
    for cx, cy in [(26,34),(27,34),(35,33),(36,33),(36,34)]:
        b[cy][cx] = FLOWER

    # --- Dead end treasure room SE ---
    rect(b, 42, 32, 8, 6, CAVE)
    rect(b, 40, 34, 2, 2, CAVE)  # tunnel
    ov(o, 46, 35, ITEM)
    ov(o, 47, 35, HIDDENITEM)

    # Warp between levels
    b[26][8] = WARP

    # Path markings in main tunnels
    path_h(b, 6, 4, 24)
    path_h(b, 20, 8, 38)
    path_v(b, 26, 12, 32)

    save("crystal_caverns", "Crystal Caverns", W, H, b, o)


# ============================================================
# MAP 6: BAMBOO FOREST (60 x 50)
# ============================================================
def gen_bamboo_forest():
    W, H = 60, 50
    b = grid(W, H, TREE)
    o = overlay(W, H)

    # Fill interior with dense trees, then carve paths and clearings
    # Tree border is natural since fill is TREE

    # --- Clearings ---
    # Clearing 1: Shrine (center-north)
    rect(b, 25, 8, 10, 8, GRASS)
    b[11][29] = STATUE
    b[11][30] = STATUE
    ov(o, 28, 13, SIGN)
    ov(o, 31, 13, SIGN)
    rect(b, 27, 14, 6, 1, FLOWER)

    # Clearing 2: Pond (west)
    rect(b, 8, 20, 10, 8, GRASS)
    water_body(b, 10, 22, 6, 4)

    # Clearing 3: Tall grass meadow (east)
    rect(b, 42, 18, 12, 10, GRASS)
    rect(b, 44, 20, 8, 6, TALLGRASS)
    ov(o, 48, 19, TDOWN)

    # Clearing 4: Hermit house (deep south)
    rect(b, 30, 38, 8, 6, GRASS)
    bldg(b, 32, 39, HOUSE_SM)
    ov(o, 35, 42, NPC)
    ov(o, 33, 42, SIGN)

    # Clearing 5: Hidden rare grass (behind cut trees)
    rect(b, 50, 38, 6, 6, GRASS)
    rect(b, 51, 39, 4, 4, RAREGRASS)

    # Clearing 6: Small rest area (south entrance)
    rect(b, 25, 44, 10, 4, GRASS)

    # --- Zigzag main path (no straight runs > 5 tiles) ---
    # South entrance
    rect(b, 28, 47, 4, 3, PATH)
    # Zigzag upward
    path_v(b, 29, 44, 47)
    path_h(b, 44, 27, 30)
    path_v(b, 27, 40, 44)
    path_h(b, 40, 27, 33)
    path_v(b, 33, 36, 40)
    path_h(b, 36, 30, 34)
    path_v(b, 30, 32, 36)
    path_h(b, 32, 26, 31)
    path_v(b, 26, 28, 32)
    path_h(b, 28, 18, 27)
    # West branch to pond clearing
    path_v(b, 18, 23, 28)
    path_h(b, 23, 15, 19)
    # Continue north from main junction
    path_v(b, 22, 22, 28)
    path_h(b, 22, 22, 28)
    path_v(b, 28, 16, 22)
    path_h(b, 16, 25, 29)
    # Connect to shrine clearing
    path_v(b, 25, 14, 16)

    # East branch from main path
    path_h(b, 30, 34, 42)
    path_v(b, 42, 24, 30)
    path_h(b, 24, 42, 46)
    # Connect to tall grass clearing
    path_v(b, 46, 22, 24)

    # Path to hermit (from main south)
    path_h(b, 42, 33, 38)
    path_v(b, 38, 40, 42)
    path_h(b, 40, 34, 39)

    # Hidden path behind cut trees to rare grass
    b[40][48] = CUTTREE
    b[40][49] = CUTTREE
    path_h(b, 40, 39, 48)
    rect(b, 49, 39, 2, 3, PATH)

    # North exit
    rect(b, 28, 0, 3, 9, PATH)
    path_v(b, 28, 0, 8)

    # East exit
    rect(b, W-3, 24, 3, 3, PATH)
    path_h(b, 25, 46, W-1)

    # --- Populate with details ---
    ov(o, 20, 25, NPC)    # near pond
    ov(o, 36, 34, TDOWN)  # on path
    ov(o, 24, 18, TLEFT)  # on path
    ov(o, 40, 28, TUP)    # near east clearing

    # Items off main path
    ov(o, 12, 24, ITEM)       # in pond clearing
    ov(o, 52, 40, ITEM)       # in rare grass area
    ov(o, 10, 10, HIDDENITEM) # deep in trees
    ov(o, 50, 10, HIDDENITEM) # deep NE
    ov(o, 46, 21, ITEM)       # in tall grass clearing

    # Signs
    ov(o, 28, 46, SIGN)  # south entrance
    ov(o, 29, 8, SIGN)   # north area

    save("bamboo_forest", "Bamboo Forest", W, H, b, o)


# ============================================================
# MAP 7: FROZEN GLACIER (70 x 55)
# ============================================================
def gen_frozen_glacier():
    W, H = 70, 55
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # Rock border is natural (fill is ROCK)

    # --- Ice fields (Water = ice, WaterEdge = ice edge) ---
    # Large ice field west
    water_body(b, 5, 5, 20, 15)
    # Large ice field east
    water_body(b, 40, 5, 22, 12)
    # Large ice field center-south
    water_body(b, 15, 28, 25, 14)
    # Small ice patches
    water_body(b, 50, 25, 10, 8)
    water_body(b, 5, 35, 8, 8)

    # --- Paths carved through ice ---
    # South entry
    rect(b, 32, H-3, 4, 3, PATH)

    # Main path from south entry north
    path_v(b, 33, 42, H-3)
    path_h(b, 42, 30, 34)
    path_v(b, 30, 38, 42)
    path_h(b, 38, 30, 40)
    path_v(b, 40, 33, 38)
    path_h(b, 33, 35, 41)
    path_v(b, 35, 28, 33)
    path_h(b, 28, 28, 36)

    # Path continues north through ice
    path_v(b, 28, 20, 28)
    path_h(b, 20, 25, 32)
    path_v(b, 25, 15, 20)
    path_h(b, 15, 25, 35)

    # East branch
    path_h(b, 20, 35, 50)
    path_v(b, 50, 17, 20)
    path_h(b, 17, 40, 51)

    # West branch
    path_h(b, 22, 15, 28)
    path_v(b, 15, 18, 22)

    # Path to north cave
    path_v(b, 30, 5, 15)
    path_h(b, 5, 25, 35)

    # Ice cave entrance (north)
    bldg(b, 25, 2, CAVE_ENT)
    rect(b, 25, 4, 3, 2, PATH)

    # --- Slide puzzles (long straight ice paths) ---
    # Slide 1: horizontal
    rect(b, 10, 12, 12, 1, WATER)
    # Slide 2: vertical
    rect(b, 45, 8, 1, 10, WATER)
    # Slide 3: horizontal
    rect(b, 20, 35, 10, 1, WATER)
    # Slide 4: vertical
    rect(b, 55, 15, 1, 12, WATER)

    # Pokecenter outpost at south
    bldg(b, 25, 46, POKECENTER)
    path_h(b, 49, 25, 33)
    rect(b, 25, 45, 8, 1, PATH)

    # Strength rocks blocking shortcuts
    b[25][28] = STRROCK
    b[25][29] = STRROCK
    b[15][38] = STRROCK

    # Rocky outcrops with items
    ov(o, 8, 10, ITEM)
    ov(o, 55, 8, ITEM)
    ov(o, 18, 32, ITEM)
    ov(o, 52, 30, HIDDENITEM)
    ov(o, 8, 40, ITEM)

    # 8+ trainers along the glacier path
    ov(o, 32, 40, TDOWN)
    ov(o, 38, 36, TLEFT)
    ov(o, 36, 30, TUP)
    ov(o, 26, 22, TRIGHT)
    ov(o, 30, 18, TDOWN)
    ov(o, 45, 19, TLEFT)
    ov(o, 52, 18, TDOWN)
    ov(o, 30, 8, TRIGHT)
    ov(o, 18, 20, TUP)

    # Signs
    ov(o, 27, 49, SIGN)
    ov(o, 32, 6, SIGN)

    # NPC near south outpost
    ov(o, 30, 49, NPC)

    save("frozen_glacier", "Frozen Glacier", W, H, b, o)


# ============================================================
# MAP 8: CANYON RIVER (30 x 80)
# ============================================================
def gen_canyon_river():
    W, H = 30, 80
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # --- Cliff walls on edges ---
    vline(b, 0, 0, H-1, ROCK)
    vline(b, 1, 0, H-1, ROCK)
    vline(b, 2, 0, H-1, ROCK)
    vline(b, W-1, 0, H-1, ROCK)
    vline(b, W-2, 0, H-1, ROCK)
    vline(b, W-3, 0, H-1, ROCK)

    # Grass banks on both sides of the river
    rect(b, 3, 0, 5, H, GRASS)
    rect(b, W-8, 0, 5, H, GRASS)

    # --- River running down the center (6-8 wide) ---
    rect(b, 8, 0, 2, H, WEDGE)  # left bank edge
    rect(b, 10, 0, 8, H, WATER)  # river water
    rect(b, 18, 0, 2, H, WEDGE)  # right bank edge
    # Wider in the middle (rest area)
    rect(b, 7, 35, 1, 10, WEDGE)
    rect(b, 8, 35, 2, 10, WATER)
    rect(b, 19, 35, 2, 10, WATER)
    rect(b, 21, 35, 1, 10, WEDGE)

    # --- Paths on both banks ---
    # Left bank path
    vline(b, 5, 2, H-3, PATH)
    vline(b, 6, 2, H-3, PATH)
    # Right bank path
    vline(b, 22, 2, H-3, PATH)
    vline(b, 23, 2, H-3, PATH)

    # --- Waterfall at top ---
    rect(b, 10, 0, 8, 3, ROCK)  # rocks at waterfall
    rect(b, 11, 3, 6, 2, WEDGE)
    rect(b, 12, 4, 4, 1, WATER)
    # Cave entrance behind waterfall
    b[1][13] = CAVE
    b[1][14] = CAVE
    b[1][15] = CAVE
    b[0][13] = TREE; b[0][14] = TREE; b[0][15] = TREE
    ov(o, 14, 2, HIDDENITEM)  # hidden item behind waterfall

    # North entrance (paths enter from north)
    rect(b, 5, 0, 2, 2, PATH)
    rect(b, 22, 0, 2, 2, PATH)

    # South exit
    rect(b, 5, H-2, 2, 2, PATH)
    rect(b, 22, H-2, 2, 2, PATH)

    # --- Bridges at 3 crossing points ---
    # Bridge 1 (row 18)
    bridge_h(b, 18, 7, 21)
    bridge_h(b, 19, 7, 21)
    # Bridge 2 (row 40) - in the rest area
    bridge_h(b, 40, 6, 22)
    bridge_h(b, 41, 6, 22)
    # Bridge 3 (row 62)
    bridge_h(b, 62, 7, 21)
    bridge_h(b, 63, 7, 21)

    # --- Rest area in the middle (canyon widens) ---
    rect(b, 3, 36, 5, 8, GRASS)
    rect(b, 22, 36, 5, 8, GRASS)
    # Buildings in rest area
    bldg(b, 3, 36, POKECENTER)
    bldg(b, 3, 41, HOUSE_SM)
    bldg(b, 24, 37, POKEMART)
    # NPC
    ov(o, 5, 40, NPC)
    ov(o, 24, 42, NPC)
    ov(o, 6, 44, SIGN)

    # --- Tall grass in wider sections ---
    rect(b, 3, 12, 4, 4, TALLGRASS)
    rect(b, 23, 12, 4, 4, TALLGRASS)
    rect(b, 3, 52, 4, 5, TALLGRASS)
    rect(b, 23, 52, 4, 5, TALLGRASS)
    rect(b, 3, 68, 4, 4, TALLGRASS)
    rect(b, 23, 68, 4, 4, TALLGRASS)

    # --- Trainers guard bridges ---
    ov(o, 6, 17, TDOWN)    # bridge 1 left
    ov(o, 22, 17, TDOWN)   # bridge 1 right
    ov(o, 5, 39, TRIGHT)   # bridge 2
    ov(o, 23, 39, TLEFT)   # bridge 2
    ov(o, 6, 61, TDOWN)    # bridge 3 left
    ov(o, 22, 61, TDOWN)   # bridge 3 right

    # Extra trainers
    ov(o, 5, 28, TDOWN)
    ov(o, 23, 72, TUP)

    # Items
    ov(o, 4, 8, ITEM)
    ov(o, 24, 25, ITEM)
    ov(o, 4, 58, HIDDENITEM)
    ov(o, 24, 65, ITEM)

    # Surf water in parts of the river for traversal
    rect(b, 11, 20, 6, 3, SURF)
    rect(b, 11, 50, 6, 3, SURF)
    rect(b, 11, 70, 6, 3, SURF)

    save("canyon_river", "Canyon River", W, H, b, o)


# ============================================================
# GENERATE ALL 8 MAPS
# ============================================================
if __name__ == "__main__":
    print("Generating 8 Nature/Water maps...\n")
    gen_open_ocean()
    gen_coral_archipelago()
    gen_mystic_swamp()
    gen_volcanic_island()
    gen_crystal_caverns()
    gen_bamboo_forest()
    gen_frozen_glacier()
    gen_canyon_river()
    print("\nAll 8 maps generated successfully!")

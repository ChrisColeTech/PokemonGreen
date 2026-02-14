"""Generate 8 Adventure/Special theme maps for PokemonGreen."""
import json, os

OUTPUT = r"D:\Projects\PokemonGreen\maps\exported"

# --- Helper functions ---
def grid(w, h, fill=1):
    return [[fill]*w for _ in range(h)]

def overlay(w, h):
    return [[None]*w for _ in range(h)]

def rect(g, x, y, w, h, t):
    for dy in range(h):
        for dx in range(w):
            if 0 <= y+dy < len(g) and 0 <= x+dx < len(g[0]):
                g[y+dy][x+dx] = t

def hline(g, y, x1, x2, t):
    for x in range(x1, x2+1):
        if 0 <= y < len(g) and 0 <= x < len(g[0]):
            g[y][x] = t

def vline(g, x, y1, y2, t):
    for y in range(y1, y2+1):
        if 0 <= y < len(g) and 0 <= x < len(g[0]):
            g[y][x] = t

def bldg(g, x, y, tiles):
    for dy, row in enumerate(tiles):
        for dx, t in enumerate(row):
            if 0 <= y+dy < len(g) and 0 <= x+dx < len(g[0]):
                g[y+dy][x+dx] = t

def ov(g, x, y, t):
    if 0 <= y < len(g) and 0 <= x < len(g[0]):
        g[y][x] = t

def save(mid, name, w, h, b, o):
    os.makedirs(OUTPUT, exist_ok=True)
    with open(os.path.join(OUTPUT, f"{mid}.map.json"), 'w') as f:
        json.dump({
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
        }, f, indent=2)
    print(f"Wrote {mid}.map.json ({w}x{h})")

# Tile constants
GRASS=1; PATH=2; TREE=3; WATER=0; DOOR=4; BRIDGE=5; WALL=6; TALLGRASS=7; ROCK=8
SIGN=9; NPC=10; SHOP=11; HEAL=12; ITEM=13; KEYITEM=14; CAVE=15; WARP=16
WEDGE=17; FENCE=18; FLOWER=19; TUP=20; TDOWN=21; TLEFT=22; TRIGHT=23
GYMLEADER=24; SURF=25; STROCK=26; CUTTREE=27; RAREGRASS=28; LEGENDARY=29
VBOSS=30; VDOWN=32; VUP=31; VLEFT=33; VRIGHT=34
MUP=35; MDOWN=36; MLEFT=37; MRIGHT=38
HIDDEN=40; PC=41; POKEBALL=42; STATUE=49

# Building templates
POKECENTER = [[3,3,3,3],[3,4,4,3],[3,4,4,3],[6,4,4,6]]
POKEMART = [[3,3,3,3],[3,6,6,3],[3,11,6,3],[6,4,4,6]]
GYM = [[3,3,3,3,3],[3,6,6,6,3],[3,6,12,6,3],[3,6,4,6,3],[6,6,4,6,6]]
HOUSE_SM = [[3,3,3],[3,4,3],[6,4,6]]
HOUSE_LG = [[3,3,3,3],[3,6,6,3],[3,4,6,3],[6,4,6,6]]
LAB = [[3,3,3,3,3],[3,6,6,6,3],[3,4,41,4,3],[6,4,4,4,6]]
CAVE_ENT = [[3,3,3],[15,15,15]]
GATE = [[6,6,6,6],[6,16,16,6],[6,6,6,6]]
POND = [[17,0,0,17],[0,0,0,0],[17,0,0,17]]


# ============================================================
# MAP 1: HAUNTED MANSION (50 x 40) — Interior
# ============================================================
def make_haunted_mansion():
    W, H = 50, 40
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # Entry hall at bottom center
    rect(b, 22, 36, 6, 4, PATH)  # entry corridor
    hline(b, 39, 22, 27, PATH)  # bottom edge entry
    rect(b, 20, 32, 10, 4, PATH)  # foyer

    # Main hall — large central corridor running north
    rect(b, 23, 8, 4, 24, PATH)  # main hall spine

    # Grand entrance room
    rect(b, 16, 28, 18, 4, PATH)  # wide entrance hall
    ov(o, 25, 29, SIGN)   # Welcome sign - dusty journal
    ov(o, 24, 29, NPC)    # ghost NPC at entrance

    # WEST WING
    # West corridor from main hall
    hline(b, 14, 4, 22, PATH)
    hline(b, 15, 4, 22, PATH)

    # West wing rooms
    # Room W1: Dining Hall (top-left)
    rect(b, 2, 3, 10, 6, PATH)
    vline(b, 7, 9, 14, PATH)  # corridor to dining hall
    ov(o, 4, 4, FLOWER)   # cobweb
    ov(o, 8, 4, FLOWER)   # cobweb
    ov(o, 6, 5, SIGN)     # dusty journal
    ov(o, 3, 6, TDOWN)    # ghost trainer

    # Room W2: Kitchen (mid-left)
    rect(b, 2, 11, 5, 4, PATH)
    hline(b, 13, 7, 7, PATH)  # single-tile doorway
    ov(o, 4, 12, ITEM)

    # Room W3: Library (lower-left) — has warp to basement
    rect(b, 2, 18, 8, 6, PATH)
    vline(b, 6, 16, 17, PATH)  # narrow corridor from west hall
    hline(b, 16, 4, 6, PATH)
    ov(o, 3, 20, SIGN)    # dusty journal
    ov(o, 5, 21, SIGN)    # dusty journal
    b[22][4] = WARP        # warp to basement!
    ov(o, 7, 19, NPC)     # ghost NPC

    # Room W4: Servant quarters
    rect(b, 2, 26, 6, 5, PATH)
    hline(b, 28, 8, 15, PATH)  # corridor
    vline(b, 15, 28, 31, PATH)
    hline(b, 31, 15, 19, PATH)
    ov(o, 3, 27, TDOWN)   # ghost trainer in dead end
    ov(o, 5, 29, HIDDEN)  # hidden item behind wall

    # Secret passage behind walls (west)
    vline(b, 11, 4, 10, PATH)  # hidden corridor
    ov(o, 11, 7, HIDDEN)  # marks secret passage

    # EAST WING
    # East corridor from main hall
    hline(b, 14, 27, 46, PATH)
    hline(b, 15, 27, 46, PATH)

    # Room E1: Ballroom (top-right) — large
    rect(b, 34, 3, 14, 7, PATH)
    vline(b, 40, 10, 14, PATH)  # corridor down
    ov(o, 38, 5, FLOWER)  # cobweb
    ov(o, 42, 5, FLOWER)  # cobweb
    ov(o, 40, 4, STATUE)  # creepy statue
    ov(o, 36, 6, TDOWN)   # ghost trainer
    ov(o, 44, 6, TDOWN)   # ghost trainer

    # Room E2: Study (mid-right)
    rect(b, 40, 17, 8, 5, PATH)
    hline(b, 17, 38, 39, PATH)  # narrow entry
    vline(b, 38, 16, 17, PATH)
    ov(o, 43, 18, SIGN)   # dusty journal
    ov(o, 45, 19, ITEM)

    # Room E3: Bedroom (lower-right)
    rect(b, 38, 24, 10, 5, PATH)
    hline(b, 24, 35, 37, PATH)  # corridor
    vline(b, 35, 16, 24, PATH)
    ov(o, 42, 25, FLOWER)  # cobweb
    ov(o, 40, 26, NPC)    # ghost NPC
    ov(o, 45, 27, HIDDEN)  # hidden item

    # Room E4: Trophy room (dead end reward)
    rect(b, 30, 19, 5, 4, PATH)
    vline(b, 32, 16, 18, PATH)
    ov(o, 31, 20, ITEM)
    ov(o, 33, 20, POKEBALL)

    # UPPER LEVEL — Attic access via narrow stairs
    # North corridor
    hline(b, 8, 14, 36, PATH)
    hline(b, 9, 14, 36, PATH)

    # Upper rooms connecting to north corridor
    # Room N1: Gallery
    rect(b, 14, 3, 8, 5, PATH)
    vline(b, 18, 8, 8, PATH)
    ov(o, 15, 4, FLOWER)
    ov(o, 19, 4, FLOWER)
    ov(o, 17, 5, NPC)     # ghost NPC

    # THE ATTIC — top right, reached by narrow winding path
    rect(b, 28, 2, 4, 4, PATH)  # attic room
    vline(b, 30, 6, 8, PATH)  # narrow stair
    b[3][30] = LEGENDARY   # legendary encounter in the attic!
    ov(o, 29, 2, FLOWER)
    ov(o, 31, 2, FLOWER)

    # BASEMENT — below library area, reached by warp
    # Basement corridors in bottom section
    rect(b, 2, 33, 12, 5, PATH)
    hline(b, 35, 14, 18, PATH)
    b[33][4] = WARP  # warp back from basement
    ov(o, 8, 34, TDOWN)  # ghost trainer in basement
    ov(o, 6, 35, ITEM)   # basement treasure
    ov(o, 10, 36, HIDDEN)  # hidden item in basement
    ov(o, 3, 34, SIGN)    # basement journal

    # More dead-end branches with rewards
    # Dead end off east corridor
    rect(b, 46, 8, 3, 6, PATH)
    vline(b, 47, 14, 15, PATH)
    ov(o, 47, 9, POKEBALL)

    # Flower/cobweb decorations along corridors
    ov(o, 23, 12, FLOWER)
    ov(o, 26, 12, FLOWER)
    ov(o, 23, 20, FLOWER)
    ov(o, 26, 20, FLOWER)

    # Additional ghost NPCs
    ov(o, 25, 33, NPC)

    save("haunted_mansion", "Haunted Mansion", W, H, b, o)


# ============================================================
# MAP 2: ANCIENT RUINS (65 x 55) — Outdoor/Mixed
# ============================================================
def make_ancient_ruins():
    W, H = 65, 55
    b = grid(W, H, GRASS)  # sand = grass
    o = overlay(W, H)

    # Border
    rect(b, 0, 0, W, 1, TREE)
    rect(b, 0, H-1, W, 1, TREE)
    rect(b, 0, 0, 1, H, TREE)
    rect(b, W-1, 0, 1, H, TREE)

    # Entry paths — north entry
    rect(b, 30, 0, 4, 6, PATH)

    # South entry
    rect(b, 30, 50, 4, 5, PATH)

    # Main paths winding through the ruins
    # North-south main avenue
    rect(b, 30, 5, 4, 46, PATH)

    # East-west cross path (upper)
    rect(b, 8, 15, 50, 3, PATH)

    # East-west cross path (lower)
    rect(b, 8, 38, 50, 3, PATH)

    # Winding side paths
    rect(b, 8, 10, 3, 6, PATH)
    rect(b, 8, 10, 15, 3, PATH)

    rect(b, 50, 10, 3, 6, PATH)
    rect(b, 42, 10, 11, 3, PATH)

    # ---- ANCIENT TEMPLE (center) ----
    # Large rock structure with maze interior
    rect(b, 18, 20, 28, 16, ROCK)
    # Temple entrance (south side)
    rect(b, 30, 34, 4, 2, PATH)
    # Temple interior corridors (maze-like)
    rect(b, 20, 22, 24, 2, PATH)  # top corridor
    rect(b, 20, 26, 24, 2, PATH)  # middle corridor
    rect(b, 20, 30, 24, 2, PATH)  # bottom corridor
    # Vertical connectors inside temple
    vline(b, 20, 22, 33, PATH)
    vline(b, 21, 22, 33, PATH)
    vline(b, 42, 22, 33, PATH)
    vline(b, 43, 22, 33, PATH)
    # Dead-end branches inside
    rect(b, 26, 24, 3, 2, PATH)
    rect(b, 35, 24, 3, 2, PATH)
    rect(b, 26, 28, 3, 2, PATH)
    rect(b, 35, 28, 3, 2, PATH)
    # Strength rocks blocking inner passages
    b[24][25] = STROCK
    b[28][38] = STROCK
    # Legendary deep in temple
    b[23][32] = LEGENDARY
    ov(o, 32, 22, SIGN)  # hieroglyph
    ov(o, 28, 26, SIGN)  # hieroglyph
    ov(o, 36, 30, SIGN)  # hieroglyph

    # Items in dead ends inside temple
    ov(o, 27, 24, ITEM)
    ov(o, 36, 24, HIDDEN)
    ov(o, 27, 28, POKEBALL)
    ov(o, 36, 28, ITEM)

    # Trainers inside temple (archaeologist rivals)
    ov(o, 22, 27, TDOWN)
    ov(o, 41, 23, TDOWN)
    ov(o, 30, 31, TUP)

    # ---- RUINED STRUCTURES (outside temple) ----
    # Crumbling ruin NW
    rect(b, 4, 4, 8, 6, ROCK)
    rect(b, 5, 5, 6, 4, PATH)  # hollow interior
    b[9][7] = PATH  # gap in wall (crumbling)
    b[9][8] = PATH
    ov(o, 7, 6, SIGN)   # hieroglyph
    ov(o, 6, 7, ITEM)

    # Crumbling ruin NE
    rect(b, 50, 4, 10, 5, ROCK)
    rect(b, 51, 5, 8, 3, PATH)
    b[8][55] = PATH  # gap
    ov(o, 54, 6, HIDDEN)
    ov(o, 52, 6, SIGN)

    # Crumbling ruin SW
    rect(b, 4, 42, 8, 6, ROCK)
    rect(b, 5, 43, 6, 4, PATH)
    b[46][4] = PATH  # gap
    ov(o, 7, 44, POKEBALL)

    # Crumbling ruin SE
    rect(b, 52, 42, 8, 6, ROCK)
    rect(b, 53, 43, 6, 4, PATH)
    b[46][59] = PATH  # gap
    ov(o, 56, 44, ITEM)

    # ---- WATER CHANNELS (ancient aqueducts) ----
    # Horizontal aqueduct north
    rect(b, 12, 13, 16, 1, WEDGE)
    rect(b, 12, 14, 16, 1, WATER)
    rect(b, 12, 15, 16, 1, WEDGE)
    # ... override the cross path section
    rect(b, 28, 13, 2, 1, BRIDGE)  # bridge over aqueduct
    rect(b, 28, 14, 2, 1, BRIDGE)

    # Horizontal aqueduct south
    rect(b, 38, 40, 16, 1, WEDGE)
    rect(b, 38, 41, 16, 1, WATER)
    rect(b, 38, 42, 16, 1, WEDGE)

    # Surf water section in SE aqueduct
    b[41][45] = SURF
    b[41][46] = SURF
    b[41][47] = SURF

    # ---- RARE GRASS ZONES ----
    rect(b, 4, 30, 8, 6, RAREGRASS)
    rect(b, 55, 30, 8, 5, RAREGRASS)

    # ---- TALL GRASS (encounter zones) ----
    rect(b, 14, 44, 10, 6, TALLGRASS)
    rect(b, 45, 44, 10, 6, TALLGRASS)
    rect(b, 4, 18, 6, 5, TALLGRASS)

    # ---- HIDDEN CHAMBERS behind cut trees ----
    # NE hidden valley
    rect(b, 55, 14, 8, 5, RAREGRASS)
    b[14][54] = CUTTREE
    b[15][54] = CUTTREE
    ov(o, 58, 16, ITEM)

    # SW hidden area
    b[35][7] = CUTTREE
    rect(b, 4, 36, 3, 3, PATH)
    ov(o, 5, 37, KEYITEM)

    # ---- TRAINERS (10+ archaeologist rivals) ----
    ov(o, 12, 11, TDOWN)
    ov(o, 48, 11, TDOWN)
    ov(o, 15, 39, TDOWN)
    ov(o, 50, 39, TDOWN)
    ov(o, 34, 8, TDOWN)
    ov(o, 25, 16, TRIGHT)
    ov(o, 45, 16, TLEFT)
    ov(o, 10, 38, TUP)
    ov(o, 55, 38, TUP)
    ov(o, 34, 48, TUP)

    # Signs — hieroglyphs throughout
    ov(o, 31, 2, SIGN)
    ov(o, 31, 52, SIGN)
    ov(o, 9, 16, SIGN)

    # NPCs
    ov(o, 32, 5, NPC)
    ov(o, 12, 42, NPC)

    # Paths connecting ruins to main paths
    rect(b, 8, 8, 3, 3, PATH)
    rect(b, 50, 8, 3, 3, PATH)
    rect(b, 8, 40, 3, 3, PATH)
    rect(b, 52, 40, 3, 3, PATH)

    save("ancient_ruins", "Ancient Ruins", W, H, b, o)


# ============================================================
# MAP 3: POWER PLANT (45 x 35) — Interior
# ============================================================
def make_power_plant():
    W, H = 45, 35
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # Entry at bottom-left
    rect(b, 2, 31, 4, 4, PATH)
    hline(b, 34, 2, 5, PATH)

    # Main corridor (horizontal, bottom)
    rect(b, 2, 28, 40, 3, PATH)

    # Left vertical corridor
    rect(b, 2, 4, 3, 24, PATH)

    # Right vertical corridor
    rect(b, 39, 4, 3, 24, PATH)

    # Top horizontal corridor
    rect(b, 2, 4, 40, 3, PATH)

    # Mid horizontal corridor
    rect(b, 8, 16, 28, 3, PATH)

    # ---- GENERATOR ROOM (large central area, upper) ----
    rect(b, 10, 8, 24, 7, PATH)
    # Fence-bordered machines
    rect(b, 12, 9, 8, 5, FENCE)
    rect(b, 13, 10, 6, 3, PATH)  # inside machine area
    rect(b, 24, 9, 8, 5, FENCE)
    rect(b, 25, 10, 6, 3, PATH)  # inside machine area
    # Villain boss in generator room
    ov(o, 20, 10, VBOSS)
    # Electric barriers (fence)
    hline(b, 7, 10, 33, FENCE)
    b[7][18] = PATH  # gap to enter
    b[7][26] = PATH  # gap to enter

    # ---- CONTROL ROOM (top-right) ----
    rect(b, 32, 8, 8, 6, PATH)
    vline(b, 31, 9, 11, PATH)  # doorway from generator area
    ov(o, 35, 9, PC)
    ov(o, 37, 9, PC)
    ov(o, 36, 11, KEYITEM)  # key item in control room!
    ov(o, 34, 10, SIGN)

    # ---- SERVICE TUNNELS (1-wide cave corridors) ----
    # Tunnel from left corridor going right
    hline(b, 22, 6, 15, CAVE)
    # Tunnel branching down
    vline(b, 15, 22, 27, CAVE)
    # Tunnel from right corridor going left
    hline(b, 22, 30, 38, CAVE)
    # Cross tunnel
    vline(b, 22, 19, 27, CAVE)

    # ---- STORAGE ROOMS (items) ----
    # Storage room 1 (left, mid)
    rect(b, 6, 19, 5, 4, PATH)
    hline(b, 19, 5, 5, PATH)
    ov(o, 8, 20, ITEM)
    ov(o, 9, 21, POKEBALL)

    # Storage room 2 (right, mid)
    rect(b, 34, 19, 5, 4, PATH)
    hline(b, 19, 33, 33, PATH)
    ov(o, 36, 20, ITEM)
    ov(o, 37, 21, HIDDEN)

    # Storage room 3 (bottom, near entry)
    rect(b, 14, 25, 5, 3, PATH)
    vline(b, 16, 23, 24, PATH)
    ov(o, 15, 25, ITEM)

    # ---- WARP TILES (elevators between floors) ----
    b[5][5] = WARP    # elevator NW
    b[5][40] = WARP   # elevator NE
    b[29][20] = WARP  # elevator mid

    # ---- VILLAIN MINIONS (8+) ----
    ov(o, 4, 10, MDOWN)   # left corridor
    ov(o, 4, 20, MDOWN)   # left corridor
    ov(o, 40, 10, MDOWN)  # right corridor
    ov(o, 40, 20, MDOWN)  # right corridor
    ov(o, 20, 29, MUP)    # bottom corridor
    ov(o, 30, 29, MUP)    # bottom corridor
    ov(o, 12, 17, MDOWN)  # mid corridor
    ov(o, 28, 17, MDOWN)  # mid corridor

    # Fence barriers across corridors
    b[14][4] = FENCE
    b[14][40] = FENCE
    # Gaps to pass
    b[14][3] = PATH

    # Signs
    ov(o, 3, 29, SIGN)  # "Danger: High Voltage"
    ov(o, 18, 5, SIGN)  # "Generator Room"

    save("power_plant", "Power Plant", W, H, b, o)


# ============================================================
# MAP 4: DESERT OASIS (70 x 55) — Outdoor
# ============================================================
def make_desert_oasis():
    W, H = 70, 55
    b = grid(W, H, GRASS)  # grass = sand
    o = overlay(W, H)

    # Border
    rect(b, 0, 0, W, 1, TREE)
    rect(b, 0, H-1, W, 1, TREE)
    rect(b, 0, 0, 1, H, TREE)
    rect(b, W-1, 0, 1, H, TREE)

    # Entry — east side
    rect(b, 66, 25, 4, 4, PATH)
    b[25][69] = PATH; b[26][69] = PATH; b[27][69] = PATH; b[28][69] = PATH

    # Entry — north side
    rect(b, 33, 0, 4, 5, PATH)

    # ---- MAIN PATHS winding through desert ----
    # East entry path winds west toward oasis
    rect(b, 40, 25, 30, 3, PATH)
    # Curve south
    rect(b, 40, 25, 3, 10, PATH)
    # Continue west
    rect(b, 28, 33, 15, 3, PATH)
    # North entry path winds south
    rect(b, 33, 4, 3, 15, PATH)
    # Connect to oasis area
    rect(b, 28, 17, 8, 3, PATH)
    rect(b, 28, 17, 3, 18, PATH)

    # Secondary paths
    rect(b, 15, 17, 13, 3, PATH)
    rect(b, 15, 17, 3, 20, PATH)
    rect(b, 15, 35, 13, 3, PATH)

    # ---- THE OASIS (central water body) ----
    # Water body with edge
    rect(b, 23, 20, 16, 12, WEDGE)
    rect(b, 24, 21, 14, 10, WATER)
    # Surf area in center
    rect(b, 27, 24, 6, 4, SURF)

    # Trees and flowers around oasis
    rect(b, 21, 18, 2, 16, TREE)
    rect(b, 39, 18, 2, 16, TREE)
    rect(b, 23, 18, 14, 2, TREE)
    rect(b, 23, 32, 14, 2, TREE)
    # Flower patches
    rect(b, 22, 19, 1, 14, FLOWER)
    rect(b, 40, 19, 1, 14, FLOWER)
    # Gaps for path access
    rect(b, 28, 32, 3, 2, PATH)  # south gap
    rect(b, 28, 18, 3, 2, PATH)  # north gap
    rect(b, 21, 25, 2, 3, PATH)  # west gap
    rect(b, 39, 25, 2, 3, PATH)  # east gap

    # ---- OASIS SETTLEMENT ----
    # Pokecenter
    bldg(b, 44, 14, POKECENTER)
    rect(b, 44, 18, 4, 2, PATH)
    hline(b, 18, 36, 47, PATH)

    # Houses
    bldg(b, 44, 8, HOUSE_LG)
    rect(b, 44, 12, 4, 3, PATH)
    bldg(b, 50, 8, HOUSE_SM)
    rect(b, 50, 11, 3, 4, PATH)

    # Mart
    bldg(b, 50, 14, POKEMART)
    rect(b, 50, 18, 4, 2, PATH)

    # Connect settlement
    rect(b, 44, 18, 10, 2, PATH)

    # ---- ROCK FORMATIONS (sand dunes) ----
    rect(b, 5, 5, 8, 4, ROCK)
    rect(b, 55, 5, 8, 4, ROCK)
    rect(b, 5, 42, 6, 4, ROCK)
    rect(b, 55, 42, 10, 4, ROCK)
    rect(b, 48, 38, 5, 3, ROCK)

    # ---- SANDSTORM AREAS (tall grass encounter zones) ----
    rect(b, 8, 10, 8, 6, TALLGRASS)
    rect(b, 55, 12, 8, 6, TALLGRASS)
    rect(b, 8, 38, 6, 6, TALLGRASS)
    rect(b, 45, 42, 8, 6, TALLGRASS)
    rect(b, 30, 42, 8, 8, TALLGRASS)

    # ---- ANCIENT TOMB (cave entrance, far west behind rocks) ----
    rect(b, 2, 24, 6, 4, ROCK)
    rect(b, 3, 25, 4, 2, PATH)
    bldg(b, 3, 25, CAVE_ENT)
    # Path to tomb
    rect(b, 8, 25, 7, 3, PATH)

    # ---- HIDDEN VALLEY (NE behind cut trees) ----
    rect(b, 56, 15, 8, 8, RAREGRASS)
    b[15][55] = CUTTREE
    b[16][55] = CUTTREE
    b[17][55] = CUTTREE
    ov(o, 60, 18, ITEM)
    ov(o, 58, 17, POKEBALL)

    # ---- TRAINERS along desert paths ----
    ov(o, 50, 26, TDOWN)
    ov(o, 60, 26, TLEFT)
    ov(o, 35, 8, TDOWN)
    ov(o, 17, 18, TRIGHT)
    ov(o, 17, 36, TDOWN)
    ov(o, 42, 34, TUP)
    ov(o, 10, 26, TDOWN)
    ov(o, 30, 47, TUP)

    # ---- MIRAGE ITEMS (hidden items in deep desert) ----
    ov(o, 6, 15, HIDDEN)
    ov(o, 60, 8, HIDDEN)
    ov(o, 62, 45, HIDDEN)
    ov(o, 3, 48, HIDDEN)
    ov(o, 35, 50, HIDDEN)

    # NPCs
    ov(o, 45, 19, NPC)  # oasis NPC
    ov(o, 30, 26, NPC)  # desert wanderer
    ov(o, 10, 28, NPC)  # near tomb

    # Signs
    ov(o, 34, 2, SIGN)
    ov(o, 65, 26, SIGN)
    ov(o, 9, 26, SIGN)  # "Ancient Tomb ahead"

    save("desert_oasis", "Desert Oasis", W, H, b, o)


# ============================================================
# MAP 5: SKY TOWER (40 x 55) — Interior vertical tower
# ============================================================
def make_sky_tower():
    W, H = 40, 55
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # 5 floors stacked vertically, each ~40x10
    # Floor 1 (bottom, rows 44-54): Lobby
    # Floor 2 (rows 34-43): Trainer gauntlet
    # Floor 3 (rows 23-33): Puzzle floor
    # Floor 4 (rows 13-22): Elite trainers
    # Floor 5 (rows 1-12): Rooftop

    # ---- FLOOR 1: LOBBY (rows 44-54) ----
    rect(b, 4, 45, 32, 8, PATH)
    # Entry at bottom
    rect(b, 17, 53, 6, 2, PATH)
    # Heal NPC
    ov(o, 20, 47, HEAL)
    ov(o, 18, 47, NPC)
    ov(o, 15, 48, SIGN)  # "Sky Tower"
    # Stair warp (up)
    b[45][34] = WARP  # to floor 2
    ov(o, 34, 46, SIGN)  # "Stairs"
    # Decorations
    ov(o, 6, 46, STATUE)
    ov(o, 33, 46, STATUE)

    # Rock border around floor
    hline(b, 44, 4, 35, ROCK)
    hline(b, 44, 17, 22, PATH)  # gap for stairs down from F2

    # ---- FLOOR 2: TRAINER GAUNTLET (rows 34-43) ----
    rect(b, 4, 35, 32, 8, PATH)
    # 4 fenced battle lanes
    # Lane 1
    rect(b, 5, 36, 3, 6, FENCE)
    rect(b, 6, 37, 1, 4, PATH)
    ov(o, 6, 38, TDOWN)
    # Lane 2
    rect(b, 11, 36, 3, 6, FENCE)
    rect(b, 12, 37, 1, 4, PATH)
    ov(o, 12, 38, TDOWN)
    # Lane 3
    rect(b, 17, 36, 3, 6, FENCE)
    rect(b, 18, 37, 1, 4, PATH)
    ov(o, 18, 38, TDOWN)
    # Lane 4
    rect(b, 23, 36, 3, 6, FENCE)
    rect(b, 24, 37, 1, 4, PATH)
    ov(o, 24, 38, TDOWN)

    # Corridor below and above lanes
    hline(b, 34, 4, 35, ROCK)
    hline(b, 34, 17, 22, PATH)  # gap
    # Warps
    b[35][34] = WARP  # up to floor 3
    b[42][18] = WARP  # down to floor 1

    # ---- FLOOR 3: PUZZLE FLOOR (rows 23-33) ----
    rect(b, 4, 24, 32, 9, PATH)
    # Winding path with strength rocks
    # Create maze walls inside
    rect(b, 8, 25, 1, 7, ROCK)
    rect(b, 12, 24, 1, 6, ROCK)
    rect(b, 16, 26, 1, 6, ROCK)
    rect(b, 20, 24, 1, 7, ROCK)
    rect(b, 24, 26, 1, 6, ROCK)
    rect(b, 28, 24, 1, 6, ROCK)
    rect(b, 32, 26, 1, 5, ROCK)
    # Strength rocks blocking shortcuts
    b[27][8] = STROCK
    b[25][16] = STROCK
    b[28][24] = STROCK
    b[26][32] = STROCK
    # Items in dead ends
    ov(o, 10, 30, ITEM)
    ov(o, 22, 25, HIDDEN)
    ov(o, 30, 30, POKEBALL)

    # Floor boundaries
    hline(b, 23, 4, 35, ROCK)
    hline(b, 23, 17, 22, PATH)  # gap
    # Warps
    b[24][34] = WARP  # up to floor 4
    b[32][18] = WARP  # down to floor 2

    # ---- FLOOR 4: ELITE TRAINERS (rows 13-22) ----
    rect(b, 6, 14, 28, 8, PATH)
    # Two tough trainers in arenas
    # Arena 1
    rect(b, 8, 15, 7, 5, FENCE)
    rect(b, 9, 16, 5, 3, PATH)
    ov(o, 11, 17, TDOWN)  # tough trainer 1
    b[20][11] = PATH  # entrance
    b[15][11] = PATH  # exit
    # Arena 2
    rect(b, 24, 15, 7, 5, FENCE)
    rect(b, 25, 16, 5, 3, PATH)
    ov(o, 27, 17, TDOWN)  # tough trainer 2
    b[20][27] = PATH  # entrance
    b[15][27] = PATH  # exit

    # Corridor
    hline(b, 13, 6, 33, ROCK)
    hline(b, 13, 17, 22, PATH)
    # Warps
    b[14][32] = WARP   # up to floor 5
    b[21][18] = WARP   # down to floor 3

    # ---- FLOOR 5: ROOFTOP (rows 1-12) ----
    # Open-air feel — grass tiles, flowers
    rect(b, 6, 2, 28, 10, GRASS)
    rect(b, 10, 3, 20, 8, PATH)
    # Flowers everywhere — rooftop garden
    for fx in range(7, 33, 3):
        for fy in range(2, 11, 3):
            if 0 <= fy < H and 0 <= fx < W:
                b[fy][fx] = FLOWER
    # LEGENDARY at the peak
    b[5][20] = LEGENDARY
    ov(o, 20, 3, STATUE)  # shrine
    ov(o, 18, 4, FLOWER)
    ov(o, 22, 4, FLOWER)

    # Warp back down
    b[10][18] = WARP  # down to floor 4

    # NPC on rooftop
    ov(o, 16, 6, NPC)
    ov(o, 24, 6, NPC)

    save("sky_tower", "Sky Tower", W, H, b, o)


# ============================================================
# MAP 6: UNDERGROUND LAB (55 x 45) — Interior
# ============================================================
def make_underground_lab():
    W, H = 55, 45
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # Entry at top-left
    rect(b, 2, 2, 4, 3, PATH)
    b[2][2] = WARP  # surface warp

    # ---- MAIN CORRIDORS (sterile, grid-like) ----
    # Horizontal corridors
    rect(b, 2, 4, 50, 2, PATH)
    rect(b, 2, 14, 50, 2, PATH)
    rect(b, 2, 24, 50, 2, PATH)
    rect(b, 2, 34, 50, 2, PATH)
    rect(b, 2, 42, 50, 2, PATH)

    # Vertical corridors
    rect(b, 2, 4, 2, 40, PATH)
    rect(b, 18, 4, 2, 40, PATH)
    rect(b, 34, 4, 2, 40, PATH)
    rect(b, 50, 4, 2, 40, PATH)

    # ---- CONTAINMENT CELLS (fenced rooms with rare grass) ----
    # Cell 1
    rect(b, 6, 7, 8, 5, FENCE)
    rect(b, 7, 8, 6, 3, RAREGRASS)
    b[12][10] = PATH  # entrance
    ov(o, 10, 8, SIGN)  # "Subject 001"

    # Cell 2
    rect(b, 22, 7, 8, 5, FENCE)
    rect(b, 23, 8, 6, 3, RAREGRASS)
    b[12][26] = PATH
    ov(o, 26, 8, SIGN)  # "Subject 002"

    # Cell 3
    rect(b, 38, 7, 8, 5, FENCE)
    rect(b, 39, 8, 6, 3, RAREGRASS)
    b[12][42] = PATH
    ov(o, 42, 8, SIGN)  # "Subject 003"

    # Cell 4
    rect(b, 6, 17, 8, 5, FENCE)
    rect(b, 7, 18, 6, 3, RAREGRASS)
    b[22][10] = PATH
    ov(o, 10, 18, SIGN)

    # ---- CENTRAL EXPERIMENT CHAMBER ----
    rect(b, 22, 17, 10, 7, PATH)
    rect(b, 24, 19, 6, 3, FENCE)
    rect(b, 25, 20, 4, 1, PATH)
    b[20][27] = LEGENDARY  # legendary in experiment chamber!
    ov(o, 26, 18, SIGN)   # "Project Genesis"
    b[22][27] = PATH  # entrance to inner ring

    # ---- LAB ROOMS (PC, items) ----
    # Lab room 1
    rect(b, 38, 17, 8, 5, PATH)
    ov(o, 41, 18, PC)
    ov(o, 43, 18, PC)
    ov(o, 40, 20, ITEM)

    # Lab room 2
    rect(b, 6, 27, 8, 5, PATH)
    ov(o, 9, 28, PC)
    ov(o, 11, 28, ITEM)
    ov(o, 8, 30, HIDDEN)

    # Lab room 3
    rect(b, 22, 27, 8, 5, PATH)
    ov(o, 25, 28, ITEM)
    ov(o, 27, 28, POKEBALL)

    # ---- SECURITY GATES (block sections) ----
    bldg(b, 16, 13, GATE)  # gate between sections
    bldg(b, 32, 13, GATE)
    bldg(b, 16, 23, GATE)
    bldg(b, 32, 23, GATE)

    # Key items to unlock gates
    ov(o, 44, 20, KEYITEM)  # Security Card A
    ov(o, 10, 30, KEYITEM)  # Security Card B

    # ---- DECONTAMINATION AIRLOCKS (warps) ----
    b[15][10] = WARP
    b[15][42] = WARP
    b[25][10] = WARP
    b[25][42] = WARP

    # ---- VILLAIN PRESENCE ----
    # Minions (6)
    ov(o, 5, 5, MDOWN)
    ov(o, 19, 5, MDOWN)
    ov(o, 35, 5, MDOWN)
    ov(o, 51, 15, MDOWN)
    ov(o, 3, 25, MDOWN)
    ov(o, 51, 25, MDOWN)
    # Villain trainers (2)
    ov(o, 19, 35, VDOWN)
    ov(o, 35, 35, VDOWN)
    # Boss at the end (bottom)
    rect(b, 22, 37, 10, 5, PATH)
    ov(o, 27, 39, VBOSS)
    ov(o, 25, 38, SIGN)  # "Director's Office"
    ov(o, 24, 40, ITEM)

    # Signs
    ov(o, 4, 3, SIGN)   # "Underground Research Facility"
    ov(o, 36, 4, SIGN)  # "Restricted Area"

    save("underground_lab", "Underground Lab", W, H, b, o)


# ============================================================
# MAP 7: SUNKEN SHIP (50 x 40) — Mixed water/interior
# ============================================================
def make_sunken_ship():
    W, H = 50, 40
    b = grid(W, H, WATER)  # water surrounds
    o = overlay(W, H)

    # Water edge border
    rect(b, 0, 0, W, 1, WEDGE)
    rect(b, 0, H-1, W, 1, WEDGE)
    rect(b, 0, 0, 1, H, WEDGE)
    rect(b, W-1, 0, 1, H, WEDGE)

    # Shore on south side
    rect(b, 0, 34, W, 6, GRASS)
    rect(b, 0, 34, W, 1, WEDGE)
    rect(b, 20, 36, 10, 4, PATH)  # landing area

    # ---- THE SHIP (30x25 rock structure in center) ----
    # Ship hull
    rect(b, 10, 5, 30, 25, ROCK)

    # Ship deck (top level, open)
    rect(b, 12, 7, 26, 5, PATH)
    # Mast/decorations
    ov(o, 25, 8, STATUE)   # mast
    ov(o, 15, 8, SIGN)     # ship's log

    # ---- SHIP INTERIOR ----
    # Main corridor running through ship
    rect(b, 14, 13, 22, 2, PATH)
    # Secondary corridor
    rect(b, 14, 20, 22, 2, PATH)
    # Vertical corridors
    rect(b, 14, 7, 2, 21, PATH)
    rect(b, 34, 7, 2, 21, PATH)
    rect(b, 24, 13, 2, 9, PATH)

    # ---- CARGO HOLD (large room, left side) ----
    rect(b, 16, 15, 8, 5, PATH)
    ov(o, 18, 16, ITEM)
    ov(o, 20, 16, ITEM)
    ov(o, 17, 18, POKEBALL)
    ov(o, 21, 17, HIDDEN)

    # ---- CAPTAIN'S QUARTERS (top-right) ----
    rect(b, 28, 7, 6, 5, PATH)
    ov(o, 30, 8, KEYITEM)   # captain's key
    ov(o, 32, 8, SIGN)      # captain's journal
    ov(o, 31, 10, NPC)      # ghost captain

    # ---- ENGINE ROOM (bottom, cave tiles for dark areas) ----
    rect(b, 16, 23, 18, 5, CAVE)
    # Cave encounter zone
    ov(o, 20, 24, TDOWN)   # ghost sailor
    ov(o, 28, 24, TDOWN)   # ghost sailor

    # ---- CREW QUARTERS (multiple small rooms, right side) ----
    # Room 1
    rect(b, 28, 14, 5, 3, PATH)
    ov(o, 30, 15, NPC)  # ghost sailor
    # Room 2
    rect(b, 28, 18, 5, 3, PATH)
    ov(o, 30, 19, HIDDEN)
    # Room 3
    rect(b, 37, 14, 3, 7, PATH)  # narrow corridor
    ov(o, 38, 17, ITEM)

    # ---- HOLES IN HULL (water inside ship) ----
    rect(b, 18, 22, 3, 1, WATER)
    rect(b, 30, 22, 3, 1, WATER)
    rect(b, 22, 27, 4, 1, WATER)

    # ---- SURF REQUIRED SECTIONS ----
    # Far section of the ship behind water
    rect(b, 12, 25, 2, 3, SURF)  # surf gap
    rect(b, 12, 28, 8, 2, PATH)  # hidden room behind surf
    ov(o, 14, 28, ITEM)
    ov(o, 16, 28, POKEBALL)
    ov(o, 18, 28, HIDDEN)

    # ---- BRIDGE TO SHIP ----
    rect(b, 24, 30, 2, 5, BRIDGE)

    # ---- TRAINERS (ghost sailors) ----
    ov(o, 15, 9, TDOWN)    # on deck
    ov(o, 33, 9, TDOWN)    # on deck
    ov(o, 15, 14, TRIGHT)  # corridor
    ov(o, 33, 20, TLEFT)   # corridor
    ov(o, 25, 25, TDOWN)   # engine room

    # Shore NPCs
    ov(o, 22, 37, NPC)
    ov(o, 27, 37, SIGN)  # "The SS Phantom"

    # Warp back to surface
    b[7][13] = WARP

    save("sunken_ship", "Sunken Ship", W, H, b, o)


# ============================================================
# MAP 8: SEWER NETWORK (60 x 50) — Interior
# ============================================================
def make_sewer_network():
    W, H = 60, 50
    b = grid(W, H, ROCK)
    o = overlay(W, H)

    # ---- WATER CHANNELS (3-4 wide center of tunnels) ----
    # Main horizontal channel (center)
    rect(b, 4, 23, 52, 4, WATER)
    # Walkways on both sides
    rect(b, 4, 21, 52, 2, PATH)
    rect(b, 4, 27, 52, 2, PATH)

    # Main vertical channel (center)
    rect(b, 28, 4, 4, 42, WATER)
    # Walkways
    rect(b, 26, 4, 2, 42, PATH)
    rect(b, 32, 4, 2, 42, PATH)

    # ---- MAJOR INTERSECTION HUB ----
    rect(b, 22, 19, 16, 12, PATH)
    rect(b, 26, 23, 8, 4, WATER)  # water in center of hub

    # Bridge crossings over water at intersection
    rect(b, 28, 21, 4, 2, BRIDGE)
    rect(b, 28, 27, 4, 2, BRIDGE)
    rect(b, 26, 23, 2, 4, BRIDGE)
    rect(b, 32, 23, 2, 4, BRIDGE)

    # ---- NW QUADRANT ----
    # Tunnel going NW
    rect(b, 4, 8, 20, 2, PATH)
    rect(b, 4, 10, 20, 3, WATER)
    rect(b, 4, 13, 20, 2, PATH)
    # Maintenance room NW
    rect(b, 5, 4, 8, 4, PATH)
    vline(b, 9, 4, 7, PATH)
    ov(o, 7, 5, ITEM)
    ov(o, 10, 5, POKEBALL)
    # Dead end pipe NW
    rect(b, 16, 4, 5, 4, PATH)
    ov(o, 18, 5, HIDDEN)

    # Valve room NW (strength rocks open passage)
    rect(b, 4, 16, 6, 4, PATH)
    b[17][10] = STROCK   # blocks passage

    # ---- NE QUADRANT ----
    rect(b, 36, 8, 18, 2, PATH)
    rect(b, 36, 10, 18, 3, WATER)
    rect(b, 36, 13, 18, 2, PATH)
    # Maintenance room NE
    rect(b, 44, 4, 8, 4, PATH)
    vline(b, 48, 4, 7, PATH)
    ov(o, 46, 5, ITEM)
    ov(o, 49, 5, HIDDEN)
    # Dead end with pokeball
    rect(b, 36, 4, 6, 4, PATH)
    ov(o, 38, 5, POKEBALL)

    # Valve room NE
    rect(b, 50, 16, 6, 4, PATH)
    b[17][49] = STROCK

    # ---- SW QUADRANT ----
    rect(b, 4, 31, 20, 2, PATH)
    rect(b, 4, 33, 20, 3, WATER)
    rect(b, 4, 36, 20, 2, PATH)
    # Maintenance room SW
    rect(b, 5, 40, 8, 4, PATH)
    vline(b, 9, 38, 43, PATH)
    ov(o, 7, 41, ITEM)
    ov(o, 10, 42, POKEBALL)
    # Dead end
    rect(b, 16, 40, 6, 4, PATH)
    ov(o, 19, 41, HIDDEN)

    # ---- SE QUADRANT ----
    rect(b, 36, 31, 18, 2, PATH)
    rect(b, 36, 33, 18, 3, WATER)
    rect(b, 36, 36, 18, 2, PATH)
    # Maintenance room SE
    rect(b, 44, 40, 8, 4, PATH)
    vline(b, 48, 38, 43, PATH)
    ov(o, 46, 41, ITEM)
    ov(o, 49, 42, HIDDEN)

    # ---- VILLAIN SECRET MEETING ROOM ----
    # Behind multiple gates in SE
    bldg(b, 46, 36, GATE)
    rect(b, 50, 37, 6, 6, PATH)
    bldg(b, 50, 36, GATE)
    rect(b, 52, 40, 5, 5, PATH)
    ov(o, 54, 42, VBOSS)   # villain boss
    ov(o, 53, 41, SIGN)    # "Secret Plans"
    ov(o, 55, 41, KEYITEM) # key item
    ov(o, 54, 43, HIDDEN)

    # ---- CAVE TILE SECTIONS (dark areas) ----
    rect(b, 4, 16, 3, 5, CAVE)
    rect(b, 53, 16, 3, 5, CAVE)
    rect(b, 4, 29, 3, 3, CAVE)
    rect(b, 53, 29, 3, 3, CAVE)

    # ---- WARPS TO SURFACE ----
    b[2][4] = WARP     # NW surface exit
    b[2][55] = WARP    # NE surface exit
    b[47][4] = WARP    # SW surface exit
    b[47][55] = WARP   # SE surface exit
    # Paths to warps
    rect(b, 3, 2, 3, 2, PATH)
    rect(b, 54, 2, 3, 2, PATH)
    rect(b, 3, 47, 3, 2, PATH)
    rect(b, 54, 47, 3, 2, PATH)
    # Connect warps to main tunnels
    vline(b, 4, 4, 7, PATH)
    vline(b, 5, 4, 7, PATH)
    vline(b, 55, 4, 7, PATH)
    vline(b, 56, 4, 7, PATH)
    vline(b, 4, 38, 46, PATH)
    vline(b, 5, 38, 46, PATH)
    vline(b, 55, 38, 46, PATH)
    vline(b, 56, 38, 46, PATH)

    # ---- TRAINERS (rat trainers, 8+) ----
    ov(o, 10, 9, TDOWN)
    ov(o, 10, 32, TDOWN)
    ov(o, 44, 9, TDOWN)
    ov(o, 44, 32, TDOWN)
    ov(o, 27, 10, TDOWN)
    ov(o, 33, 36, TUP)
    ov(o, 15, 22, TRIGHT)
    ov(o, 45, 22, TLEFT)
    ov(o, 27, 36, TUP)
    ov(o, 33, 10, TDOWN)

    # ---- SIGNS ----
    ov(o, 5, 3, SIGN)   # "Sector NW"
    ov(o, 55, 3, SIGN)  # "Sector NE"
    ov(o, 5, 47, SIGN)  # "Sector SW"
    ov(o, 55, 47, SIGN) # "Sector SE"
    ov(o, 30, 20, SIGN) # "Central Hub"

    save("sewer_network", "Sewer Network", W, H, b, o)


# ============================================================
# Generate all maps
# ============================================================
if __name__ == "__main__":
    print("Generating Adventure/Special maps...")
    make_haunted_mansion()
    make_ancient_ruins()
    make_power_plant()
    make_desert_oasis()
    make_sky_tower()
    make_underground_lab()
    make_sunken_ship()
    make_sewer_network()
    print("Done! All 8 maps generated.")

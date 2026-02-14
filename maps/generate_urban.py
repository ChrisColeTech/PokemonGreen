"""Generate 8 urban/modern themed maps for PokemonGreen."""
import json, os

OUTPUT = r"D:\Projects\PokemonGreen\maps\exported"

# --- Tile IDs ---
WATER=0; GRASS=1; PATH=2; TREE=3; DOOR=4; BRIDGE=5; WALL=6; TGRASS=7; ROCK=8
SIGN=9; NPC=10; SHOP=11; HEAL=12; ITEM=13; KEYITEM=14; CAVE=15; WARP=16
WEDGE=17; FENCE=18; FLOWER=19; TUP=20; TDOWN=21; TLEFT=22; TRIGHT=23
GYMLEADER=24; SURF=25; STRROCK=26; CUTTREE=27; RGRASS=28; LEGEND=29
VBOSS=30; MDOWN=36; HITEM=40; PC=41; PBALL=42; STATUE=49

# --- Building templates ---
POKECENTER = [[3,3,3,3],[3,4,4,3],[3,4,4,3],[6,4,4,6]]
POKEMART = [[3,3,3,3],[3,6,6,3],[3,11,6,3],[6,4,4,6]]
GYM = [[3,3,3,3,3],[3,6,6,6,3],[3,6,12,6,3],[3,6,4,6,3],[6,6,4,6,6]]
HOUSE_SM = [[3,3,3],[3,4,3],[6,4,6]]
HOUSE_LG = [[3,3,3,3],[3,6,6,3],[3,4,6,3],[6,4,6,6]]
LAB = [[3,3,3,3,3],[3,6,6,6,3],[3,4,41,4,3],[6,4,4,4,6]]
GATE = [[6,6,6,6],[6,16,16,6],[6,6,6,6]]
POND = [[17,0,0,17],[0,0,0,0],[17,0,0,17]]

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
    for x in range(x1, x2+1):
        if 0<=y<len(g) and 0<=x<len(g[0]):
            g[y][x] = t

def vline(g, x, y1, y2, t):
    for y in range(y1, y2+1):
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

def path_h(g, y, x1, x2, width=2):
    """Horizontal path of given width."""
    for w in range(width):
        hline(g, y+w, x1, x2, PATH)

def path_v(g, x, y1, y2, width=2):
    """Vertical path of given width."""
    for w in range(width):
        vline(g, x+w, y1, y2, PATH)

def border(g, w, h, t):
    """Draw border around entire grid."""
    hline(g, 0, 0, w-1, t)
    hline(g, h-1, 0, w-1, t)
    vline(g, 0, 0, h-1, t)
    vline(g, w-1, 0, h-1, t)

def scatter_overlay(o, x, y, w, h, tile, positions):
    """Place overlay tile at specific positions within a region."""
    for px, py in positions:
        ov(o, x+px, y+py, tile)


# ============================================================
# MAP 1: MEGA MALL (45 x 35) - Interior
# ============================================================
def make_mega_mall():
    W, H = 45, 35
    b = grid(W, H, ROCK)  # Interior walls
    o = overlay(W, H)

    # Carve out the main floor area
    rect(b, 1, 1, W-2, H-2, PATH)

    # Entrance at bottom center
    rect(b, 19, H-3, 7, 3, PATH)
    rect(b, 20, H-1, 5, 1, DOOR)

    # === GROUND FLOOR (bottom half, rows 18-33) ===
    # Lobby area is open path already

    # Walls to create lobby boundary
    hline(b, 17, 1, W-2, ROCK)
    rect(b, 10, 17, 3, 1, PATH)  # passage left
    rect(b, 32, 17, 3, 1, PATH)  # passage right
    rect(b, 20, 17, 5, 1, PATH)  # passage center

    # 4 Shop counters on ground floor
    # Counter 1 (left)
    rect(b, 3, 20, 5, 1, FENCE)
    ov(o, 5, 21, SHOP)
    ov(o, 3, 21, NPC)
    ov(o, 4, 22, SIGN)

    # Counter 2 (center-left)
    rect(b, 12, 20, 5, 1, FENCE)
    ov(o, 14, 21, SHOP)
    ov(o, 12, 21, NPC)

    # Counter 3 (center-right)
    rect(b, 27, 20, 5, 1, FENCE)
    ov(o, 29, 21, SHOP)
    ov(o, 27, 21, NPC)

    # Counter 4 (right)
    rect(b, 36, 20, 5, 1, FENCE)
    ov(o, 38, 21, SHOP)
    ov(o, 36, 21, NPC)
    ov(o, 37, 22, SIGN)

    # Ground floor decorations
    ov(o, 22, 25, STATUE)  # Center statue
    ov(o, 22, 28, NPC)     # Greeter NPC
    ov(o, 15, 30, NPC)
    ov(o, 30, 30, NPC)
    ov(o, 10, 25, SIGN)    # Directory sign
    ov(o, 34, 25, SIGN)    # Directory sign

    # Benches (signs) along lobby walls
    for x in [5, 8, 36, 39]:
        ov(o, x, 31, SIGN)

    # === FLOOR 2 via warps (left side, rows 9-16) ===
    # Wall separating floor 2
    hline(b, 8, 1, W-2, ROCK)
    rect(b, 10, 8, 3, 1, PATH)  # passage
    rect(b, 32, 8, 3, 1, PATH)  # passage

    # Warps: ground -> floor 2
    b[18][3] = WARP   # Left stairs
    b[18][41] = WARP  # Right stairs
    b[16][3] = WARP   # Arrive on floor 2 left
    b[16][41] = WARP  # Arrive on floor 2 right

    # Food court (floor 2 left, rows 9-16)
    # Flower decorations (planters)
    for x in range(3, 15, 3):
        for y in [10, 12]:
            b[y][x] = FLOWER

    # Food stalls
    rect(b, 3, 14, 4, 1, FENCE)
    ov(o, 4, 15, SHOP)
    ov(o, 5, 15, NPC)
    rect(b, 9, 14, 4, 1, FENCE)
    ov(o, 10, 15, SHOP)
    ov(o, 11, 15, NPC)

    # Tables (signs as seats)
    for x in [4, 7, 10]:
        ov(o, x, 11, SIGN)

    # Electronics wing (floor 2 right, rows 9-16)
    rect(b, 25, 10, 6, 1, FENCE)
    rect(b, 33, 10, 6, 1, FENCE)
    ov(o, 27, 11, SHOP)
    ov(o, 35, 11, SHOP)
    ov(o, 25, 12, NPC)
    ov(o, 33, 12, NPC)
    ov(o, 29, 13, ITEM)
    ov(o, 37, 14, NPC)

    # === FLOOR 3 / Back storage (rows 1-7) ===
    # Warps floor 2 -> floor 3
    b[9][3] = WARP
    b[9][41] = WARP
    b[7][3] = WARP
    b[7][41] = WARP

    # Back storage area (restricted feel)
    rect(b, 15, 2, 1, 5, ROCK)  # Divider wall
    rect(b, 29, 2, 1, 5, ROCK)  # Divider wall

    # Storage room left
    ov(o, 5, 3, HITEM)
    ov(o, 8, 5, ITEM)
    ov(o, 3, 4, NPC)  # Guard

    # Storage room center - VIP area
    ov(o, 22, 3, KEYITEM)  # Key item deep inside
    ov(o, 20, 4, NPC)
    ov(o, 24, 4, NPC)
    rect(b, 18, 2, 9, 1, FENCE)  # Rope barrier
    b[2][22] = PATH  # Gap to enter

    # Storage room right
    ov(o, 35, 3, HITEM)
    ov(o, 38, 5, ITEM)
    ov(o, 40, 2, NPC)

    # Additional NPCs scattered around
    ov(o, 20, 10, NPC)
    ov(o, 22, 15, NPC)
    ov(o, 8, 25, NPC)
    ov(o, 37, 25, NPC)

    save("mega_mall", "Mega Mall", W, H, b, o)


# ============================================================
# MAP 2: GREENLEAF HIGH SCHOOL (40 x 30) - Interior
# ============================================================
def make_high_school():
    W, H = 40, 30
    b = grid(W, H, ROCK)  # Interior rock walls
    o = overlay(W, H)

    # Main hall - central horizontal corridor
    path_h(b, 14, 1, W-2, 2)  # Main hall row 14-15

    # Entrance at bottom
    path_v(b, 18, 14, H-2, 3)
    rect(b, 18, H-1, 3, 1, DOOR)

    # Lobby area
    rect(b, 10, 22, 20, 6, PATH)
    ov(o, 19, 25, SIGN)   # Welcome sign
    ov(o, 15, 24, NPC)    # Receptionist
    ov(o, 25, 24, NPC)    # Student

    # === LEFT WING: Classrooms (rows 2-13, cols 1-18) ===
    # North-south corridor left
    path_v(b, 8, 1, 13, 2)

    # Classroom 1 (top-left)
    rect(b, 1, 2, 6, 5, PATH)
    rect(b, 2, 3, 4, 1, FENCE)  # Desk row
    rect(b, 2, 5, 4, 1, FENCE)  # Desk row
    ov(o, 3, 2, NPC)   # Teacher
    ov(o, 2, 4, NPC)   # Student
    ov(o, 5, 4, NPC)   # Student
    b[6][8] = PATH  # Door to corridor

    # Classroom 2 (mid-left)
    rect(b, 1, 8, 6, 5, PATH)
    rect(b, 2, 9, 4, 1, FENCE)
    rect(b, 2, 11, 4, 1, FENCE)
    ov(o, 3, 8, NPC)
    ov(o, 4, 10, NPC)
    b[12][8] = PATH

    # Classroom 3 (top-right of left wing)
    rect(b, 11, 2, 7, 5, PATH)
    rect(b, 12, 3, 5, 1, FENCE)
    rect(b, 12, 5, 5, 1, FENCE)
    ov(o, 14, 2, NPC)  # Teacher
    ov(o, 12, 4, NPC)
    ov(o, 15, 4, NPC)
    b[6][11] = PATH  # Connect to vertical corridor
    path_h(b, 6, 9, 11, 1)

    # Classroom 4 (mid-right of left wing)
    rect(b, 11, 8, 7, 5, PATH)
    rect(b, 12, 9, 5, 1, FENCE)
    rect(b, 12, 11, 5, 1, FENCE)
    ov(o, 14, 8, NPC)
    ov(o, 13, 10, NPC)
    b[12][11] = PATH
    path_h(b, 12, 9, 11, 1)

    # === RIGHT WING: Gym + Library (rows 2-13, cols 21-38) ===
    # North-south corridor right
    path_v(b, 29, 1, 13, 2)

    # Gymnasium (top-right, big open area)
    rect(b, 21, 1, 8, 7, PATH)
    # Gym floor markings (flowers as court lines)
    hline(b, 2, 22, 27, FLOWER)
    hline(b, 6, 22, 27, FLOWER)
    vline(b, 22, 2, 6, FLOWER)
    vline(b, 27, 2, 6, FLOWER)
    ov(o, 24, 4, NPC)  # Coach
    b[4][29] = PATH  # Door

    # Library (bottom-right)
    rect(b, 21, 9, 8, 5, PATH)
    # Bookshelves (signs)
    for y in [10, 12]:
        for x in [22, 24, 26]:
            ov(o, x, y, SIGN)
    ov(o, 23, 11, NPC)  # Librarian
    b[12][29] = PATH

    # Far right rooms
    rect(b, 32, 2, 6, 5, PATH)
    ov(o, 34, 3, NPC)  # Science room NPC
    ov(o, 35, 4, ITEM)  # Lab supplies
    rect(b, 32, 3, 1, 1, FENCE)
    rect(b, 37, 3, 1, 1, FENCE)
    b[5][31] = PATH
    path_h(b, 5, 30, 31, 1)

    # Principal's office (far right bottom)
    rect(b, 32, 9, 6, 5, PATH)
    rect(b, 33, 10, 4, 1, FENCE)  # Desk
    ov(o, 35, 11, NPC)     # Principal
    ov(o, 34, 12, KEYITEM)  # Key item!
    b[12][31] = PATH
    path_h(b, 12, 30, 31, 1)

    # Corridor signs
    ov(o, 10, 14, SIGN)
    ov(o, 30, 14, SIGN)

    # Bathroom area (small room bottom-left)
    rect(b, 2, 18, 5, 3, PATH)
    ov(o, 3, 19, SIGN)
    path_h(b, 19, 7, 9, 1)
    path_v(b, 9, 15, 19, 1)

    # Cafeteria (bottom-right before lobby)
    rect(b, 28, 18, 10, 4, PATH)
    for x in [30, 33, 36]:
        ov(o, x, 19, SIGN)  # Tables
    ov(o, 29, 20, NPC)
    ov(o, 35, 20, NPC)
    path_h(b, 20, 20, 27, 1)
    path_v(b, 27, 15, 20, 1)

    save("greenleaf_high_school", "Greenleaf High School", W, H, b, o)


# ============================================================
# MAP 3: MILITARY COMPOUND (70 x 55) - Outdoor
# ============================================================
def make_military_compound():
    W, H = 70, 55
    b = grid(W, H, GRASS)
    o = overlay(W, H)

    # Heavy fence border
    border(b, W, H, FENCE)
    # Second fence layer for thickness
    rect(b, 1, 1, W-2, 1, FENCE)
    rect(b, 1, H-2, W-2, 1, FENCE)
    vline(b, 1, 1, H-2, FENCE)
    vline(b, W-2, 1, H-2, FENCE)

    # Gate entries
    # North gate
    bldg(b, 32, 0, GATE)
    path_v(b, 33, 3, 8, 2)

    # South gate
    bldg(b, 32, H-3, GATE)
    path_v(b, 33, 46, H-4, 2)

    # West gate (for villain infiltration)
    b[25][0] = WALL; b[25][1] = WARP; b[26][0] = WALL; b[26][1] = WARP
    b[25][0] = FENCE; b[26][0] = FENCE

    # === MAIN ROADS ===
    # Central N-S boulevard
    path_v(b, 33, 3, 51, 4)

    # E-W boulevard (upper)
    path_h(b, 15, 5, 64, 3)

    # E-W boulevard (lower)
    path_h(b, 38, 5, 64, 3)

    # Secondary roads
    path_v(b, 15, 3, 51, 2)  # Left avenue
    path_v(b, 55, 3, 51, 2)  # Right avenue

    # === NW QUADRANT: Barracks (rows 3-14, cols 3-30) ===
    # Row of 6 small houses (barracks)
    for i in range(6):
        bldg(b, 4 + i*5, 4, HOUSE_SM)
    for i in range(6):
        bldg(b, 4 + i*5, 9, HOUSE_SM)

    # Soldiers patrolling
    ov(o, 6, 7, NPC)
    ov(o, 16, 7, NPC)
    ov(o, 26, 7, NPC)
    ov(o, 11, 12, NPC)
    ov(o, 21, 12, NPC)
    ov(o, 8, 14, SIGN)  # Barracks sign

    # === NE QUADRANT: Command Center + training (rows 3-14, cols 38-66) ===
    # Command center (lab building)
    bldg(b, 45, 4, LAB)
    ov(o, 47, 9, NPC)  # Commander
    ov(o, 50, 7, SIGN)  # Command sign

    # Comms building
    bldg(b, 58, 4, HOUSE_LG)
    ov(o, 60, 9, NPC)

    # Flagpole
    ov(o, 52, 5, STATUE)

    # === CENTRAL: Training Grounds (rows 18-35) ===
    # Training lanes (fenced battle lanes with trainers)
    for lane_y in [20, 25, 30]:
        rect(b, 5, lane_y, 25, 1, FENCE)
        rect(b, 5, lane_y+3, 25, 1, FENCE)
        path_h(b, lane_y+1, 6, 28, 2)
        # Trainers in each lane
        ov(o, 10, lane_y+1, TDOWN)
        ov(o, 18, lane_y+2, TUP)
        ov(o, 24, lane_y+1, TDOWN)

    ov(o, 5, 19, SIGN)  # "Training Grounds"

    # === SE QUADRANT: Armory + Storage (rows 40-52, cols 38-66) ===
    # Armory (house_large with items)
    bldg(b, 42, 42, HOUSE_LG)
    ov(o, 44, 46, ITEM)
    ov(o, 46, 46, ITEM)

    bldg(b, 50, 42, HOUSE_LG)
    ov(o, 52, 46, ITEM)

    # Weapon storage
    bldg(b, 58, 42, HOUSE_LG)
    ov(o, 60, 46, HITEM)

    ov(o, 48, 40, SIGN)  # Armory sign
    ov(o, 55, 48, NPC)   # Guard

    # === SW QUADRANT: Obstacle Course (rows 40-52, cols 3-30) ===
    # Winding path through rocks and cut trees
    path_h(b, 42, 4, 12, 2)
    # Rock obstacles
    rect(b, 8, 44, 3, 2, ROCK)
    rect(b, 14, 42, 2, 3, ROCK)
    path_v(b, 12, 42, 48, 2)
    path_h(b, 48, 12, 22, 2)

    # Cut tree gates
    b[45][18] = CUTTREE
    b[46][18] = CUTTREE
    b[48][25] = CUTTREE

    # Rocks to navigate around
    rect(b, 20, 44, 4, 2, ROCK)
    rect(b, 26, 42, 3, 3, ROCK)
    path_v(b, 24, 42, 50, 2)
    path_h(b, 50, 4, 28, 2)

    ov(o, 6, 41, SIGN)  # "Obstacle Course"
    ov(o, 15, 50, ITEM)
    ov(o, 27, 50, HITEM)

    # === VILLAIN INFILTRATION PATH (hidden, west side) ===
    # Secret path from west gate
    path_h(b, 25, 2, 14, 2)
    rect(b, 4, 25, 2, 2, STRROCK)  # Strength rocks block path
    # Behind the rocks
    ov(o, 8, 25, HITEM)
    # Minions guarding
    ov(o, 10, 26, MDOWN)
    ov(o, 12, 25, MDOWN)

    # Deep inside - villain boss in hidden bunker
    rect(b, 3, 34, 8, 4, PATH)
    rect(b, 3, 34, 8, 1, ROCK)
    rect(b, 3, 37, 8, 1, ROCK)
    vline(b, 3, 34, 37, ROCK)
    vline(b, 10, 34, 37, ROCK)
    b[35][7] = PATH; b[36][7] = PATH  # entrance
    path_v(b, 7, 27, 34, 2)
    ov(o, 5, 35, VBOSS)
    ov(o, 7, 35, KEYITEM)

    save("military_compound", "Military Compound", W, H, b, o)


# ============================================================
# MAP 4: NEON DOWNTOWN (75 x 60) - Outdoor
# ============================================================
def make_neon_downtown():
    W, H = 75, 60
    b = grid(W, H, GRASS)
    o = overlay(W, H)

    # Tree border
    border(b, W, H, TREE)

    # === GRID SYSTEM: 4-wide boulevards ===
    # Main E-W boulevards
    path_h(b, 10, 1, W-2, 4)   # North blvd
    path_h(b, 28, 1, W-2, 4)   # Central blvd
    path_h(b, 48, 1, W-2, 4)   # South blvd

    # Main N-S boulevards
    path_v(b, 10, 1, H-2, 4)   # West blvd
    path_v(b, 35, 1, H-2, 4)   # Center blvd
    path_v(b, 60, 1, H-2, 4)   # East blvd

    # Secondary streets (2-wide)
    path_h(b, 20, 1, W-2, 2)
    path_h(b, 40, 1, W-2, 2)
    path_v(b, 22, 1, H-2, 2)
    path_v(b, 48, 1, H-2, 2)

    # === EXITS (4+) ===
    # North exit
    rect(b, 36, 0, 4, 1, PATH)

    # South exit
    rect(b, 36, H-1, 4, 1, PATH)

    # West exit
    rect(b, 0, 29, 1, 4, PATH)

    # East exit
    rect(b, W-1, 29, 1, 4, PATH)

    # SE exit
    rect(b, W-1, 49, 1, 4, PATH)

    # === NW DISTRICT: Residential ===
    bldg(b, 3, 3, HOUSE_LG)
    bldg(b, 3, 14, HOUSE_LG)
    bldg(b, 15, 3, HOUSE_SM)
    bldg(b, 15, 14, HOUSE_LG)
    bldg(b, 3, 22, HOUSE_SM)
    bldg(b, 7, 22, HOUSE_SM)

    # Residential NPCs
    ov(o, 5, 8, NPC)
    ov(o, 17, 8, NPC)
    ov(o, 5, 18, NPC)
    ov(o, 17, 18, NPC)
    ov(o, 4, 25, NPC)
    ov(o, 9, 25, NPC)

    # Fenced yards
    rect(b, 2, 7, 6, 1, FENCE)
    rect(b, 14, 7, 5, 1, FENCE)

    # === NE DISTRICT: Commercial ===
    bldg(b, 42, 3, POKECENTER)
    bldg(b, 50, 3, POKEMART)
    bldg(b, 42, 14, POKEMART)
    bldg(b, 50, 14, POKEMART)

    # Shop signs
    ov(o, 44, 8, SIGN)
    ov(o, 52, 8, SIGN)
    ov(o, 44, 19, SIGN)

    # Shopping NPCs
    ov(o, 46, 9, NPC)
    ov(o, 54, 9, NPC)
    ov(o, 46, 19, NPC)
    ov(o, 53, 19, NPC)
    ov(o, 48, 12, NPC)

    # === CENTER: Civic Plaza with Gym ===
    # Central plaza (big open area)
    rect(b, 26, 32, 22, 14, PATH)

    # Gym in center of plaza
    bldg(b, 33, 33, GYM)

    # Statue + fountain
    ov(o, 30, 38, STATUE)
    ov(o, 31, 38, STATUE)
    bldg(b, 40, 38, POND)

    # Plaza decorations
    for x in [27, 29, 44, 46]:
        b[34][x] = FLOWER
        b[42][x] = FLOWER
    for x in [28, 32, 41, 45]:
        b[44][x] = FLOWER

    ov(o, 28, 36, NPC)
    ov(o, 45, 36, NPC)
    ov(o, 37, 44, NPC)
    ov(o, 30, 44, SIGN)  # Plaza sign
    ov(o, 44, 44, SIGN)

    # === SW DISTRICT: Entertainment ===
    bldg(b, 3, 34, HOUSE_LG)
    bldg(b, 3, 42, HOUSE_LG)
    bldg(b, 15, 34, HOUSE_SM)
    bldg(b, 15, 42, HOUSE_SM)

    # Back alley with hidden items (between buildings)
    path_v(b, 8, 34, 46, 1)
    ov(o, 8, 38, HITEM)
    ov(o, 8, 44, HITEM)
    ov(o, 8, 36, NPC)  # Shady NPC

    # === SE DISTRICT: Waterfront ===
    # Small park area
    rect(b, 52, 34, 12, 8, GRASS)
    for y in range(35, 39):
        for x in range(53, 63, 2):
            b[y][x] = FLOWER
    bldg(b, 55, 42, POND)
    ov(o, 58, 40, NPC)

    # === BACK ALLEYS (1-wide secret paths) ===
    # Alley behind NE district
    path_v(b, 58, 3, 12, 1)
    ov(o, 58, 7, HITEM)
    ov(o, 58, 5, ITEM)

    # Alley in south
    path_h(b, 54, 3, 20, 1)
    ov(o, 10, 54, HITEM)
    ov(o, 18, 54, ITEM)

    # === ROOFTOP ACCESS (warp tiles) ===
    b[5][66] = WARP   # Rooftop warp NE
    b[44][66] = WARP  # Rooftop warp SE
    ov(o, 66, 3, SIGN)  # "Roof Access"

    # === UNDERGROUND PASSAGE (cave tiles, tunnel N-S) ===
    # Entrance north
    b[13][25] = WARP
    ov(o, 25, 12, SIGN)  # "Underground"
    # Tunnel (represented as cave tiles in a column)
    for y in range(15, 27):
        b[y][25] = CAVE
        b[y][26] = CAVE
    # Exit south
    b[47][25] = WARP
    ov(o, 25, 46, SIGN)

    # === TRAINERS scattered around ===
    ov(o, 12, 21, TDOWN)
    ov(o, 37, 20, TDOWN)
    ov(o, 50, 21, TLEFT)
    ov(o, 12, 41, TUP)
    ov(o, 50, 41, TUP)
    ov(o, 62, 20, TDOWN)
    ov(o, 62, 41, TUP)

    # More signs for city feel
    ov(o, 11, 1, SIGN)
    ov(o, 36, 1, SIGN)
    ov(o, 61, 1, SIGN)

    save("neon_downtown", "Neon Downtown", W, H, b, o)


# ============================================================
# MAP 5: TRANSIT HUB (55 x 40) - Mixed
# ============================================================
def make_transit_hub():
    W, H = 55, 40
    b = grid(W, H, ROCK)  # Interior
    o = overlay(W, H)

    # Carve main concourse (center open area)
    rect(b, 1, 1, W-2, H-2, PATH)

    # Walls creating structure
    # Top wall with gaps for platforms
    hline(b, 8, 1, W-2, ROCK)
    rect(b, 6, 8, 3, 1, PATH)   # Gap to platform 1
    rect(b, 20, 8, 3, 1, PATH)  # Gap to platform 2
    rect(b, 33, 8, 3, 1, PATH)  # Gap to platform 3
    rect(b, 46, 8, 3, 1, PATH)  # Gap to platform 4

    # Bottom wall
    hline(b, 30, 1, W-2, ROCK)
    rect(b, 25, 30, 5, 1, PATH)  # Main entrance gap

    # Entrance at bottom
    rect(b, 24, H-2, 7, 2, PATH)
    rect(b, 25, H-1, 5, 1, DOOR)

    # === MAIN CONCOURSE (rows 9-29) ===
    # Ticket counters
    rect(b, 4, 12, 8, 1, FENCE)
    ov(o, 6, 13, NPC)   # Ticket agent
    ov(o, 9, 13, NPC)   # Ticket agent
    ov(o, 5, 14, SIGN)  # "Tickets"

    rect(b, 42, 12, 8, 1, FENCE)
    ov(o, 44, 13, NPC)
    ov(o, 47, 13, NPC)
    ov(o, 43, 14, SIGN)

    # Shops in concourse
    bldg(b, 16, 16, POKEMART)
    bldg(b, 34, 16, POKEMART)

    # Pokecenter
    bldg(b, 24, 22, POKECENTER)

    # Information desk (center)
    ov(o, 27, 14, STATUE)  # Info kiosk
    ov(o, 26, 15, NPC)
    ov(o, 28, 15, NPC)

    # Benches
    for x in [8, 14, 38, 44]:
        ov(o, x, 20, SIGN)
        ov(o, x, 24, SIGN)

    # Waiting area NPCs
    ov(o, 10, 22, NPC)
    ov(o, 42, 22, NPC)
    ov(o, 20, 28, NPC)
    ov(o, 34, 28, NPC)

    # === PLATFORMS (rows 1-7) ===
    # Platform 1 (far left)
    rect(b, 2, 1, 9, 7, PATH)
    rect(b, 2, 1, 9, 1, FENCE)  # Platform edge
    b[2][7] = WARP; b[2][8] = WARP  # Destination warp
    ov(o, 4, 3, TDOWN)  # Trainer waiting
    ov(o, 6, 5, NPC)
    ov(o, 3, 6, SIGN)   # "Platform 1 - Route 5"

    # Platform 2
    rect(b, 15, 1, 9, 7, PATH)
    rect(b, 15, 1, 9, 1, FENCE)
    b[2][20] = WARP; b[2][21] = WARP
    ov(o, 17, 4, TDOWN)
    ov(o, 19, 3, NPC)
    ov(o, 16, 6, SIGN)  # "Platform 2 - Harbor"

    # Platform 3
    rect(b, 28, 1, 9, 7, PATH)
    rect(b, 28, 1, 9, 1, FENCE)
    b[2][33] = WARP; b[2][34] = WARP
    ov(o, 30, 5, NPC)
    ov(o, 33, 4, TDOWN)
    ov(o, 29, 6, SIGN)  # "Platform 3 - Tech Campus"

    # Platform 4 (far right)
    rect(b, 41, 1, 12, 7, PATH)
    rect(b, 41, 1, 12, 1, FENCE)
    b[2][46] = WARP; b[2][47] = WARP
    ov(o, 44, 3, TDOWN)
    ov(o, 48, 5, NPC)
    ov(o, 42, 6, SIGN)  # "Platform 4 - Military Zone"

    # === OUTSIDE BUS STOPS (bottom area, rows 31-38) ===
    rect(b, 1, 31, W-2, 8, GRASS)
    path_h(b, 34, 3, 50, 3)  # Sidewalk

    # Bus stop signs
    for x in [8, 20, 35, 48]:
        ov(o, x, 33, SIGN)
        ov(o, x, 35, NPC)

    # Items in corners
    ov(o, 2, 36, HITEM)
    ov(o, 51, 36, HITEM)
    ov(o, 27, 10, ITEM)

    save("transit_hub", "Transit Hub", W, H, b, o)


# ============================================================
# MAP 6: HARBOR DISTRICT (65 x 50) - Outdoor
# ============================================================
def make_harbor_district():
    W, H = 65, 50
    b = grid(W, H, GRASS)
    o = overlay(W, H)

    # Tree border
    border(b, W, H, TREE)

    # === WATER (south and west) ===
    # Large water body (south)
    rect(b, 1, 36, 63, 13, WATER)
    # Water edge framing
    hline(b, 35, 1, 63, WEDGE)
    # Surf water areas
    rect(b, 10, 38, 15, 6, SURF)
    rect(b, 30, 38, 10, 6, SURF)

    # West waterfront (docks)
    rect(b, 1, 10, 12, 25, WATER)
    vline(b, 12, 10, 34, WEDGE)
    hline(b, 10, 1, 11, WEDGE)

    # === DOCK PIERS (bridges over water) ===
    # Pier 1
    path_h(b, 15, 5, 12, 2)
    rect(b, 5, 15, 1, 4, BRIDGE)
    rect(b, 6, 15, 1, 4, BRIDGE)
    rect(b, 5, 19, 6, 1, BRIDGE)
    ov(o, 7, 16, NPC)   # Docker
    ov(o, 9, 18, SIGN)  # Pier 1

    # Pier 2
    path_h(b, 22, 5, 12, 2)
    rect(b, 5, 22, 1, 4, BRIDGE)
    rect(b, 6, 22, 1, 4, BRIDGE)
    rect(b, 5, 26, 6, 1, BRIDGE)
    ov(o, 7, 24, NPC)
    ov(o, 9, 25, SIGN)

    # Pier 3
    path_h(b, 29, 5, 12, 2)
    rect(b, 5, 29, 1, 4, BRIDGE)
    rect(b, 6, 29, 1, 4, BRIDGE)
    rect(b, 5, 33, 6, 1, BRIDGE)
    ov(o, 7, 31, NPC)
    ov(o, 9, 32, ITEM)  # Fishing reward

    # === MAIN ROADS ===
    # Waterfront road (N-S along docks)
    path_v(b, 14, 3, 34, 3)

    # Central E-W boulevard
    path_h(b, 18, 14, 62, 4)

    # East N-S road
    path_v(b, 45, 3, 34, 3)

    # North E-W road
    path_h(b, 8, 14, 62, 2)

    # South approach road
    path_v(b, 30, 22, 34, 2)

    # Cross streets
    path_v(b, 25, 3, 34, 2)
    path_h(b, 28, 25, 44, 2)

    # === EXITS ===
    # North exit
    rect(b, 30, 0, 4, 1, PATH)
    path_v(b, 30, 0, 8, 4)

    # East exit
    rect(b, W-1, 19, 1, 4, PATH)

    # West exit (from dock road)
    rect(b, 0, 5, 1, 3, PATH)
    path_h(b, 5, 0, 14, 3)

    # Surf-only south exit (hidden island path)
    rect(b, 55, H-1, 4, 1, SURF)

    # === WAREHOUSES (west side) ===
    bldg(b, 18, 3, HOUSE_LG)
    bldg(b, 18, 10, HOUSE_LG)
    bldg(b, 18, 22, HOUSE_LG)
    ov(o, 20, 8, NPC)
    ov(o, 20, 14, ITEM)
    ov(o, 20, 26, HITEM)
    ov(o, 22, 5, SIGN)  # Warehouse sign

    # === EAST SIDE: Residential + Commercial ===
    # Commercial
    bldg(b, 35, 3, POKECENTER)
    bldg(b, 50, 3, POKEMART)
    ov(o, 37, 8, NPC)
    ov(o, 52, 8, SIGN)

    # Residential
    bldg(b, 35, 10, HOUSE_LG)
    bldg(b, 42, 10, HOUSE_SM)
    bldg(b, 50, 10, HOUSE_LG)
    bldg(b, 56, 10, HOUSE_SM)
    bldg(b, 35, 23, HOUSE_SM)
    bldg(b, 42, 23, HOUSE_LG)
    bldg(b, 50, 23, HOUSE_LG)

    # Residential NPCs
    ov(o, 37, 15, NPC)
    ov(o, 44, 15, NPC)
    ov(o, 52, 15, NPC)
    ov(o, 58, 15, NPC)
    ov(o, 37, 27, NPC)
    ov(o, 48, 27, NPC)
    ov(o, 54, 27, NPC)

    # === HIDDEN ISLAND (in south water) ===
    rect(b, 50, 40, 8, 6, GRASS)
    rect(b, 50, 40, 8, 1, WEDGE)
    rect(b, 50, 45, 8, 1, WEDGE)
    vline(b, 50, 40, 45, WEDGE)
    vline(b, 57, 40, 45, WEDGE)
    rect(b, 51, 41, 6, 4, GRASS)
    ov(o, 53, 42, KEYITEM)
    ov(o, 55, 43, ITEM)
    b[42][53] = RGRASS
    b[43][54] = RGRASS
    b[42][54] = RGRASS
    b[43][53] = RGRASS

    # Fishing spots (NPCs on dock edges)
    ov(o, 15, 33, NPC)
    ov(o, 15, 27, NPC)

    # Trainers
    ov(o, 28, 10, TDOWN)
    ov(o, 40, 20, TLEFT)
    ov(o, 55, 20, TDOWN)
    ov(o, 32, 30, TUP)

    # Signs for navigation
    ov(o, 14, 4, SIGN)   # "Harbor District"
    ov(o, 30, 9, SIGN)   # "Downtown ->"
    ov(o, 45, 4, SIGN)   # "Pokecenter"

    save("harbor_district", "Harbor District", W, H, b, o)


# ============================================================
# MAP 7: TECH CAMPUS (60 x 45) - Outdoor
# ============================================================
def make_tech_campus():
    W, H = 60, 45
    b = grid(W, H, GRASS)
    o = overlay(W, H)

    # Tree + fence border (gated campus)
    border(b, W, H, TREE)
    # Inner fence
    for x in range(2, W-2):
        b[1][x] = FENCE
        b[H-2][x] = FENCE
    for y in range(2, H-2):
        b[y][1] = FENCE
        b[y][W-2] = FENCE

    # === MAIN ENTRY (south gate) ===
    bldg(b, 27, H-4, GATE)
    rect(b, 28, H-2, 2, 2, PATH)  # Path through fence
    b[H-2][28] = PATH; b[H-2][29] = PATH
    b[H-1][28] = PATH; b[H-1][29] = PATH  # Exit to outside

    # Secondary gate (east)
    bldg(b, W-4, 20, GATE)
    b[21][W-2] = PATH; b[21][W-1] = PATH
    b[22][W-2] = PATH; b[22][W-1] = PATH

    # === ROAD NETWORK ===
    # Main entry boulevard (S to center)
    path_v(b, 28, 15, H-5, 4)

    # Central E-W boulevard
    path_h(b, 15, 4, 55, 3)

    # N-S campus road west
    path_v(b, 15, 4, 40, 2)

    # N-S campus road east
    path_v(b, 45, 4, 40, 2)

    # Upper cross road
    path_h(b, 8, 4, 55, 2)

    # Lower cross road
    path_h(b, 32, 4, 55, 2)

    # Diagonal stepping path (NW to center) -- approximate with L-shapes
    for i in range(5):
        x = 5 + i*4
        y = 12 - i
        b[y][x] = PATH
        b[y][x+1] = PATH
        b[y+1][x+1] = PATH
        b[y+1][x+2] = PATH

    # === BUILDINGS ===
    # Lab 1 (NW - AI Research)
    bldg(b, 5, 3, LAB)
    ov(o, 7, 8, NPC)   # Researcher
    ov(o, 9, 5, SIGN)  # "AI Research Lab"

    # Lab 2 (NE - Biotech)
    bldg(b, 48, 3, LAB)
    ov(o, 50, 8, NPC)
    ov(o, 52, 5, SIGN)

    # Lab 3 (center-north - Main Research)
    bldg(b, 25, 3, LAB)
    ov(o, 27, 8, NPC)
    ov(o, 29, 5, SIGN)  # "Main Research"
    ov(o, 30, 8, NPC)

    # Server room (interior building with PC tiles)
    rect(b, 48, 22, 8, 6, ROCK)
    rect(b, 49, 23, 6, 4, PATH)
    b[26][49] = DOOR; b[26][50] = DOOR
    ov(o, 51, 23, PC)
    ov(o, 53, 23, PC)
    ov(o, 52, 24, NPC)  # Sysadmin
    ov(o, 54, 25, ITEM)
    ov(o, 50, 25, HITEM)

    # Cafeteria building
    rect(b, 5, 22, 8, 6, ROCK)
    rect(b, 6, 23, 6, 4, PATH)
    b[26][7] = DOOR; b[26][8] = DOOR
    # Tables inside
    ov(o, 7, 24, SIGN)   # Table
    ov(o, 9, 24, SIGN)   # Table
    ov(o, 8, 25, NPC)
    ov(o, 10, 25, NPC)

    # === MANICURED GROUNDS ===
    # Flower gardens along main boulevard
    for y in range(18, 30, 3):
        for x in [24, 25, 34, 35]:
            if b[y][x] == GRASS:
                b[y][x] = FLOWER

    # Central courtyard with fountain
    bldg(b, 30, 20, POND)
    ov(o, 33, 20, STATUE)  # Founder statue
    ov(o, 28, 22, NPC)     # Tour guide

    # Flower beds near labs
    for x in range(6, 10):
        b[10][x] = FLOWER
    for x in range(49, 53):
        b[10][x] = FLOWER

    # === TESTING GROUNDS (tall grass zone for experimental pokemon) ===
    rect(b, 5, 34, 15, 8, TGRASS)
    rect(b, 8, 36, 6, 4, RGRASS)  # Rare encounters
    # Fenced area
    hline(b, 33, 4, 20, FENCE)
    hline(b, 42, 4, 20, FENCE)
    vline(b, 4, 33, 42, FENCE)
    vline(b, 20, 33, 42, FENCE)
    b[37][20] = PATH; b[38][20] = PATH  # Entrance gap
    ov(o, 18, 36, SIGN)  # "Authorized Personnel Only"
    ov(o, 10, 35, NPC)   # Researcher in field

    # === ADDITIONAL NPCS (researchers everywhere) ===
    ov(o, 20, 10, NPC)
    ov(o, 35, 10, NPC)
    ov(o, 40, 18, NPC)
    ov(o, 20, 25, NPC)
    ov(o, 40, 30, NPC)
    ov(o, 25, 35, NPC)
    ov(o, 45, 38, NPC)

    # Items scattered
    ov(o, 3, 30, HITEM)
    ov(o, 55, 12, ITEM)
    ov(o, 40, 40, ITEM)
    ov(o, 10, 12, ITEM)

    # Trainers (rival researchers)
    ov(o, 12, 16, TDOWN)
    ov(o, 42, 16, TDOWN)
    ov(o, 30, 36, TRIGHT)

    save("tech_campus", "Tech Campus", W, H, b, o)


# ============================================================
# MAP 8: SPORTS COMPLEX (50 x 40) - Mixed
# ============================================================
def make_sports_complex():
    W, H = 50, 40
    b = grid(W, H, GRASS)
    o = overlay(W, H)

    # Rock/wall border (indoor-outdoor complex)
    border(b, W, H, ROCK)

    # === MAIN CONCOURSE (ring road around stadium) ===
    # Outer ring path
    path_h(b, 5, 3, 46, 3)    # Top
    path_h(b, 34, 3, 46, 3)   # Bottom
    path_v(b, 3, 5, 34, 3)    # Left
    path_v(b, 44, 5, 34, 3)   # Right

    # === ENTRANCES ===
    # South entrance
    rect(b, 23, H-1, 4, 1, PATH)
    path_v(b, 23, 34, H-2, 4)

    # North entrance
    rect(b, 23, 0, 4, 1, PATH)
    path_v(b, 23, 1, 5, 4)

    # East entrance
    rect(b, W-1, 18, 1, 4, PATH)
    path_h(b, 18, 44, W-2, 4)

    # === CENTRAL STADIUM (fenced oval with gym leader) ===
    # Stadium floor
    rect(b, 14, 12, 22, 16, PATH)

    # Fence oval (approximated as rectangle with corners)
    hline(b, 11, 14, 35, FENCE)
    hline(b, 28, 14, 35, FENCE)
    vline(b, 13, 12, 27, FENCE)
    vline(b, 36, 12, 27, FENCE)

    # Corner cutoffs for oval feel
    b[11][14] = ROCK; b[11][35] = ROCK
    b[28][14] = ROCK; b[28][35] = ROCK

    # Stadium entrance (south gap in fence)
    b[28][24] = PATH; b[28][25] = PATH; b[28][26] = PATH

    # Battle arena markings inside
    rect(b, 18, 15, 14, 10, PATH)
    hline(b, 14, 18, 31, FLOWER)  # Court line top
    hline(b, 25, 18, 31, FLOWER)  # Court line bottom
    vline(b, 18, 14, 25, FLOWER)  # Court line left
    vline(b, 31, 14, 25, FLOWER)  # Court line right

    # Center circle
    b[19][24] = FLOWER; b[19][25] = FLOWER
    b[20][23] = FLOWER; b[20][26] = FLOWER
    b[21][24] = FLOWER; b[21][25] = FLOWER

    # Gym Leader in center
    ov(o, 24, 18, GYMLEADER)
    ov(o, 25, 20, SIGN)  # "Champion's Arena"

    # Audience NPCs around the ring
    ov(o, 15, 13, NPC)
    ov(o, 20, 13, NPC)
    ov(o, 28, 13, NPC)
    ov(o, 34, 13, NPC)
    ov(o, 15, 27, NPC)
    ov(o, 34, 27, NPC)

    # === POKECENTER + MART on concourse ===
    bldg(b, 4, 8, POKECENTER)
    bldg(b, 40, 8, POKEMART)
    ov(o, 6, 5, SIGN)   # "Pokecenter"
    ov(o, 42, 5, SIGN)  # "Pokemart"

    # === TRAINING ROOMS (4 fenced areas, each with trainer) ===
    # Training room 1 (NW)
    rect(b, 4, 14, 7, 5, PATH)
    rect(b, 4, 14, 7, 1, FENCE)
    rect(b, 4, 18, 7, 1, FENCE)
    vline(b, 4, 14, 18, FENCE)
    vline(b, 10, 14, 18, FENCE)
    b[18][7] = PATH  # Door
    ov(o, 7, 16, TDOWN)

    # Training room 2 (NE)
    rect(b, 39, 14, 7, 5, PATH)
    rect(b, 39, 14, 7, 1, FENCE)
    rect(b, 39, 18, 7, 1, FENCE)
    vline(b, 39, 14, 18, FENCE)
    vline(b, 45, 14, 18, FENCE)
    b[18][42] = PATH
    ov(o, 42, 16, TDOWN)

    # Training room 3 (SW)
    rect(b, 4, 24, 7, 5, PATH)
    rect(b, 4, 24, 7, 1, FENCE)
    rect(b, 4, 28, 7, 1, FENCE)
    vline(b, 4, 24, 28, FENCE)
    vline(b, 10, 24, 28, FENCE)
    b[24][7] = PATH
    ov(o, 7, 26, TUP)

    # Training room 4 (SE)
    rect(b, 39, 24, 7, 5, PATH)
    rect(b, 39, 24, 7, 1, FENCE)
    rect(b, 39, 28, 7, 1, FENCE)
    vline(b, 39, 24, 28, FENCE)
    vline(b, 45, 24, 28, FENCE)
    b[24][42] = PATH
    ov(o, 42, 26, TUP)

    # === LOCKER ROOM (bottom-left, interior with items) ===
    rect(b, 3, 30, 10, 5, ROCK)
    rect(b, 4, 31, 8, 3, PATH)
    b[33][6] = DOOR; b[33][7] = DOOR
    ov(o, 5, 31, ITEM)
    ov(o, 8, 31, ITEM)
    ov(o, 10, 32, HITEM)
    ov(o, 6, 32, NPC)  # Athlete

    # === VIP SECTION (bottom-right, behind cut tree) ===
    rect(b, 37, 30, 10, 5, PATH)
    rect(b, 37, 30, 10, 1, FENCE)
    b[30][40] = CUTTREE  # Cut tree gate!
    b[30][41] = CUTTREE

    # Trophy room with statues
    ov(o, 39, 32, STATUE)
    ov(o, 42, 32, STATUE)
    ov(o, 45, 32, STATUE)
    ov(o, 41, 33, KEYITEM)  # VIP reward
    ov(o, 44, 33, ITEM)
    ov(o, 40, 34, SIGN)  # "Hall of Champions"

    # === EXTRA DECORATIONS ===
    # Flower planters along concourse
    for x in [10, 15, 35, 40]:
        b[6][x] = FLOWER
        b[33][x] = FLOWER

    # Concourse NPCs
    ov(o, 12, 6, NPC)
    ov(o, 38, 6, NPC)
    ov(o, 4, 22, NPC)
    ov(o, 46, 22, NPC)
    ov(o, 20, 35, NPC)
    ov(o, 30, 35, NPC)

    # Signs
    ov(o, 23, 35, SIGN)  # "Welcome to Sports Complex"
    ov(o, 10, 30, SIGN)  # "Locker Room"
    ov(o, 37, 29, SIGN)  # "VIP Access"

    save("sports_complex", "Sports Complex", W, H, b, o)


# ============================================================
# GENERATE ALL 8 MAPS
# ============================================================
if __name__ == "__main__":
    print("Generating 8 urban/modern maps...")
    print("=" * 50)
    make_mega_mall()
    make_high_school()
    make_military_compound()
    make_neon_downtown()
    make_transit_hub()
    make_harbor_district()
    make_tech_campus()
    make_sports_complex()
    print("=" * 50)
    print("Done! All 8 maps generated.")

# Map Path Templates — Modern Scale

Design reference for PokemonGreen maps. Every map is big, dense, and full of life.
Uses `pokemon-green-default` registry (51 tiles, 11 buildings).

No more shack towns. The player lives in a city. The world is expansive.

---

## 1. Design Philosophy

### Scale Targets

| Map Type            | Grid Size  | Cells  | Tile Size | Viewport at 1x         |
|---------------------|------------|--------|-----------|------------------------|
| Home City           | 64 x 48   | 3,072  | 32px      | 2048 x 1536 (scrolls)  |
| Coastal Route       | 25 x 56   | 1,400  | 32px      | 800 x 1792 (scrolls V) |
| Wild Zone           | 56 x 44   | 2,464  | 32px      | 1792 x 1408            |
| Safari Zone         | 50 x 40   | 2,000  | 32px      | 1600 x 1280            |
| Cave Complex        | 42 x 34   | 1,428  | 32px      | 1344 x 1088            |
| Lake District       | 48 x 38   | 1,824  | 32px      | 1536 x 1216            |
| Battle Arena        | 36 x 28   | 1,008  | 32px      | 1152 x 896             |
| Building Interior   | 24 x 18   | 432    | 32px      | 768 x 576              |

**Why 32px?** Smallest size where color-coded tiles are visually distinct. At these grid
sizes the maps always scroll, which is correct — the player should never see the whole
map at once. Scrolling = a world that feels bigger than the screen.

### Road Hierarchy (Modernized)

```
Boulevard    (4 wide) ====  City arteries, connects districts
Avenue       (3 wide) ===   Major through-roads, connects exits
Street       (2 wide) ==    Connects buildings to avenues
Alley/Trail  (1 wide) =     Dead-ends, secrets, shortcuts
```

### District-Based Design
Instead of scattering buildings randomly, group them into districts:
- **Commercial**: Pokecenter + Pokemart cluster, signs, NPCs
- **Residential**: Houses in rows with shared streets, fenced yards
- **Research**: Lab + related buildings, gated campus
- **Recreation**: Park, pond, flowers, open grass, benches (signs)
- **Civic**: Gym, statues, plaza, event space
- **Transit**: Gates at map edges, wide approach roads
- **Wild**: Tall grass, rare grass, encounter-heavy zones
- **Water**: Lakes, ponds, surf areas, bridges, waterfronts

### Path Density Targets

| Zone Type    | Path %  | Why                                          |
|--------------|---------|----------------------------------------------|
| City         | 25-35%  | Dense urban grid, lots of walkable surface    |
| Route        | 12-18%  | Corridor with breathing room                  |
| Wild Zone    | 8-14%   | Mostly terrain, paths are suggestions          |
| Safari Zone  | 15-20%  | Guided but with off-trail areas                |
| Cave         | 18-25%  | Tunnels carved through rock                    |
| Lake         | 10-15%  | Water dominates, paths hug shoreline           |
| Battle Arena | 30-40%  | Highly structured, mostly walkable             |

---

## 2. ASCII Legend (All Templates)

Dense format — each character = one cell, no spaces between cells.

```
TERRAIN                          INTERACTIVE / ENTITY
.  Grass (1)                     S  Sign (9)
=  Path (2)                      N  NPC (10)
#  Tree (3)                      I  Item (13)
~  Water (0)                     H  Hidden Item (40)
@  Rock (8)                      W  Warp (16)
^  Tall Grass (7)                X  Cut Tree (27)
*  Flower (19)                   O  Strength Rock (26)
E  Water Edge (17)               +  Door (4)
|  Fence V (18)                  C  Cave (15)
-  Fence H (18)                  $  Statue (49)
B  Bridge (5)                    !  Pokeball pickup (42)
?  Rare Grass (28)
K  Surf Water (25)
L  Legendary (29)

BUILDINGS (shown as labeled footprint blocks)
P4x4 = Pokecenter    M4x4 = Pokemart     G5x5 = Gym
R5x4 = Lab           h3x3 = House Small  H4x4 = House Large
c3x2 = Cave Ent.     g4x3 = Gate         d4x3 = Pond

In the ASCII grids, buildings appear as their letter repeated
to fill their footprint. Door row uses + for door cells.

TRAINERS (single cell)
t  = Trainer (any direction)
v  = Villain trainer
m  = Minion trainer
```

---

## 3. Templates

---

### Template A: Home City — Greenleaf Metro (64 x 48)

The player lives here. This is a real city — commercial strip, residential blocks,
research campus, civic plaza, waterfront park, and a lake. Six exits connect to
the wider world. The player should feel like they're leaving a thriving place
every time they set out on a route.

**Districts:**
- NW: Transit Gate + Route exits
- N-Center: Civic Plaza (Gym, Statue, event stage)
- NE: Research Campus (Lab, school house, gated)
- W: Residential blocks (6+ houses, fenced yards, alleys)
- Center: Commercial strip (Pokecenter, 2 Pokemarts, NPCs)
- E: Recreation park (pond, flowers, fountain, benches)
- SE: Waterfront + lake access (surf water, bridge, fishing)
- S: South gate exits

```
         1111111111222222222233333333334444444444555555555566666
1234567890123456789012345678901234567890123456789012345678901234
################################################################  row 01
#...........====.......................=====....................#  row 02
#..........S====.......................=====....................#  row 03
#..........============================.====...................#  row 04  N. Boulevard
#..........============================.====...................#  row 05
#===.......====.......................====......................#  row 06  W exit
#===.......====.......................====......................#  row 07
#===.......====.......................====......................#  row 08
#..........====.......................====......................#  row 09
#...gggg...====.......GGGGG..........====........RRRRR.........#  row 10  Gate, Gym, Lab
#...g++g...====.......GGGGG....$=....====........RRRRR.........#  row 11  Statue on plaza
#...gggg...====.......GGGGG...N==....====........RRRRR.........#  row 12
#..........====.......GGGGG...===....====........R+++R.........#  row 13
#..........====.......G++GG===S==....====..........==..........#  row 14
#..........====.........==...====....====..........==..........#  row 15
#..........====.........==...====....====.......h h h h........#  row 16  campus houses
#========================================....===h h h h........#  row 17  MAIN BOULEVARD
#========================================....===h+h+h+h........#  row 18
#..........====.........==...====....====..........==..........#  row 19
#..........====.........==...====.N..====..........==..........#  row 20
#..........====..PPPP...==...====....====........------........#  row 21  Pokecenter
#..........====..PPPP........====....====..........==..........#  row 22
#..........====..PPPP........====....====..........==..........#  row 23
#..........====..P++P==......====....=============================  row 24  E exit
#..........====.....===..S...====....=============================  row 25
#..........====.....==.......====....====..........................#  row 26
#..........====..MMMM..MMMM.====....====..........***.....EEE..#  row 27  2 Marts, park
#..........====..MMMM..MMMM.====....====..........***.....E~~E.#  row 28
#..........====..MMMM..MMMM.====....====..ddd....***.....E~~E.#  row 29  Pond
#..........====..M++M..M++M.====....====..d~d....***..N..E~~E.#  row 30
#..........====.....========.====....====..ddd....***.....E~~E.#  row 31
#..........====.....====.....====....====..........***.....EEE..#  row 32
#.|HHHH|..====.....====.....====....====..........................#  row 33  Residential W
#.|HHHH|..====.....====.....====....====...^^^^^^^^^^..........#  row 34  Grass zone
#.|HHHH|..====.....====.....====....====...^^^^^^^^^^..........#  row 35
#.|H++H|..====.....====.....====....====...^^?????^^^..........#  row 36  Rare grass
#.........====.....====.....====....====...^^^^^^^^^^..........#  row 37
#.|hhhh|..====.....====.....====....====...^^^^^^^^^^..........#  row 38
#.|hhhh|..====.....====.....====....====..........................#  row 39
#.|h++h|..====.....====.....====....====...........BBBBB.......#  row 40  Bridge
#.........====.....====.....====....====...EEEEE==BBBBB==......#  row 41  Waterfront
#.|HHHH|..====.....====.....====....====...E~~~~==.....==......#  row 42
#.|HHHH|..====.....====.....====....====...E~KKK==..H.==......#  row 43  Surf water
#.|HHHH|..====....================..====...E~KKK........I.....#  row 44  Hidden+Item
#.|H++H|..====....================..====...E~~~~...............#  row 45
#..........====...........====..........EEEEE..................#  row 46
#..........====...........====.....................................#  row 47
################################################################  row 48
              ^^^^            ^^^^
              S gate          S gate
```

**Path network analysis (64 x 48 = 3,072 cells):**
- **6 exits**: West (rows 6-8), East (rows 24-25), 2x South (cols ~14-17, ~30-33), North implicit via gate
- **Main Boulevard**: row 17-18, runs full width — the city's spine
- **N-S Avenues**: 4 parallel avenues (cols ~11-14, ~24-27, ~37-40, ~50-53) create a grid
- **Civic Plaza** (N-center): Gym (5x5) + Statue + NPCs, sign-posted
- **Commercial Strip** (center-W): Pokecenter + 2 Pokemarts lining the avenue, doors face the street
- **Research Campus** (NE): Lab (5x4) + 3 small houses (school/dorms), fenced
- **Residential** (W): 3 large houses + 1 small house in fenced lots along alley
- **Recreation Park** (E): Pond + flower garden + bench NPCs
- **Waterfront** (SE): Lake with Surf water, bridge to island, hidden item reward
- **Wild Zone teaser** (S-center): tall grass + rare grass patch — encounter zone inside the city
- **Path density**: ~28% (urban)

**Player flow:**
1. Start at home (any residential house) -> walk to avenue
2. Avenue leads to commercial strip -> heal, shop
3. Boulevard leads to Gym -> first challenge
4. Explore campus, park, waterfront as optional content
5. South gates lead to routes, east exit to coastal area
6. Surf water and Cut Trees gate late-game content within the city itself

---

### Template B: Coastal Route (25 x 56)

Wide corridor connecting Home City to the next major area. Runs north-south along
a coastline. Beach, cliffs, trainer gauntlet, rest stop, tall grass meadows.
The water is always visible on the east side — the player feels the geography.

```
         1111111111222222
1234567890123456789012345
#########################  row 01
#====....................#  row 02  N entry (from city)
#====....................#  row 03
#====....................#  row 04
#====.......^^^..E~~~~E.#  row 05  Grass + ocean view
#====.......^^^..E~~~~E.#  row 06
#====...t...^^^..E~~~~E.#  row 07  Trainer
#====.......^^^..E~~~~E.#  row 08
#.====......^^^..EEEEEE.#  row 09  Path curves
#..====..........@@@@...#  row 10  Rocky bluff
#..====.........@@..@@..#  row 11
#..====..S......@....@..#  row 12  Sign: "Trainer's Alley"
#..====.........@@..@@..#  row 13
#..====..........@@@@...#  row 14
#...====..t..........I..#  row 15  Trainer + item on bluff
#...====.......E~~~~~E..#  row 16
#...====.......E~~~~~E..#  row 17
#..====........E~~K~~E..#  row 18  Surf spot
#..====........EEEEEEE..#  row 19
#..====.................#  row 20
#..====..PPPP...........#  row 21  Rest stop Pokecenter
#..====..PPPP...........#  row 22
#..====..PPPP...........#  row 23
#..====..P++P===........#  row 24
#..====.....====........#  row 25
#..====.....====..hhhh..#  row 26  Rest house
#..====.....====..hhhh..#  row 27
#..====.....====..h++h..#  row 28
#..========.====.====...#  row 29
#..........======.......#  row 30  Path widens at meadow
#..........======.......#  row 31
#..^^^^....======..N....#  row 32  NPC: route tips
#..^^^^....======.......#  row 33
#..^^^^..t.======.......#  row 34  Trainer in grass
#..^^^^....======.......#  row 35
#..^^^^....======.......#  row 36
#..........======.......#  row 37
#.........======........#  row 38  Curves back
#........======.........#  row 39
#.......======..........#  row 40
#......======...........#  row 41
#.....======......EEE...#  row 42
#.....======......E~~E..#  row 43  Coastal pond
#..t..======......E~~E..#  row 44  Trainer
#.....======......EEE...#  row 45
#.....======............#  row 46
#......======...........#  row 47
#.......======..^^^.....#  row 48  More grass
#........======.^^^.....#  row 49
#.........====..^^^.....#  row 50
#..........====.........#  row 51
#.......t..====..S......#  row 52  Last trainer + sign
#..........====.........#  row 53
#..........====.........#  row 54
#..........====.........#  row 55
#########################  row 56  S exit (to next area)
           ^^^^
           exit
```

**Path analysis (25 x 56 = 1,400 cells):**
- **2 exits**: North (col 1-4), South (col 11-14)
- **The path is not straight** — it snakes left and right as it goes south, following the coastline
- **Coastline** on the east: water edge tiles create a visual wall, Surf spot mid-route
- **6 trainers**: spaced every ~8 rows, some on chokepoints, some avoidable
- **Rest stop** at row 21-28: Pokecenter + house, a safe island mid-route
- **Terrain variety**: rocky bluff (rows 10-14), meadow (rows 30-37), coastal pond (rows 42-45)
- **Path density**: ~15%
- **Flow**: not a straight line — the player is always turning, always seeing new terrain

---

### Template C: Wild Zone — Evergreen Expanse (56 x 44)

Open exploration area. No forced path — the player chooses where to go.
Tall grass everywhere, pockets of rare encounters, items scattered off-trail,
a central lake, cave entrance to a dungeon, and a legendary encounter
hidden behind multiple gates (Cut Tree + Strength Rock + Surf).

```
         111111111122222222223333333333444444444455555555
123456789012345678901234567890123456789012345678901234567
########################################################  row 01
#====..##^^^^^^^^^^^^##..........##^^^^^^^^^............#  row 02  N entry
#====..##^^^^^^^^^^^^##..........##^^^^^^^^^............#  row 03
#====..##^^^^^^^^^^^^##..........##^^^^^^^^^............#  row 04
#====...^^^^^^^^^^^^.....========..^^^^^^^..............#  row 05
#====..........t.........========..........===..........#  row 06
#====....................========..........===..........#  row 07
#.====...................========..........===..........#  row 08
#..====..........I.......========..........===..........#  row 09  Item
#..====..................========..........===..........#  row 10
#..====........===================================.....#  row 11
#...===........===................................=.....#  row 12
#...===........===..EEEEEEEEEEEE..........^^^.....=....#  row 13
#...===........===..E~~~~~~~~~~E..........^^^.....=....#  row 14
#....===.......===..E~~~~~~~~~~E..........^^^.....=....#  row 15
#....===.......===..E~~~~KK~~~~E..........^^^..t..=....#  row 16  Surf in lake
#....===.......===..E~~~~~~~~~~E..........^^^.....=....#  row 17
#....===.......===..E~~~~~~~~~~E..H.......^^^.....=....#  row 18  Hidden item
#....===.......===..E~~~~~~~~~~E..........^^^.....=....#  row 19
#....===.......===..EEEEEEEEEEEE..............====.....#  row 20
#...====.......===...........N................====..^^^^#  row 21  NPC
#...====.......=================================..^^^^^#  row 22
#..====........................................===.^^^^^#  row 23
#..====........................................===.^?^^^#  row 24  Rare grass
#..====........................................===.^^^^^#  row 25
#.====.........................................===.^^^^^#  row 26
#.====..^^^^...........@@@@@@@@@@..............===......#  row 27
#.====..^^^^...........@........@..............===......#  row 28
#.====..^^^^..I........@..ccc..@...............===......#  row 29  Cave + item
#.====..^^^^...........@..CCC..@...............===......#  row 30  Cave entrance
#.====..^^^^...........@........@..............===......#  row 31
#.====..^^^^...........@@@@O@@@@...............===......#  row 32  Str Rock gate
#..====.^^^^.......................====.........===.....#  row 33
#..====..........................=====..........===.....#  row 34
#...====........................====............===.....#  row 35
#...====.......##X##............====.......^^^^.===.....#  row 36  Cut Tree gate
#....====......##.##...N........====.......^^^^.===.....#  row 37
#....====......##.##............====.......^^^^.........#  row 38
#.....====.....##?##............====.......^^^^.====....#  row 39  Rare behind Cut
#.....====.....##?##............====............====....#  row 40
#......====....##L##............====............====....#  row 41  LEGENDARY!!
#......====.................====....................=====#  row 42
#.......===================..........................===#  row 43
########################################################  row 44  S exit (col 53-55)
                                                   ^^^
                                                  S exit
```

**Path analysis (56 x 44 = 2,464 cells):**
- **3 exits**: North (col 1-4), South (col 53-55), + cave entrance internal
- **Two main trails** form a large loop — player can go clockwise or counterclockwise
- **Central lake** (12 x 8): Surf water in the middle, unreachable island later in game
- **Cave entrance** (rows 29-30): inside a rock enclosure, Strength Rock gates a shortcut
- **Legendary encounter** (row 41): hidden behind tree wall + Cut Tree (must have Cut HM), with Rare Grass leading to it — this is the deepest secret on the map
- **6 tall grass zones**: varying sizes (4x6, 5x7, etc.), spread across the map so encounters aren't clumped
- **2 rare grass zones**: one behind Cut Tree (SW), one in the east meadow
- **Items**: scattered off-trail, rewarding exploration
- **Trainers**: only 2 — this is a wild zone, not a trainer gauntlet
- **Path density**: ~12% (the point is to wander)

---

### Template D: Safari Zone (50 x 40)

Curated encounter area. The player pays to enter and gets limited steps/balls.
The zone is divided into 4 quadrants by water channels, connected by bridges.
Each quadrant has different encounter tables. Rare and legendary encounters
are deep in the hardest-to-reach quadrant.

```
         11111111112222222222333333333344444444
12345678901234567890123456789012345678901234567890
##################################################  row 01
#...=====================...........====..........#  row 02  Entry (N)
#...=====================...........====..........#  row 03
#.......====.............N..........====..........#  row 04  NPC: Safari rules
#.......====.........................====..........#  row 05
#.......====......^^^^^^^^^^^........====.........#  row 06
#.......====......^^^^^^^^^^^........====.........#  row 07  ZONE A: Common
#.......====......^^^^^I^^^^^........====.........#  row 08
#.......====......^^^^^^^^^^^........====.........#  row 09
#.......====......^^^^^^^^^^^........====.........#  row 10
#.......====.........................====.........#  row 11
#.......====.........................====.........#  row 12
#EEEEEEE====EEEEEEEEEEEEEEEEBBBBBBBB====EEEEEEEEE#  row 13  Water channel + bridge
#~~~~~~~====~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~====~~~~#  row 14
#EEEEEEE====EEEEEEEEEEEEEEEEEEEEEEEE====EEEEEEEEE#  row 15
#.......====.........................====.........#  row 16
#.......====.........................====.........#  row 17
#.......====......^^^^^^^^^^^........====.........#  row 18
#.......====......^^^^^^^^^^^........====.........#  row 19  ZONE B: Uncommon
#.......====......^^^^^^^^^^^........====.........#  row 20
#.......====......^^^^!^^^^^^........====.........#  row 21  Pokeball pickup
#.......====......^^^^^^^^^^^........====.........#  row 22
#.......====.........................====.........#  row 23
#.......====.........................====.........#  row 24
#EEEEEEE====EEEEEEEEEEEEEEEEEBBBBBBBB===EEEEEEEEE#  row 25  2nd water channel
#~~~~~~~====~~~~~~~~~~~~~~~~~~~~~~~~~====~~~~~~~~~#  row 26
#EEEEEEE====EEEEEEEEEEEEEEEEEEEEEEEEE===EEEEEEEEE#  row 27
#.......====.........................====.........#  row 28
#.......====.........................====.........#  row 29
#.......====......?????????..........====.........#  row 30
#.......====......?????????..........====.........#  row 31  ZONE C: Rare
#.......====......???H?????..........====.........#  row 32  Hidden item
#.......====......?????????..........====.........#  row 33
#.......====......?????????..........====.........#  row 34
#.......====.........................====.........#  row 35
#.......====...............====......====.........#  row 36
#.......===================..========..===========#  row 37
#............................====.................#  row 38
#..............***..***......====.....L...........#  row 39  LEGENDARY in Zone D
#..............***..***......====.................#  row 40  Flower garden + legend
##################################################
```

**Path analysis (50 x 40 = 2,000 cells):**
- **1 entry** (north), **no free exit** — player must use escape item or run out of steps
- **Grid path**: 2 north-south avenues connected by east-west bridges over water channels
- **4 zones** separated by water:
  - **Zone A** (rows 6-10): Common encounters, easy to reach
  - **Zone B** (rows 18-22): Uncommon, requires crossing first water channel
  - **Zone C** (rows 30-34): Rare grass, deep in the zone
  - **Zone D** (rows 38-40): Legendary encounter, flower garden, deepest point
- **Bridges**: only 2 crossing points per channel — funneling movement, eating steps
- **Items**: 1 per zone, placed deep to cost maximum steps
- **The cost-distance tradeoff**: the deeper you go, the better the encounters but the fewer steps you have left. The path forces you through each zone sequentially.
- **Path density**: ~18%

---

### Template E: Mt. Granite — Cave Complex (42 x 34)

Multi-chamber cave. Rock walls everywhere. Branching tunnels with dead ends
that reward exploration. Strength Rocks gate shortcuts. A villain hideout
in the deepest chamber. This is a dungeon — the player should get lost.

```
         111111111122222222223333333333444
123456789012345678901234567890123456789012
@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@  row 01
@@@@ccc@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@  row 02  Cave entry
@@@@CCC@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@  row 03
@@@@===@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@  row 04
@@@@===@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@  row 05
@@@@===@@@@@@@@@@@@@==============@@@@@@@@  row 06
@@@@=======@@@@@@@@@@@@@@@@@@@@@@=@@@@@@@@  row 07
@@@@@@@@@@=@@@@@@@@@@@@@@@@@@@@@@=@@@@@@@@  row 08
@@@@@@@@@@=@@@@=======@@@@@@@I@@=@@@@@@@@  row 09  Item on dead end
@@@@@@@@@@=@@@@=@@@@@=@@@@@@@@@@=@@@@@@@@  row 10
@@@@@I@@@@=@@@@=@@@@@=@@@@@@@@@@=@@@@@@@@  row 11  Item
@@@@@@@@@@=@@@@=@@@@@=@@@@@@@@@@=@@@@@@@@  row 12
@@@@==============================@@@@@@@@  row 13  Main east-west tunnel
@@@@=@@@@@@@@@@=@@@@@=@@@@@@@@@@@@@@@@=@@@  row 14
@@@@=@@@@@@@@@@=@@@@@=@@@@@@@@@@@@@@@@=@@@  row 15
@@@@=@@@@@@@@@@=@@@@@========@@@@@@@@@=@@@  row 16
@@@@=@@@@@@@@@@=@@@@@@@@@@@@@=@@@@@@@@=@@@  row 17
@@@@=@@@@@@@@@@=@@@@@@@@@@@@@=@@@@@@@@=@@@  row 18
@@@@=======O===@@@@@@@@@@@@@===========@@@  row 19  O gates shortcut
@@@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@@@@@@@=@@@  row 20
@@@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@H@@@@@=@@@  row 21  Hidden item
@@@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@@@@@@@=@@@  row 22
@@@=================================@@=@@@  row 23  Deep tunnel
@@@=@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@=@@@  row 24
@@@=@@m@@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@=@@@  row 25  Minion
@@@=@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@=@@@  row 26
@@@=======@@@@@@@@@====================@@@  row 27
@@@@@@@@@@=@@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@@  row 28
@@@@@@@@@@=@@v@@@@@@@@@@@@@@@@@@@@@@@@=@@@  row 29  Villain
@@@@@@@@@@=@@@@@@@@@@@@@@@@@@@@@@@@@@@=@@@  row 30
@@@@@@@@@@=========@@@@@@@@@@@@@@@@@@@@=@@@  row 31
@@@@@@@@@@@@@@@@@@=@@@@@@@@@@@@@@@@@@@@=@@@  row 32
@@@@@@@@@@@@@@@@@@=@@@@@@@@@!@@@@@@@@@@=@@@  row 33  Pokeball deep
@@@@@@@@@@@@@@@@@@====W@@@@@@@@@@W====@@@@  row 34  Warps: deeper / exit
@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
```

**Path analysis (42 x 34 = 1,428 cells):**
- **1 entry** (cave entrance, row 2-3), **2 warps** (row 34: one deeper, one back)
- **Main tunnel** (row 13): east-west spine connecting the upper and lower halves
- **Upper half**: entry + 2 branch paths with dead-end items
- **Lower half**: villain hideout — minion at row 25, villain boss at row 29
- **Strength Rock** (row 19): blocks a shortcut between upper and lower sections
- **Dead ends**: always have an item or hidden item — never punish exploration
- **Branching factor**: at every junction, 2-3 choices. Most lead somewhere, 1 is a dead end
- **Enemies**: minion + villain deep in the cave, not near the entrance
- **Path density**: ~22%

---

### Template F: Lake Serenity (48 x 38)

Water-dominated map. A massive lake in the center with islands, bridges,
shoreline paths, and a hidden grotto. Surf unlocks most of the content.
Early-game: you can only walk the shore. Late-game: you surf to islands,
find the legendary, access the grotto.

```
         111111111122222222223333333333444444
123456789012345678901234567890123456789012345678
################################################  row 01
#====..........EEEEEEEEEEEEEEEEEE..............#  row 02  W entry
#====..........E~~~~~~~~~~~~~~~~E..............#  row 03
#====..........E~~~~~~~~~~~~~~~~E..............#  row 04
#====..........E~~~~~~~~~~~~~~~~E..........=====#  row 05  E exit
#.====.........E~~~~~~~~~~~~~~~~E..........=====#  row 06
#..====........E~~~~EEEEEE~~~~~~E..........=====#  row 07
#..====........E~~~~E....E~~~~~~E..............#  row 08
#...====.......E~~~~E.N..E~~~~~~E..............#  row 09  NPC on island
#...====.......E~~~~E....E~~~~~~E..............#  row 10
#...====.......E~~~~E..I.E~~~~~~E..............#  row 11  Item on island
#....====......E~~~~EEEEEE~~~~~~E..............#  row 12
#....====......E~~~~~~~~~~~~~~~~E..............#  row 13
#....====..S...E~~KKKKKKKKKK~~~~E..............#  row 14  Surf lane
#.....====.....E~~KKKKKKKKKK~~~~E..............#  row 15
#.....====.....E~~~~~~~~~~~~~~~~E..............#  row 16
#.....====.....E~~~~~~~~~~~~~~~~E..............#  row 17
#......========BBBBBBBB~~~~~~~~~E..............#  row 18  Bridge to south shore
#..............E~~~~~~~~~~~~~~~~E.........^^^^.#  row 19
#..............E~~~~~~~~~~~~~~~~E.........^^^^.#  row 20  Grass zone
#..............E~~~~EEEEEEEE~~~~E.........^^^^.#  row 21
#..............E~~~~E......E~~~~E..............#  row 22
#..............E~~~~E..??..E~~~~E..............#  row 23  Rare grass island
#..............E~~~~E..??..E~~~~E..............#  row 24
#..............E~~~~E......E~~~~E..............#  row 25
#..............E~~~~EEEEEEEE~~~~E..............#  row 26
#..............E~~~~~~~~~~~~~~~~E..............#  row 27
#..............E~~~~~~~~~~~~~~~~E..............#  row 28
#..............E~~~~~~~~~E....E~E..............#  row 29
#..............E~~~~~~~~~E.L..E~E..............#  row 30  LEGENDARY on island
#..............E~~~~~~~~~E....E~E..............#  row 31
#..............E~~~~~~~~~EEEEE~E...............#  row 32
#..............E~~~~~~~~~~~~~~~~E..............#  row 33
#..............EEEEEEEEEEEEEEEEEE..............#  row 34
#..====..............====..........................#  row 35
#..====..............====..........................#  row 36
#..====..............====..........................#  row 37  S shoreline exits
################################################  row 38
   ^^^^              ^^^^
   SW exit           SE exit
```

**Path analysis (48 x 38 = 1,824 cells):**
- **4 exits**: West (rows 2-6), East (rows 5-7), SW (rows 35-37), SE (rows 35-37)
- **The lake** dominates ~40% of the map — water edge frames it, surf water inside
- **3 islands**:
  - NPC island (rows 8-11): accessible early via... nothing. Need Surf. Tease.
  - Rare grass island (rows 22-25): Surf-only, rare encounters
  - Legendary island (rows 29-31): deepest, smallest, most rewarding
- **1 bridge** (row 18): connects west shore path to south shore — early-game access
- **Shoreline path**: wraps around the west and south sides of the lake
- **Surf lane** (rows 14-15): clear surf water channel for navigation
- **Tall grass** on east shore: encounters near the east exit
- **Path density**: ~11% (water is the dominant terrain, paths hug the shore)

---

### Template G: Battle Arena (36 x 28)

Tournament grounds. Highly structured, mostly paved. The player fights through
a gauntlet of trainers arranged in a bracket. Heal station between rounds.
Final chamber has the Gym Leader or Elite 4 member.

```
         111111111122222222223333333
123456789012345678901234567890123456
####################################  row 01
#====..............................#  row 02  Entry
#====..............................#  row 03
#====...============================#  row 04  Main concourse
#........==........................#  row 05
#..PPPP..==...MMMM.................#  row 06  Pokecenter + Mart
#..PPPP..==...MMMM.................#  row 07
#..PPPP..==...MMMM.................#  row 08
#..P++P==.....M++M==..............#  row 09
#........======....................#  row 10
#........==........................#  row 11
#===================================  row 12  Arena entrance boulevard
#........==........................#  row 13
#..||||..==..||||.....||||..||||...#  row 14  Fenced battle lanes
#..| t|..==..|t |.....| t|..|t |..#  row 15  Round 1: 4 trainers
#..||||..==..||||.....||||..||||...#  row 16
#........===========================#  row 17
#........==........................#  row 18
#..||||..==..||||..................#  row 19
#..| t|..==..|t |..................#  row 20  Round 2: 2 trainers
#..||||..==..||||..................#  row 21
#........==========================#  row 22
#........==........................#  row 23
#..........==......................#  row 24
#..........||||||||||..............#  row 25
#..........|...t....|..............#  row 26  FINAL: Gym Leader / E4
#..........||||||||||..............#  row 27
####################################  row 28
```

**Path analysis (36 x 28 = 1,008 cells):**
- **1 entry** (NW), **no exit until victory** (warp after defeating final trainer)
- **Concourse** (rows 4-11): services area — Pokecenter and Pokemart before battle
- **3 rounds** of battle:
  - Round 1 (rows 14-16): 4 fenced lanes, each with a trainer — player picks a path
  - Round 2 (rows 19-21): 2 fenced lanes — fewer choices, harder trainers
  - Final (rows 25-27): single large arena, boss trainer
- **Fences** create lanes — the player is funneled but gets to choose which trainer to face
- **Heal between rounds**: player can backtrack to Pokecenter concourse
- **Path density**: ~35% (almost entirely paved — this is a constructed arena)

---

### Template H: Department Store Interior (24 x 18)

Multi-purpose building interior. Ground floor: lobby + shop counters.
Upper area: PC storage, special NPCs, back room with key item.
Represents a large building's inside — could be a dept store, HQ, or school.

```
         111111111122222
123456789012345678901234
@@@@@@@@@@@@@@@@@@@@@@@@  row 01
@....==================@  row 02  Top corridor
@....==..........==....@  row 03
@.N..==..........==.P..@  row 04  NPC + PC
@....==..........==....@  row 05
@....==..14......==....@  row 06  Key Item (14)
@====..==========..====@  row 07  Cross corridor
@==..................==.@  row 08
@==..||||....||||...==.@  row 09  Shop counters (fences)
@==..|$.|....|$.|...==.@  row 10  $ = Shop entity
@==..||||....||||...==.@  row 11
@==..................==.@  row 12
@==..N....S....N....==.@  row 13  NPCs + Sign (welcome)
@==..................==.@  row 14
@======================.@  row 15  Main lobby corridor
@..........++..........@  row 16  Entrance doors
@..........++..........@  row 17
@@@@@@@@@@@@@@@@@@@@@@@@  row 18
```

**Path analysis (24 x 18 = 432 cells):**
- **1 entry**: double door at bottom (row 16-17)
- **Lobby** (rows 14-15): wide corridor, welcoming
- **Shop floor** (rows 8-13): 2 fenced counter areas with shop NPCs, browsing space
- **Upper area** (rows 2-7): key item room, PC, special NPC — feels restricted/important
- **Rock walls** (@) instead of trees — this is interior
- **Path density**: ~40% (indoor = mostly floor)

---

## 4. Path Quality Checklist (Updated)

- [ ] Every building door connects to a path — no orphaned doors
- [ ] Entry/exit points are on edges and 2+ tiles wide (4 wide for city gates)
- [ ] Map has 2+ exits minimum (except dungeon/arena maps)
- [ ] Districts are visually distinct — don't blend residential into commercial
- [ ] Main boulevard/avenue is identifiable within 3 seconds of entering
- [ ] Tall grass zones are 3+ tiles deep and 4+ tiles wide (real encounter zones, not strips)
- [ ] Water bodies are large enough to justify Surf (8x6 minimum surf area)
- [ ] Cut Tree / Strength Rock gates block optional high-value content
- [ ] Items and hidden items are OFF the main path (reward exploration)
- [ ] Path density matches zone type (see targets in Section 1)
- [ ] No unreachable walkable tiles
- [ ] NPC or sign within 5 tiles of every entry point
- [ ] Trainers are placed at chokepoints on 1-wide path sections
- [ ] The map has visual landmarks — pond, statue, large building — for orientation
- [ ] Late-game content (Surf areas, Cut paths, legendaries) is visible but inaccessible early

---

## 5. Tile ID Quick Reference

| ID | Name          | Walk | Primary Use                              |
|----|---------------|------|------------------------------------------|
| 0  | Water         | No   | Lakes, rivers, ocean (visual)            |
| 1  | Grass         | Yes  | Default fill everywhere                  |
| 2  | Path          | Yes  | All roads, streets, floors               |
| 3  | Tree          | No   | Borders, forests, exterior walls         |
| 4  | Door          | Yes  | Building entrances                       |
| 5  | Bridge        | Yes  | Over water crossings                     |
| 7  | Tall Grass    | Yes  | Wild encounter zones                     |
| 8  | Rock          | No   | Cave walls, boulders, interior walls     |
| 9  | Sign          | No   | Directions, lore, benches                |
| 10 | NPC           | No   | Characters, quest givers                 |
| 13 | Item          | Yes  | Visible pickups                          |
| 15 | Cave          | Yes  | Cave floor (encounter tile)              |
| 16 | Warp          | Yes  | Teleport / floor transition              |
| 17 | Water Edge    | No   | Shoreline framing                        |
| 18 | Fence         | No   | Yard boundaries, arena lanes             |
| 19 | Flower        | Yes  | Parks, gardens, decoration               |
| 25 | Surf Water    | No   | Surfable water (late-game access)        |
| 26 | Str. Rock     | No   | HM gate — push to clear                 |
| 27 | Cut Tree      | No   | HM gate — cut to clear                  |
| 28 | Rare Grass    | Yes  | Premium encounter zones                  |
| 29 | Legendary     | Yes  | Boss encounter tile                      |
| 40 | Hidden Item   | Yes  | Invisible pickup (reward exploration)    |
| 42 | Pokeball      | Yes  | Ball pickup in safari/wild               |
| 49 | Statue        | No   | Landmarks, plaza centerpieces            |

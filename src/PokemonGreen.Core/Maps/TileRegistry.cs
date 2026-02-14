namespace PokemonGreen.Core.Maps;

/// <summary>
/// Static registry containing all tile definitions for the game.
/// </summary>
public static class TileRegistry
{
    private static readonly Dictionary<int, TileDefinition> _tiles = new()
    {
        // Terrain (0-15)
        [0] = new TileDefinition(0, "Water", false, "#3890f8", TileCategory.Terrain),
        [1] = new TileDefinition(1, "Grass", true, "#7ec850", TileCategory.Terrain),
        [2] = new TileDefinition(2, "Path", true, "#d4a574", TileCategory.Terrain),
        [3] = new TileDefinition(3, "Floor", true, "#c0a080", TileCategory.Terrain),
        [4] = new TileDefinition(4, "Bridge", true, "#8b7355", TileCategory.Terrain),
        [5] = new TileDefinition(5, "DeepWater", false, "#2060c0", TileCategory.Terrain),
        [6] = new TileDefinition(6, "Sand", true, "#e8d8a0", TileCategory.Terrain),
        [7] = new TileDefinition(7, "Snow", true, "#f0f8ff", TileCategory.Terrain),
        [8] = new TileDefinition(8, "Ice", true, "#b0e0f8", TileCategory.Terrain, "slippery"),
        [9] = new TileDefinition(9, "Mud", true, "#6b4423", TileCategory.Terrain, "slow"),
        [10] = new TileDefinition(10, "Lava", false, "#ff4500", TileCategory.Terrain),
        [11] = new TileDefinition(11, "Void", false, "#000000", TileCategory.Terrain),
        [12] = new TileDefinition(12, "CaveFloor", true, "#606060", TileCategory.Terrain),
        [13] = new TileDefinition(13, "WoodFloor", true, "#a0522d", TileCategory.Terrain),
        [14] = new TileDefinition(14, "Carpet", true, "#8b0000", TileCategory.Terrain),
        [15] = new TileDefinition(15, "Tiles", true, "#d3d3d3", TileCategory.Terrain),

        // Decoration (16-31)
        [16] = new TileDefinition(16, "Tree", false, "#228b22", TileCategory.Decoration),
        [17] = new TileDefinition(17, "Rock", false, "#808080", TileCategory.Decoration),
        [18] = new TileDefinition(18, "Flower", true, "#ff69b4", TileCategory.Decoration),
        [19] = new TileDefinition(19, "Statue", false, "#a9a9a9", TileCategory.Decoration),
        [20] = new TileDefinition(20, "Bush", false, "#2e8b57", TileCategory.Decoration),
        [21] = new TileDefinition(21, "Stump", false, "#8b4513", TileCategory.Decoration),
        [22] = new TileDefinition(22, "Boulder", false, "#696969", TileCategory.Decoration),
        [23] = new TileDefinition(23, "Sign", false, "#deb887", TileCategory.Decoration, "readable"),
        [24] = new TileDefinition(24, "Fence", false, "#cd853f", TileCategory.Decoration),
        [25] = new TileDefinition(25, "Torch", false, "#ffa500", TileCategory.Decoration),
        [26] = new TileDefinition(26, "Chest", false, "#8b4513", TileCategory.Decoration, "openable"),
        [27] = new TileDefinition(27, "Barrel", false, "#a0522d", TileCategory.Decoration),
        [28] = new TileDefinition(28, "Crate", false, "#d2691e", TileCategory.Decoration),
        [29] = new TileDefinition(29, "Pot", false, "#cd5c5c", TileCategory.Decoration),
        [30] = new TileDefinition(30, "Bookshelf", false, "#8b4513", TileCategory.Decoration, "readable"),
        [31] = new TileDefinition(31, "Table", false, "#deb887", TileCategory.Decoration),

        // Interactive (32-47)
        [32] = new TileDefinition(32, "Door", true, "#8b4513", TileCategory.Interactive, "door"),
        [33] = new TileDefinition(33, "Warp", true, "#9400d3", TileCategory.Interactive, "warp"),
        [34] = new TileDefinition(34, "PC", false, "#4169e1", TileCategory.Interactive, "pc"),
        [35] = new TileDefinition(35, "HealingMachine", false, "#ff6b6b", TileCategory.Interactive, "heal"),
        [36] = new TileDefinition(36, "Mart", false, "#4682b4", TileCategory.Interactive, "shop"),
        [37] = new TileDefinition(37, "StrengthRock", false, "#708090", TileCategory.Interactive, "strength"),
        [38] = new TileDefinition(38, "CutTree", false, "#556b2f", TileCategory.Interactive, "cut"),
        [39] = new TileDefinition(39, "SmashRock", false, "#778899", TileCategory.Interactive, "rock_smash"),
        [40] = new TileDefinition(40, "WaterfallClimb", false, "#4682b4", TileCategory.Interactive, "waterfall"),
        [41] = new TileDefinition(41, "Dive", false, "#1e90ff", TileCategory.Interactive, "dive"),
        [42] = new TileDefinition(42, "Switch", true, "#daa520", TileCategory.Interactive, "switch"),
        [43] = new TileDefinition(43, "PressurePlate", true, "#c0c0c0", TileCategory.Interactive, "pressure_plate"),
        [44] = new TileDefinition(44, "LockedDoor", false, "#654321", TileCategory.Interactive, "locked_door"),
        [45] = new TileDefinition(45, "HiddenItem", true, "#00000000", TileCategory.Interactive, "hidden_item"),
        [46] = new TileDefinition(46, "ItemBall", true, "#ff0000", TileCategory.Interactive, "item_ball"),
        [47] = new TileDefinition(47, "Teleporter", true, "#00ffff", TileCategory.Interactive, "teleport"),

        // Entity (48-55)
        [48] = new TileDefinition(48, "NPC", false, "#ffd700", TileCategory.Entity, "npc"),
        [49] = new TileDefinition(49, "ServiceNPC", false, "#ffa500", TileCategory.Entity, "service_npc"),
        [50] = new TileDefinition(50, "Nurse", false, "#ffb6c1", TileCategory.Entity, "nurse"),
        [51] = new TileDefinition(51, "Clerk", false, "#87ceeb", TileCategory.Entity, "clerk"),
        [52] = new TileDefinition(52, "GymGuide", false, "#98fb98", TileCategory.Entity, "gym_guide"),
        [53] = new TileDefinition(53, "Rival", false, "#ff4500", TileCategory.Entity, "rival"),
        [54] = new TileDefinition(54, "Professor", false, "#f5f5dc", TileCategory.Entity, "professor"),
        [55] = new TileDefinition(55, "Mom", false, "#ffb6c1", TileCategory.Entity, "mom"),

        // Trainer (56-71)
        [56] = new TileDefinition(56, "TrainerUp", false, "#dc143c", TileCategory.Trainer, "trainer_up"),
        [57] = new TileDefinition(57, "TrainerDown", false, "#dc143c", TileCategory.Trainer, "trainer_down"),
        [58] = new TileDefinition(58, "TrainerLeft", false, "#dc143c", TileCategory.Trainer, "trainer_left"),
        [59] = new TileDefinition(59, "TrainerRight", false, "#dc143c", TileCategory.Trainer, "trainer_right"),
        [60] = new TileDefinition(60, "GymTrainer", false, "#b22222", TileCategory.Trainer, "gym_trainer"),
        [61] = new TileDefinition(61, "GymLeader", false, "#8b0000", TileCategory.Trainer, "gym_leader"),
        [62] = new TileDefinition(62, "EliteFour", false, "#4b0082", TileCategory.Trainer, "elite_four"),
        [63] = new TileDefinition(63, "Champion", false, "#ffd700", TileCategory.Trainer, "champion"),
        [64] = new TileDefinition(64, "Rocket", false, "#2f4f4f", TileCategory.Trainer, "team_rocket"),
        [65] = new TileDefinition(65, "Aqua", false, "#00bfff", TileCategory.Trainer, "team_aqua"),
        [66] = new TileDefinition(66, "Magma", false, "#ff4500", TileCategory.Trainer, "team_magma"),
        [67] = new TileDefinition(67, "Galactic", false, "#483d8b", TileCategory.Trainer, "team_galactic"),
        [68] = new TileDefinition(68, "Plasma", false, "#87cefa", TileCategory.Trainer, "team_plasma"),
        [69] = new TileDefinition(69, "Flare", false, "#ff6347", TileCategory.Trainer, "team_flare"),
        [70] = new TileDefinition(70, "Skull", false, "#2f2f2f", TileCategory.Trainer, "team_skull"),
        [71] = new TileDefinition(71, "Yell", false, "#ff1493", TileCategory.Trainer, "team_yell"),

        // Encounter (72-79)
        [72] = new TileDefinition(72, "TallGrass", true, "#5a9c3a", TileCategory.Encounter, "wild_encounter"),
        [73] = new TileDefinition(73, "RareGrass", true, "#4a8c2a", TileCategory.Encounter, "rare_encounter"),
        [74] = new TileDefinition(74, "DarkGrass", true, "#3a7c1a", TileCategory.Encounter, "double_encounter"),
        [75] = new TileDefinition(75, "CaveEncounter", true, "#505050", TileCategory.Encounter, "cave_encounter"),
        [76] = new TileDefinition(76, "WaterEncounter", false, "#3890f8", TileCategory.Encounter, "water_encounter"),
        [77] = new TileDefinition(77, "SurfEncounter", false, "#4090ff", TileCategory.Encounter, "surf_encounter"),
        [78] = new TileDefinition(78, "FishingSpot", false, "#2080e8", TileCategory.Encounter, "fishing"),
        [79] = new TileDefinition(79, "Headbutt", false, "#228b22", TileCategory.Encounter, "headbutt"),

        // Structure (80-95)
        [80] = new TileDefinition(80, "Wall", false, "#404040", TileCategory.Structure),
        [81] = new TileDefinition(81, "LedgeDown", true, "#7ec850", TileCategory.Structure, "ledge_down"),
        [82] = new TileDefinition(82, "LedgeLeft", true, "#7ec850", TileCategory.Structure, "ledge_left"),
        [83] = new TileDefinition(83, "LedgeRight", true, "#7ec850", TileCategory.Structure, "ledge_right"),
        [84] = new TileDefinition(84, "Blocked", false, "#303030", TileCategory.Structure),
        [85] = new TileDefinition(85, "SpinUp", true, "#a0a0a0", TileCategory.Structure, "spin_up"),
        [86] = new TileDefinition(86, "SpinDown", true, "#a0a0a0", TileCategory.Structure, "spin_down"),
        [87] = new TileDefinition(87, "SpinLeft", true, "#a0a0a0", TileCategory.Structure, "spin_left"),
        [88] = new TileDefinition(88, "SpinRight", true, "#a0a0a0", TileCategory.Structure, "spin_right"),
        [89] = new TileDefinition(89, "StairsUp", true, "#8b7355", TileCategory.Structure, "stairs_up"),
        [90] = new TileDefinition(90, "StairsDown", true, "#8b7355", TileCategory.Structure, "stairs_down"),
        [91] = new TileDefinition(91, "Ladder", true, "#a0522d", TileCategory.Structure, "ladder"),
        [92] = new TileDefinition(92, "Ramp", true, "#9b8b7b", TileCategory.Structure, "ramp"),
        [93] = new TileDefinition(93, "Cliff", false, "#5a5a5a", TileCategory.Structure),
        [94] = new TileDefinition(94, "Waterfall", false, "#4090ff", TileCategory.Structure, "waterfall_hm"),
        [95] = new TileDefinition(95, "Whirlpool", false, "#3080e8", TileCategory.Structure, "whirlpool_hm"),

        // Item (96-103)
        [96] = new TileDefinition(96, "Pokeball", true, "#ff0000", TileCategory.Item, "item_pokeball"),
        [97] = new TileDefinition(97, "Potion", true, "#9370db", TileCategory.Item, "item_potion"),
        [98] = new TileDefinition(98, "Revive", true, "#ffd700", TileCategory.Item, "item_revive"),
        [99] = new TileDefinition(99, "RareCandy", true, "#00ffff", TileCategory.Item, "item_rare_candy"),
        [100] = new TileDefinition(100, "TM", true, "#cd853f", TileCategory.Item, "item_tm"),
        [101] = new TileDefinition(101, "HM", true, "#daa520", TileCategory.Item, "item_hm"),
        [102] = new TileDefinition(102, "KeyItem", true, "#ffa500", TileCategory.Item, "item_key"),
        [103] = new TileDefinition(103, "Berry", true, "#ff6b6b", TileCategory.Item, "item_berry"),
    };

    /// <summary>
    /// Gets a tile definition by its ID.
    /// </summary>
    /// <param name="id">The tile ID.</param>
    /// <returns>The tile definition, or null if not found.</returns>
    public static TileDefinition? GetTile(int id)
    {
        return _tiles.TryGetValue(id, out var tile) ? tile : null;
    }

    /// <summary>
    /// Gets all tile definitions belonging to a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>An enumerable of matching tile definitions.</returns>
    public static IEnumerable<TileDefinition> GetTilesByCategory(TileCategory category)
    {
        return _tiles.Values.Where(t => t.Category == category);
    }

    /// <summary>
    /// Gets all registered tile definitions.
    /// </summary>
    public static IEnumerable<TileDefinition> AllTiles => _tiles.Values;

    /// <summary>
    /// Gets the total number of registered tiles.
    /// </summary>
    public static int Count => _tiles.Count;
}

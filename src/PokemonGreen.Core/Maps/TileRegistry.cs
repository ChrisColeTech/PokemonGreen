using System.Collections.ObjectModel;

namespace PokemonGreen.Core.Maps;

public enum TileCategory
{
    Terrain,
    Decoration,
    Interactive,
    Entity,
    Trainer,
    Encounter,
    Structure,
    Item
}

public readonly record struct TileDefinition(
    int Id,
    string Name,
    TileCategory Category,
    TileVisualKind VisualKind,
    bool Walkable,
    byte Red = 128,
    byte Green = 128,
    byte Blue = 128,
    TileOverlayKind OverlayKind = TileOverlayKind.None
);

public static class TileRegistry
{
    public static IReadOnlyDictionary<int, TileDefinition> All { get; }
    public static IReadOnlyList<TileDefinition> List { get; }

    static TileRegistry()
    {
        var tiles = new Dictionary<int, TileDefinition>
        {
            [0] = New(0, "Water", TileCategory.Terrain, TileVisualKind.AnimatedWater, false),
            [1] = New(1, "Grass", TileCategory.Terrain, TileVisualKind.Grass, true),
            [2] = New(2, "Path", TileCategory.Terrain, TileVisualKind.Path, true),
            [3] = New(3, "Tree", TileCategory.Decoration, TileVisualKind.Tree, false),
            [4] = New(4, "Door", TileCategory.Interactive, TileVisualKind.InteractiveObject, true, 139, 69, 19, TileOverlayKind.Door),
            [5] = New(5, "Bridge", TileCategory.Terrain, TileVisualKind.Path, true, 107, 68, 35),
            [6] = New(6, "Cave", TileCategory.Interactive, TileVisualKind.InteractiveObject, false, 85, 85, 85),
            [7] = New(7, "Tall Grass", TileCategory.Encounter, TileVisualKind.Grass, true, 26, 138, 26),
            [8] = New(8, "Rock", TileCategory.Decoration, TileVisualKind.Rock, false),
            [9] = New(9, "Sign", TileCategory.Interactive, TileVisualKind.InteractiveObject, false, 139, 115, 85, TileOverlayKind.Sign),
            [10] = New(10, "NPC", TileCategory.Entity, TileVisualKind.EntityMarker, false, 255, 107, 107, TileOverlayKind.EntityNpc),
            [11] = New(11, "Service NPC", TileCategory.Entity, TileVisualKind.EntityMarker, false, 255, 217, 61, TileOverlayKind.EntityService),
            [12] = New(12, "Heal NPC", TileCategory.Entity, TileVisualKind.EntityMarker, false, 107, 203, 119, TileOverlayKind.EntityService),
            [13] = New(13, "Item", TileCategory.Entity, TileVisualKind.EntityMarker, true, 157, 78, 221, TileOverlayKind.EntityItem),
            [14] = New(14, "Hidden Item", TileCategory.Entity, TileVisualKind.EntityMarker, true, 224, 64, 251, TileOverlayKind.EntityItem),
            [15] = New(15, "Wall", TileCategory.Structure, TileVisualKind.Solid, false, 44, 44, 44),
            [16] = New(16, "Warp", TileCategory.Interactive, TileVisualKind.InteractiveObject, true, 0, 206, 201, TileOverlayKind.Warp),
            [17] = New(17, "Deep Water", TileCategory.Terrain, TileVisualKind.AnimatedWater, false, 41, 128, 185),
            [18] = New(18, "Floor", TileCategory.Terrain, TileVisualKind.Path, true, 121, 85, 72),
            [19] = New(19, "Flower", TileCategory.Decoration, TileVisualKind.Flower, true),
            [20] = New(20, "Trainer Up", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 255, 146, 43, TileOverlayKind.TrainerUp),
            [21] = New(21, "Trainer Down", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 245, 132, 32, TileOverlayKind.TrainerDown),
            [22] = New(22, "Trainer Left", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 235, 121, 24, TileOverlayKind.TrainerLeft),
            [23] = New(23, "Trainer Right", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 255, 163, 66, TileOverlayKind.TrainerRight),
            [24] = New(24, "Trainer Boss", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 255, 23, 68, TileOverlayKind.TrainerBoss),
            [25] = New(25, "Shallow Water", TileCategory.Terrain, TileVisualKind.AnimatedWater, false, 30, 144, 255),
            [26] = New(26, "Strength Rock", TileCategory.Interactive, TileVisualKind.Rock, false, 141, 110, 99, TileOverlayKind.StrengthRock),
            [27] = New(27, "Cut Tree", TileCategory.Interactive, TileVisualKind.Tree, false, 76, 175, 80, TileOverlayKind.CutTree),
            [28] = New(28, "Rare Grass", TileCategory.Encounter, TileVisualKind.Grass, true, 255, 215, 0),
            [29] = New(29, "Special Tile", TileCategory.Terrain, TileVisualKind.Grass, true, 255, 0, 255),
            [30] = New(30, "Elite Trainer", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 128, 0, 128, TileOverlayKind.TrainerBoss),
            [31] = New(31, "Gym Trainer Up", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 74, 0, 128, TileOverlayKind.TrainerUp),
            [32] = New(32, "Gym Trainer Down", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 88, 18, 141, TileOverlayKind.TrainerDown),
            [33] = New(33, "Gym Trainer Left", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 64, 0, 112, TileOverlayKind.TrainerLeft),
            [34] = New(34, "Gym Trainer Right", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 96, 24, 160, TileOverlayKind.TrainerRight),
            [35] = New(35, "Gym Leader Up", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 106, 13, 173, TileOverlayKind.TrainerUp),
            [36] = New(36, "Gym Leader Down", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 120, 28, 188, TileOverlayKind.TrainerDown),
            [37] = New(37, "Gym Leader Left", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 90, 0, 150, TileOverlayKind.TrainerLeft),
            [38] = New(38, "Gym Leader Right", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 132, 45, 198, TileOverlayKind.TrainerRight),
            [39] = New(39, "Rival", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 220, 20, 60, TileOverlayKind.TrainerDown),
            [40] = New(40, "Pokeball Item", TileCategory.Entity, TileVisualKind.EntityMarker, true, 70, 130, 180, TileOverlayKind.EntityItem),
            [41] = New(41, "PC", TileCategory.Interactive, TileVisualKind.InteractiveObject, false, 169, 169, 169, TileOverlayKind.Pc),
            [42] = New(42, "Pokeball Visible", TileCategory.Entity, TileVisualKind.EntityMarker, true, 255, 0, 0, TileOverlayKind.EntityItem),
            [43] = New(43, "Champion", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 192, 192, 192, TileOverlayKind.TrainerBoss),
            [44] = New(44, "Elite Four 1", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 255, 215, 0, TileOverlayKind.TrainerBoss),
            [45] = New(45, "Elite Four 2", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 240, 198, 0, TileOverlayKind.TrainerBoss),
            [46] = New(46, "Elite Four 3", TileCategory.Trainer, TileVisualKind.TrainerMarker, false, 200, 16, 54, TileOverlayKind.TrainerBoss),
            [47] = New(47, "Professor", TileCategory.Entity, TileVisualKind.EntityMarker, false, 139, 69, 19, TileOverlayKind.EntityNpc),
            [48] = New(48, "Mom NPC", TileCategory.Entity, TileVisualKind.EntityMarker, false, 221, 160, 221, TileOverlayKind.EntityNpc),
            [49] = New(49, "Statue", TileCategory.Decoration, TileVisualKind.Statue, false, overlayKind: TileOverlayKind.Statue),
            [50] = New(50, "Hidden Pokeball", TileCategory.Entity, TileVisualKind.EntityMarker, true, 0, 206, 209, TileOverlayKind.EntityItem),
            [51] = New(51, "Pokeball", TileCategory.Item, TileVisualKind.Item, true),
            [52] = New(52, "Great Ball", TileCategory.Item, TileVisualKind.Item, true),
            [53] = New(53, "Ultra Ball", TileCategory.Item, TileVisualKind.Item, true),
            [54] = New(54, "Potion", TileCategory.Item, TileVisualKind.Item, true),
            [55] = New(55, "Super Potion", TileCategory.Item, TileVisualKind.Item, true),
            [56] = New(56, "Fire Stone", TileCategory.Item, TileVisualKind.Item, true),
            [57] = New(57, "Water Stone", TileCategory.Item, TileVisualKind.Item, true),
            [58] = New(58, "Thunder Stone", TileCategory.Item, TileVisualKind.Item, true),
            [59] = New(59, "Leaf Stone", TileCategory.Item, TileVisualKind.Item, true),
            [60] = New(60, "Moon Stone", TileCategory.Item, TileVisualKind.Item, true),
            [61] = New(61, "Red Crystal", TileCategory.Item, TileVisualKind.Item, true),
            [62] = New(62, "Blue Crystal", TileCategory.Item, TileVisualKind.Item, true),
            [63] = New(63, "Green Crystal", TileCategory.Item, TileVisualKind.Item, true),
            [64] = New(64, "Apple", TileCategory.Item, TileVisualKind.Item, true),
            [65] = New(65, "Red Berry", TileCategory.Item, TileVisualKind.Item, true),
            [66] = New(66, "Blue Berry", TileCategory.Item, TileVisualKind.Item, true),
            [67] = New(67, "Green Herb", TileCategory.Item, TileVisualKind.Item, true),
            [68] = New(68, "Mega Ring", TileCategory.Item, TileVisualKind.Item, true),
            [69] = New(69, "Master Ball", TileCategory.Item, TileVisualKind.Item, true),
            [70] = New(70, "Yellow Crystal", TileCategory.Item, TileVisualKind.Item, true),
        };

        All = new ReadOnlyDictionary<int, TileDefinition>(tiles);
        List = tiles.Values.OrderBy(t => t.Id).ToList();
    }

    public static bool TryGet(int id, out TileDefinition tile) => All.TryGetValue(id, out tile);

    public static TileDefinition Get(int id) => All.TryGetValue(id, out var tile) ? tile : throw new KeyNotFoundException($"Tile {id} not found");

    public static IReadOnlySet<int> GetWalkableTileIds() => All.Values
        .Where(t => t.Walkable)
        .Select(t => t.Id)
        .ToHashSet();

    public static IReadOnlySet<int> GetDecorationTileIds() => All.Values
        .Where(t => t.Category is TileCategory.Decoration or TileCategory.Item)
        .Select(t => t.Id)
        .ToHashSet();

    private static TileDefinition New(int id, string name, TileCategory category, TileVisualKind visualKind, bool walkable, byte red = 128, byte green = 128, byte blue = 128, TileOverlayKind overlayKind = TileOverlayKind.None)
        => new(id, name, category, visualKind, walkable, red, green, blue, overlayKind);
}

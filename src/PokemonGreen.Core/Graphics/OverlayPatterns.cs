using System.Collections.Generic;
using PokemonGreen.Core.Maps;

namespace PokemonGreen.Core.Graphics;

public static class OverlayPatterns
{
    public static readonly IReadOnlyDictionary<TileOverlayKind, string[]> Patterns =
        new Dictionary<TileOverlayKind, string[]>
        {
            [TileOverlayKind.Door] = [
                "01110",
                "10001",
                "10001",
                "10001",
                "11111",
            ],
            [TileOverlayKind.Sign] = [
                "11111",
                "11111",
                "00100",
                "00100",
                "00100",
            ],
            [TileOverlayKind.Warp] = [
                "00100",
                "01110",
                "11111",
                "01110",
                "00100",
            ],
            [TileOverlayKind.StrengthRock] = [
                "10001",
                "01010",
                "00100",
                "01010",
                "10001",
            ],
            [TileOverlayKind.CutTree] = [
                "10000",
                "01000",
                "00100",
                "00010",
                "00001",
            ],
            [TileOverlayKind.Pc] = [
                "11111",
                "10001",
                "11111",
                "00100",
                "01110",
            ],
            [TileOverlayKind.Statue] = [
                "01110",
                "00100",
                "00100",
                "01110",
                "11111",
            ],
            [TileOverlayKind.EntityNpc] = [
                "00100",
                "01110",
                "00100",
                "01110",
                "01110",
            ],
            [TileOverlayKind.EntityService] = [
                "00100",
                "00100",
                "11111",
                "00100",
                "00100",
            ],
            [TileOverlayKind.EntityItem] = [
                "00100",
                "01110",
                "11111",
                "01110",
                "00100",
            ],
            [TileOverlayKind.TrainerUp] = [
                "00100",
                "01110",
                "10101",
                "00100",
                "00100",
            ],
            [TileOverlayKind.TrainerDown] = [
                "00100",
                "00100",
                "10101",
                "01110",
                "00100",
            ],
            [TileOverlayKind.TrainerLeft] = [
                "00100",
                "01000",
                "11111",
                "01000",
                "00100",
            ],
            [TileOverlayKind.TrainerRight] = [
                "00100",
                "00010",
                "11111",
                "00010",
                "00100",
            ],
            [TileOverlayKind.TrainerBoss] = [
                "10101",
                "01110",
                "11111",
                "01110",
                "00100",
            ],
        };
}
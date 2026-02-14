using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Direction = PokemonGreen.Core.Player.Direction;
using PlayerState = PokemonGreen.Core.Player.PlayerState;

namespace PokemonGreen.Core.Rendering;

public class PlayerRenderer
{
    private const int FrameWidth = 64;
    private const int FrameHeight = 64;

    private static readonly Dictionary<Direction, int> DirectionRow = new()
    {
        { Direction.Up, 0 },
        { Direction.Left, 1 },
        { Direction.Down, 2 },
        { Direction.Right, 3 },
    };

    private static readonly Dictionary<PlayerState, string> StateToSheet = new()
    {
        { PlayerState.Idle, "idle" },
        { PlayerState.Walk, "walk" },
        { PlayerState.Run, "run" },
        { PlayerState.Jump, "jump" },
        { PlayerState.Combat, "slash" },
        { PlayerState.Spellcast, "slash" },
        { PlayerState.Climb, "idle" },
    };

    public void Draw(
        SpriteBatch spriteBatch,
        Player.Player player,
        Systems.Camera camera,
        int tileSize = 32)
    {
        var sheetName = StateToSheet.GetValueOrDefault(player.State, "idle");
        var sheet = TextureStore.LoadPlayerSheet(sheetName);
        if (sheet == null)
            return;

        int row = DirectionRow.GetValueOrDefault(player.Facing, 2);
        int col = player.AnimationFrame;

        var sourceRect = new Rectangle(col * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);

        float worldX = player.X * tileSize;
        float worldY = player.Y * tileSize - player.JumpHeight * tileSize;

        float scale = (float)tileSize / FrameWidth;
        int scaledWidth = (int)(FrameWidth * scale);
        int scaledHeight = (int)(FrameHeight * scale);

        int offsetX = (tileSize - scaledWidth) / 2;
        int offsetY = tileSize - scaledHeight;

        spriteBatch.Draw(
            sheet,
            new Rectangle(
                (int)(worldX + offsetX),
                (int)(worldY + offsetY),
                scaledWidth,
                scaledHeight),
            sourceRect,
            Color.White);
    }
}

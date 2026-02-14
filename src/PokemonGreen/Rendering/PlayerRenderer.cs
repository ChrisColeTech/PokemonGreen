using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Systems;
using Direction = PokemonGreen.Core.Player.Direction;
using Player = PokemonGreen.Core.Player.Player;
using PlayerState = PokemonGreen.Core.Player.PlayerState;

#nullable enable

namespace PokemonGreen.Rendering;

/// <summary>
/// Renders the player using sprite sheet animations.
/// </summary>
public class PlayerRenderer
{
    private const int FrameWidth = 64;
    private const int FrameHeight = 64;

    // Row order in the sprite sheets: Up=0, Left=1, Down=2, Right=3
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

    private readonly Dictionary<string, Texture2D> _sheets = new();
    private string? _contentPath;

    /// <summary>
    /// Loads all player sprite sheets from the Content/player directory.
    /// </summary>
    public void LoadContent(string contentRootPath)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _contentPath = Path.Combine(baseDir, contentRootPath, "player");
        foreach (var name in new[] { "idle", "walk", "run", "jump", "slash" })
        {
            var path = Path.Combine(_contentPath, $"{name}.png");
            if (File.Exists(path))
                _sheets[name] = TextureStore.LoadFromFile(path);
        }
    }

    /// <summary>
    /// Draws the player at their current position using the appropriate sprite frame.
    /// </summary>
    public void Draw(
        SpriteBatch spriteBatch,
        Player player,
        Camera camera,
        GraphicsDevice graphicsDevice,
        int tileSize = 32)
    {
        var sheetName = StateToSheet.GetValueOrDefault(player.State, "idle");
        if (!_sheets.TryGetValue(sheetName, out var sheet))
            return;

        int row = DirectionRow.GetValueOrDefault(player.Facing, 2);
        int col = player.AnimationFrame;

        var sourceRect = new Rectangle(col * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);

        // Convert player tile position to world position
        float worldX = player.X * tileSize;
        float worldY = player.Y * tileSize - player.JumpHeight * tileSize;

        // Scale the sprite to match the tile size. Sprite is 64x64 but occupies one tile.
        float scale = (float)tileSize / FrameWidth;
        int scaledWidth = tileSize;
        int scaledHeight = tileSize;

        // Center the sprite on the tile center (feet aligned)
        int offsetX = (scaledWidth - (int)(FrameWidth * scale)) / 2;
        int offsetY = scaledHeight - (int)(FrameHeight * scale);

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

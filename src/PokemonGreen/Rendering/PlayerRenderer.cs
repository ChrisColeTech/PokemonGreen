using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Systems;
using Direction = PokemonGreen.Core.Player.Direction;
using Player = PokemonGreen.Core.Player.Player;

#nullable enable

namespace PokemonGreen.Rendering;

/// <summary>
/// Renders the player sprite.
/// </summary>
public class PlayerRenderer
{
    private Texture2D? _playerTexture;
    private readonly Color _playerColor = new Color(220, 20, 60); // Crimson red

    /// <summary>
    /// Size of the player sprite in pixels.
    /// </summary>
    public int PlayerSize { get; set; } = 28;

    /// <summary>
    /// Draws the player at their current position.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="player">The player to draw.</param>
    /// <param name="camera">The camera for viewport calculations.</param>
    /// <param name="graphicsDevice">The graphics device for creating textures.</param>
    /// <param name="tileSize">The size of each tile in pixels.</param>
    public void Draw(
        SpriteBatch spriteBatch,
        Player player,
        Camera camera,
        GraphicsDevice graphicsDevice,
        int tileSize = 32)
    {
        // Create texture if needed
        _playerTexture ??= TextureStore.CreateColorTexture(graphicsDevice, _playerColor);

        // Convert player tile position to world position (center of tile)
        float worldX = player.X * tileSize;
        float worldY = player.Y * tileSize;

        // Convert to screen position
        var (screenX, screenY) = camera.WorldToScreen(worldX, worldY);

        // Calculate scaled sizes
        int scaledTileSize = (int)(tileSize * camera.Zoom);
        int scaledPlayerSize = (int)(PlayerSize * camera.Zoom);

        // Center the player sprite within the tile
        int offsetX = (scaledTileSize - scaledPlayerSize) / 2;
        int offsetY = (scaledTileSize - scaledPlayerSize) / 2;

        // Draw the player as a colored rectangle
        spriteBatch.Draw(
            _playerTexture,
            new Rectangle(
                screenX + offsetX,
                screenY + offsetY,
                scaledPlayerSize,
                scaledPlayerSize),
            Color.White);

        // Draw a small indicator showing facing direction
        DrawFacingIndicator(spriteBatch, player.Facing, screenX, screenY, scaledTileSize, graphicsDevice);
    }

    /// <summary>
    /// Draws a small indicator showing which direction the player is facing.
    /// </summary>
    private void DrawFacingIndicator(
        SpriteBatch spriteBatch,
        Direction facing,
        int screenX,
        int screenY,
        int scaledTileSize,
        GraphicsDevice graphicsDevice)
    {
        var indicatorTexture = TextureStore.CreateColorTexture(graphicsDevice, Color.White);
        int indicatorSize = (int)(6 * (scaledTileSize / 32f));
        int centerX = screenX + scaledTileSize / 2 - indicatorSize / 2;
        int centerY = screenY + scaledTileSize / 2 - indicatorSize / 2;

        int indicatorX = centerX;
        int indicatorY = centerY;

        int offset = scaledTileSize / 4;

        switch (facing)
        {
            case Direction.Up:
                indicatorY = screenY + offset / 2;
                break;
            case Direction.Down:
                indicatorY = screenY + scaledTileSize - offset / 2 - indicatorSize;
                break;
            case Direction.Left:
                indicatorX = screenX + offset / 2;
                break;
            case Direction.Right:
                indicatorX = screenX + scaledTileSize - offset / 2 - indicatorSize;
                break;
        }

        spriteBatch.Draw(
            indicatorTexture,
            new Rectangle(indicatorX, indicatorY, indicatorSize, indicatorSize),
            Color.White);
    }
}

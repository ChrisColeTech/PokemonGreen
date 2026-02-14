using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Systems;

/// <summary>
/// 2D camera system for viewport management and coordinate transformations.
/// </summary>
public class Camera
{
    private float _x;
    private float _y;
    private float _zoom = 1.0f;

    /// <summary>
    /// Camera center X position in world coordinates.
    /// </summary>
    public float X
    {
        get => _x;
        set => _x = value;
    }

    /// <summary>
    /// Camera center Y position in world coordinates.
    /// </summary>
    public float Y
    {
        get => _y;
        set => _y = value;
    }

    /// <summary>
    /// Viewport width in pixels.
    /// </summary>
    public int ViewportWidth { get; set; }

    /// <summary>
    /// Viewport height in pixels.
    /// </summary>
    public int ViewportHeight { get; set; }

    /// <summary>
    /// Camera zoom level. Default is 1.0 (no zoom).
    /// Values greater than 1.0 zoom in, less than 1.0 zoom out.
    /// </summary>
    public float Zoom
    {
        get => _zoom;
        set => _zoom = MathHelper.Max(0.1f, value);
    }

    /// <summary>
    /// Computed visible area in world coordinates.
    /// </summary>
    public Rectangle Bounds
    {
        get
        {
            float scaledWidth = ViewportWidth / Zoom;
            float scaledHeight = ViewportHeight / Zoom;
            return new Rectangle(
                (int)(X - scaledWidth / 2),
                (int)(Y - scaledHeight / 2),
                (int)scaledWidth,
                (int)scaledHeight
            );
        }
    }

    /// <summary>
    /// Creates a new camera with the specified viewport dimensions.
    /// </summary>
    /// <param name="viewportWidth">Viewport width in pixels.</param>
    /// <param name="viewportHeight">Viewport height in pixels.</param>
    public Camera(int viewportWidth, int viewportHeight)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    /// <summary>
    /// Smoothly follows a target position using linear interpolation.
    /// </summary>
    /// <param name="targetX">Target X position in world coordinates.</param>
    /// <param name="targetY">Target Y position in world coordinates.</param>
    /// <param name="lerp">Interpolation factor (0.0 to 1.0). Lower values = smoother/slower follow.</param>
    public void Follow(float targetX, float targetY, float lerp = 0.1f)
    {
        X = MathHelper.Lerp(X, targetX, lerp);
        Y = MathHelper.Lerp(Y, targetY, lerp);
    }

    /// <summary>
    /// Clamps camera position to prevent showing areas outside the map boundaries.
    /// </summary>
    /// <param name="mapWidth">Map width in tiles.</param>
    /// <param name="mapHeight">Map height in tiles.</param>
    /// <param name="tileSize">Size of each tile in pixels.</param>
    public void ClampToMap(int mapWidth, int mapHeight, int tileSize)
    {
        float worldWidth = mapWidth * tileSize;
        float worldHeight = mapHeight * tileSize;

        float halfViewWidth = (ViewportWidth / Zoom) / 2;
        float halfViewHeight = (ViewportHeight / Zoom) / 2;

        // Clamp X
        if (worldWidth <= ViewportWidth / Zoom)
        {
            // Map is smaller than viewport, center it
            X = worldWidth / 2;
        }
        else
        {
            X = MathHelper.Clamp(X, halfViewWidth, worldWidth - halfViewWidth);
        }

        // Clamp Y
        if (worldHeight <= ViewportHeight / Zoom)
        {
            // Map is smaller than viewport, center it
            Y = worldHeight / 2;
        }
        else
        {
            Y = MathHelper.Clamp(Y, halfViewHeight, worldHeight - halfViewHeight);
        }
    }

    /// <summary>
    /// Converts world coordinates to screen coordinates.
    /// </summary>
    /// <param name="worldX">X position in world coordinates.</param>
    /// <param name="worldY">Y position in world coordinates.</param>
    /// <returns>Screen coordinates as (screenX, screenY).</returns>
    public (int screenX, int screenY) WorldToScreen(float worldX, float worldY)
    {
        float screenX = (worldX - X) * Zoom + ViewportWidth / 2f;
        float screenY = (worldY - Y) * Zoom + ViewportHeight / 2f;
        return ((int)screenX, (int)screenY);
    }

    /// <summary>
    /// Converts screen coordinates to world coordinates.
    /// </summary>
    /// <param name="screenX">X position in screen coordinates.</param>
    /// <param name="screenY">Y position in screen coordinates.</param>
    /// <returns>World coordinates as (worldX, worldY).</returns>
    public (float worldX, float worldY) ScreenToWorld(int screenX, int screenY)
    {
        float worldX = (screenX - ViewportWidth / 2f) / Zoom + X;
        float worldY = (screenY - ViewportHeight / 2f) / Zoom + Y;
        return (worldX, worldY);
    }

    /// <summary>
    /// Updates camera position to follow the player and clamps to map boundaries.
    /// </summary>
    /// <param name="playerX">Player X position in world coordinates.</param>
    /// <param name="playerY">Player Y position in world coordinates.</param>
    /// <param name="mapWidth">Map width in tiles.</param>
    /// <param name="mapHeight">Map height in tiles.</param>
    /// <param name="tileSize">Size of each tile in pixels.</param>
    public void Update(float playerX, float playerY, int mapWidth, int mapHeight, int tileSize)
    {
        Follow(playerX, playerY);
        ClampToMap(mapWidth, mapHeight, tileSize);
    }

    /// <summary>
    /// Gets the transformation matrix for use with SpriteBatch.
    /// </summary>
    /// <returns>Transformation matrix combining translation and scale.</returns>
    public Matrix GetTransformMatrix()
    {
        return Matrix.CreateTranslation(-X, -Y, 0) *
               Matrix.CreateScale(Zoom, Zoom, 1) *
               Matrix.CreateTranslation(ViewportWidth / 2f, ViewportHeight / 2f, 0);
    }
}

using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Systems;

/// <summary>
/// Follows the player, clamps to map edges, and produces a transform matrix
/// for SpriteBatch rendering. Zoom is set once on map load and does not change
/// on window resize. Position (X, Y) is the world-space point at screen center.
/// </summary>
public class Camera
{
    private float _zoom = 3.0f;

    public float X { get; set; }
    public float Y { get; set; }

    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }

    public float Zoom
    {
        get => _zoom;
        set => _zoom = MathHelper.Max(0.1f, value);
    }

    /// <summary>
    /// The world-space rectangle currently visible on screen, accounting for zoom.
    /// Used by TileRenderer for culling.
    /// </summary>
    public Rectangle Bounds
    {
        get
        {
            float scaledWidth = ViewportWidth / Zoom;
            float scaledHeight = ViewportHeight / Zoom;
            return new Rectangle(
                (int)(X - scaledWidth / 2f),
                (int)(Y - scaledHeight / 2f),
                (int)scaledWidth,
                (int)scaledHeight);
        }
    }

    public Camera(int viewportWidth, int viewportHeight)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    /// <summary>
    /// Centers the camera on the given world-space target.
    /// </summary>
    public void Follow(float targetX, float targetY)
    {
        X = targetX;
        Y = targetY;
    }

    /// <summary>
    /// Prevents the camera from showing space beyond the map edges.
    /// If the map is smaller than the viewport (at current zoom), centers the camera on the map.
    /// </summary>
    public void ClampToMap(int mapWidth, int mapHeight, int tileSize)
    {
        float worldWidth = mapWidth * tileSize;
        float worldHeight = mapHeight * tileSize;

        float halfViewWidth = ViewportWidth / Zoom / 2f;
        float halfViewHeight = ViewportHeight / Zoom / 2f;

        if (worldWidth <= ViewportWidth / Zoom)
            X = worldWidth / 2f;
        else
            X = MathHelper.Clamp(X, halfViewWidth, worldWidth - halfViewWidth);

        if (worldHeight <= ViewportHeight / Zoom)
            Y = worldHeight / 2f;
        else
            Y = MathHelper.Clamp(Y, halfViewHeight, worldHeight - halfViewHeight);
    }

    /// <summary>
    /// Follows the player then clamps to map bounds. Called every frame by GameWorld.
    /// </summary>
    public void Update(float playerX, float playerY, int mapWidth, int mapHeight, int tileSize)
    {
        Follow(playerX, playerY);
        ClampToMap(mapWidth, mapHeight, tileSize);
    }

    /// <summary>
    /// Produces the transform matrix passed to SpriteBatch.Begin().
    /// Translates by negative camera position, scales by zoom, then re-centers on the viewport.
    /// </summary>
    public Matrix GetTransformMatrix()
    {
        return Matrix.CreateTranslation(-X, -Y, 0f) *
               Matrix.CreateScale(Zoom, Zoom, 1f) *
               Matrix.CreateTranslation(ViewportWidth / 2f, ViewportHeight / 2f, 0f);
    }

    /// <summary>
    /// Converts a world-space position to screen-space pixel coordinates.
    /// </summary>
    public (int screenX, int screenY) WorldToScreen(float worldX, float worldY)
    {
        float screenX = (worldX - X) * Zoom + ViewportWidth / 2f;
        float screenY = (worldY - Y) * Zoom + ViewportHeight / 2f;
        return ((int)screenX, (int)screenY);
    }

    /// <summary>
    /// Converts screen-space pixel coordinates to a world-space position.
    /// </summary>
    public (float worldX, float worldY) ScreenToWorld(int screenX, int screenY)
    {
        float worldX = (screenX - ViewportWidth / 2f) / Zoom + X;
        float worldY = (screenY - ViewportHeight / 2f) / Zoom + Y;
        return (worldX, worldY);
    }
}

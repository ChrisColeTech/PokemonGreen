using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Core.Graphics;

public class Camera
{
    public Vector2 Position { get; private set; }
    public float Zoom { get; private set; } = 2.0f;
    public Matrix Transform { get; private set; }
    
    private const float MinZoom = 0.75f;
    private const float MaxZoom = 4.0f;

    public void Update(Vector2 targetPosition, int mapWidth, int mapHeight, int tileSize, Viewport viewport)
    {
        var mapPixelWidth = mapWidth * tileSize;
        var mapPixelHeight = mapHeight * tileSize;

        var halfViewWidth = viewport.Width / (2f * Zoom);
        var halfViewHeight = viewport.Height / (2f * Zoom);

        var minCameraX = halfViewWidth;
        var maxCameraX = mapPixelWidth - halfViewWidth;
        var minCameraY = halfViewHeight;
        var maxCameraY = mapPixelHeight - halfViewHeight;

        float newPosX, newPosY;

        if (mapPixelWidth <= (halfViewWidth * 2f))
        {
            newPosX = mapPixelWidth / 2f;
        }
        else
        {
            newPosX = Math.Clamp(targetPosition.X, minCameraX, maxCameraX);
        }

        if (mapPixelHeight <= (halfViewHeight * 2f))
        {
            newPosY = mapPixelHeight / 2f;
        }
        else
        {
            newPosY = Math.Clamp(targetPosition.Y, minCameraY, maxCameraY);
        }

        Position = new Vector2(newPosX, newPosY);

        Transform =
            Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
            * Matrix.CreateScale(Zoom, Zoom, 1f)
            * Matrix.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f);
    }

    public void ZoomIn(float amount = 0.02f)
    {
        Zoom = Math.Clamp(Zoom + amount, MinZoom, MaxZoom);
    }

    public void ZoomOut(float amount = 0.02f)
    {
        Zoom = Math.Clamp(Zoom - amount, MinZoom, MaxZoom);
    }
}
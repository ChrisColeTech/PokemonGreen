using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Core.Graphics;

public class Camera
{
    public Vector2 Position { get; private set; }
    public float Zoom { get; private set; } = 1.0f;
    public Matrix Transform { get; private set; }
    public bool AutoScale { get; set; } = true;

    public void Update(Vector2 targetPosition, int mapWidth, int mapHeight, int tileSize, Viewport viewport)
    {
        var mapPixelWidth = (float)mapWidth * tileSize;
        var mapPixelHeight = (float)mapHeight * tileSize;

        if (AutoScale)
        {
            var zoomX = viewport.Width / mapPixelWidth;
            var zoomY = viewport.Height / mapPixelHeight;
            Zoom = Math.Min(zoomX, zoomY);
        }

        var halfViewWidth = viewport.Width / (2f * Zoom);
        var halfViewHeight = viewport.Height / (2f * Zoom);

        float newPosX, newPosY;

        if (mapPixelWidth <= viewport.Width / Zoom)
        {
            newPosX = mapPixelWidth / 2f;
        }
        else
        {
            var minCameraX = halfViewWidth;
            var maxCameraX = mapPixelWidth - halfViewWidth;
            newPosX = Math.Clamp(targetPosition.X, minCameraX, maxCameraX);
        }

        if (mapPixelHeight <= viewport.Height / Zoom)
        {
            newPosY = mapPixelHeight / 2f;
        }
        else
        {
            var minCameraY = halfViewHeight;
            var maxCameraY = mapPixelHeight - halfViewHeight;
            newPosY = Math.Clamp(targetPosition.Y, minCameraY, maxCameraY);
        }

        Position = new Vector2(newPosX, newPosY);

        Transform =
            Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
            * Matrix.CreateScale(Zoom, Zoom, 1f)
            * Matrix.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f);
    }

    public void ZoomIn(float amount = 0.5f) { }
    public void ZoomOut(float amount = 0.5f) { }
    public void ResetZoom() { }
}
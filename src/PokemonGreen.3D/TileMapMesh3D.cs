#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Maps;

namespace PokemonGreen._3D;

/// <summary>
/// Generates a 3D mesh from 2D MapDefinitions.
/// Supports building an entire world (multiple maps placed by their WorldX/WorldY grid positions).
/// Each tile becomes a colored quad; non-walkable overlays become extruded blocks.
/// </summary>
public sealed class TileMapMesh3D
{
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private int _triangleCount;
    private int _vertexCount;

    /// <summary>Cached list of maps for collision/encounter lookups.</summary>
    private List<MapDefinition> _worldMaps = new();

    /// <summary>World-space size of each tile.</summary>
    public float TileWorldSize { get; set; } = 2f;

    /// <summary>Height of extruded overlay blocks (walls, trees, rocks).</summary>
    public float BlockHeight { get; set; } = 1.2f;

    /// <summary>Y offset for the ground plane.</summary>
    public float GroundY { get; set; } = 0f;

    /// <summary>World-space origin offset so the world can be centered.</summary>
    public Vector3 Origin { get; set; } = Vector3.Zero;

    public int TriangleCount => _triangleCount;

    /// <summary>
    /// Build the mesh for all maps in a world. Maps are placed at their
    /// WorldX/WorldY grid positions, each offset by mapWidth * tileSize.
    /// </summary>
    public void BuildWorld(GraphicsDevice device, string worldId)
    {
        MapRegistry.Initialize();

        var maps = MapCatalog.GetAllMaps()
            .Where(m => string.Equals(m.WorldId, worldId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (maps.Count == 0)
        {
            Console.WriteLine($"[TileMapMesh3D] No maps found for world '{worldId}'");
            return;
        }

        _worldMaps = maps;

        var verts = new List<VertexPositionColor>();
        var indices = new List<int>();

        foreach (var map in maps)
        {
            // Each map is offset by its grid position × map size in tiles × tile world size
            float offsetX = map.WorldX * map.Width * TileWorldSize;
            float offsetZ = map.WorldY * map.Height * TileWorldSize;

            BuildMap(map, offsetX, offsetZ, verts, indices);

            Console.WriteLine($"[TileMapMesh3D] Added '{map.Name}' at grid ({map.WorldX},{map.WorldY}) → world offset ({offsetX:F0},{offsetZ:F0})");
        }

        // Upload to GPU buffers
        if (verts.Count > 0)
        {
            _vertexBuffer = new VertexBuffer(device, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());

            _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());

            _vertexCount = verts.Count;
            _triangleCount = indices.Count / 3;
        }

        Console.WriteLine($"[TileMapMesh3D] World '{worldId}': {maps.Count} maps, {_vertexCount} verts, {_triangleCount} triangles");
    }

    /// <summary>
    /// Build the mesh for a single map at the given world-space offset.
    /// </summary>
    public void Build(GraphicsDevice device, MapDefinition map)
    {
        var verts = new List<VertexPositionColor>();
        var indices = new List<int>();

        BuildMap(map, 0f, 0f, verts, indices);

        if (verts.Count > 0)
        {
            _vertexBuffer = new VertexBuffer(device, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());

            _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());

            _vertexCount = verts.Count;
            _triangleCount = indices.Count / 3;
        }
    }

    private void BuildMap(MapDefinition map, float offsetX, float offsetZ,
        List<VertexPositionColor> verts, List<int> indices)
    {
        for (int ty = 0; ty < map.Height; ty++)
        {
            for (int tx = 0; tx < map.Width; tx++)
            {
                float wx = offsetX + tx * TileWorldSize;
                float wz = offsetZ + ty * TileWorldSize;

                int baseTileId = map.GetBaseTile(tx, ty);
                var baseDef = TileRegistry.GetTile(baseTileId);
                Color baseColor = baseDef != null ? ParseHexColor(baseDef.Color) : Color.Magenta;

                // Ground quad for every tile
                AddQuad(verts, indices, wx, wz, TileWorldSize, GroundY, baseColor);

                // Overlay
                int? overlayId = map.GetOverlayTile(tx, ty);
                if (overlayId is int oid)
                {
                    var overlayDef = TileRegistry.GetTile(oid);
                    if (overlayDef != null)
                    {
                        Color overlayColor = ParseHexColor(overlayDef.Color);

                        if (!overlayDef.Walkable)
                        {
                            AddBlock(verts, indices, wx, wz, TileWorldSize,
                                GroundY, BlockHeight, overlayColor);
                        }
                        else
                        {
                            // Walkable overlay — slightly raised tinted quad
                            AddQuad(verts, indices, wx, wz, TileWorldSize,
                                GroundY + 0.02f, overlayColor);
                        }
                    }
                }
            }
        }
    }

    /// <summary>Draw the world mesh.</summary>
    public void Draw(GraphicsDevice device, BasicEffect effect)
    {
        if (_vertexBuffer == null || _indexBuffer == null || _triangleCount == 0) return;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        effect.World = Matrix.CreateTranslation(Origin);

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triangleCount);
        }
    }

    /// <summary>
    /// Get the world-space center of the map grid, useful for spawning the player.
    /// </summary>
    public static Vector3 GetWorldCenter(string worldId, float tileWorldSize, float groundY = 0f)
    {
        var maps = MapCatalog.GetAllMaps()
            .Where(m => string.Equals(m.WorldId, worldId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (maps.Count == 0) return Vector3.Zero;

        // Find bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var map in maps)
        {
            float x0 = map.WorldX * map.Width * tileWorldSize;
            float z0 = map.WorldY * map.Height * tileWorldSize;
            float x1 = x0 + map.Width * tileWorldSize;
            float z1 = z0 + map.Height * tileWorldSize;

            minX = MathF.Min(minX, x0);
            maxX = MathF.Max(maxX, x1);
            minZ = MathF.Min(minZ, z0);
            maxZ = MathF.Max(maxZ, z1);
        }

        return new Vector3((minX + maxX) / 2f, groundY, (minZ + maxZ) / 2f);
    }

    // ── Collision & encounter queries ─────────────────────────────

    /// <summary>
    /// Resolve a world-space position to the map and tile coordinates.
    /// Returns false if the position is outside all maps.
    /// </summary>
    public bool TryGetTile(float worldX, float worldZ, out MapDefinition? map, out int tileX, out int tileY)
    {
        foreach (var m in _worldMaps)
        {
            float mapOriginX = m.WorldX * m.Width * TileWorldSize;
            float mapOriginZ = m.WorldY * m.Height * TileWorldSize;

            float localX = worldX - mapOriginX;
            float localZ = worldZ - mapOriginZ;

            int tx = (int)MathF.Floor(localX / TileWorldSize);
            int ty = (int)MathF.Floor(localZ / TileWorldSize);

            if (tx >= 0 && tx < m.Width && ty >= 0 && ty < m.Height)
            {
                map = m;
                tileX = tx;
                tileY = ty;
                return true;
            }
        }

        map = null;
        tileX = 0;
        tileY = 0;
        return false;
    }

    /// <summary>
    /// Check if a world-space position is walkable.
    /// Returns false if out of bounds or on a non-walkable tile.
    /// </summary>
    public bool IsWalkable(float worldX, float worldZ)
    {
        if (!TryGetTile(worldX, worldZ, out var map, out int tx, out int ty))
            return false; // outside all maps = blocked

        // Check base tile
        int baseTileId = map!.GetBaseTile(tx, ty);
        var baseDef = TileRegistry.GetTile(baseTileId);
        if (baseDef == null || !baseDef.Walkable)
            return false;

        // Check overlay tile
        int? overlayId = map.GetOverlayTile(tx, ty);
        if (overlayId is int oid)
        {
            var overlayDef = TileRegistry.GetTile(oid);
            if (overlayDef != null && !overlayDef.Walkable)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get the overlay behavior string at a world position (e.g. "wild_encounter").
    /// Returns null if no overlay or no behavior.
    /// </summary>
    public string? GetOverlayBehavior(float worldX, float worldZ)
    {
        if (!TryGetTile(worldX, worldZ, out var map, out int tx, out int ty))
            return null;

        int? overlayId = map!.GetOverlayTile(tx, ty);
        if (overlayId is int oid)
        {
            var overlayDef = TileRegistry.GetTile(oid);
            return overlayDef?.OverlayBehavior;
        }

        return null;
    }

    // ── Geometry helpers ──────────────────────────────────────────

    private static void AddQuad(
        List<VertexPositionColor> verts, List<int> indices,
        float x, float z, float size, float y, Color color)
    {
        int baseIndex = verts.Count;

        verts.Add(new VertexPositionColor(new Vector3(x, y, z), color));
        verts.Add(new VertexPositionColor(new Vector3(x + size, y, z), color));
        verts.Add(new VertexPositionColor(new Vector3(x + size, y, z + size), color));
        verts.Add(new VertexPositionColor(new Vector3(x, y, z + size), color));

        indices.Add(baseIndex);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }

    private static void AddBlock(
        List<VertexPositionColor> verts, List<int> indices,
        float x, float z, float size, float groundY, float height, Color color)
    {
        float x0 = x, x1 = x + size;
        float z0 = z, z1 = z + size;
        float y0 = groundY, y1 = groundY + height;

        var topColor = color;
        var sideColor = new Color(
            (int)(color.R * 0.7f), (int)(color.G * 0.7f), (int)(color.B * 0.7f));
        var darkColor = new Color(
            (int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));

        // Top
        AddFace(verts, indices,
            new Vector3(x0, y1, z0), new Vector3(x1, y1, z0),
            new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), topColor);
        // Front (z1)
        AddFace(verts, indices,
            new Vector3(x0, y0, z1), new Vector3(x1, y0, z1),
            new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), sideColor);
        // Back (z0)
        AddFace(verts, indices,
            new Vector3(x1, y0, z0), new Vector3(x0, y0, z0),
            new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), sideColor);
        // Right (x1)
        AddFace(verts, indices,
            new Vector3(x1, y0, z1), new Vector3(x1, y0, z0),
            new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), darkColor);
        // Left (x0)
        AddFace(verts, indices,
            new Vector3(x0, y0, z0), new Vector3(x0, y0, z1),
            new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), darkColor);
    }

    private static void AddFace(
        List<VertexPositionColor> verts, List<int> indices,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        int baseIndex = verts.Count;
        verts.Add(new VertexPositionColor(a, color));
        verts.Add(new VertexPositionColor(b, color));
        verts.Add(new VertexPositionColor(c, color));
        verts.Add(new VertexPositionColor(d, color));

        indices.Add(baseIndex);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }

    private static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Color.Magenta;

        ReadOnlySpan<char> span = hex.AsSpan();
        if (span[0] == '#') span = span.Slice(1);

        if (span.Length == 8)
        {
            int r = int.Parse(span.Slice(0, 2), NumberStyles.HexNumber);
            int g = int.Parse(span.Slice(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(span.Slice(4, 2), NumberStyles.HexNumber);
            int a = int.Parse(span.Slice(6, 2), NumberStyles.HexNumber);
            return new Color(r, g, b, a);
        }

        if (span.Length >= 6)
        {
            int r = int.Parse(span.Slice(0, 2), NumberStyles.HexNumber);
            int g = int.Parse(span.Slice(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(span.Slice(4, 2), NumberStyles.HexNumber);
            return new Color(r, g, b);
        }

        return Color.Magenta;
    }
}

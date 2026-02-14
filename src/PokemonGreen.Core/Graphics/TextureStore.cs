using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Core.Graphics;

public class TextureStore
{
    public Texture2D Grass { get; set; } = null!;
    public Texture2D[] WaterFrames { get; set; } = new Texture2D[4];
    public Texture2D Path { get; set; } = null!;
    public Texture2D Tree { get; set; } = null!;
    public Texture2D Rock { get; set; } = null!;
    public Texture2D Flower { get; set; } = null!;
    public Texture2D Interactive { get; set; } = null!;
    public Texture2D Entity { get; set; } = null!;
    public Texture2D Trainer { get; set; } = null!;
    public Texture2D Statue { get; set; } = null!;
    public Texture2D PlayerWalk { get; set; } = null!;
    public Texture2D PlayerIdle { get; set; } = null!;
    public Texture2D PlayerRun { get; set; } = null!;
    public Texture2D PlayerJump { get; set; } = null!;
    public Texture2D PlayerClimb { get; set; } = null!;
    public Texture2D PlayerCombat { get; set; } = null!;
    public Texture2D PlayerSpellcast { get; set; } = null!;
    public Texture2D Pixel { get; set; } = null!;
    
    public Dictionary<int, Texture2D> Items { get; } = new();

    public void Load(ContentManager content, GraphicsDevice graphicsDevice)
    {
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });

        Grass = LoadTextureOrFallback(content, "Sprites/tile_grass", Pixel);
        var staticWaterTexture = LoadTextureOrFallback(content, "Sprites/tile_water", Pixel);
        WaterFrames[0] = LoadTextureOrFallback(content, "Sprites/tile_water_0", staticWaterTexture);
        WaterFrames[1] = LoadTextureOrFallback(content, "Sprites/tile_water_1", staticWaterTexture);
        WaterFrames[2] = LoadTextureOrFallback(content, "Sprites/tile_water_2", staticWaterTexture);
        WaterFrames[3] = LoadTextureOrFallback(content, "Sprites/tile_water_3", staticWaterTexture);
        Path = LoadTextureOrFallback(content, "Sprites/tile_path", Grass);
        Tree = LoadTextureOrFallback(content, "Sprites/tile_tree", Grass);

        Rock = LoadTextureOrFallback(content, "Sprites/tile_rock", Path);
        Flower = LoadTextureOrFallback(content, "Sprites/tile_flower", Grass);
        Interactive = LoadTextureOrFallback(content, "Sprites/tile_interactive", Path);
        Entity = LoadTextureOrFallback(content, "Sprites/tile_entity", Path);
        Trainer = LoadTextureOrFallback(content, "Sprites/tile_trainer", Entity);
        Statue = LoadTextureOrFallback(content, "Sprites/tile_statue", Rock);

        PlayerWalk = LoadTextureOrFallback(content, "Sprites/player_walk", Pixel);
        PlayerIdle = LoadTextureOrFallback(content, "Sprites/player_idle", Pixel);
        PlayerRun = LoadTextureOrFallback(content, "Sprites/player_run", Pixel);
        PlayerJump = LoadTextureOrFallback(content, "Sprites/player_jump", PlayerWalk);
        PlayerClimb = LoadTextureOrFallback(content, "Sprites/player_climb", PlayerWalk);
        PlayerCombat = LoadTextureOrFallback(content, "Sprites/player_combat", PlayerWalk);
        PlayerSpellcast = LoadTextureOrFallback(content, "Sprites/player_spellcast", PlayerWalk);

        LoadItems(content);
    }

    private void LoadItems(ContentManager content)
    {
        var itemFiles = new Dictionary<int, string>
        {
            [51] = "pokeball",
            [52] = "greatball",
            [53] = "ultraball",
            [54] = "potion",
            [55] = "superpotion",
            [56] = "stone_fire",
            [57] = "stone_water",
            [58] = "stone_thunder",
            [59] = "stone_leaf",
            [60] = "stone_moon",
            [61] = "crystal_red",
            [62] = "crystal_blue",
            [63] = "crystal_green",
            [64] = "fruit_apple",
            [65] = "berry_red",
            [66] = "berry_blue",
            [67] = "herb_green",
            [68] = "megaring",
            [69] = "masterball",
            [70] = "crystal_yellow",
        };

        Console.WriteLine($"[TextureStore] Loading {itemFiles.Count} items...");
        
        foreach (var (tileId, fileName) in itemFiles)
        {
            var texture = LoadTextureOrFallback(content, $"Sprites/Items/{fileName}", Entity);
            Items[tileId] = texture;
            
            var isFallback = texture == Entity;
            Console.WriteLine($"[TextureStore] Item {tileId} ({fileName}): {(isFallback ? "FALLBACK" : "LOADED")}");
        }
    }

    private Texture2D LoadTextureOrFallback(ContentManager content, string contentPath, Texture2D fallbackTexture)
    {
        try
        {
            return content.Load<Texture2D>(contentPath);
        }
        catch (System.Exception)
        {
            return fallbackTexture;
        }
    }
}
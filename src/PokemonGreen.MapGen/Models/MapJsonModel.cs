namespace PokemonGreen.MapGen.Models;

public class MapJsonModel
{
    public int Version { get; set; }
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int[][] Tiles { get; set; } = [];
}

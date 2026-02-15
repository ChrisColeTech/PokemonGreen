using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokemonGreen.Core.NPCs;

public class NPCData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = "";
    
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    
    [JsonPropertyName("frameWidth")]
    public int FrameWidth { get; set; }
    
    [JsonPropertyName("frameHeight")]
    public int FrameHeight { get; set; }
    
    [JsonPropertyName("framesPerDirection")]
    public int FramesPerDirection { get; set; }
    
    [JsonPropertyName("animations")]
    public string[] Animations { get; set; } = System.Array.Empty<string>();
}

using System;
using System.Collections.Generic;
using PokemonGreen.Core.Items;
using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Save;

/// <summary>
/// Snapshot of all player state for save/load.
/// Gameplay operates on live objects; this is only used at save/load boundaries.
/// </summary>
public class GameSaveData
{
    // Player profile
    public string PlayerName { get; set; } = "Red";
    public int Money { get; set; }
    public double PlaytimeSeconds { get; set; }

    // World position
    public string MapId { get; set; } = "";
    public float PlayerX { get; set; }
    public float PlayerY { get; set; }
    public int Facing { get; set; } = 1; // Direction enum ordinal

    // 3D POC: selected character model folder
    public string? SelectedCharacter { get; set; }

    // Progress
    public int BadgeCount { get; set; }
    public HashSet<string> StoryFlags { get; set; } = new();

    // Pokemon
    public Party Party { get; set; } = new();
    public PCBoxes PCBoxes { get; set; } = new();

    // Inventory
    public PlayerInventory Inventory { get; set; } = new();

    // Time
    public double GameTimeSeconds { get; set; }
    public DateTime SavedAt { get; set; }
}

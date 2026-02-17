#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
namespace PokemonGreen.Core.Save;

/// <summary>
/// Wraps SaveManager for the 3D POC — loads/saves cube state,
/// character selection, and story flags to a dedicated save slot.
/// </summary>
public class PersistenceManager3D
{
    private readonly SaveManager _saveManager = new();
    private const int SaveSlot = 99; // dedicated slot for 3D POC

    /// <summary>Story flags persisted across sessions.</summary>
    public HashSet<string> StoryFlags { get; private set; } = new();

    /// <summary>The last-saved character folder name (null if never saved).</summary>
    public string? RestoredCharacterFolder { get; private set; }

    /// <summary>
    /// Load save data from disk. Returns the number of cubes that were
    /// already collected (so the caller can sync cube system state).
    /// </summary>
    public void Load()
    {
        var saveData = _saveManager.Load(SaveSlot);
        if (saveData != null)
        {
            StoryFlags = saveData.StoryFlags;
            RestoredCharacterFolder = saveData.SelectedCharacter;
            Console.WriteLine($"[Save] Loaded save from slot {SaveSlot}");
        }
        else
        {
            StoryFlags = new HashSet<string>();
            Console.WriteLine("[Save] No save found, starting fresh");
        }
    }

    /// <summary>
    /// Persist current state to disk.
    /// </summary>
    public void Save(Vector3 playerPosition, string currentCharacterFolder, int cubeCount)
    {
        var data = new GameSaveData
        {
            PlayerName = "Red",
            MapId = "3d_overworld",
            PlayerX = playerPosition.X,
            PlayerY = playerPosition.Z, // map Y = world Z
            SelectedCharacter = currentCharacterFolder,
            StoryFlags = StoryFlags,
            SavedAt = DateTime.UtcNow,
        };
        _saveManager.Save(SaveSlot, data);
        Console.WriteLine($"[Save] Saved {cubeCount} cubes to slot {SaveSlot}");
    }
}

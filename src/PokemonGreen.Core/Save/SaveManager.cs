using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using PokemonGreen.Core.Items;
using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Save;

/// <summary>
/// Summary info about an existing save slot, read without loading full data.
/// </summary>
public class SaveSlotInfo
{
    public int Slot { get; set; }
    public string PlayerName { get; set; } = "";
    public int BadgeCount { get; set; }
    public int PartyCount { get; set; }
    public DateTime SavedAt { get; set; }
    public string MapId { get; set; } = "";
}

/// <summary>
/// SQLite-backed persistence service. Supports multiple save slots.
/// Each slot is a separate .db file: save1.db, save2.db, save3.db.
/// </summary>
public class SaveManager : IDisposable
{
    private const int SchemaVersion = 3;

    public static string SaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PokemonGreen", "Saves");

    public static string GetSavePath(int slot) =>
        Path.Combine(SaveDirectory, $"save{slot}.db");

    public static bool HasSave(int slot) => File.Exists(GetSavePath(slot));

    /// <summary>
    /// List all existing save slots with summary info (name, badges, timestamp).
    /// Scans the save directory for any save*.db files.
    /// </summary>
    public List<SaveSlotInfo> GetSaveSlots()
    {
        var results = new List<SaveSlotInfo>();

        if (!Directory.Exists(SaveDirectory))
            return results;

        foreach (var file in Directory.GetFiles(SaveDirectory, "save*.db"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!fileName.StartsWith("save") || !int.TryParse(fileName.AsSpan(4), out int slot))
                continue;

            try
            {
                using var conn = new SqliteConnection($"Data Source={file};Mode=ReadOnly");
                conn.Open();

                var info = new SaveSlotInfo { Slot = slot };

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name, badge_count, map_id, saved_at FROM player WHERE id = 1";
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        info.PlayerName = reader.GetString(0);
                        info.BadgeCount = reader.GetInt32(1);
                        info.MapId = reader.GetString(2);
                        info.SavedAt = DateTime.Parse(reader.GetString(3));
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM pokemon WHERE party_slot IS NOT NULL";
                    info.PartyCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                results.Add(info);
            }
            catch
            {
                // Corrupt save file — skip it
            }
        }

        results.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return results;
    }

    /// <summary>
    /// Returns the lowest unused slot number (starting from 1).
    /// </summary>
    public int NextAvailableSlot()
    {
        var existing = GetSaveSlots();
        var used = new HashSet<int>(existing.Count);
        foreach (var s in existing)
            used.Add(s.Slot);

        int slot = 1;
        while (used.Contains(slot))
            slot++;
        return slot;
    }

    // ── Save ─────────────────────────────────────────────────────────

    public void Save(int slot, GameSaveData data)
    {
        Directory.CreateDirectory(SaveDirectory);

        string path = GetSavePath(slot);
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        EnsureSchema(conn);

        using var tx = conn.BeginTransaction();

        // Clear all mutable tables
        Exec(conn, "DELETE FROM pokemon");
        Exec(conn, "DELETE FROM pc_boxes");
        Exec(conn, "DELETE FROM inventory");
        Exec(conn, "DELETE FROM story_flags");

        // Player
        Exec(conn, @"INSERT OR REPLACE INTO player
            (id, name, money, playtime_seconds, map_id, player_x, player_y, facing, selected_character, badge_count, game_time_seconds, saved_at)
            VALUES (1, @name, @money, @playtime, @map, @px, @py, @facing, @selchar, @badges, @gametime, @saved)",
            ("@name", data.PlayerName),
            ("@money", data.Money),
            ("@playtime", data.PlaytimeSeconds),
            ("@map", data.MapId),
            ("@px", data.PlayerX),
            ("@py", data.PlayerY),
            ("@facing", data.Facing),
            ("@selchar", (object?)data.SelectedCharacter ?? DBNull.Value),
            ("@badges", data.BadgeCount),
            ("@gametime", data.GameTimeSeconds),
            ("@saved", data.SavedAt.ToString("o")));

        // Party Pokemon
        for (int i = 0; i < data.Party.Count; i++)
            WritePokemon(conn, data.Party[i], partySlot: i, boxNumber: null, boxSlot: null);

        // PC Pokemon
        for (int b = 0; b < PCBoxes.NumBoxes; b++)
        {
            var box = data.PCBoxes[b];
            Exec(conn, "INSERT INTO pc_boxes (box_number, name) VALUES (@bn, @name)",
                ("@bn", b), ("@name", box.Name));

            for (int s = 0; s < PCBox.Capacity; s++)
            {
                var pkmn = box[s];
                if (pkmn != null)
                    WritePokemon(conn, pkmn, partySlot: null, boxNumber: b, boxSlot: s);
            }
        }

        // Inventory
        foreach (var (category, items) in data.Inventory.GetAllPouches())
        {
            foreach (var itemSlot in items)
            {
                Exec(conn, "INSERT INTO inventory (item_id, category, quantity) VALUES (@id, @cat, @qty)",
                    ("@id", itemSlot.ItemId), ("@cat", (int)category), ("@qty", itemSlot.Quantity));
            }
        }

        // Story flags
        foreach (var flag in data.StoryFlags)
            Exec(conn, "INSERT INTO story_flags (flag) VALUES (@f)", ("@f", flag));

        tx.Commit();
    }

    // ── Load ─────────────────────────────────────────────────────────

    public GameSaveData? Load(int slot)
    {
        string path = GetSavePath(slot);
        if (!File.Exists(path))
            return null;

        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        EnsureSchema(conn);

        var data = new GameSaveData();

        // Player
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM player WHERE id = 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            data.PlayerName = reader.GetString(reader.GetOrdinal("name"));
            data.Money = reader.GetInt32(reader.GetOrdinal("money"));
            data.PlaytimeSeconds = reader.GetDouble(reader.GetOrdinal("playtime_seconds"));
            data.MapId = reader.GetString(reader.GetOrdinal("map_id"));
            data.PlayerX = reader.GetFloat(reader.GetOrdinal("player_x"));
            data.PlayerY = reader.GetFloat(reader.GetOrdinal("player_y"));
            data.Facing = reader.GetInt32(reader.GetOrdinal("facing"));
            var selCharOrd = reader.GetOrdinal("selected_character");
            data.SelectedCharacter = reader.IsDBNull(selCharOrd) ? null : reader.GetString(selCharOrd);
            data.BadgeCount = reader.GetInt32(reader.GetOrdinal("badge_count"));
            data.GameTimeSeconds = reader.GetDouble(reader.GetOrdinal("game_time_seconds"));
            var savedAtStr = reader.GetString(reader.GetOrdinal("saved_at"));
            data.SavedAt = DateTime.Parse(savedAtStr);
        }

        // Party Pokemon
        data.Party = new Party();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM pokemon WHERE party_slot IS NOT NULL ORDER BY party_slot";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.Party.Add(ReadPokemon(reader));
        }

        // PC Boxes
        data.PCBoxes = new PCBoxes();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT box_number, name FROM pc_boxes ORDER BY box_number";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int bn = reader.GetInt32(0);
                string name = reader.GetString(1);
                if (bn >= 0 && bn < PCBoxes.NumBoxes)
                    data.PCBoxes[bn].Name = name;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM pokemon WHERE box_number IS NOT NULL ORDER BY box_number, box_slot";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int bn = reader.GetInt32(reader.GetOrdinal("box_number"));
                int bs = reader.GetInt32(reader.GetOrdinal("box_slot"));
                var pkmn = ReadPokemon(reader);
                if (bn >= 0 && bn < PCBoxes.NumBoxes)
                    data.PCBoxes[bn].StoreAt(bs, pkmn);
            }
        }

        // Inventory
        data.Inventory = new PlayerInventory();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT item_id, quantity FROM inventory";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int itemId = reader.GetInt32(0);
                int qty = reader.GetInt32(1);
                data.Inventory.AddItem(itemId, qty);
            }
        }

        // Story flags
        data.StoryFlags = new HashSet<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT flag FROM story_flags";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                data.StoryFlags.Add(reader.GetString(0));
        }

        return data;
    }

    // ── Delete ────────────────────────────────────────────────────────

    public void DeleteSave(int slot)
    {
        string path = GetSavePath(slot);
        if (File.Exists(path))
            File.Delete(path);
    }

    // ── Schema ────────────────────────────────────────────────────────

    private static void EnsureSchema(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='schema_version'";
        var result = check.ExecuteScalar();

        if (result == null)
        {
            CreateSchema(conn);
            return;
        }

        using var versionCmd = conn.CreateCommand();
        versionCmd.CommandText = "SELECT version FROM schema_version LIMIT 1";
        var version = Convert.ToInt32(versionCmd.ExecuteScalar());

        if (version < SchemaVersion)
            MigrateSchema(conn, version);
    }

    private static void CreateSchema(SqliteConnection conn)
    {
        Exec(conn, "CREATE TABLE schema_version (version INTEGER NOT NULL)");
        Exec(conn, $"INSERT INTO schema_version (version) VALUES ({SchemaVersion})");

        Exec(conn, @"CREATE TABLE player (
            id               INTEGER PRIMARY KEY CHECK (id = 1),
            name             TEXT    NOT NULL DEFAULT 'Red',
            money            INTEGER NOT NULL DEFAULT 0,
            playtime_seconds REAL    NOT NULL DEFAULT 0.0,
            map_id           TEXT    NOT NULL,
            player_x         REAL    NOT NULL,
            player_y         REAL    NOT NULL,
            facing           INTEGER NOT NULL DEFAULT 1,
            selected_character TEXT,
            badge_count      INTEGER NOT NULL DEFAULT 0,
            game_time_seconds REAL   NOT NULL DEFAULT 900.0,
            saved_at         TEXT    NOT NULL
        )");

        Exec(conn, @"CREATE TABLE pokemon (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            nickname     TEXT    NOT NULL,
            species_id   INTEGER NOT NULL,
            level        INTEGER NOT NULL,
            current_hp   INTEGER NOT NULL,
            max_hp       INTEGER NOT NULL,
            gender       INTEGER NOT NULL,
            status       TEXT,
            held_item_id INTEGER,
            attack       INTEGER NOT NULL,
            defense      INTEGER NOT NULL,
            sp_attack    INTEGER NOT NULL,
            sp_defense   INTEGER NOT NULL,
            speed        INTEGER NOT NULL,
            experience   INTEGER NOT NULL,
            growth_rate  INTEGER NOT NULL,
            ivs          TEXT    NOT NULL,
            evs          TEXT    NOT NULL,
            move_ids     TEXT    NOT NULL,
            move_pps     TEXT    NOT NULL,
            party_slot   INTEGER,
            box_number   INTEGER,
            box_slot     INTEGER,
            CHECK (
                (party_slot IS NOT NULL AND box_number IS NULL AND box_slot IS NULL) OR
                (party_slot IS NULL AND box_number IS NOT NULL AND box_slot IS NOT NULL)
            )
        )");

        Exec(conn, "CREATE UNIQUE INDEX idx_party ON pokemon(party_slot) WHERE party_slot IS NOT NULL");
        Exec(conn, "CREATE UNIQUE INDEX idx_box ON pokemon(box_number, box_slot) WHERE box_number IS NOT NULL");

        Exec(conn, @"CREATE TABLE pc_boxes (
            box_number INTEGER PRIMARY KEY,
            name       TEXT NOT NULL
        )");

        Exec(conn, @"CREATE TABLE inventory (
            item_id  INTEGER PRIMARY KEY,
            category INTEGER NOT NULL,
            quantity INTEGER NOT NULL
        )");

        Exec(conn, "CREATE TABLE story_flags (flag TEXT PRIMARY KEY NOT NULL)");
    }

    private static void MigrateSchema(SqliteConnection conn, int fromVersion)
    {
        if (fromVersion < 2)
        {
            Exec(conn, "ALTER TABLE player ADD COLUMN game_time_seconds REAL NOT NULL DEFAULT 900.0");
        }
        if (fromVersion < 3)
        {
            Exec(conn, "ALTER TABLE player ADD COLUMN selected_character TEXT");
        }

        Exec(conn, $"UPDATE schema_version SET version = {SchemaVersion}");
    }

    // ── Pokemon read/write ────────────────────────────────────────────

    private static void WritePokemon(SqliteConnection conn, PartyPokemon pkmn,
        int? partySlot, int? boxNumber, int? boxSlot)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO pokemon
            (nickname, species_id, level, current_hp, max_hp, gender, status, held_item_id,
             attack, defense, sp_attack, sp_defense, speed,
             experience, growth_rate, ivs, evs, move_ids, move_pps,
             party_slot, box_number, box_slot)
            VALUES
            (@nick, @species, @level, @hp, @maxhp, @gender, @status, @held,
             @atk, @def, @spa, @spd, @spe,
             @exp, @growth, @ivs, @evs, @moves, @pps,
             @pslot, @bnum, @bslot)";

        cmd.Parameters.AddWithValue("@nick", pkmn.Nickname);
        cmd.Parameters.AddWithValue("@species", pkmn.SpeciesId);
        cmd.Parameters.AddWithValue("@level", pkmn.Level);
        cmd.Parameters.AddWithValue("@hp", pkmn.CurrentHP);
        cmd.Parameters.AddWithValue("@maxhp", pkmn.MaxHP);
        cmd.Parameters.AddWithValue("@gender", (int)pkmn.Gender);
        cmd.Parameters.AddWithValue("@status", pkmn.StatusCondition != StatusCondition.None
            ? pkmn.StatusCondition.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@held", pkmn.HeldItemId.HasValue ? pkmn.HeldItemId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@atk", pkmn.Attack);
        cmd.Parameters.AddWithValue("@def", pkmn.Defense);
        cmd.Parameters.AddWithValue("@spa", pkmn.SpAttack);
        cmd.Parameters.AddWithValue("@spd", pkmn.SpDefense);
        cmd.Parameters.AddWithValue("@spe", pkmn.Speed);
        cmd.Parameters.AddWithValue("@exp", (long)pkmn.ExperiencePoints);
        cmd.Parameters.AddWithValue("@growth", (int)pkmn.GrowthRate);
        cmd.Parameters.AddWithValue("@ivs", SqliteHelpers.ToCSV(pkmn.IVs));
        cmd.Parameters.AddWithValue("@evs", SqliteHelpers.ToCSV(pkmn.EVs));
        cmd.Parameters.AddWithValue("@moves", SqliteHelpers.ToCSV(pkmn.MoveIds));
        cmd.Parameters.AddWithValue("@pps", SqliteHelpers.ToCSV(pkmn.MovePPs));
        cmd.Parameters.AddWithValue("@pslot", partySlot.HasValue ? partySlot.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@bnum", boxNumber.HasValue ? boxNumber.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@bslot", boxSlot.HasValue ? boxSlot.Value : DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    private static PartyPokemon ReadPokemon(SqliteDataReader reader)
    {
        return new PartyPokemon
        {
            Nickname = reader.GetString(reader.GetOrdinal("nickname")),
            SpeciesId = reader.GetInt32(reader.GetOrdinal("species_id")),
            Level = reader.GetInt32(reader.GetOrdinal("level")),
            CurrentHP = reader.GetInt32(reader.GetOrdinal("current_hp")),
            MaxHP = reader.GetInt32(reader.GetOrdinal("max_hp")),
            Gender = (Gender)reader.GetInt32(reader.GetOrdinal("gender")),
            StatusCondition = reader.IsDBNull(reader.GetOrdinal("status"))
                ? StatusCondition.None
                : Enum.TryParse<StatusCondition>(reader.GetString(reader.GetOrdinal("status")), out var sc)
                    ? sc : StatusCondition.None,
            HeldItemId = reader.IsDBNull(reader.GetOrdinal("held_item_id")) ? null : reader.GetInt32(reader.GetOrdinal("held_item_id")),
            Attack = reader.GetInt32(reader.GetOrdinal("attack")),
            Defense = reader.GetInt32(reader.GetOrdinal("defense")),
            SpAttack = reader.GetInt32(reader.GetOrdinal("sp_attack")),
            SpDefense = reader.GetInt32(reader.GetOrdinal("sp_defense")),
            Speed = reader.GetInt32(reader.GetOrdinal("speed")),
            ExperiencePoints = (uint)reader.GetInt64(reader.GetOrdinal("experience")),
            GrowthRate = (GrowthRate)reader.GetInt32(reader.GetOrdinal("growth_rate")),
            IVs = SqliteHelpers.FromCSV(reader.GetString(reader.GetOrdinal("ivs"))),
            EVs = SqliteHelpers.FromCSV(reader.GetString(reader.GetOrdinal("evs"))),
            MoveIds = SqliteHelpers.FromCSV(reader.GetString(reader.GetOrdinal("move_ids"))),
            MovePPs = SqliteHelpers.FromCSV(reader.GetString(reader.GetOrdinal("move_pps"))),
        };
    }

    // ── Exec helpers ──────────────────────────────────────────────────

    private static void Exec(SqliteConnection conn, string sql, params (string name, object value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Connection is opened/closed per operation, nothing to dispose
    }
}

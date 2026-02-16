"""
Fetch Pokemon species data from PokeAPI's GitHub CSV files and generate species.json.
Downloads 5 CSV files total (not 1600+ API calls).

Run once: python fetch-species.py
Output: ../Data/species.json
"""

import csv
import io
import json
import os
import urllib.request

MAX_ID = 802  # Gen 7 (Sun/Moon)
OUTPUT_PATH = os.path.join(os.path.dirname(__file__), "..", "Data", "species.json")

CSV_BASE = "https://raw.githubusercontent.com/PokeAPI/pokeapi/master/data/v2/csv"

GROWTH_RATE_MAP = {
    1: "Slow",
    2: "MediumFast",
    3: "Fast",
    4: "MediumSlow",
    5: "Erratic",
    6: "Fluctuating",
}

# PokeAPI type IDs -> names
TYPE_MAP = {
    1: "Normal", 2: "Fighting", 3: "Flying", 4: "Poison", 5: "Ground",
    6: "Rock", 7: "Bug", 8: "Ghost", 9: "Steel", 10: "Fire",
    11: "Water", 12: "Grass", 13: "Electric", 14: "Psychic", 15: "Ice",
    16: "Dragon", 17: "Dark", 18: "Fairy",
}

# PokeAPI stat IDs -> JSON keys
STAT_MAP = {
    1: "hp", 2: "attack", 3: "defense", 4: "spAttack", 5: "spDefense", 6: "speed",
}


def fetch_csv(filename):
    url = f"{CSV_BASE}/{filename}"
    print(f"Downloading {filename}...")
    req = urllib.request.Request(url, headers={"User-Agent": "PokemonGreen/1.0"})
    with urllib.request.urlopen(req, timeout=30) as resp:
        text = resp.read().decode("utf-8")
    return list(csv.DictReader(io.StringIO(text)))


def main():
    os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)

    # Download all CSVs (5 requests total)
    pokemon_csv = fetch_csv("pokemon.csv")                    # id, species_id, base_experience
    species_csv = fetch_csv("pokemon_species.csv")            # id, name, growth_rate_id, capture_rate
    stats_csv = fetch_csv("pokemon_stats.csv")                # pokemon_id, stat_id, base_stat
    types_csv = fetch_csv("pokemon_types.csv")                # pokemon_id, type_id, slot
    species_names_csv = fetch_csv("pokemon_species_names.csv")  # pokemon_species_id, local_language_id, name

    # Build English name lookup: species_id -> name
    names = {}
    for row in species_names_csv:
        if row["local_language_id"] == "9":  # English
            sid = int(row["pokemon_species_id"])
            if sid <= MAX_ID:
                names[sid] = row["name"]

    # Build species info: id -> (growth_rate_id, capture_rate)
    species_info = {}
    for row in species_csv:
        sid = int(row["id"])
        if sid <= MAX_ID:
            species_info[sid] = {
                "growth_rate_id": int(row["growth_rate_id"]),
                "capture_rate": int(row["capture_rate"]),
            }

    # Build pokemon info: pokemon_id -> (species_id, base_experience)
    # Only take the default form (pokemon_id == species_id for base forms)
    pokemon_info = {}
    for row in pokemon_csv:
        pid = int(row["id"])
        if pid <= MAX_ID:
            pokemon_info[pid] = {
                "species_id": int(row["species_id"]) if "species_id" in row else pid,
                "base_experience": int(row["base_experience"]) if row.get("base_experience") else 64,
            }

    # Build stats: pokemon_id -> {stat_key: value}
    stats = {}
    for row in stats_csv:
        pid = int(row["pokemon_id"])
        if pid <= MAX_ID:
            stat_id = int(row["stat_id"])
            if stat_id in STAT_MAP:
                if pid not in stats:
                    stats[pid] = {}
                stats[pid][STAT_MAP[stat_id]] = int(row["base_stat"])

    # Build types: pokemon_id -> [type1, type2]
    types = {}
    for row in types_csv:
        pid = int(row["pokemon_id"])
        if pid <= MAX_ID:
            slot = int(row["slot"])
            type_id = int(row["type_id"])
            if pid not in types:
                types[pid] = {}
            types[pid][slot] = TYPE_MAP.get(type_id, "Normal")

    # Assemble final data
    species_list = []
    for sid in range(1, MAX_ID + 1):
        info = species_info.get(sid, {})
        pstats = stats.get(sid, {})
        ptypes = types.get(sid, {})

        entry = {
            "id": sid,
            "name": names.get(sid, f"Pokemon#{sid}"),
            "hp": pstats.get("hp", 50),
            "attack": pstats.get("attack", 50),
            "defense": pstats.get("defense", 50),
            "spAttack": pstats.get("spAttack", 50),
            "spDefense": pstats.get("spDefense", 50),
            "speed": pstats.get("speed", 50),
            "type1": ptypes.get(1, "Normal"),
            "type2": ptypes.get(2),
            "baseExpYield": pokemon_info.get(sid, {}).get("base_experience", 64),
            "growthRate": GROWTH_RATE_MAP.get(info.get("growth_rate_id", 2), "MediumFast"),
            "catchRate": info.get("capture_rate", 45),
        }
        species_list.append(entry)

    with open(OUTPUT_PATH, "w") as f:
        json.dump(species_list, f, indent=2)

    print(f"Done! {len(species_list)} species saved to {OUTPUT_PATH}")


if __name__ == "__main__":
    main()

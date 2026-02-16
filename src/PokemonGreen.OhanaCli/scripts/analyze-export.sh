#!/bin/bash
# Analyze the exported SunMoon GARC data to understand grouping patterns
# Usage: bash analyze-export.sh <export-dir>

EXPORT_DIR="${1:-D:/Projects/PokemonGreen/Assets/SunMoon/0_9_4}"

echo "=== Analyzing: $EXPORT_DIR ==="
echo

# Count totals
total_folders=$(ls -d "$EXPORT_DIR"/file_*/ 2>/dev/null | wc -l)
dae_folders=$(find "$EXPORT_DIR" -name "*.dae" -printf '%h\n' | sort -u | wc -l)
tex_only_folders=0
mixed_folders=0

echo "Total exported folders: $total_folders"
echo "Folders with DAE models: $dae_folders"

# Build a map of what each folder contains
declare -A folder_type  # model, textures, mixed
declare -A folder_pokemon_id

for dir in "$EXPORT_DIR"/file_*/; do
    basename=$(basename "$dir")
    idx=${basename#file_}

    has_dae=$(find "$dir" -maxdepth 1 -name "*.dae" | wc -l)
    has_png=$(find "$dir" -maxdepth 1 -name "*.png" | wc -l)

    if [ "$has_dae" -gt 0 ] && [ "$has_png" -gt 0 ]; then
        folder_type[$idx]="mixed"
        ((mixed_folders++))
    elif [ "$has_dae" -gt 0 ]; then
        folder_type[$idx]="model"
    elif [ "$has_png" -gt 0 ]; then
        folder_type[$idx]="textures"
        ((tex_only_folders++))

        # Extract Pokemon ID from first non-Dummy texture
        pokemon_id=$(ls "$dir" | grep -v DummyTex | head -1 | grep -oP 'pm\d+_\d+' || echo "unknown")
        folder_pokemon_id[$idx]=$pokemon_id
    fi
done

echo "Folders with only DAE (no textures): $((dae_folders - mixed_folders))"
echo "Folders with only textures (no DAE): $tex_only_folders"
echo "Folders with both DAE and textures: $mixed_folders"
echo

# Analyze grouping pattern
echo "=== Grouping Pattern Analysis ==="
echo "Looking at sequences of model -> texture entries..."
echo

# Find all model entries and what follows them
model_indices=()
for idx in $(echo "${!folder_type[@]}" | tr ' ' '\n' | sort); do
    if [ "${folder_type[$idx]}" = "model" ]; then
        model_indices+=("$idx")
    fi
done

echo "Total model entries: ${#model_indices[@]}"

# Check how many texture entries follow each model
declare -A group_sizes
for model_idx in "${model_indices[@]}"; do
    count=0
    next=$((10#$model_idx + 1))
    while true; do
        next_padded=$(printf "%05d" $next)
        if [ "${folder_type[$next_padded]}" = "textures" ]; then
            ((count++))
            ((next++))
        else
            break
        fi
    done
    group_sizes[$count]=$(( ${group_sizes[$count]:-0} + 1 ))
done

echo
echo "Texture entries following each model:"
for size in $(echo "${!group_sizes[@]}" | tr ' ' '\n' | sort -n); do
    echo "  $size texture entries: ${group_sizes[$size]} models"
done

# Sample some groups with Pokemon IDs
echo
echo "=== Sample Groups (first 20) ==="
count=0
for model_idx in "${model_indices[@]}"; do
    if [ $count -ge 20 ]; then break; fi

    echo -n "  Model file_$model_idx"

    # Collect following texture entries
    tex_entries=""
    pokemon_id="unknown"
    next=$((10#$model_idx + 1))
    while true; do
        next_padded=$(printf "%05d" $next)
        if [ "${folder_type[$next_padded]}" = "textures" ]; then
            tex_entries="$tex_entries $next_padded"
            if [ "$pokemon_id" = "unknown" ] && [ -n "${folder_pokemon_id[$next_padded]}" ] && [ "${folder_pokemon_id[$next_padded]}" != "unknown" ]; then
                pokemon_id="${folder_pokemon_id[$next_padded]}"
            fi
            ((next++))
        else
            break
        fi
    done

    echo " -> textures:$tex_entries -> ID: $pokemon_id"
    ((count++))
done

# Check for texture entries NOT preceded by a model
echo
echo "=== Orphaned Texture Entries (not following a model) ==="
orphan_count=0
for idx in $(echo "${!folder_type[@]}" | tr ' ' '\n' | sort); do
    if [ "${folder_type[$idx]}" = "textures" ]; then
        prev=$((10#$idx - 1))
        prev_padded=$(printf "%05d" $prev)
        prev_type="${folder_type[$prev_padded]}"
        if [ "$prev_type" != "model" ] && [ "$prev_type" != "textures" ]; then
            ((orphan_count++))
            if [ $orphan_count -le 10 ]; then
                echo "  file_$idx (prev file_$prev_padded is ${prev_type:-not exported})"
            fi
        fi
    fi
done
echo "Total orphaned texture entries: $orphan_count"

echo
echo "=== Done ==="

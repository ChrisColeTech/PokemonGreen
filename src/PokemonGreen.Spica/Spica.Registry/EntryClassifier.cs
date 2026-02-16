using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Texture;
using SPICA.Formats.GFL2;
using SPICA.Formats.GFL2.Model;
using SPICA.Formats.GFL2.Motion;
using SPICA.Formats.GFL2.Texture;

using System.Text;

namespace Spica.Registry
{
    enum EntryType { Unknown, Model, Texture, Animation, ShinyTexture, Metadata }

    class EntryInfo
    {
        public int Index;
        public int CompressedSize;
        public int DecompressedSize;
        public EntryType Type = EntryType.Unknown;
        public string Pokemon;            // e.g. "pm0001_00"
        public List<string> ModelNames  = new();
        public List<int> MeshCounts     = new();
        public int BoneCount;
        public List<string> TextureNames = new();
        public List<string> AnimationNames = new();
        public int FrameCount;
    }

    class PokemonGroup
    {
        public string Id;                 // e.g. "pm0001_00"
        public int ModelEntry = -1;
        public List<int> TextureEntries   = new();
        public List<int> AnimationEntries = new();
    }

    static class EntryClassifier
    {
        const uint GFModelConstant   = 0x15122117;
        const uint GFTextureConstant = 0x15041213;
        const uint GFMotionConstant  = 0x00060000;
        const uint GFModelPack       = 0x00010000;
        const uint BCHConstant       = 0x00484342;

        public static EntryInfo Classify(int index, byte[] raw, H3DDict<H3DBone> skeleton)
        {
            var info = new EntryInfo { Index = index, CompressedSize = raw.Length };

            byte[] data = raw;
            if (LZSS.IsCompressed(data))
            {
                try { data = LZSS.Decompress(data); }
                catch { return info; }
            }

            info.DecompressedSize = data.Length;

            if (data.Length < 4) return info;

            using var ms = new MemoryStream(data);
            var reader = new BinaryReader(ms);

            uint magicNum = BitConverter.ToUInt32(data, 0);

            // Check for GFPackage (2 uppercase ASCII bytes) — but skip known 4-byte magics
            // that happen to start with uppercase bytes (e.g. BCH = 0x42,0x43)
            byte b0 = data[0], b1 = data[1];
            if (b0 >= 'A' && b0 <= 'Z' && b1 >= 'A' && b1 <= 'Z' && data.Length >= 0x80
                && magicNum != BCHConstant)
            {
                string magic = Encoding.ASCII.GetString(data, 0, 2);
                return ClassifyGFPackage(info, magic, ms, reader, skeleton);
            }

            switch (magicNum)
            {
                case GFModelConstant:
                    info.Type = EntryType.Model;
                    try
                    {
                        var model = new GFModel(reader, "Model");
                        var h3d = model.ToH3DModel();
                        info.ModelNames.Add(model.Name);
                        info.MeshCounts.Add(h3d.Meshes.Count);
                        info.BoneCount = h3d.Skeleton.Count;
                        info.Pokemon = ExtractPokemonId(h3d);
                    }
                    catch { }
                    break;

                case GFTextureConstant:
                    info.Type = EntryType.Texture;
                    try
                    {
                        var tex = new GFTexture(reader);
                        var h3dTex = tex.ToH3DTexture();
                        info.TextureNames.Add(h3dTex.Name);
                        info.Pokemon = ExtractPokemonIdFromTexture(h3dTex.Name);
                    }
                    catch { }
                    break;

                case GFModelPack:
                    info.Type = EntryType.Model;
                    try
                    {
                        var pack = new GFModelPack(reader);
                        var h3d = pack.ToH3D();
                        foreach (var mdl in h3d.Models)
                        {
                            info.ModelNames.Add(mdl.Name);
                            info.MeshCounts.Add(mdl.Meshes.Count);
                        }
                        if (h3d.Models.Count > 0)
                        {
                            info.BoneCount = h3d.Models[0].Skeleton.Count;
                            info.Pokemon = ExtractPokemonId(h3d.Models[0]);
                        }
                        foreach (var tex in h3d.Textures)
                            info.TextureNames.Add(tex.Name);
                    }
                    catch { }
                    break;

                case GFMotionConstant:
                    info.Type = EntryType.Animation;
                    if (skeleton != null)
                    {
                        try
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            var mot = new GFMotion(reader, 0);
                            info.FrameCount = (int)mot.FramesCount;
                            var anim = mot.ToH3DSkeletalAnimation(skeleton);
                            if (anim != null)
                                info.AnimationNames.Add(anim.Name ?? $"Motion_{index}");
                        }
                        catch { }
                    }
                    break;

                case BCHConstant:
                    // BCH can contain models, textures, or animations
                    try
                    {
                        var h3d = H3D.Open(data);
                        if (h3d.Models.Count > 0)
                        {
                            info.Type = EntryType.Model;
                            foreach (var mdl in h3d.Models)
                            {
                                info.ModelNames.Add(mdl.Name);
                                info.MeshCounts.Add(mdl.Meshes.Count);
                            }
                            info.BoneCount = h3d.Models[0].Skeleton.Count;
                        }
                        else if (h3d.Textures.Count > 0)
                        {
                            info.Type = EntryType.Texture;
                            foreach (var tex in h3d.Textures)
                                info.TextureNames.Add(tex.Name);
                            if (h3d.Textures.Count > 0)
                                info.Pokemon = ExtractPokemonIdFromTexture(h3d.Textures[0].Name);
                        }
                        else if (h3d.SkeletalAnimations.Count > 0)
                        {
                            info.Type = EntryType.Animation;
                            foreach (var anim in h3d.SkeletalAnimations)
                                info.AnimationNames.Add(anim.Name);
                        }
                    }
                    catch { }
                    break;
            }

            return info;
        }

        static EntryInfo ClassifyGFPackage(EntryInfo info, string magic, MemoryStream ms, BinaryReader reader, H3DDict<H3DBone> skeleton)
        {
            switch (magic)
            {
                case "PC":
                    try
                    {
                        ms.Seek(2, SeekOrigin.Begin);
                        ushort entryCount = reader.ReadUInt16();
                        long tablePos = ms.Position;
                        uint firstStart = reader.ReadUInt32();
                        ms.Seek(tablePos - 4 + firstStart, SeekOrigin.Begin);
                        uint firstMagic = reader.ReadUInt32();

                        if (firstMagic == GFModelConstant)
                        {
                            info.Type = EntryType.Model;
                            ms.Seek(tablePos - 4 + firstStart, SeekOrigin.Begin);
                            var model = new GFModel(reader, "PM_HighPoly");
                            var h3dModel = model.ToH3DModel();
                            info.ModelNames.Add("PM_HighPoly");
                            info.ModelNames.Add("PM_LowPoly");
                            info.MeshCounts.Add(h3dModel.Meshes.Count);
                            info.BoneCount = h3dModel.Skeleton.Count;
                            info.Pokemon = ExtractPokemonId(h3dModel);
                        }
                        else if (firstMagic == GFTextureConstant)
                        {
                            info.Type = EntryType.Texture;
                            ms.Seek(tablePos - 4 + firstStart, SeekOrigin.Begin);
                            var tex = new GFTexture(reader);
                            var h3dTex = tex.ToH3DTexture();
                            info.TextureNames.Add(h3dTex.Name);
                            info.Pokemon = ExtractPokemonIdFromTexture(h3dTex.Name);
                            // Count remaining texture sub-entries
                            for (int e = 1; e < entryCount; e++)
                            {
                                try
                                {
                                    ms.Seek(tablePos + e * 4, SeekOrigin.Begin);
                                    uint off = reader.ReadUInt32();
                                    ms.Seek(tablePos - 4 + off, SeekOrigin.Begin);
                                    uint m = reader.ReadUInt32();
                                    if (m == GFTextureConstant)
                                    {
                                        ms.Seek(tablePos - 4 + off, SeekOrigin.Begin);
                                        var t2 = new GFTexture(reader);
                                        info.TextureNames.Add(t2.ToH3DTexture().Name);
                                    }
                                }
                                catch { break; }
                            }
                        }
                        else if (firstMagic == GFMotionConstant)
                        {
                            info.Type = EntryType.Animation;
                            if (skeleton != null)
                            {
                                ms.Seek(tablePos - 4 + firstStart, SeekOrigin.Begin);
                                var mot = new GFMotion(reader, 0);
                                info.FrameCount = (int)mot.FramesCount;
                                var anim = mot.ToH3DSkeletalAnimation(skeleton);
                                if (anim != null)
                                    info.AnimationNames.Add(anim.Name ?? $"Motion_{info.Index}");
                            }
                        }
                        else
                        {
                            // Shader data ("Body"=0x79646F42) or packed params (GFModelPack=0x00010000)
                            info.Type = EntryType.Metadata;
                        }
                    }
                    catch { }
                    break;

                case "PK":
                case "PB":
                    info.Type = EntryType.Animation;
                    break;

                case "BS":
                    info.Type = EntryType.Animation;
                    break;

                case "AD":
                case "PT":
                    info.Type = EntryType.Texture;
                    break;

                case "CM":
                case "MM":
                case "GR":
                case "BG":
                    info.Type = EntryType.Model;
                    break;
            }

            return info;
        }

        static string ExtractPokemonId(H3DModel model)
        {
            // Try to extract from material texture names like "pm0001_00_BodyA1"
            foreach (var mat in model.Materials)
            {
                string name = mat.Texture0Name;
                if (name != null && name.StartsWith("pm"))
                {
                    // Extract "pm0001_00" from "pm0001_00_BodyA1"
                    int idx = name.IndexOf('_', 2);
                    if (idx > 0)
                    {
                        int idx2 = name.IndexOf('_', idx + 1);
                        if (idx2 > 0) return name.Substring(0, idx2);
                    }
                }
            }
            return null;
        }

        static string ExtractPokemonIdFromTexture(string texName)
        {
            // "pm0001_00_BodyA1.tga" -> "pm0001_00"
            if (texName != null && texName.StartsWith("pm"))
            {
                int idx = texName.IndexOf('_', 2);
                if (idx > 0)
                {
                    int idx2 = texName.IndexOf('_', idx + 1);
                    if (idx2 > 0) return texName.Substring(0, idx2);
                }
            }
            return null;
        }

        /// <summary>
        /// Group entries into Pokemon by proximity and matching IDs.
        /// Model entries start a new group. Subsequent texture/animation entries
        /// belong to the most recent model.
        /// </summary>
        public static List<PokemonGroup> BuildGroups(List<EntryInfo> entries)
        {
            var groups = new List<PokemonGroup>();
            PokemonGroup current = null;

            foreach (var entry in entries)
            {
                if (entry.Type == EntryType.Model && entry.Pokemon != null)
                {
                    current = new PokemonGroup { Id = entry.Pokemon, ModelEntry = entry.Index };
                    groups.Add(current);
                }
                else if (current != null)
                {
                    if (entry.Type == EntryType.Texture)
                    {
                        current.TextureEntries.Add(entry.Index);
                    }
                    else if (entry.Type == EntryType.Animation)
                    {
                        current.AnimationEntries.Add(entry.Index);
                    }
                }
            }

            return groups;
        }
    }
}

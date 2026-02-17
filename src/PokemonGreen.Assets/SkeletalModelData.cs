#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Assimp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AssimpMatrix = Assimp.Matrix4x4;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaQuat = Microsoft.Xna.Framework.Quaternion;

namespace PokemonGreen.Assets;

// ─── Data types ──────────────────────────────────────────────

internal struct BoneInfo
{
    public string Name;
    public int ParentIndex;           // -1 = root
    public XnaMatrix OffsetMatrix;    // mesh-space → bone-space (from skin controller)
    public XnaMatrix LocalBindPose;   // local transform from scene node
    public XnaMatrix GlobalTransform; // computed each frame
    public XnaMatrix FinalMatrix;     // offset * global (sent to skinning)
}

internal struct VecKey { public double Time; public Vector3 Value; }
internal struct QuatKey { public double Time; public XnaQuat Value; }

internal struct AnimChannel
{
    public int BoneIndex;
    public VecKey[] PositionKeys;
    public QuatKey[] RotationKeys;
    public VecKey[] ScaleKeys;
}

// ─── Per-mesh skinned data ───────────────────────────────────

public class SkeletalMeshData : IDisposable
{
    internal VertexPositionNormalTexture[] BindVertices;
    internal byte[][] BoneIndices; // [vertexCount][4]
    internal float[][] BoneWeights; // [vertexCount][4]

    public DynamicVertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public Texture2D? Texture { get; }
    public int PrimitiveCount { get; }

    internal VertexPositionNormalTexture[] TransformedVertices;

    public SkeletalMeshData(GraphicsDevice device, VertexPositionNormalTexture[] bindVerts,
        byte[][] boneIdx, float[][] boneWts, int[] indices, Texture2D? texture)
    {
        BindVertices = bindVerts;
        BoneIndices = boneIdx;
        BoneWeights = boneWts;
        TransformedVertices = new VertexPositionNormalTexture[bindVerts.Length];
        Array.Copy(bindVerts, TransformedVertices, bindVerts.Length);

        VertexBuffer = new DynamicVertexBuffer(device, typeof(VertexPositionNormalTexture),
            bindVerts.Length, BufferUsage.None);
        VertexBuffer.SetData(TransformedVertices);

        IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits,
            indices.Length, BufferUsage.WriteOnly);
        IndexBuffer.SetData(indices);

        Texture = texture;
        PrimitiveCount = indices.Length / 3;
    }

    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
        Texture?.Dispose();
    }
}

// ─── Full skeletal model ─────────────────────────────────────

public class SkeletalModelData : IDisposable
{
    public List<SkeletalMeshData> Meshes { get; } = new();
    public Vector3 BoundsMin { get; set; } = new(float.MaxValue);
    public Vector3 BoundsMax { get; set; } = new(float.MinValue);

    internal BoneInfo[] Bones = Array.Empty<BoneInfo>();
    internal AnimChannel[] Channels = Array.Empty<AnimChannel>();
    internal double AnimDuration;
    internal double TicksPerSecond;

    // Multi-clip support (lazy-loaded)
    private readonly Dictionary<string, AnimationClip> _clips = new();
    private readonly Dictionary<string, string> _pendingClipPaths = new();
    private readonly List<string> _clipOrder = new(); // preserves manifest insertion order
    private Dictionary<string, int>? _boneNameMap;
    private AnimationClip? _activeClip;

    /// <summary>All available animation clip names (both loaded and pending), in manifest order.</summary>
    public IReadOnlyList<string> ClipNames => _clipOrder;

    /// <summary>Number of registered animation clips.</summary>
    public int ClipCount => _clipOrder.Count;

    /// <summary>Name of the currently playing clip, or null if using baked animation.</summary>
    public string? ActiveClipName => _activeClip?.Name;

    private XnaMatrix[] _boneLocalTransforms = Array.Empty<XnaMatrix>();
    private bool _loggedFirstFrame;

    private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skeletal_log.txt");
    private static void Log(string msg) => File.AppendAllText(_logPath, msg + "\n");

    /// <summary>
    /// Switch to a named animation clip. If the clip hasn't been parsed yet,
    /// it will be lazy-loaded from the registered DAE path on first play.
    /// </summary>
    public void Play(string clipName)
    {
        if (_clips.TryGetValue(clipName, out var clip))
        {
            _activeClip = clip;
            return;
        }

        // Lazy load: parse the clip DAE on first Play()
        if (_pendingClipPaths.Remove(clipName, out var clipPath))
        {
            LoadClipInternal(clipPath, clipName);
            if (_clips.TryGetValue(clipName, out clip))
                _activeClip = clip;
        }
    }

    /// <summary>Play a clip by its manifest order index (0-based).</summary>
    public void PlayIndex(int index)
    {
        if (index >= 0 && index < _clipOrder.Count)
            Play(_clipOrder[index]);
    }

    /// <summary>
    /// Register a clip for lazy loading. The DAE file will only be parsed
    /// when Play() is called for this clip name.
    /// </summary>
    public void RegisterClip(string clipName, string clipDaePath)
    {
        _pendingClipPaths[clipName] = clipDaePath;
        if (!_clipOrder.Contains(clipName))
            _clipOrder.Add(clipName);
    }

    /// <summary>
    /// Advance the skeleton animation and transform all mesh vertices on the CPU.
    /// Uses the active clip if set, otherwise falls back to baked animation data.
    /// </summary>
    public void Update(double totalSeconds)
    {
        // Determine which animation data to use
        var channels = _activeClip?.Channels ?? Channels;
        double animDuration = _activeClip?.Duration ?? AnimDuration;
        double ticksPerSecond = _activeClip?.TicksPerSecond ?? TicksPerSecond;

        if (Bones.Length == 0 || channels.Length == 0)
            return;

        double tps = ticksPerSecond > 0 ? ticksPerSecond : 25.0;
        double durationSec = animDuration / tps;
        if (durationSec <= 0) return;

        double t = (totalSeconds % durationSec) * tps; // time in ticks, looping

        if (_boneLocalTransforms.Length != Bones.Length)
            _boneLocalTransforms = new XnaMatrix[Bones.Length];

        // 1. Start with bind-pose local transforms
        for (int i = 0; i < Bones.Length; i++)
            _boneLocalTransforms[i] = Bones[i].LocalBindPose;

        // 2. Override with animated transforms where we have channels
        foreach (ref readonly var ch in channels.AsSpan())
        {
            var pos = InterpolateVec(ch.PositionKeys, t);
            var rot = InterpolateQuat(ch.RotationKeys, t);
            var scl = InterpolateVec(ch.ScaleKeys, t);

            // Compose: Scale → Rotate → Translate
            _boneLocalTransforms[ch.BoneIndex] =
                XnaMatrix.CreateScale(scl) *
                XnaMatrix.CreateFromQuaternion(rot) *
                XnaMatrix.CreateTranslation(pos);
        }

        // 3. Walk hierarchy (parent-first order) to build global transforms
        for (int i = 0; i < Bones.Length; i++)
        {
            if (Bones[i].ParentIndex >= 0)
                Bones[i].GlobalTransform = _boneLocalTransforms[i] * Bones[Bones[i].ParentIndex].GlobalTransform;
            else
                Bones[i].GlobalTransform = _boneLocalTransforms[i];

            // Final = OffsetMatrix * GlobalTransform
            // OffsetMatrix = inverse(bind-pose global) → at bind pose this yields Identity
            Bones[i].FinalMatrix = Bones[i].OffsetMatrix * Bones[i].GlobalTransform;
        }

        // Log first frame diagnostics
        if (!_loggedFirstFrame)
        {
            _loggedFirstFrame = true;
            Log($"  [Frame0] t={t:F4} (totalSec={totalSeconds:F4})");
            for (int i = 0; i < Math.Min(5, Bones.Length); i++)
            {
                ref var b = ref Bones[i];
                var fm = b.FinalMatrix;
                Log($"    Bone[{i}] '{b.Name}' FinalMatrix diag=({fm.M11:F4},{fm.M22:F4},{fm.M33:F4},{fm.M44:F4})");
                Log($"      row0=({fm.M11:F4},{fm.M12:F4},{fm.M13:F4},{fm.M14:F4})");
                Log($"      row1=({fm.M21:F4},{fm.M22:F4},{fm.M23:F4},{fm.M24:F4})");
                Log($"      row2=({fm.M31:F4},{fm.M32:F4},{fm.M33:F4},{fm.M34:F4})");
                Log($"      row3=({fm.M41:F4},{fm.M42:F4},{fm.M43:F4},{fm.M44:F4})");
                var bp = b.LocalBindPose;
                var lc = _boneLocalTransforms[i];
                Log($"      BindLocal diag=({bp.M11:F4},{bp.M22:F4},{bp.M33:F4}) trans=({bp.M41:F4},{bp.M42:F4},{bp.M43:F4})");
                Log($"      AnimLocal diag=({lc.M11:F4},{lc.M22:F4},{lc.M33:F4}) trans=({lc.M41:F4},{lc.M42:F4},{lc.M43:F4})");
            }
            // Log sample vertex before/after
            if (Meshes.Count > 0 && Meshes[0].BindVertices.Length > 0)
            {
                var bv = Meshes[0].BindVertices[0];
                var tv = Meshes[0].TransformedVertices[0];
                Log($"    Vert[0] bind=({bv.Position.X:F2},{bv.Position.Y:F2},{bv.Position.Z:F2}) " +
                    $"skinned=({tv.Position.X:F2},{tv.Position.Y:F2},{tv.Position.Z:F2})");
            }
        }

        // 4. Skin vertices on CPU
        foreach (var mesh in Meshes)
        {
            for (int v = 0; v < mesh.BindVertices.Length; v++)
            {
                var bindPos = mesh.BindVertices[v].Position;
                var bindNorm = mesh.BindVertices[v].Normal;
                var bIdx = mesh.BoneIndices[v];
                var bWt = mesh.BoneWeights[v];

                var pos = Vector3.Zero;
                var norm = Vector3.Zero;

                for (int b = 0; b < 4; b++)
                {
                    float w = bWt[b];
                    if (w <= 0f) continue;
                    ref readonly var mat = ref Bones[bIdx[b]].FinalMatrix;
                    pos += Vector3.Transform(bindPos, mat) * w;
                    norm += Vector3.TransformNormal(bindNorm, mat) * w;
                }

                if (norm.LengthSquared() > 0.001f)
                    norm = Vector3.Normalize(norm);

                mesh.TransformedVertices[v].Position = pos;
                mesh.TransformedVertices[v].Normal = norm;
                mesh.TransformedVertices[v].TextureCoordinate = mesh.BindVertices[v].TextureCoordinate;
            }

            mesh.VertexBuffer.SetData(mesh.TransformedVertices);
        }
    }

    public void Draw(GraphicsDevice device, AlphaTestEffect effect)
    {
        // Pokemon models use tiled UVs — need Wrap addressing
        var prevSampler = device.SamplerStates[0];
        device.SamplerStates[0] = SamplerState.LinearWrap;

        foreach (var mesh in Meshes)
        {
            if (mesh.Texture != null)
                effect.Texture = mesh.Texture;

            device.SetVertexBuffer(mesh.VertexBuffer);
            device.Indices = mesh.IndexBuffer;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(
                    Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList,
                    0, 0, mesh.PrimitiveCount);
            }
        }

        device.SamplerStates[0] = prevSampler;
    }

    /// <summary>
    /// Eagerly load an animation clip from a clip-only DAE file.
    /// Prefer RegisterClip() + Play() for lazy loading.
    /// </summary>
    public void LoadClip(string clipDaePath, string clipName)
    {
        LoadClipInternal(clipDaePath, clipName);
        if (!_clipOrder.Contains(clipName))
            _clipOrder.Add(clipName);
    }

    private void LoadClipInternal(string clipDaePath, string clipName)
    {
        var clip = ParseClipDae(clipDaePath, clipName, GetBoneNameMap());
        if (clip != null)
            _clips[clipName] = clip;
    }

    /// <summary>
    /// Cached bone name → index map. Built once, supports both "Waist" (Assimp node name)
    /// and "Waist_bone_id" (COLLADA node id, used in animation channel targets).
    /// </summary>
    private Dictionary<string, int> GetBoneNameMap()
    {
        if (_boneNameMap == null)
        {
            _boneNameMap = new Dictionary<string, int>();
            for (int i = 0; i < Bones.Length; i++)
            {
                _boneNameMap[Bones[i].Name] = i;
                _boneNameMap[Bones[i].Name + "_bone_id"] = i;
            }
        }
        return _boneNameMap;
    }

    /// <summary>
    /// Parse a clip-only DAE file that uses matrix animation channels.
    /// Channel targets: "{BoneName}_bone_id/transform" with 4x4 matrix output.
    /// </summary>
    private static AnimationClip? ParseClipDae(string clipDaePath, string clipName,
        Dictionary<string, int> boneNameToIndex)
    {
        var doc = XDocument.Load(clipDaePath);
        XNamespace ns = doc.Root?.Name.Namespace ?? "";

        var libAnims = doc.Root?.Element(ns + "library_animations");
        if (libAnims == null) return null;

        double maxTime = 0;
        var channels = new List<AnimChannel>();

        foreach (var anim in libAnims.Elements(ns + "animation"))
            ParseMatrixAnimationElement(anim, ns, boneNameToIndex, channels, ref maxTime);

        // Also try nested animations
        foreach (var anim in libAnims.Elements(ns + "animation"))
            foreach (var nested in anim.Elements(ns + "animation"))
                ParseMatrixAnimationElement(nested, ns, boneNameToIndex, channels, ref maxTime);

        if (channels.Count == 0)
        {
            // Fall back to per-component Euler parsing (legacy baked DAEs)
            return null;
        }

        return new AnimationClip(clipName, maxTime, 1.0, channels.ToArray());
    }

    /// <summary>
    /// Parse a single COLLADA animation element with matrix channel output.
    /// Target format: "{BoneName}_bone_id/transform"
    /// Output: N * 16 float values (N 4x4 matrices, one per keyframe).
    /// Each matrix is decomposed into translation + rotation + scale for interpolation.
    /// </summary>
    private static void ParseMatrixAnimationElement(XElement anim, XNamespace ns,
        Dictionary<string, int> boneNameToIndex,
        List<AnimChannel> channels, ref double maxTime)
    {
        var channel = anim.Element(ns + "channel");
        if (channel == null) return;

        string target = channel.Attribute("target")?.Value ?? "";
        // Format: "Waist_bone_id/transform"
        int slashIdx = target.IndexOf('/');
        if (slashIdx < 0) return;

        string boneName = target.Substring(0, slashIdx);
        string property = target.Substring(slashIdx + 1);

        // Only handle matrix transform channels
        if (property != "transform") return;

        if (!boneNameToIndex.TryGetValue(boneName, out int boneIdx))
            return;

        // Find INPUT and OUTPUT source references from the sampler
        var sampler = anim.Element(ns + "sampler");
        if (sampler == null) return;

        string? inputSourceId = null, outputSourceId = null;
        foreach (var input in sampler.Elements(ns + "input"))
        {
            string semantic = input.Attribute("semantic")?.Value ?? "";
            string sourceRef = (input.Attribute("source")?.Value ?? "").TrimStart('#');
            if (semantic == "INPUT") inputSourceId = sourceRef;
            else if (semantic == "OUTPUT") outputSourceId = sourceRef;
        }
        if (inputSourceId == null || outputSourceId == null) return;

        // Read float arrays from source elements
        double[]? times = null;
        float[]? matValues = null;
        foreach (var source in anim.Elements(ns + "source"))
        {
            string sourceId = source.Attribute("id")?.Value ?? "";
            var floatArray = source.Element(ns + "float_array");
            if (floatArray == null) continue;

            if (sourceId == inputSourceId)
                times = ParseFloatArrayDouble(floatArray.Value);
            else if (sourceId == outputSourceId)
                matValues = ParseFloatArrayFloat(floatArray.Value);
        }

        if (times == null || matValues == null || times.Length == 0)
            return;

        int frameCount = times.Length;
        if (matValues.Length != frameCount * 16)
            return; // Each frame has 16 floats (4x4 matrix)

        if (times[^1] > maxTime)
            maxTime = times[^1];

        // Decompose each keyframe matrix into translation, rotation, scale
        var posKeys = new VecKey[frameCount];
        var rotKeys = new QuatKey[frameCount];
        var sclKeys = new VecKey[frameCount];

        for (int f = 0; f < frameCount; f++)
        {
            int offset = f * 16;

            // COLLADA stores matrices in row-major order (same as XNA)
            // But our SPICA exporter writes Matrix3x4 which is row-major transposed...
            // The DAE matrix data layout: a11 a12 a13 a14  a21 a22 a23 a24  a31 a32 a33 a34  a41 a42 a43 a44
            // COLLADA convention: row-major (same as reading left-to-right, top-to-bottom)
            var mat = new XnaMatrix(
                matValues[offset + 0], matValues[offset + 1], matValues[offset + 2], matValues[offset + 3],
                matValues[offset + 4], matValues[offset + 5], matValues[offset + 6], matValues[offset + 7],
                matValues[offset + 8], matValues[offset + 9], matValues[offset + 10], matValues[offset + 11],
                matValues[offset + 12], matValues[offset + 13], matValues[offset + 14], matValues[offset + 15]);

            // XNA Matrix.Decompose expects XNA row-major format
            // COLLADA is row-major, but XNA reads M11=row0col0, M12=row0col1...
            // Need to transpose because COLLADA row-major → XNA row-major requires transpose
            // (COLLADA: mat[row][col], XNA: M{row}{col} but stored column-major internally)
            mat = XnaMatrix.Transpose(mat);

            if (mat.Decompose(out var scale, out var rotation, out var translation))
            {
                posKeys[f] = new VecKey { Time = times[f], Value = translation };
                rotKeys[f] = new QuatKey { Time = times[f], Value = rotation };
                sclKeys[f] = new VecKey { Time = times[f], Value = scale };
            }
            else
            {
                // Decompose failed — extract translation directly, use identity rotation
                posKeys[f] = new VecKey { Time = times[f], Value = new Vector3(mat.M41, mat.M42, mat.M43) };
                rotKeys[f] = new QuatKey { Time = times[f], Value = XnaQuat.Identity };
                sclKeys[f] = new VecKey { Time = times[f], Value = Vector3.One };
            }
        }

        channels.Add(new AnimChannel
        {
            BoneIndex = boneIdx,
            PositionKeys = posKeys,
            RotationKeys = rotKeys,
            ScaleKeys = sclKeys,
        });
    }

    public void Dispose()
    {
        foreach (var mesh in Meshes)
            mesh.Dispose();
    }

    // ─── Keyframe interpolation ──────────────────────────────

    private static Vector3 InterpolateVec(VecKey[] keys, double t)
    {
        if (keys.Length == 0) return Vector3.One; // default scale
        if (keys.Length == 1 || t <= keys[0].Time) return keys[0].Value;
        if (t >= keys[^1].Time) return keys[^1].Value;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (t < keys[i + 1].Time)
            {
                float f = (float)((t - keys[i].Time) / (keys[i + 1].Time - keys[i].Time));
                return Vector3.Lerp(keys[i].Value, keys[i + 1].Value, f);
            }
        }
        return keys[^1].Value;
    }

    private static XnaQuat InterpolateQuat(QuatKey[] keys, double t)
    {
        if (keys.Length == 0) return XnaQuat.Identity;
        if (keys.Length == 1 || t <= keys[0].Time) return keys[0].Value;
        if (t >= keys[^1].Time) return keys[^1].Value;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (t < keys[i + 1].Time)
            {
                float f = (float)((t - keys[i].Time) / (keys[i + 1].Time - keys[i].Time));
                return XnaQuat.Slerp(keys[i].Value, keys[i + 1].Value, f);
            }
        }
        return keys[^1].Value;
    }

    // ─── Assimp → XNA conversions ────────────────────────────

    internal static XnaMatrix ToXna(AssimpMatrix m)
    {
        // Assimp Matrix4x4: A1-A4 = row 1, B1-B4 = row 2, etc.
        // But Assimp internally uses column-major, so we need to transpose.
        // XNA Matrix: M11-M14 = row 1, etc. (row-major)
        return new XnaMatrix(
            m.A1, m.B1, m.C1, m.D1,
            m.A2, m.B2, m.C2, m.D2,
            m.A3, m.B3, m.C3, m.D3,
            m.A4, m.B4, m.C4, m.D4);
    }

    internal static XnaQuat ToXna(Assimp.Quaternion q)
    {
        return new XnaQuat(q.X, q.Y, q.Z, q.W);
    }

    internal static Vector3 ToXna(Vector3D v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    // ─── Loading ─────────────────────────────────────────────

    public static SkeletalModelData Load(string daeFilePath, GraphicsDevice graphicsDevice)
    {
        Log($"\n=== Loading skeletal model: {daeFilePath} ===");
        var result = new SkeletalModelData();
        string directory = Path.GetDirectoryName(daeFilePath) ?? "";
        string folderPrefix = Path.GetFileName(directory) ?? "";

        // Pre-build texture list for fallback — check both same directory and textures/ subdirectory
        var folderTextures = new List<string>();
        string texturesSubDir = Path.Combine(directory, "textures");
        if (!string.IsNullOrEmpty(directory))
        {
            var searchDirs = new[] { directory, texturesSubDir };
            foreach (var searchDir in searchDirs)
            {
                if (!Directory.Exists(searchDir)) continue;
                foreach (var file in Directory.EnumerateFiles(searchDir, $"{folderPrefix}_*.png"))
                {
                    string name = Path.GetFileName(file);
                    if (name.Contains("Nor", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Dummy", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Inc", StringComparison.OrdinalIgnoreCase))
                        continue;
                    folderTextures.Add(file);
                }
            }
            folderTextures.Sort(StringComparer.OrdinalIgnoreCase);
        }

        using var importer = new AssimpContext();
        // No FlipUVs — UV convention handled by texture/sampler
        // No PreTransformVertices — we need the skeleton for animation
        var scene = importer.ImportFile(daeFilePath,
            PostProcessSteps.Triangulate |
            PostProcessSteps.GenerateSmoothNormals);

        if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete))
            throw new InvalidOperationException($"Failed to load model: {daeFilePath}");

        // Log root node info for debugging
        var rootXform = ToXna(scene.RootNode.Transform);
        Log($"  RootNode='{scene.RootNode.Name}' transform diag=({rootXform.M11:F4},{rootXform.M22:F4},{rootXform.M33:F4},{rootXform.M44:F4})");
        Log($"    row0=({rootXform.M11:F4},{rootXform.M12:F4},{rootXform.M13:F4},{rootXform.M14:F4})");
        Log($"    row3=({rootXform.M41:F4},{rootXform.M42:F4},{rootXform.M43:F4},{rootXform.M44:F4})");

        // ── Step 1: Collect all bone names from skin controllers ──
        var boneNamesFromSkin = new HashSet<string>();
        for (int m = 0; m < scene.MeshCount; m++)
            foreach (var bone in scene.Meshes[m].Bones)
                boneNamesFromSkin.Add(bone.Name);

        Log($"  Bones referenced by skin: {boneNamesFromSkin.Count}");

        // ── Step 2: Build bone hierarchy from only the skeleton nodes ──
        // Find all nodes that are bones or ancestors of bones
        var relevantNodes = new HashSet<string>(boneNamesFromSkin);
        MarkAncestors(scene.RootNode, relevantNodes);

        var boneList = new List<BoneInfo>();
        var boneNameToIndex = new Dictionary<string, int>();
        BuildBoneList(scene.RootNode, relevantNodes, boneList, boneNameToIndex, -1);

        result.Bones = boneList.ToArray();
        Log($"  Bone hierarchy: {result.Bones.Length} nodes");
        for (int i = 0; i < Math.Min(10, result.Bones.Length); i++)
            Log($"    [{i}] '{result.Bones[i].Name}' parent={result.Bones[i].ParentIndex}");

        // ── Step 3: Set offset matrices from mesh bone data ──
        for (int m = 0; m < scene.MeshCount; m++)
            foreach (var bone in scene.Meshes[m].Bones)
                if (boneNameToIndex.TryGetValue(bone.Name, out int idx))
                    result.Bones[idx].OffsetMatrix = ToXna(bone.OffsetMatrix);

        // ── Step 4: Extract animation from DAE XML directly ──
        // Assimp can't handle per-component Euler angle channels (rotation.X/Y/Z)
        // so we parse the COLLADA library_animations ourselves.
        ParseColladaAnimations(daeFilePath, boneNameToIndex, result);

        // ── Step 5: Extract meshes with bone weights ──
        for (int m = 0; m < scene.MeshCount; m++)
        {
            var mesh = scene.Meshes[m];
            bool hasUVs = mesh.HasTextureCoords(0);

            // Build per-vertex bone influences
            var influences = new List<(int boneIdx, float weight)>[mesh.VertexCount];
            for (int i = 0; i < mesh.VertexCount; i++)
                influences[i] = new List<(int, float)>();

            foreach (var bone in mesh.Bones)
            {
                if (!boneNameToIndex.TryGetValue(bone.Name, out int bIdx))
                    continue;
                foreach (var vw in bone.VertexWeights)
                    influences[(int)vw.VertexID].Add((bIdx, vw.Weight));
            }

            var vertBoneIdx = new byte[mesh.VertexCount][];
            var vertBoneWt = new float[mesh.VertexCount][];
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                vertBoneIdx[i] = new byte[4];
                vertBoneWt[i] = new float[] { 0, 0, 0, 0 };

                var inf = influences[i];
                inf.Sort((a, b) => b.weight.CompareTo(a.weight));

                float total = 0f;
                int count = Math.Min(4, inf.Count);
                for (int j = 0; j < count; j++)
                    total += inf[j].weight;

                if (total > 0f)
                {
                    for (int j = 0; j < count; j++)
                    {
                        vertBoneIdx[i][j] = (byte)inf[j].boneIdx;
                        vertBoneWt[i][j] = inf[j].weight / total;
                    }
                }
                else
                {
                    vertBoneWt[i][0] = 1f; // bind to root
                }
            }

            // Build vertices (raw UVs — no FlipUVs)
            var vertices = new VertexPositionNormalTexture[mesh.VertexCount];
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var pos = mesh.Vertices[i];
                var normal = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 1, 0);
                var uv = hasUVs ? mesh.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);

                vertices[i] = new VertexPositionNormalTexture(
                    new Vector3(pos.X, pos.Y, pos.Z),
                    new Vector3(normal.X, normal.Y, normal.Z),
                    new Vector2(uv.X, uv.Y));
            }

            // Extract indices
            var indices = new List<int>();
            for (int f = 0; f < mesh.FaceCount; f++)
            {
                var face = mesh.Faces[f];
                for (int fi = 0; fi < face.IndexCount; fi++)
                    indices.Add(face.Indices[fi]);
            }

            // Resolve texture — check both model directory and textures/ subdirectory
            Texture2D? texture = null;
            string? texturePath = null;
            string matName = "";
            if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
            {
                var material = scene.Materials[mesh.MaterialIndex];
                matName = material.Name ?? "";

                // Primary: Assimp resolved the diffuse path via library_images
                if (material.HasTextureDiffuse && !string.IsNullOrEmpty(material.TextureDiffuse.FilePath))
                {
                    texturePath = BattleModelLoader.ResolveDiffusePath(directory, material.TextureDiffuse.FilePath);
                    // Also try textures/ subdirectory
                    if (texturePath == null && Directory.Exists(texturesSubDir))
                        texturePath = BattleModelLoader.ResolveDiffusePath(texturesSubDir, material.TextureDiffuse.FilePath);
                }

                // Fallback: match material name to texture filename
                if (texturePath == null && !string.IsNullOrEmpty(material.Name))
                {
                    string cleanName = material.Name.Replace("_mat", "");
                    string folderName = Path.GetFileName(directory) ?? "";
                    texturePath = BattleModelLoader.FindTextureForMaterial(directory, folderName, cleanName);
                    // Also try textures/ subdirectory
                    if (texturePath == null && Directory.Exists(texturesSubDir))
                        texturePath = BattleModelLoader.FindTextureForMaterial(texturesSubDir, folderName, cleanName);
                }

                // Last resort: by mesh index
                if (texturePath == null && m < folderTextures.Count)
                    texturePath = folderTextures[m];

                if (texturePath != null)
                {
                    using var stream = File.OpenRead(texturePath);
                    texture = Texture2D.FromStream(graphicsDevice, stream);
                }
            }

            Log($"  [Mesh {m}] mat='{matName}' hasUVs={hasUVs} verts={mesh.VertexCount} " +
                $"bones={mesh.Bones.Count} tex='{Path.GetFileName(texturePath ?? "NONE")}'");

            // Track bounds
            foreach (var v in vertices)
            {
                var p = v.Position;
                if (p.X < result.BoundsMin.X) result.BoundsMin = new Vector3(p.X, result.BoundsMin.Y, result.BoundsMin.Z);
                if (p.Y < result.BoundsMin.Y) result.BoundsMin = new Vector3(result.BoundsMin.X, p.Y, result.BoundsMin.Z);
                if (p.Z < result.BoundsMin.Z) result.BoundsMin = new Vector3(result.BoundsMin.X, result.BoundsMin.Y, p.Z);
                if (p.X > result.BoundsMax.X) result.BoundsMax = new Vector3(p.X, result.BoundsMax.Y, result.BoundsMax.Z);
                if (p.Y > result.BoundsMax.Y) result.BoundsMax = new Vector3(result.BoundsMax.X, p.Y, result.BoundsMax.Z);
                if (p.Z > result.BoundsMax.Z) result.BoundsMax = new Vector3(result.BoundsMax.X, result.BoundsMax.Y, p.Z);
            }

            var meshData = new SkeletalMeshData(graphicsDevice, vertices,
                vertBoneIdx, vertBoneWt, indices.ToArray(), texture);
            result.Meshes.Add(meshData);
        }

        Log($"  Total: {result.Meshes.Count} meshes, bounds=({result.BoundsMin.X:F1},{result.BoundsMin.Y:F1},{result.BoundsMin.Z:F1}) to ({result.BoundsMax.X:F1},{result.BoundsMax.Y:F1},{result.BoundsMax.Z:F1})");
        return result;
    }

    /// <summary>
    /// Walk the scene graph upward and mark all ancestors of bone nodes as relevant.
    /// </summary>
    private static bool MarkAncestors(Node node, HashSet<string> relevant)
    {
        bool childIsRelevant = false;
        foreach (var child in node.Children)
        {
            if (MarkAncestors(child, relevant))
                childIsRelevant = true;
        }
        if (childIsRelevant || relevant.Contains(node.Name))
        {
            relevant.Add(node.Name);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Build a flat bone list from only the relevant (skeleton) nodes.
    /// Nodes not in the relevant set are skipped entirely.
    /// </summary>
    private static void BuildBoneList(Node node, HashSet<string> relevant,
        List<BoneInfo> boneList, Dictionary<string, int> nameToIndex, int parentIndex)
    {
        if (!relevant.Contains(node.Name))
            return;

        int myIndex = boneList.Count;
        nameToIndex[node.Name] = myIndex;
        boneList.Add(new BoneInfo
        {
            Name = node.Name,
            ParentIndex = parentIndex,
            OffsetMatrix = XnaMatrix.Identity,
            LocalBindPose = ToXna(node.Transform),
            GlobalTransform = XnaMatrix.Identity,
            FinalMatrix = XnaMatrix.Identity,
        });

        foreach (var child in node.Children)
            BuildBoneList(child, relevant, boneList, nameToIndex, myIndex);
    }

    // ─── COLLADA animation parsing ────────────────────────────

    /// <summary>
    /// Parse per-component Euler angle animations from COLLADA XML.
    /// Assimp can't handle channel targets like "Waist_bone_id/rotation.X".
    /// </summary>
    private static void ParseColladaAnimations(string daeFilePath,
        Dictionary<string, int> boneNameToIndex, SkeletalModelData result)
    {
        var doc = XDocument.Load(daeFilePath);
        XNamespace ns = doc.Root?.Name.Namespace ?? "";

        // 1. Parse default transform component values from visual_scene nodes
        var nodeDefaults = new Dictionary<string, Dictionary<string, float>>();
        var visualScene = doc.Root?.Element(ns + "library_visual_scenes")
                                  ?.Element(ns + "visual_scene");
        if (visualScene != null)
            ParseNodeDefaults(visualScene, ns, nodeDefaults);

        // 2. Parse all per-component animation channels
        // Grouped: boneName → property → axis → (times, values)
        var boneChannels = new Dictionary<string,
            Dictionary<string, Dictionary<string, (double[] times, float[] values)>>>();
        double maxTime = 0;

        var libAnims = doc.Root?.Element(ns + "library_animations");
        if (libAnims == null)
        {
            Log("  NO ANIMATION section in DAE");
            return;
        }

        foreach (var anim in libAnims.Elements(ns + "animation"))
            ParseAnimationElement(anim, ns, boneChannels, ref maxTime);

        if (boneChannels.Count == 0)
        {
            Log("  No animation channels parsed from DAE");
            return;
        }

        // 3. Build AnimChannels — combine per-component data into Vec3/Quat keys
        var channels = new List<AnimChannel>();

        foreach (var (boneName, props) in boneChannels)
        {
            if (!boneNameToIndex.TryGetValue(boneName, out int boneIdx))
                continue;

            nodeDefaults.TryGetValue(boneName, out var defaults);

            // Position from translation.X/Y/Z
            VecKey[] posKeys;
            if (props.TryGetValue("translation", out var transAxes))
                posKeys = CombineVecKeys(transAxes, defaults, "translation", 0f);
            else
            {
                var bp = result.Bones[boneIdx].LocalBindPose;
                posKeys = new[] { new VecKey { Time = 0, Value = bp.Translation } };
            }

            // Rotation from rotation.X/Y/Z (Euler degrees → quaternion)
            QuatKey[] rotKeys;
            if (props.TryGetValue("rotation", out var rotAxes))
                rotKeys = CombineRotKeys(rotAxes, defaults);
            else
                rotKeys = Array.Empty<QuatKey>();

            // Scale from scale.X/Y/Z
            VecKey[] sclKeys;
            if (props.TryGetValue("scale", out var scaleAxes))
                sclKeys = CombineVecKeys(scaleAxes, defaults, "scale", 1f);
            else
                sclKeys = new[] { new VecKey { Time = 0, Value = Vector3.One } };

            channels.Add(new AnimChannel
            {
                BoneIndex = boneIdx,
                PositionKeys = posKeys,
                RotationKeys = rotKeys,
                ScaleKeys = sclKeys,
            });
        }

        result.Channels = channels.ToArray();
        result.AnimDuration = maxTime;
        result.TicksPerSecond = 1.0; // times are in seconds

        Log($"  Animation: {result.Channels.Length} bone channels, duration={maxTime:F3}s");
    }

    /// <summary>
    /// Recursively parse node transform components (translate, rotate, scale)
    /// from the visual_scene so we have default values for non-animated axes.
    /// </summary>
    private static void ParseNodeDefaults(XElement parent, XNamespace ns,
        Dictionary<string, Dictionary<string, float>> nodeDefaults)
    {
        foreach (var node in parent.Elements(ns + "node"))
        {
            string nodeId = node.Attribute("id")?.Value ?? "";
            if (!string.IsNullOrEmpty(nodeId))
            {
                var defaults = new Dictionary<string, float>();

                // <translate>x y z</translate>
                var translate = node.Element(ns + "translate");
                if (translate != null)
                {
                    var parts = translate.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float tx))
                            defaults["translation.X"] = tx;
                        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ty))
                            defaults["translation.Y"] = ty;
                        if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float tz))
                            defaults["translation.Z"] = tz;
                    }
                }

                // <rotate sid="rotation.X">1 0 0 angle_degrees</rotate>
                foreach (var rotate in node.Elements(ns + "rotate"))
                {
                    string sid = rotate.Attribute("sid")?.Value ?? "";
                    if (!sid.StartsWith("rotation.")) continue;
                    var parts = rotate.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        string axis = sid.Substring("rotation.".Length);
                        if (float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float angle))
                            defaults[$"rotation.{axis}"] = angle;
                    }
                }

                // <scale>sx sy sz</scale>
                var scale = node.Element(ns + "scale");
                if (scale != null)
                {
                    var parts = scale.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float sx))
                            defaults["scale.X"] = sx;
                        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float sy))
                            defaults["scale.Y"] = sy;
                        if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float sz))
                            defaults["scale.Z"] = sz;
                    }
                }

                if (defaults.Count > 0)
                    nodeDefaults[nodeId] = defaults;
            }

            ParseNodeDefaults(node, ns, nodeDefaults);
        }
    }

    /// <summary>
    /// Parse a single COLLADA animation element (handles nested animations too).
    /// Extracts channel target, time array, and value array.
    /// </summary>
    private static void ParseAnimationElement(XElement anim, XNamespace ns,
        Dictionary<string, Dictionary<string, Dictionary<string, (double[] times, float[] values)>>> boneChannels,
        ref double maxTime)
    {
        // Handle nested animation elements
        foreach (var childAnim in anim.Elements(ns + "animation"))
            ParseAnimationElement(childAnim, ns, boneChannels, ref maxTime);

        var channel = anim.Element(ns + "channel");
        if (channel == null) return;

        string target = channel.Attribute("target")?.Value ?? "";
        // Format: "Waist_bone_id/rotation.X"
        int slashIdx = target.IndexOf('/');
        if (slashIdx < 0) return;

        string boneName = target.Substring(0, slashIdx);
        string propAxis = target.Substring(slashIdx + 1);

        int dotIdx = propAxis.IndexOf('.');
        if (dotIdx < 0) return;

        string property = propAxis.Substring(0, dotIdx); // "rotation", "translation", "scale"
        string axis = propAxis.Substring(dotIdx + 1);     // "X", "Y", "Z"

        // Find INPUT and OUTPUT source references from the sampler
        var sampler = anim.Element(ns + "sampler");
        if (sampler == null) return;

        string? inputSourceId = null, outputSourceId = null;
        foreach (var input in sampler.Elements(ns + "input"))
        {
            string semantic = input.Attribute("semantic")?.Value ?? "";
            string sourceRef = (input.Attribute("source")?.Value ?? "").TrimStart('#');
            if (semantic == "INPUT") inputSourceId = sourceRef;
            else if (semantic == "OUTPUT") outputSourceId = sourceRef;
        }
        if (inputSourceId == null || outputSourceId == null) return;

        // Read float arrays from source elements
        double[]? times = null;
        float[]? values = null;
        foreach (var source in anim.Elements(ns + "source"))
        {
            string sourceId = source.Attribute("id")?.Value ?? "";
            var floatArray = source.Element(ns + "float_array");
            if (floatArray == null) continue;

            if (sourceId == inputSourceId)
                times = ParseFloatArrayDouble(floatArray.Value);
            else if (sourceId == outputSourceId)
                values = ParseFloatArrayFloat(floatArray.Value);
        }

        if (times == null || values == null || times.Length == 0 || times.Length != values.Length)
            return;

        if (times[^1] > maxTime)
            maxTime = times[^1];

        // Store grouped by bone → property → axis
        if (!boneChannels.TryGetValue(boneName, out var props))
            boneChannels[boneName] = props = new();
        if (!props.TryGetValue(property, out var axes))
            props[property] = axes = new();
        axes[axis] = (times, values);
    }

    private static double[] ParseFloatArrayDouble(string text)
    {
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        return result;
    }

    private static float[] ParseFloatArrayFloat(string text)
    {
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        return result;
    }

    /// <summary>
    /// Merge per-axis component channels (X/Y/Z) into combined Vector3 keys.
    /// Uses all unique time values across axes; missing axes use bind-pose defaults.
    /// </summary>
    private static VecKey[] CombineVecKeys(
        Dictionary<string, (double[] times, float[] values)> axes,
        Dictionary<string, float>? defaults, string property, float defaultValue)
    {
        var allTimes = new SortedSet<double>();
        foreach (var (_, (times, _)) in axes)
            foreach (var t in times)
                allTimes.Add(t);

        if (allTimes.Count == 0) return Array.Empty<VecKey>();

        float defX = defaults != null && defaults.TryGetValue($"{property}.X", out float dx) ? dx : defaultValue;
        float defY = defaults != null && defaults.TryGetValue($"{property}.Y", out float dy) ? dy : defaultValue;
        float defZ = defaults != null && defaults.TryGetValue($"{property}.Z", out float dz) ? dz : defaultValue;

        var result = new VecKey[allTimes.Count];
        int idx = 0;
        foreach (var t in allTimes)
        {
            float x = axes.TryGetValue("X", out var ax) ? LerpComponent(ax.times, ax.values, t) : defX;
            float y = axes.TryGetValue("Y", out var ay) ? LerpComponent(ay.times, ay.values, t) : defY;
            float z = axes.TryGetValue("Z", out var az) ? LerpComponent(az.times, az.values, t) : defZ;
            result[idx++] = new VecKey { Time = t, Value = new Vector3(x, y, z) };
        }
        return result;
    }

    /// <summary>
    /// Merge per-axis rotation channels (Euler degrees) into quaternion keys.
    /// COLLADA node transforms apply as: translate * rotZ * rotY * rotX * scale,
    /// so intrinsic rotation order is X→Y→Z.
    /// </summary>
    private static QuatKey[] CombineRotKeys(
        Dictionary<string, (double[] times, float[] values)> axes,
        Dictionary<string, float>? defaults)
    {
        var allTimes = new SortedSet<double>();
        foreach (var (_, (times, _)) in axes)
            foreach (var t in times)
                allTimes.Add(t);

        if (allTimes.Count == 0) return Array.Empty<QuatKey>();

        float defX = defaults != null && defaults.TryGetValue("rotation.X", out float dx) ? dx : 0f;
        float defY = defaults != null && defaults.TryGetValue("rotation.Y", out float dy) ? dy : 0f;
        float defZ = defaults != null && defaults.TryGetValue("rotation.Z", out float dz) ? dz : 0f;

        var result = new QuatKey[allTimes.Count];
        int idx = 0;
        foreach (var t in allTimes)
        {
            float rx = axes.TryGetValue("X", out var ax) ? LerpComponent(ax.times, ax.values, t) : defX;
            float ry = axes.TryGetValue("Y", out var ay) ? LerpComponent(ay.times, ay.values, t) : defY;
            float rz = axes.TryGetValue("Z", out var az) ? LerpComponent(az.times, az.values, t) : defZ;

            // Degrees → radians
            float radX = MathHelper.ToRadians(rx);
            float radY = MathHelper.ToRadians(ry);
            float radZ = MathHelper.ToRadians(rz);

            // Compose: intrinsic X → Y → Z (COLLADA order)
            var qx = XnaQuat.CreateFromAxisAngle(Vector3.UnitX, radX);
            var qy = XnaQuat.CreateFromAxisAngle(Vector3.UnitY, radY);
            var qz = XnaQuat.CreateFromAxisAngle(Vector3.UnitZ, radZ);
            result[idx++] = new QuatKey { Time = t, Value = qx * qy * qz };
        }
        return result;
    }

    /// <summary>
    /// Linearly interpolate a single float component at time t.
    /// </summary>
    private static float LerpComponent(double[] times, float[] values, double t)
    {
        if (values.Length == 0) return 0f;
        if (values.Length == 1 || t <= times[0]) return values[0];
        if (t >= times[^1]) return values[^1];

        for (int i = 0; i < times.Length - 1; i++)
        {
            if (t <= times[i + 1])
            {
                float f = (float)((t - times[i]) / (times[i + 1] - times[i]));
                return values[i] + (values[i + 1] - values[i]) * f;
            }
        }
        return values[^1];
    }
}

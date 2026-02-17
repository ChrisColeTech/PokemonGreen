using System.Globalization;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Rendering.Skeletal;

public static class ColladaSkeletalLoader
{
    private static readonly XNamespace ColladaNs = "http://www.collada.org/2005/11/COLLADASchema";

    public static SkeletonRig LoadSkeleton(string daePath)
    {
        XDocument doc = XDocument.Load(daePath);
        XElement? visualScene = doc.Descendants(ColladaNs + "library_visual_scenes")
            .Elements(ColladaNs + "visual_scene")
            .FirstOrDefault();
        if (visualScene is null)
        {
            throw new InvalidDataException("DAE has no visual_scene");
        }

        List<SkeletonBone> bones = new();
        foreach (XElement rootNode in visualScene.Elements(ColladaNs + "node"))
        {
            ParseJointRecursive(rootNode, -1, bones);
        }

        if (bones.Count == 0)
        {
            throw new InvalidDataException("DAE has no JOINT nodes");
        }

        return new SkeletonRig(bones);
    }

    public static SkeletalAnimationClip LoadClip(string clipDaePath, SkeletonRig rig, string clipName)
    {
        XDocument doc = XDocument.Load(clipDaePath);

        Dictionary<string, float[]> floatSources = doc
            .Descendants(ColladaNs + "source")
            .Where(x => x.Attribute("id") is not null)
            .Select(x => new
            {
                Id = x.Attribute("id")!.Value,
                Array = ParseFloats(x.Element(ColladaNs + "float_array")?.Value)
            })
            .Where(x => x.Array.Length > 0)
            .ToDictionary(x => x.Id, x => x.Array, StringComparer.Ordinal);

        List<BoneAnimationTrack> tracks = new();
        float clipDuration = 0f;

        foreach (XElement animation in doc.Descendants(ColladaNs + "animation"))
        {
            XElement? sampler = animation.Element(ColladaNs + "sampler");
            XElement? channel = animation.Element(ColladaNs + "channel");
            if (sampler is null || channel is null)
            {
                continue;
            }

            string target = channel.Attribute("target")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target) || !target.EndsWith("/transform", StringComparison.Ordinal))
            {
                continue;
            }

            string targetBone = target[..target.IndexOf('/')] ;
            if (!rig.TryGetBoneIndex(targetBone, out int boneIndex))
            {
                continue;
            }

            string? inputSourceId = ResolveSamplerSourceId(sampler, "INPUT");
            string? outputSourceId = ResolveSamplerSourceId(sampler, "OUTPUT");
            if (inputSourceId is null || outputSourceId is null)
            {
                continue;
            }

            if (!floatSources.TryGetValue(inputSourceId, out float[]? times))
            {
                continue;
            }

            if (!floatSources.TryGetValue(outputSourceId, out float[]? values))
            {
                continue;
            }

            int matrixCount = values.Length / 16;
            int keyCount = Math.Min(times.Length, matrixCount);
            if (keyCount == 0)
            {
                continue;
            }

            List<AnimationKeyframe> keyframes = new(keyCount);
            for (int i = 0; i < keyCount; i++)
            {
                Matrix m = ParseMatrix(values, i * 16);
                keyframes.Add(new AnimationKeyframe
                {
                    TimeSeconds = times[i],
                    Transform = m
                });
            }

            clipDuration = Math.Max(clipDuration, keyframes[^1].TimeSeconds);
            tracks.Add(new BoneAnimationTrack
            {
                BoneIndex = boneIndex,
                Keyframes = keyframes
            });
        }

        return new SkeletalAnimationClip
        {
            Name = clipName,
            DurationSeconds = clipDuration,
            Tracks = tracks
        };
    }

    private static void ParseJointRecursive(XElement node, int parentIndex, List<SkeletonBone> bones)
    {
        string? type = node.Attribute("type")?.Value;
        bool isJoint = string.Equals(type, "JOINT", StringComparison.OrdinalIgnoreCase);

        int currentParentIndex = parentIndex;
        if (isJoint)
        {
            int index = bones.Count;
            string name = node.Attribute("name")?.Value ?? node.Attribute("sid")?.Value ?? $"bone_{index}";
            string nodeId = node.Attribute("id")?.Value ?? name;
            Matrix bindLocal = ParseNodeLocalTransform(node);

            bones.Add(new SkeletonBone
            {
                Index = index,
                Name = name,
                NodeId = nodeId,
                ParentIndex = parentIndex,
                BindLocalTransform = bindLocal
            });

            currentParentIndex = index;
        }

        foreach (XElement child in node.Elements(ColladaNs + "node"))
        {
            ParseJointRecursive(child, currentParentIndex, bones);
        }
    }

    private static Matrix ParseNodeLocalTransform(XElement node)
    {
        XElement? matrixElement = node.Element(ColladaNs + "matrix");
        if (matrixElement is not null)
        {
            float[] values = ParseFloats(matrixElement.Value);
            if (values.Length >= 16)
            {
                return ParseMatrix(values, 0);
            }
        }

        Vector3 translation = Vector3.Zero;
        Vector3 scale = Vector3.One;
        Quaternion rotation = Quaternion.Identity;

        foreach (XElement transform in node.Elements())
        {
            string localName = transform.Name.LocalName;
            if (localName == "translate")
            {
                float[] values = ParseFloats(transform.Value);
                if (values.Length >= 3)
                {
                    translation = new Vector3(values[0], values[1], values[2]);
                }

                continue;
            }

            if (localName == "rotate")
            {
                float[] values = ParseFloats(transform.Value);
                if (values.Length >= 4)
                {
                    Vector3 axis = new Vector3(values[0], values[1], values[2]);
                    float axisLength = axis.Length();
                    if (axisLength > 0f)
                    {
                        axis /= axisLength;
                        float radians = MathHelper.ToRadians(values[3]);
                        rotation *= Quaternion.CreateFromAxisAngle(axis, radians);
                    }
                }

                continue;
            }

            if (localName == "scale")
            {
                float[] values = ParseFloats(transform.Value);
                if (values.Length >= 3)
                {
                    scale = new Vector3(values[0], values[1], values[2]);
                }
            }
        }

        return Matrix.CreateScale(scale) * Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(translation);
    }

    private static string? ResolveSamplerSourceId(XElement sampler, string semantic)
    {
        XElement? input = sampler.Elements(ColladaNs + "input")
            .FirstOrDefault(x => string.Equals(x.Attribute("semantic")?.Value, semantic, StringComparison.Ordinal));
        if (input is null)
        {
            return null;
        }

        string? source = input.Attribute("source")?.Value;
        return source?.TrimStart('#');
    }

    private static Matrix ParseMatrix(float[] values, int offset)
    {
        return new Matrix(
            values[offset], values[offset + 1], values[offset + 2], values[offset + 3],
            values[offset + 4], values[offset + 5], values[offset + 6], values[offset + 7],
            values[offset + 8], values[offset + 9], values[offset + 10], values[offset + 11],
            values[offset + 12], values[offset + 13], values[offset + 14], values[offset + 15]);
    }

    private static float[] ParseFloats(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<float>();

        string[] parts = value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        float[] result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        }

        return result;
    }
}

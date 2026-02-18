using System.Globalization;
using System.Text;
using System.Xml;
using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;

public sealed class TrinityAnimationDaeClipWriter
{
    public void Write(string outputPath, ExportArmature armature, TrinityDecodedAnimation clip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(armature);
        ArgumentNullException.ThrowIfNull(clip);

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var stream = File.Create(outputPath);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineOnAttributes = false,
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("COLLADA", "http://www.collada.org/2005/11/COLLADASchema");
        writer.WriteAttributeString("version", "1.4.1");

        WriteAsset(writer);
        WriteLibraryAnimations(writer, clip);
        WriteVisualScene(writer, armature, clip.Name);
        WriteScene(writer);

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteAsset(XmlWriter writer)
    {
        writer.WriteStartElement("asset");
        writer.WriteElementString("created", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        writer.WriteElementString("modified", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

        writer.WriteStartElement("unit");
        writer.WriteAttributeString("name", "meter");
        writer.WriteAttributeString("meter", "1");
        writer.WriteEndElement();

        writer.WriteElementString("up_axis", "Y_UP");
        writer.WriteEndElement();
    }

    private static void WriteLibraryAnimations(XmlWriter writer, TrinityDecodedAnimation clip)
    {
        writer.WriteStartElement("library_animations");

        foreach (var track in clip.BoneTracks.OrderBy(item => item.BoneName, StringComparer.Ordinal))
        {
            var nodeId = BuildBoneNodeId(track.BoneName);
            var animationId = SanitizeId(track.BoneName, "anim");
            var inputId = animationId + "-input";
            var outputId = animationId + "-output";
            var interpolationId = animationId + "-interpolation";
            var samplerId = animationId + "-sampler";

            var poses = track.Poses.OrderBy(item => item.Frame).ToList();
            var frameRate = clip.FrameRate <= 0f ? 30f : clip.FrameRate;
            var times = poses.Select(pose => pose.Frame / frameRate).ToList();
            var matrices = poses.Select(BuildTransformMatrix).ToList();

            writer.WriteStartElement("animation");
            writer.WriteAttributeString("id", animationId);

            WriteFloatSource(writer, inputId, times, 1, "TIME");
            WriteFloatSource(writer, outputId, matrices.SelectMany(values => values), 16, "TRANSFORM");
            WriteNameSource(writer, interpolationId, Enumerable.Repeat("LINEAR", poses.Count));

            writer.WriteStartElement("sampler");
            writer.WriteAttributeString("id", samplerId);
            WriteInput(writer, "INPUT", inputId);
            WriteInput(writer, "OUTPUT", outputId);
            WriteInput(writer, "INTERPOLATION", interpolationId);
            writer.WriteEndElement();

            writer.WriteStartElement("channel");
            writer.WriteAttributeString("source", $"#{samplerId}");
            writer.WriteAttributeString("target", $"{nodeId}/matrix");
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteVisualScene(XmlWriter writer, ExportArmature armature, string clipName)
    {
        writer.WriteStartElement("library_visual_scenes");
        writer.WriteStartElement("visual_scene");
        writer.WriteAttributeString("id", "Scene");
        writer.WriteAttributeString("name", clipName);

        var children = new Dictionary<int, List<int>>();
        for (var i = 0; i < armature.Bones.Count; i++)
        {
            var parent = armature.Bones[i].ParentIndex;
            if (!children.TryGetValue(parent, out var siblings))
            {
                siblings = [];
                children.Add(parent, siblings);
            }

            siblings.Add(i);
        }

        if (!children.TryGetValue(-1, out var roots) || roots.Count == 0)
        {
            roots = [0];
        }

        foreach (var root in roots.OrderBy(item => item))
        {
            WriteBoneNodeRecursive(writer, armature, root, children);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteBoneNodeRecursive(
        XmlWriter writer,
        ExportArmature armature,
        int boneIndex,
        IReadOnlyDictionary<int, List<int>> children)
    {
        if (boneIndex < 0 || boneIndex >= armature.Bones.Count)
        {
            return;
        }

        var bone = armature.Bones[boneIndex];

        writer.WriteStartElement("node");
        writer.WriteAttributeString("id", BuildBoneNodeId(bone.Name));
        writer.WriteAttributeString("sid", SanitizeId(bone.Name, "bone"));
        writer.WriteAttributeString("name", bone.Name);
        writer.WriteAttributeString("type", "JOINT");
        writer.WriteElementString("matrix", FormatMatrix(BuildTransformMatrix(
            new TrinityDecodedBonePose(0f, bone.Translation, bone.RotationEulerRadians, bone.Scale))));

        if (children.TryGetValue(boneIndex, out var childIndices))
        {
            foreach (var childIndex in childIndices.OrderBy(item => item))
            {
                WriteBoneNodeRecursive(writer, armature, childIndex, children);
            }
        }

        writer.WriteEndElement();
    }

    private static void WriteScene(XmlWriter writer)
    {
        writer.WriteStartElement("scene");
        writer.WriteStartElement("instance_visual_scene");
        writer.WriteAttributeString("url", "#Scene");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static IReadOnlyList<float> BuildTransformMatrix(TrinityDecodedBonePose pose)
    {
        var sx = pose.Scale.X;
        var sy = pose.Scale.Y;
        var sz = pose.Scale.Z;

        var rx = pose.RotationEulerRadians.X;
        var ry = pose.RotationEulerRadians.Y;
        var rz = pose.RotationEulerRadians.Z;

        var cx = MathF.Cos(rx);
        var sinX = MathF.Sin(rx);
        var cy = MathF.Cos(ry);
        var sinY = MathF.Sin(ry);
        var cz = MathF.Cos(rz);
        var sinZ = MathF.Sin(rz);

        var m11 = cy * cz * sx;
        var m12 = (cz * sinX * sinY - cx * sinZ) * sy;
        var m13 = (cx * cz * sinY + sinX * sinZ) * sz;

        var m21 = cy * sinZ * sx;
        var m22 = (cx * cz + sinX * sinY * sinZ) * sy;
        var m23 = (-cz * sinX + cx * sinY * sinZ) * sz;

        var m31 = -sinY * sx;
        var m32 = cy * sinX * sy;
        var m33 = cx * cy * sz;

        return
        [
            m11, m12, m13, 0f,
            m21, m22, m23, 0f,
            m31, m32, m33, 0f,
            pose.Translation.X, pose.Translation.Y, pose.Translation.Z, 1f,
        ];
    }

    private static string BuildBoneNodeId(string boneName)
    {
        return SanitizeId(boneName, "joint");
    }

    private static string SanitizeId(string name, string suffix)
    {
        var safe = string.IsNullOrWhiteSpace(name)
            ? "bone"
            : new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray());

        return $"{safe}-{suffix}";
    }

    private static void WriteFloatSource(XmlWriter writer, string sourceId, IEnumerable<float> values, int stride, params string[] parameters)
    {
        var data = values.ToList();

        writer.WriteStartElement("source");
        writer.WriteAttributeString("id", sourceId);

        var arrayId = sourceId + "-array";
        writer.WriteStartElement("float_array");
        writer.WriteAttributeString("id", arrayId);
        writer.WriteAttributeString("count", data.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteString(string.Join(" ", data.Select(Format)));
        writer.WriteEndElement();

        writer.WriteStartElement("technique_common");
        writer.WriteStartElement("accessor");
        writer.WriteAttributeString("source", $"#{arrayId}");
        writer.WriteAttributeString("count", (data.Count / stride).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("stride", stride.ToString(CultureInfo.InvariantCulture));

        foreach (var parameter in parameters)
        {
            writer.WriteStartElement("param");
            writer.WriteAttributeString("name", parameter);
            writer.WriteAttributeString("type", "float");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteNameSource(XmlWriter writer, string sourceId, IEnumerable<string> values)
    {
        var data = values.ToList();

        writer.WriteStartElement("source");
        writer.WriteAttributeString("id", sourceId);

        var arrayId = sourceId + "-array";
        writer.WriteStartElement("Name_array");
        writer.WriteAttributeString("id", arrayId);
        writer.WriteAttributeString("count", data.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteString(string.Join(" ", data));
        writer.WriteEndElement();

        writer.WriteStartElement("technique_common");
        writer.WriteStartElement("accessor");
        writer.WriteAttributeString("source", $"#{arrayId}");
        writer.WriteAttributeString("count", data.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("stride", "1");
        writer.WriteStartElement("param");
        writer.WriteAttributeString("name", "INTERPOLATION");
        writer.WriteAttributeString("type", "Name");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteInput(XmlWriter writer, string semantic, string sourceId)
    {
        writer.WriteStartElement("input");
        writer.WriteAttributeString("semantic", semantic);
        writer.WriteAttributeString("source", $"#{sourceId}");
        writer.WriteEndElement();
    }

    private static string Format(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string FormatMatrix(IReadOnlyList<float> values)
    {
        return string.Join(" ", values.Select(Format));
    }
}

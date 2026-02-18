using PokemonGreen.SwitchToolboxCli.Core.Models;
using System.Globalization;
using System.Text;
using System.Xml;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Dae;

public sealed class ColladaWriter : IDisposable
{
    private readonly XmlWriter _writer;

    public ColladaWriter(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _writer = XmlWriter.Create(output, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineOnAttributes = false,
        });
    }

    public void Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _writer.WriteStartDocument();
        _writer.WriteStartElement("COLLADA", "http://www.collada.org/2005/11/COLLADASchema");
        _writer.WriteAttributeString("version", "1.4.1");

        WriteAsset();
        WriteLibraryGeometries(model);

        var armature = model.Armature;
        if (armature is { HasBones: true })
        {
            WriteLibraryControllers(model, armature);
        }

        WriteVisualScenes(model, armature);
        WriteScene();

        _writer.WriteEndElement();
        _writer.WriteEndDocument();
    }

    public void Dispose()
    {
        _writer.Dispose();
    }

    private void WriteAsset()
    {
        _writer.WriteStartElement("asset");
        _writer.WriteElementString("created", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        _writer.WriteElementString("modified", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

        _writer.WriteStartElement("unit");
        _writer.WriteAttributeString("name", "meter");
        _writer.WriteAttributeString("meter", "1");
        _writer.WriteEndElement();

        _writer.WriteElementString("up_axis", "Y_UP");
        _writer.WriteEndElement();
    }

    private void WriteLibraryGeometries(ExportModel model)
    {
        _writer.WriteStartElement("library_geometries");
        foreach (var mesh in model.Meshes)
        {
            WriteGeometry(mesh);
        }

        _writer.WriteEndElement();
    }

    private void WriteLibraryControllers(ExportModel model, ExportArmature armature)
    {
        _writer.WriteStartElement("library_controllers");

        var rootBoneIndex = GetRootBoneIndex(armature);
        foreach (var mesh in model.Meshes)
        {
            WriteController(mesh, armature, rootBoneIndex);
        }

        _writer.WriteEndElement();
    }

    private void WriteController(ExportMesh mesh, ExportArmature armature, int rootBoneIndex)
    {
        var meshId = SanitizeId(mesh.MeshName, "mesh");
        var controllerId = SanitizeId(mesh.MeshName, "controller");
        var jointsId = $"{controllerId}-joints";
        var bindPosesId = $"{controllerId}-bind-poses";
        var weightsId = $"{controllerId}-weights";

        _writer.WriteStartElement("controller");
        _writer.WriteAttributeString("id", controllerId);
        _writer.WriteAttributeString("name", controllerId);

        _writer.WriteStartElement("skin");
        _writer.WriteAttributeString("source", $"#{SanitizeId(mesh.MeshName, "geometry")}");

        _writer.WriteElementString("bind_shape_matrix", "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1");

        WriteNameSource(jointsId, armature.Bones.Select(bone => BuildBoneNodeId(bone.Name)));
        WriteFloatSource(
            bindPosesId,
            armature.Bones.SelectMany(bone => bone.InverseBindMatrix.ToRowMajor()),
            16,
            "TRANSFORM");
        WriteFloatSource(weightsId, [1f], 1, "WEIGHT");

        _writer.WriteStartElement("joints");
        WriteInput("JOINT", jointsId);
        WriteInput("INV_BIND_MATRIX", bindPosesId);
        _writer.WriteEndElement();

        _writer.WriteStartElement("vertex_weights");
        _writer.WriteAttributeString("count", mesh.Vertices.Count.ToString(CultureInfo.InvariantCulture));
        WriteInput("JOINT", jointsId, 0);
        WriteInput("WEIGHT", weightsId, 1);

        _writer.WriteStartElement("vcount");
        _writer.WriteString(string.Join(" ", Enumerable.Repeat("1", mesh.Vertices.Count)));
        _writer.WriteEndElement();

        var pairs = new List<int>(mesh.Vertices.Count * 2);
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            pairs.Add(rootBoneIndex);
            pairs.Add(0);
        }

        _writer.WriteStartElement("v");
        _writer.WriteString(string.Join(" ", pairs.Select(v => v.ToString(CultureInfo.InvariantCulture))));
        _writer.WriteEndElement();

        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    private void WriteGeometry(ExportMesh mesh)
    {
        var geometryId = SanitizeId(mesh.MeshName, "geometry");
        var meshId = SanitizeId(mesh.MeshName, "mesh");
        var positionsId = $"{meshId}-positions";
        var verticesId = $"{meshId}-vertices";
        var normalsId = $"{meshId}-normals";
        var uvId = $"{meshId}-uv";

        _writer.WriteStartElement("geometry");
        _writer.WriteAttributeString("id", geometryId);
        _writer.WriteAttributeString("name", mesh.MeshName);

        _writer.WriteStartElement("mesh");

        WriteFloatSource(positionsId, mesh.Vertices.SelectMany(v => new[] { v.X, v.Y, v.Z }), 3, "X", "Y", "Z");
        if (mesh.HasNormals)
        {
            WriteFloatSource(normalsId, mesh.Normals.SelectMany(v => new[] { v.X, v.Y, v.Z }), 3, "X", "Y", "Z");
        }

        if (mesh.HasUvs)
        {
            WriteFloatSource(uvId, mesh.Uvs.SelectMany(v => new[] { v.X, v.Y }), 2, "S", "T");
        }

        _writer.WriteStartElement("vertices");
        _writer.WriteAttributeString("id", verticesId);
        WriteInput("POSITION", positionsId);
        _writer.WriteEndElement();

        var triangleCount = mesh.Indices.Count / 3;
        _writer.WriteStartElement("triangles");
        _writer.WriteAttributeString("count", triangleCount.ToString(CultureInfo.InvariantCulture));

        WriteInput("VERTEX", verticesId, 0);
        var nextOffset = 1;
        if (mesh.HasNormals)
        {
            WriteInput("NORMAL", normalsId, nextOffset++);
        }

        if (mesh.HasUvs)
        {
            WriteInput("TEXCOORD", uvId, nextOffset, 0);
        }

        _writer.WriteStartElement("p");
        _writer.WriteString(BuildIndexPayload(mesh));
        _writer.WriteEndElement();

        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    private void WriteVisualScenes(ExportModel model, ExportArmature? armature)
    {
        _writer.WriteStartElement("library_visual_scenes");
        _writer.WriteStartElement("visual_scene");
        _writer.WriteAttributeString("id", "Scene");
        _writer.WriteAttributeString("name", model.ModelName);

        var rootBoneId = string.Empty;
        if (armature is { HasBones: true })
        {
            rootBoneId = BuildBoneNodeId(armature.Bones[GetRootBoneIndex(armature)].Name);
            WriteArmatureNodes(armature);
        }

        foreach (var mesh in model.Meshes)
        {
            _writer.WriteStartElement("node");
            _writer.WriteAttributeString("id", SanitizeId(mesh.MeshName, "node"));
            _writer.WriteAttributeString("name", mesh.MeshName);
            _writer.WriteAttributeString("type", "NODE");

            if (armature is { HasBones: true } && !string.IsNullOrWhiteSpace(rootBoneId))
            {
                _writer.WriteStartElement("instance_controller");
                _writer.WriteAttributeString("url", $"#{SanitizeId(mesh.MeshName, "controller")}");
                _writer.WriteElementString("skeleton", $"#{rootBoneId}");
                _writer.WriteEndElement();
            }
            else
            {
                _writer.WriteStartElement("instance_geometry");
                _writer.WriteAttributeString("url", $"#{SanitizeId(mesh.MeshName, "geometry")}");
                _writer.WriteEndElement();
            }

            _writer.WriteEndElement();
        }

        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    private void WriteArmatureNodes(ExportArmature armature)
    {
        var children = new Dictionary<int, List<int>>();
        for (var i = 0; i < armature.Bones.Count; i++)
        {
            var parent = armature.Bones[i].ParentIndex;
            if (!children.TryGetValue(parent, out var list))
            {
                list = [];
                children.Add(parent, list);
            }

            list.Add(i);
        }

        if (!children.TryGetValue(-1, out var roots) || roots.Count == 0)
        {
            roots = [0];
        }

        foreach (var rootIndex in roots.Distinct().OrderBy(index => index))
        {
            WriteBoneNodeRecursive(armature, rootIndex, children);
        }
    }

    private void WriteBoneNodeRecursive(ExportArmature armature, int boneIndex, IReadOnlyDictionary<int, List<int>> children)
    {
        if (boneIndex < 0 || boneIndex >= armature.Bones.Count)
        {
            return;
        }

        var bone = armature.Bones[boneIndex];

        _writer.WriteStartElement("node");
        _writer.WriteAttributeString("id", BuildBoneNodeId(bone.Name));
        _writer.WriteAttributeString("sid", SanitizeId(bone.Name, "bone"));
        _writer.WriteAttributeString("name", bone.Name);
        _writer.WriteAttributeString("type", "JOINT");

        _writer.WriteStartElement("translate");
        _writer.WriteString($"{Format(bone.Translation.X)} {Format(bone.Translation.Y)} {Format(bone.Translation.Z)}");
        _writer.WriteEndElement();

        _writer.WriteStartElement("rotate");
        _writer.WriteString($"1 0 0 {Format(RadiansToDegrees(bone.RotationEulerRadians.X))}");
        _writer.WriteEndElement();
        _writer.WriteStartElement("rotate");
        _writer.WriteString($"0 1 0 {Format(RadiansToDegrees(bone.RotationEulerRadians.Y))}");
        _writer.WriteEndElement();
        _writer.WriteStartElement("rotate");
        _writer.WriteString($"0 0 1 {Format(RadiansToDegrees(bone.RotationEulerRadians.Z))}");
        _writer.WriteEndElement();

        _writer.WriteStartElement("scale");
        _writer.WriteString($"{Format(bone.Scale.X)} {Format(bone.Scale.Y)} {Format(bone.Scale.Z)}");
        _writer.WriteEndElement();

        if (children.TryGetValue(boneIndex, out var boneChildren))
        {
            foreach (var childIndex in boneChildren.OrderBy(index => index))
            {
                WriteBoneNodeRecursive(armature, childIndex, children);
            }
        }

        _writer.WriteEndElement();
    }

    private void WriteScene()
    {
        _writer.WriteStartElement("scene");
        _writer.WriteStartElement("instance_visual_scene");
        _writer.WriteAttributeString("url", "#Scene");
        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    private void WriteFloatSource(string sourceId, IEnumerable<float> values, int stride, params string[] parameters)
    {
        var list = values.ToList();

        _writer.WriteStartElement("source");
        _writer.WriteAttributeString("id", sourceId);

        var arrayId = $"{sourceId}-array";
        _writer.WriteStartElement("float_array");
        _writer.WriteAttributeString("id", arrayId);
        _writer.WriteAttributeString("count", list.Count.ToString(CultureInfo.InvariantCulture));
        _writer.WriteString(string.Join(" ", list.Select(Format)));
        _writer.WriteEndElement();

        _writer.WriteStartElement("technique_common");
        _writer.WriteStartElement("accessor");
        _writer.WriteAttributeString("source", $"#{arrayId}");
        _writer.WriteAttributeString("count", (list.Count / stride).ToString(CultureInfo.InvariantCulture));
        _writer.WriteAttributeString("stride", stride.ToString(CultureInfo.InvariantCulture));

        foreach (var parameter in parameters)
        {
            _writer.WriteStartElement("param");
            _writer.WriteAttributeString("name", parameter);
            _writer.WriteAttributeString("type", "float");
            _writer.WriteEndElement();
        }

        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    private void WriteNameSource(string sourceId, IEnumerable<string> values)
    {
        var list = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();

        _writer.WriteStartElement("source");
        _writer.WriteAttributeString("id", sourceId);

        var arrayId = $"{sourceId}-array";
        _writer.WriteStartElement("Name_array");
        _writer.WriteAttributeString("id", arrayId);
        _writer.WriteAttributeString("count", list.Count.ToString(CultureInfo.InvariantCulture));
        _writer.WriteString(string.Join(" ", list));
        _writer.WriteEndElement();

        _writer.WriteStartElement("technique_common");
        _writer.WriteStartElement("accessor");
        _writer.WriteAttributeString("source", $"#{arrayId}");
        _writer.WriteAttributeString("count", list.Count.ToString(CultureInfo.InvariantCulture));
        _writer.WriteAttributeString("stride", "1");

        _writer.WriteStartElement("param");
        _writer.WriteAttributeString("name", "JOINT");
        _writer.WriteAttributeString("type", "Name");
        _writer.WriteEndElement();

        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndElement();
    }

    private void WriteInput(string semantic, string sourceId, int? offset = null, int? set = null)
    {
        _writer.WriteStartElement("input");
        _writer.WriteAttributeString("semantic", semantic);
        _writer.WriteAttributeString("source", $"#{sourceId}");

        if (offset.HasValue)
        {
            _writer.WriteAttributeString("offset", offset.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (set.HasValue)
        {
            _writer.WriteAttributeString("set", set.Value.ToString(CultureInfo.InvariantCulture));
        }

        _writer.WriteEndElement();
    }

    private static string BuildIndexPayload(ExportMesh mesh)
    {
        var hasNormals = mesh.HasNormals;
        var hasUvs = mesh.HasUvs;
        var indices = new List<int>(mesh.Indices.Count * (1 + (hasNormals ? 1 : 0) + (hasUvs ? 1 : 0)));

        foreach (var index in mesh.Indices)
        {
            indices.Add(index);
            if (hasNormals)
            {
                indices.Add(index);
            }

            if (hasUvs)
            {
                indices.Add(index);
            }
        }

        return string.Join(" ", indices.Select(i => i.ToString(CultureInfo.InvariantCulture)));
    }

    private static int GetRootBoneIndex(ExportArmature armature)
    {
        for (var i = 0; i < armature.Bones.Count; i++)
        {
            if (armature.Bones[i].ParentIndex < 0)
            {
                return i;
            }
        }

        return 0;
    }

    private static string BuildBoneNodeId(string name)
    {
        return SanitizeId(name, "joint");
    }

    private static string SanitizeId(string name, string suffix)
    {
        var safe = string.IsNullOrWhiteSpace(name)
            ? "mesh"
            : new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray());

        return $"{safe}-{suffix}";
    }

    private static string Format(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static float RadiansToDegrees(float radians)
    {
        return radians * (180f / MathF.PI);
    }
}

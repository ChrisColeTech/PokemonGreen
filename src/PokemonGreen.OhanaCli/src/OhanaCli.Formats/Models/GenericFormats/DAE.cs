using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace OhanaCli.Formats.Models.GenericFormats
{
    public class DAE
    {
        [XmlRootAttribute("COLLADA", Namespace = "http://www.collada.org/2005/11/COLLADASchema")]
        public class COLLADA
        {
            [XmlAttribute]
            public string version = "1.4.1";

            public daeAsset asset = new daeAsset();

            [XmlArrayItem("image")]
            public List<daeImage> library_images = new List<daeImage>();

            [XmlArrayItem("material")]
            public List<daeMaterial> library_materials = new List<daeMaterial>();

            [XmlArrayItem("effect")]
            public List<daeEffect> library_effects = new List<daeEffect>();

            [XmlArrayItem("geometry")]
            public List<daeGeometry> library_geometries = new List<daeGeometry>();

            [XmlArrayItem("controller")]
            public List<daeController> library_controllers;

            [XmlArrayItem("animation")]
            public List<daeAnimation> library_animations;

            [XmlArrayItem("visual_scene")]
            public List<daeVisualScene> library_visual_scenes = new List<daeVisualScene>();

            [XmlArrayItem("instance_visual_scene")]
            public List<daeInstaceVisualScene> scene = new List<daeInstaceVisualScene>();
        }

        public class daeAsset
        {
            public string created;
            public string modified;
            public string up_axis;
        }

        public class daeImage
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public string init_from;
        }

        public class daeInstanceEffect
        {
            [XmlAttribute]
            public string url;
        }

        public class daeMaterial
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public daeInstanceEffect instance_effect = new daeInstanceEffect();
        }

        public class daeParamSurfaceElement
        {
            [XmlAttribute]
            public string type;

            public string init_from;
            public string format;
        }

        public class daeParamSampler2DElement
        {
            public string source;
            public string wrap_s;
            public string wrap_t;
            public string minfilter;
            public string magfilter;
            public string mipfilter;
        }

        public class daeParam
        {
            [XmlAttribute]
            public string sid;

            [XmlElement(IsNullable = false)]
            public daeParamSurfaceElement surface;

            [XmlElement(IsNullable = false)]
            public daeParamSampler2DElement sampler2D;
        }

        public class daePhongDiffuseTexture
        {
            [XmlAttribute]
            public string texture;

            [XmlAttribute]
            public string texcoord = "uv";
        }

        public class daePhongDiffuse
        {
            public daePhongDiffuseTexture texture = new daePhongDiffuseTexture();
        }

        public class daeColor
        {
            public string color;

            public void set(Color col)
            {
                color = string.Format(
                    "{0} {1} {2} {3}",
                    getString(col.R / 255f),
                    getString(col.G / 255f),
                    getString(col.B / 255f),
                    getString(col.A / 255f));
            }

            private string getString(float value)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public class daePhong
        {
            public daeColor emission = new daeColor();
            public daeColor ambient = new daeColor();
            public daePhongDiffuse diffuse = new daePhongDiffuse();
            public daeColor specular = new daeColor();
        }

        public class daeTechnique
        {
            [XmlAttribute]
            public string sid;

            public daePhong phong = new daePhong();
        }

        public class daeProfile
        {
            [XmlAttribute]
            public string sid;

            [XmlElement("newparam")]
            public List<daeParam> newparam = new List<daeParam>();
            public daeTechnique technique = new daeTechnique();
        }

        public class daeEffect
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public daeProfile profile_COMMON = new daeProfile();
        }

        public class daeFloatArray
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public uint count;

            [XmlText]
            public string data;

            public void set(List<float> content)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < content.Count; i++)
                {
                    if (i < content.Count - 1)
                        strArray.Append(content[i].ToString(CultureInfo.InvariantCulture) + " ");
                    else
                        strArray.Append(content[i].ToString(CultureInfo.InvariantCulture));
                }
                count = (uint)content.Count;
                data = strArray.ToString();
            }

            public List<float> get()
            {
                List<float> output = new List<float>();
                string[] values = data.Split(Convert.ToChar(" "));
                for (int i = 0; i < values.Length; i++) output.Add(float.Parse(values[i]));
                return output;
            }
        }

        public class daeNameArray
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public uint count;

            [XmlText]
            public string data;

            public void set(List<string> content)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < content.Count; i++)
                {
                    if (i < content.Count - 1)
                        strArray.Append(content[i] + " ");
                    else
                        strArray.Append(content[i]);
                }
                count = (uint)content.Count;
                data = strArray.ToString();
            }

            public List<string> get()
            {
                List<string> output = new List<string>();
                string[] values = data.Split(Convert.ToChar(" "));
                output.AddRange(values);
                return output;
            }
        }

        public class daeAccessorParam
        {
            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public string type;
        }

        public class daeAccessor
        {
            [XmlAttribute]
            public string source;

            [XmlAttribute]
            public uint count;

            [XmlAttribute]
            public uint stride;

            [XmlElement("param")]
            public List<daeAccessorParam> param = new List<daeAccessorParam>();

            public void addParam(string name, string type)
            {
                daeAccessorParam prm = new daeAccessorParam();

                prm.name = name;
                prm.type = type;

                param.Add(prm);
            }
        }

        public class daeMeshTechnique
        {
            public daeAccessor accessor = new daeAccessor();
        }

        public class daeSource
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            [XmlElement(IsNullable = false)]
            public daeNameArray Name_array;

            [XmlElement(IsNullable = false)]
            public daeFloatArray float_array;

            public daeMeshTechnique technique_common = new daeMeshTechnique();
        }

        public class daeInput
        {
            [XmlAttribute]
            public string semantic;

            [XmlAttribute]
            public string source;
        }

        public class daeInputTable
        {
            [XmlAttribute]
            public string id;

            [XmlElement("input")]
            public List<daeInput> input = new List<daeInput>();

            public void addInput(string semantic, string source)
            {
                daeInput i = new daeInput();

                i.semantic = semantic;
                i.source = source;

                input.Add(i);
            }
        }

        public class daeInputOffset
        {
            [XmlAttribute]
            public string semantic;

            [XmlAttribute]
            public string source;

            [XmlAttribute]
            public uint offset;

            [XmlAttribute]
            public uint set;

            public bool ShouldSerializeset() { return semantic == "TEXCOORD"; }
        }

        public class daeTriangles
        {
            [XmlAttribute]
            public string material;

            [XmlAttribute]
            public uint count;

            [XmlElement("input")]
            public List<daeInputOffset> input = new List<daeInputOffset>();

            public string p;

            public void addInput(string semantic, string source, uint offset = 0, uint set = 0)
            {
                daeInputOffset i = new daeInputOffset();

                i.semantic = semantic;
                i.source = source;
                i.offset = offset;
                i.set = set;

                input.Add(i);
            }

            public void set(List<uint> indices)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < indices.Count; i++)
                {
                    if (i < indices.Count - 1)
                        strArray.Append(indices[i].ToString(CultureInfo.InvariantCulture) + " ");
                    else
                        strArray.Append(indices[i].ToString(CultureInfo.InvariantCulture));
                }
                count = (uint)(indices.Count / 3);
                p = strArray.ToString();
            }

            public List<uint> get()
            {
                List<uint> output = new List<uint>();
                string[] values = p.Split(Convert.ToChar(" "));
                for (int i = 0; i < values.Length; i++) output.Add(uint.Parse(values[i]));
                return output;
            }
        }

        public class daeMesh
        {
            [XmlElement("source")]
            public List<daeSource> source = new List<daeSource>();

            public daeInputTable vertices = new daeInputTable();
            public daeTriangles triangles = new daeTriangles();
        }

        public class daeGeometry
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            public daeMesh mesh = new daeMesh();
        }

        public class daeVertexWeights
        {
            [XmlAttribute]
            public uint count;

            [XmlElement("input")]
            public List<daeInputOffset> input = new List<daeInputOffset>();

            public string vcount;
            public string v;

            public void addInput(string semantic, string source, uint offset = 0)
            {
                daeInputOffset i = new daeInputOffset();

                i.semantic = semantic;
                i.source = source;
                i.offset = offset;

                input.Add(i);
            }
        }

        public class daeSkin
        {
            [XmlAttribute]
            public string source;

            public daeMatrix bind_shape_matrix = new daeMatrix();

            [XmlElement("source")]
            public List<daeSource> src = new List<daeSource>();

            public daeInputTable joints = new daeInputTable();
            public daeVertexWeights vertex_weights = new daeVertexWeights();
        }

        public class daeController
        {
            [XmlAttribute]
            public string id;

            public daeSkin skin = new daeSkin();
        }

        public class daeMatrix
        {
            [XmlText]
            public string data;

            public void set(RenderBase.OMatrix mtx)
            {
                StringBuilder strArray = new StringBuilder();
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (i == 3 && j == 3)
                            strArray.Append(mtx[j, i].ToString(CultureInfo.InvariantCulture));
                        else
                            strArray.Append(mtx[j, i].ToString(CultureInfo.InvariantCulture) + " ");
                    }

                }
                data = strArray.ToString();
            }

            public RenderBase.OMatrix get()
            {
                RenderBase.OMatrix output = new RenderBase.OMatrix();
                string[] values = data.Split(Convert.ToChar(" "));
                int k = 0;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        output[j, i] = float.Parse(values[k++]);
                    }

                }
                return output;
            }
        }

        public class daeTranslate
        {
            [XmlAttribute]
            public string sid;

            [XmlText]
            public string data;

            public void set(float x, float y, float z)
            {
                data = x.ToString(CultureInfo.InvariantCulture) + " " +
                       y.ToString(CultureInfo.InvariantCulture) + " " +
                       z.ToString(CultureInfo.InvariantCulture);
            }
        }

        public class daeRotate
        {
            [XmlAttribute]
            public string sid;

            [XmlText]
            public string data;

            public void set(float axisX, float axisY, float axisZ, float angleDegrees)
            {
                data = axisX.ToString(CultureInfo.InvariantCulture) + " " +
                       axisY.ToString(CultureInfo.InvariantCulture) + " " +
                       axisZ.ToString(CultureInfo.InvariantCulture) + " " +
                       angleDegrees.ToString(CultureInfo.InvariantCulture);
            }
        }

        public class daeScale
        {
            [XmlAttribute]
            public string sid;

            [XmlText]
            public string data;

            public void set(float x, float y, float z)
            {
                data = x.ToString(CultureInfo.InvariantCulture) + " " +
                       y.ToString(CultureInfo.InvariantCulture) + " " +
                       z.ToString(CultureInfo.InvariantCulture);
            }
        }

        public class daeBindMaterialInstace
        {
            [XmlAttribute]
            public string symbol;

            [XmlAttribute]
            public string target;
        }

        public class daeBindMaterial
        {
            public daeBindMaterialInstace instance_material = new daeBindMaterialInstace();
        }

        public class daeBindMaterialTechnique
        {
            public daeBindMaterial technique_common = new daeBindMaterial();
        }

        public class daeInstanceGeometry
        {
            [XmlAttribute]
            public string url;

            public daeBindMaterialTechnique bind_material = new daeBindMaterialTechnique();
        }

        public class daeInstanceController
        {
            [XmlAttribute]
            public string url;

            public string skeleton;
            public daeBindMaterialTechnique bind_material = new daeBindMaterialTechnique();
        }

        public class daeNode
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            [XmlAttribute]
            public string sid;

            [XmlAttribute]
            public string type = "NODE";

            // Decomposed transforms for joint nodes (order matters for COLLADA)
            [XmlElement("scale", IsNullable = false)]
            public daeScale scale;

            [XmlElement("rotate", IsNullable = false)]
            public List<daeRotate> rotate;

            [XmlElement("translate", IsNullable = false)]
            public daeTranslate translate;

            // Matrix transform for non-joint nodes
            [XmlElement("matrix", IsNullable = false)]
            public daeMatrix matrix;

            [XmlElement("node", IsNullable = false)]
            public List<daeNode> childs;

            [XmlElement(IsNullable = false)]
            public daeInstanceGeometry instance_geometry;

            [XmlElement(IsNullable = false)]
            public daeInstanceController instance_controller;
        }

        public class daeVisualScene
        {
            [XmlAttribute]
            public string id;

            [XmlAttribute]
            public string name;

            [XmlElement("node")]
            public List<daeNode> node = new List<daeNode>();
        }

        public class daeInstaceVisualScene
        {
            [XmlAttribute]
            public string url;
        }

        public class daeAnimation
        {
            [XmlAttribute]
            public string id;

            [XmlElement("source")]
            public List<daeSource> source = new List<daeSource>();

            [XmlElement("sampler")]
            public List<daeAnimationSampler> sampler = new List<daeAnimationSampler>();

            [XmlElement("channel")]
            public List<daeChannel> channel = new List<daeChannel>();
        }

        public class daeAnimationSampler
        {
            [XmlAttribute]
            public string id;

            [XmlElement("input")]
            public List<daeInput> input = new List<daeInput>();

            public void addInput(string semantic, string source)
            {
                daeInput i = new daeInput();
                i.semantic = semantic;
                i.source = source;
                input.Add(i);
            }
        }

        public class daeChannel
        {
            [XmlAttribute]
            public string source;

            [XmlAttribute]
            public string target;
        }

        /// <summary>
        ///     Exports a Model to the Collada format.
        ///     See: https://www.khronos.org/files/collada_spec_1_4.pdf for more information.
        /// </summary>
        /// <param name="model">The Model that will be exported</param>
        /// <param name="fileName">The output File Name</param>
        /// <param name="modelIndex">Index of the model to be exported</param>
        /// <param name="skeletalAnimationIndex">(Optional) Index of the skeletal animation</param>
        public static void export(RenderBase.OModelGroup model, string fileName, int modelIndex, int skeletalAnimationIndex = -1)
        {
            RenderBase.OModel mdl = model.model[modelIndex];
            COLLADA dae = new COLLADA();

            dae.asset.created = DateTime.Now.ToString("yyyy-MM-ddThh:mm:ssZ");
            dae.asset.modified = dae.asset.created;
            dae.asset.up_axis = "Y_UP";

            foreach (RenderBase.OTexture tex in model.texture)
            {
                daeImage img = new daeImage();
                img.id = tex.name + "_id";
                img.name = tex.name;
                img.init_from = "./" + tex.name + ".png";

                dae.library_images.Add(img);
            }

            foreach (RenderBase.OMaterial mat in mdl.material)
            {
                daeMaterial mtl = new daeMaterial();
                mtl.name = mat.name + "_mat";
                mtl.id = mtl.name + "_id";
                mtl.instance_effect.url = "#eff_" + mat.name + "_id";

                dae.library_materials.Add(mtl);

                daeEffect eff = new daeEffect();
                eff.id = "eff_" + mat.name + "_id";
                eff.name = "eff_" + mat.name;

                daeParam surface = new daeParam();
                surface.surface = new daeParamSurfaceElement();
                surface.sid = "img_surface_" + mat.name;
                surface.surface.type = "2D";
                surface.surface.init_from = mat.name0 + "_id";
                surface.surface.format = "PNG";
                eff.profile_COMMON.newparam.Add(surface);

                daeParam sampler = new daeParam();
                sampler.sampler2D = new daeParamSampler2DElement();
                sampler.sid = "img_sampler_" + mat.name;
                sampler.sampler2D.source = "img_surface_" + mat.name;

                switch (mat.textureMapper[0].wrapU)
                {
                    case RenderBase.OTextureWrap.repeat: sampler.sampler2D.wrap_s = "WRAP"; break;
                    case RenderBase.OTextureWrap.mirroredRepeat: sampler.sampler2D.wrap_s = "MIRROR"; break;
                    case RenderBase.OTextureWrap.clampToEdge: sampler.sampler2D.wrap_s = "CLAMP"; break;
                    case RenderBase.OTextureWrap.clampToBorder: sampler.sampler2D.wrap_s = "BORDER"; break;
                    default: sampler.sampler2D.wrap_s = "NONE"; break;
                }

                switch (mat.textureMapper[0].wrapV)
                {
                    case RenderBase.OTextureWrap.repeat: sampler.sampler2D.wrap_t = "WRAP"; break;
                    case RenderBase.OTextureWrap.mirroredRepeat: sampler.sampler2D.wrap_t = "MIRROR"; break;
                    case RenderBase.OTextureWrap.clampToEdge: sampler.sampler2D.wrap_t = "CLAMP"; break;
                    case RenderBase.OTextureWrap.clampToBorder: sampler.sampler2D.wrap_t = "BORDER"; break;
                    default: sampler.sampler2D.wrap_t = "NONE"; break;
                }

                switch (mat.textureMapper[0].minFilter)
                {
                    case RenderBase.OTextureMinFilter.linearMipmapLinear: sampler.sampler2D.minfilter = "LINEAR_MIPMAP_LINEAR"; break;
                    case RenderBase.OTextureMinFilter.linearMipmapNearest: sampler.sampler2D.minfilter = "LINEAR_MIPMAP_NEAREST"; break;
                    case RenderBase.OTextureMinFilter.nearestMipmapLinear: sampler.sampler2D.minfilter = "NEAREST_MIPMAP_LINEAR"; break;
                    case RenderBase.OTextureMinFilter.nearestMipmapNearest: sampler.sampler2D.minfilter = "NEAREST_MIPMAP_NEAREST"; break;
                    default: sampler.sampler2D.minfilter = "NONE"; break;
                }

                switch (mat.textureMapper[0].magFilter)
                {
                    case RenderBase.OTextureMagFilter.linear: sampler.sampler2D.magfilter = "LINEAR"; break;
                    case RenderBase.OTextureMagFilter.nearest: sampler.sampler2D.magfilter = "NEAREST"; break;
                    default: sampler.sampler2D.magfilter = "NONE"; break;
                }

                sampler.sampler2D.mipfilter = sampler.sampler2D.magfilter;

                eff.profile_COMMON.newparam.Add(sampler);

                eff.profile_COMMON.technique.sid = "img_technique";
                eff.profile_COMMON.technique.phong.emission.set(Color.Black);
                eff.profile_COMMON.technique.phong.ambient.set(Color.Black);
                eff.profile_COMMON.technique.phong.specular.set(Color.White);
                eff.profile_COMMON.technique.phong.diffuse.texture.texture = "img_sampler_" + mat.name;

                dae.library_effects.Add(eff);
            }

            string jointNames = null;
            string invBindPoses = null;
            for (int index = 0; index < mdl.skeleton.Count; index++)
            {
                RenderBase.OMatrix transform = new RenderBase.OMatrix();
                transformSkeleton(mdl.skeleton, index, ref transform);

                jointNames += mdl.skeleton[index].name;
                daeMatrix mtx = new daeMatrix();
                mtx.set(transform.invert());
                invBindPoses += mtx.data;
                if (index < mdl.skeleton.Count - 1)
                {
                    jointNames += " ";
                    invBindPoses += " ";
                }
            }

            int meshIndex = 0;
            daeVisualScene vs = new daeVisualScene();
            vs.name = "vs_" + mdl.name;
            vs.id = vs.name + "_id";
            if (mdl.skeleton.Count > 0) writeSkeleton(mdl.skeleton, 0, ref vs.node);
            foreach (RenderBase.OMesh obj in mdl.mesh)
            {
                //Geometry
                daeGeometry geometry = new daeGeometry();

                string meshName = "mesh_" + meshIndex++ + "_" + obj.name;
                geometry.id = meshName + "_id";
                geometry.name = meshName;

                MeshUtils.optimizedMesh mesh = MeshUtils.optimizeMesh(obj);
                List<float> positions = new List<float>();
                List<float> normals = new List<float>();
                List<float> uv0 = new List<float>();
                List<float> uv1 = new List<float>();
                List<float> uv2 = new List<float>();
                List<float> colors = new List<float>();
                foreach (RenderBase.OVertex vtx in mesh.vertices)
                {
                    positions.Add(vtx.position.x);
                    positions.Add(vtx.position.y);
                    positions.Add(vtx.position.z);

                    if (mesh.hasNormal)
                    {
                        normals.Add(vtx.normal.x);
                        normals.Add(vtx.normal.y);
                        normals.Add(vtx.normal.z);
                    }

                    if (mesh.texUVCount > 0)
                    {
                        uv0.Add(vtx.texture0.x);
                        uv0.Add(vtx.texture0.y);
                    }

                    if (mesh.texUVCount > 1)
                    {
                        uv1.Add(vtx.texture1.x);
                        uv1.Add(vtx.texture1.y);
                    }

                    if (mesh.texUVCount > 2)
                    {
                        uv2.Add(vtx.texture2.x);
                        uv2.Add(vtx.texture2.y);
                    }

                    if (mesh.hasColor)
                    {
                        colors.Add(((vtx.diffuseColor >> 16) & 0xff) / 255f);
                        colors.Add(((vtx.diffuseColor >> 8) & 0xff) / 255f);
                        colors.Add((vtx.diffuseColor & 0xff) / 255f);
                        colors.Add(((vtx.diffuseColor >> 24) & 0xff) / 255f);
                    }
                }

                daeSource position = new daeSource();
                position.name = meshName + "_position";
                position.id = position.name + "_id";
                position.float_array = new daeFloatArray();
                position.float_array.id = position.name + "_array_id";
                position.float_array.set(positions);
                position.technique_common.accessor.source = "#" + position.float_array.id;
                position.technique_common.accessor.count = (uint)mesh.vertices.Count;
                position.technique_common.accessor.stride = 3;
                position.technique_common.accessor.addParam("X", "float");
                position.technique_common.accessor.addParam("Y", "float");
                position.technique_common.accessor.addParam("Z", "float");

                geometry.mesh.source.Add(position);

                daeSource normal = new daeSource();
                if (mesh.hasNormal)
                {
                    normal.name = meshName + "_normal";
                    normal.id = normal.name + "_id";
                    normal.float_array = new daeFloatArray();
                    normal.float_array.id = normal.name + "_array_id";
                    normal.float_array.set(normals);
                    normal.technique_common.accessor.source = "#" + normal.float_array.id;
                    normal.technique_common.accessor.count = (uint)mesh.vertices.Count;
                    normal.technique_common.accessor.stride = 3;
                    normal.technique_common.accessor.addParam("X", "float");
                    normal.technique_common.accessor.addParam("Y", "float");
                    normal.technique_common.accessor.addParam("Z", "float");

                    geometry.mesh.source.Add(normal);
                }

                daeSource[] texUV = new daeSource[3];
                for (int i = 0; i < mesh.texUVCount; i++)
                {
                    texUV[i] = new daeSource();

                    texUV[i].name = meshName + "_uv" + i;
                    texUV[i].id = texUV[i].name + "_id";
                    texUV[i].float_array = new daeFloatArray();
                    texUV[i].float_array.id = texUV[i].name + "_array_id";
                    texUV[i].technique_common.accessor.source = "#" + texUV[i].float_array.id;
                    texUV[i].technique_common.accessor.count = (uint)mesh.vertices.Count;
                    texUV[i].technique_common.accessor.stride = 2;
                    texUV[i].technique_common.accessor.addParam("S", "float");
                    texUV[i].technique_common.accessor.addParam("T", "float");

                    geometry.mesh.source.Add(texUV[i]);
                }

                daeSource color = new daeSource();
                if (mesh.hasColor)
                {
                    color.name = meshName + "_color";
                    color.id = color.name + "_id";
                    color.float_array = new daeFloatArray();
                    color.float_array.id = color.name + "_array_id";
                    color.float_array.set(colors);
                    color.technique_common.accessor.source = "#" + color.float_array.id;
                    color.technique_common.accessor.count = (uint)mesh.vertices.Count;
                    color.technique_common.accessor.stride = 4;
                    color.technique_common.accessor.addParam("R", "float");
                    color.technique_common.accessor.addParam("G", "float");
                    color.technique_common.accessor.addParam("B", "float");
                    color.technique_common.accessor.addParam("A", "float");

                    geometry.mesh.source.Add(color);
                }

                geometry.mesh.vertices.id = meshName + "_vertices_id";
                geometry.mesh.vertices.addInput("POSITION", "#" + position.id);


                geometry.mesh.triangles.material = mdl.material[obj.materialId].name;
                geometry.mesh.triangles.addInput("VERTEX", "#" + geometry.mesh.vertices.id);
                if (mesh.hasNormal) geometry.mesh.triangles.addInput("NORMAL", "#" + normal.id);
                if (mesh.hasColor) geometry.mesh.triangles.addInput("COLOR", "#" + color.id);
                if (mesh.texUVCount > 0)
                {
                    texUV[0].float_array.set(uv0);
                    geometry.mesh.triangles.addInput("TEXCOORD", "#" + texUV[0].id);
                }
                if (mesh.texUVCount > 1)
                {
                    texUV[1].float_array.set(uv1);
                    geometry.mesh.triangles.addInput("TEXCOORD", "#" + texUV[1].id, 0, 1);
                }
                if (mesh.texUVCount > 2)
                {
                    texUV[2].float_array.set(uv2);
                    geometry.mesh.triangles.addInput("TEXCOORD", "#" + texUV[2].id, 0, 2);
                }
                geometry.mesh.triangles.set(mesh.indices);

                dae.library_geometries.Add(geometry);

                bool hasNode = obj.vertices[0].node.Count > 0;
                bool hasWeight = obj.vertices[0].weight.Count > 0;
                bool hasController = hasNode && hasWeight;

                //Controller
                daeController controller = new daeController();
                if (hasController)
                {
                    controller.id = meshName + "_ctrl_id";

                    controller.skin.source = "#" + geometry.id;
                    controller.skin.bind_shape_matrix.set(new RenderBase.OMatrix());

                    daeSource joints = new daeSource();
                    joints.id = meshName + "_ctrl_joint_names_id";
                    joints.Name_array = new daeNameArray();
                    joints.Name_array.id = meshName + "_ctrl_joint_names_array_id";
                    joints.Name_array.count = (uint)mdl.skeleton.Count;
                    joints.Name_array.data = jointNames;
                    joints.technique_common.accessor.source = "#" + joints.Name_array.id;
                    joints.technique_common.accessor.count = joints.Name_array.count;
                    joints.technique_common.accessor.stride = 1;
                    joints.technique_common.accessor.addParam("JOINT", "Name");

                    controller.skin.src.Add(joints);

                    daeSource bindPoses = new daeSource();
                    bindPoses.id = meshName + "_ctrl_inv_bind_poses_id";
                    bindPoses.float_array = new daeFloatArray();
                    bindPoses.float_array.id = meshName + "_ctrl_inv_bind_poses_array_id";
                    bindPoses.float_array.count = (uint)(mdl.skeleton.Count * 16);
                    bindPoses.float_array.data = invBindPoses;
                    bindPoses.technique_common.accessor.source = "#" + bindPoses.float_array.id;
                    bindPoses.technique_common.accessor.count = (uint)mdl.skeleton.Count;
                    bindPoses.technique_common.accessor.stride = 16;
                    bindPoses.technique_common.accessor.addParam("TRANSFORM", "float4x4");

                    controller.skin.src.Add(bindPoses);

                    daeSource weights = new daeSource();
                    weights.id = meshName + "_ctrl_weights_id";
                    weights.float_array = new daeFloatArray();
                    weights.float_array.id = meshName + "_ctrl_weights_array_id";
                    weights.technique_common.accessor.source = "#" + weights.float_array.id;
                    weights.technique_common.accessor.stride = 1;
                    weights.technique_common.accessor.addParam("WEIGHT", "float");

                    StringBuilder w = new StringBuilder();
                    StringBuilder vcount = new StringBuilder();
                    StringBuilder v = new StringBuilder();

                    float[] wLookBack = new float[32];
                    uint wLookBackIndex = 0;
                    int buffLen = 0;

                    int wIndex = 0;
                    int wCount = 0;
                    foreach (RenderBase.OVertex vtx in mesh.vertices)
                    {
                        int count = Math.Min(vtx.node.Count, vtx.weight.Count);

                        vcount.Append(count + " ");
                        for (int n = 0; n < count; n++)
                        {
                            v.Append(vtx.node[n] + " ");
                            bool found = false;
                            uint bPos = (wLookBackIndex - 1) & 0x1f;
                            for (int i = 0; i < buffLen; i++)
                            {
                                if (wLookBack[bPos] == vtx.weight[n])
                                {
                                    v.Append(wIndex - (i + 1) + " ");
                                    found = true;
                                    break;
                                }
                                bPos = (bPos - 1) & 0x1f;
                            }

                            if (!found)
                            {
                                v.Append(wIndex++ + " ");
                                w.Append(vtx.weight[n].ToString(CultureInfo.InvariantCulture) + " ");
                                wCount++;

                                wLookBack[wLookBackIndex] = vtx.weight[n];
                                wLookBackIndex = (wLookBackIndex + 1) & 0x1f;
                                if (buffLen < wLookBack.Length) buffLen++;
                            }
                        }
                    }

                    weights.float_array.data = w.ToString().TrimEnd();
                    weights.float_array.count = (uint)wCount;
                    weights.technique_common.accessor.count = (uint)wCount;

                    controller.skin.src.Add(weights);
                    controller.skin.vertex_weights.vcount = vcount.ToString().TrimEnd();
                    controller.skin.vertex_weights.v = v.ToString().TrimEnd();
                    controller.skin.vertex_weights.count = (uint)mesh.vertices.Count;
                    controller.skin.joints.addInput("JOINT", "#" + joints.id);
                    controller.skin.joints.addInput("INV_BIND_MATRIX", "#" + bindPoses.id);

                    controller.skin.vertex_weights.addInput("JOINT", "#" + joints.id);
                    controller.skin.vertex_weights.addInput("WEIGHT", "#" + weights.id, 1);

                    if (dae.library_controllers == null) dae.library_controllers = new List<daeController>();
                    dae.library_controllers.Add(controller);
                }

                //Visual scene node
                daeNode node = new daeNode();
                node.name = "vsn_" + meshName;
                node.id = node.name + "_id";
                node.matrix = new daeMatrix();
                node.matrix.set(new RenderBase.OMatrix());
                if (hasController)
                {
                    node.instance_controller = new daeInstanceController();
                    node.instance_controller.url = "#" + controller.id;
                    node.instance_controller.skeleton = "#" + mdl.skeleton[0].name + "_bone_id";
                    node.instance_controller.bind_material.technique_common.instance_material.symbol = mdl.material[obj.materialId].name;
                    node.instance_controller.bind_material.technique_common.instance_material.target = "#" + mdl.material[obj.materialId].name + "_mat_id";
                }
                else
                {
                    node.instance_geometry = new daeInstanceGeometry();
                    node.instance_geometry.url = "#" + geometry.id;
                    node.instance_geometry.bind_material.technique_common.instance_material.symbol = mdl.material[obj.materialId].name;
                    node.instance_geometry.bind_material.technique_common.instance_material.target = "#" + mdl.material[obj.materialId].name + "_mat_id";
                }

                vs.node.Add(node);
            }
            dae.library_visual_scenes.Add(vs);

            daeInstaceVisualScene scene = new daeInstaceVisualScene();
            scene.url = "#" + vs.id;
            dae.scene.Add(scene);

            if (skeletalAnimationIndex > -1 && skeletalAnimationIndex < model.skeletalAnimation.list.Count)
            {
                exportAnimation(dae, mdl, (RenderBase.OSkeletalAnimation)model.skeletalAnimation.list[skeletalAnimationIndex]);
            }

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "\t"
            };

            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "http://www.collada.org/2005/11/COLLADASchema");
            XmlSerializer serializer = new XmlSerializer(typeof(COLLADA));
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            using (XmlWriter output = XmlWriter.Create(fs, settings))
            {
                serializer.Serialize(output, dae, ns);
            }
        }

        /// <summary>
        ///     Transforms a Skeleton from relative to absolute positions.
        /// </summary>
        /// <param name="skeleton">The skeleton</param>
        /// <param name="index">Index of the bone to convert</param>
        /// <param name="target">Target matrix to save bone transformation</param>
        private static void transformSkeleton(List<RenderBase.OBone> skeleton, int index, ref RenderBase.OMatrix target)
        {
            target *= RenderBase.OMatrix.rotateX(skeleton[index].rotation.x);
            target *= RenderBase.OMatrix.rotateY(skeleton[index].rotation.y);
            target *= RenderBase.OMatrix.rotateZ(skeleton[index].rotation.z);
            target *= RenderBase.OMatrix.translate(skeleton[index].translation);
            if (skeleton[index].parentId > -1) transformSkeleton(skeleton, skeleton[index].parentId, ref target);
        }

        /// <summary>
        ///     Writes the skeleton hierarchy to the DAE.
        /// </summary>
        /// <param name="skeleton">The skeleton</param>
        /// <param name="index">Index of the current bone (root bone when it's not a recursive call)</param>
        /// <param name="nodes">List with the DAE nodes</param>
        private static float toDeg(float radians) { return radians * (180.0f / (float)Math.PI); }

        private static void writeSkeleton(List<RenderBase.OBone> skeleton, int index, ref List<daeNode> nodes)
        {
            daeNode node = new daeNode();
            node.name = skeleton[index].name;
            node.id = node.name + "_bone_id";
            node.sid = node.name;
            node.type = "JOINT";

            RenderBase.OBone bone = skeleton[index];

            // Decomposed transforms with SIDs (order: S * Rx * Ry * Rz * T)
            node.scale = new daeScale();
            node.scale.sid = "scale";
            node.scale.set(bone.scale.x == 0 ? 1 : bone.scale.x,
                           bone.scale.y == 0 ? 1 : bone.scale.y,
                           bone.scale.z == 0 ? 1 : bone.scale.z);

            node.rotate = new List<daeRotate>();
            daeRotate rx = new daeRotate(); rx.sid = "rotationX"; rx.set(1, 0, 0, toDeg(bone.rotation.x));
            daeRotate ry = new daeRotate(); ry.sid = "rotationY"; ry.set(0, 1, 0, toDeg(bone.rotation.y));
            daeRotate rz = new daeRotate(); rz.sid = "rotationZ"; rz.set(0, 0, 1, toDeg(bone.rotation.z));
            node.rotate.Add(rx);
            node.rotate.Add(ry);
            node.rotate.Add(rz);

            node.translate = new daeTranslate();
            node.translate.sid = "translation";
            node.translate.set(bone.translation.x, bone.translation.y, bone.translation.z);

            for (int i = 0; i < skeleton.Count; i++)
            {
                if (skeleton[i].parentId == index)
                {
                    if (node.childs == null) node.childs = new List<daeNode>();
                    writeSkeleton(skeleton, i, ref node.childs);
                }
            }

            nodes.Add(node);
        }

        private static void exportAnimation(
            COLLADA dae,
            RenderBase.OModel mdl,
            RenderBase.OSkeletalAnimation anim)
        {
            if (dae.library_animations == null) dae.library_animations = new List<daeAnimation>();

            foreach (RenderBase.OSkeletalAnimationBone bone in anim.bone)
            {
                // Find skeleton bone index
                int boneIndex = -1;
                for (int i = 0; i < mdl.skeleton.Count; i++)
                {
                    if (mdl.skeleton[i].name == bone.name) { boneIndex = i; break; }
                }
                if (boneIndex == -1) continue;

                string boneSid = bone.name;
                string nodeId = boneSid + "_bone_id";
                string[] axisNames = { "scaleX", "scaleY", "scaleZ", "rotationX", "rotationY", "rotationZ", "translationX", "translationY", "translationZ" };
                string[] targetPaths = {
                    nodeId + "/scale.X",
                    nodeId + "/scale.Y",
                    nodeId + "/scale.Z",
                    nodeId + "/rotationX.ANGLE",
                    nodeId + "/rotationY.ANGLE",
                    nodeId + "/rotationZ.ANGLE",
                    nodeId + "/translation.X",
                    nodeId + "/translation.Y",
                    nodeId + "/translation.Z"
                };
                // Rotation axes (indices 3-5) need radian-to-degree conversion
                bool[] needsDegConversion = { false, false, false, true, true, true, false, false, false };

                RenderBase.OAnimationKeyFrameGroup[] groups = {
                    bone.scaleX, bone.scaleY, bone.scaleZ,
                    bone.rotationX, bone.rotationY, bone.rotationZ,
                    bone.translationX, bone.translationY, bone.translationZ
                };

                for (int axis = 0; axis < 9; axis++)
                {
                    if (!groups[axis].exists || groups[axis].keyFrames.Count == 0) continue;

                    daeAnimation animNode = new daeAnimation();
                    string animId = "anim_" + bone.name + "_" + axisNames[axis];
                    animNode.id = animId;

                    // INPUT source (time)
                    daeSource inputSrc = new daeSource();
                    inputSrc.id = animId + "_input";
                    inputSrc.float_array = new daeFloatArray();
                    inputSrc.float_array.id = animId + "_input_array";
                    List<float> times = new List<float>();
                    foreach (RenderBase.OAnimationKeyFrame kf in groups[axis].keyFrames)
                        times.Add(kf.frame / anim.frameSize);
                    inputSrc.float_array.set(times);
                    inputSrc.technique_common.accessor.source = "#" + inputSrc.float_array.id;
                    inputSrc.technique_common.accessor.count = (uint)times.Count;
                    inputSrc.technique_common.accessor.stride = 1;
                    inputSrc.technique_common.accessor.addParam("TIME", "float");
                    animNode.source.Add(inputSrc);

                    // OUTPUT source (values)
                    daeSource outputSrc = new daeSource();
                    outputSrc.id = animId + "_output";
                    outputSrc.float_array = new daeFloatArray();
                    outputSrc.float_array.id = animId + "_output_array";
                    List<float> values = new List<float>();
                    foreach (RenderBase.OAnimationKeyFrame kf in groups[axis].keyFrames)
                        values.Add(needsDegConversion[axis] ? toDeg(kf.value) : kf.value);
                    outputSrc.float_array.set(values);
                    outputSrc.technique_common.accessor.source = "#" + outputSrc.float_array.id;
                    outputSrc.technique_common.accessor.count = (uint)values.Count;
                    outputSrc.technique_common.accessor.stride = 1;
                    outputSrc.technique_common.accessor.addParam("VALUE", "float");
                    animNode.source.Add(outputSrc);

                    // INTERPOLATION source
                    daeSource interpSrc = new daeSource();
                    interpSrc.id = animId + "_interpolation";
                    interpSrc.Name_array = new daeNameArray();
                    interpSrc.Name_array.id = animId + "_interpolation_array";
                    List<string> interps = new List<string>();
                    for (int k = 0; k < groups[axis].keyFrames.Count; k++)
                        interps.Add("HERMITE");
                    interpSrc.Name_array.set(interps);
                    interpSrc.technique_common.accessor.source = "#" + interpSrc.Name_array.id;
                    interpSrc.technique_common.accessor.count = (uint)interps.Count;
                    interpSrc.technique_common.accessor.stride = 1;
                    interpSrc.technique_common.accessor.addParam("INTERPOLATION", "Name");
                    animNode.source.Add(interpSrc);

                    // IN_TANGENT source
                    daeSource inTangentSrc = new daeSource();
                    inTangentSrc.id = animId + "_in_tangent";
                    inTangentSrc.float_array = new daeFloatArray();
                    inTangentSrc.float_array.id = animId + "_in_tangent_array";
                    List<float> inTangents = new List<float>();
                    foreach (RenderBase.OAnimationKeyFrame kf in groups[axis].keyFrames)
                    {
                        inTangents.Add(kf.frame / anim.frameSize);
                        inTangents.Add(needsDegConversion[axis] ? toDeg(kf.inSlope) : kf.inSlope);
                    }
                    inTangentSrc.float_array.set(inTangents);
                    inTangentSrc.technique_common.accessor.source = "#" + inTangentSrc.float_array.id;
                    inTangentSrc.technique_common.accessor.count = (uint)groups[axis].keyFrames.Count;
                    inTangentSrc.technique_common.accessor.stride = 2;
                    inTangentSrc.technique_common.accessor.addParam("X", "float");
                    inTangentSrc.technique_common.accessor.addParam("Y", "float");
                    animNode.source.Add(inTangentSrc);

                    // OUT_TANGENT source
                    daeSource outTangentSrc = new daeSource();
                    outTangentSrc.id = animId + "_out_tangent";
                    outTangentSrc.float_array = new daeFloatArray();
                    outTangentSrc.float_array.id = animId + "_out_tangent_array";
                    List<float> outTangents = new List<float>();
                    foreach (RenderBase.OAnimationKeyFrame kf in groups[axis].keyFrames)
                    {
                        outTangents.Add(kf.frame / anim.frameSize);
                        outTangents.Add(needsDegConversion[axis] ? toDeg(kf.outSlope) : kf.outSlope);
                    }
                    outTangentSrc.float_array.set(outTangents);
                    outTangentSrc.technique_common.accessor.source = "#" + outTangentSrc.float_array.id;
                    outTangentSrc.technique_common.accessor.count = (uint)groups[axis].keyFrames.Count;
                    outTangentSrc.technique_common.accessor.stride = 2;
                    outTangentSrc.technique_common.accessor.addParam("X", "float");
                    outTangentSrc.technique_common.accessor.addParam("Y", "float");
                    animNode.source.Add(outTangentSrc);

                    // Sampler
                    daeAnimationSampler samp = new daeAnimationSampler();
                    samp.id = animId + "_sampler";
                    samp.addInput("INPUT", "#" + inputSrc.id);
                    samp.addInput("OUTPUT", "#" + outputSrc.id);
                    samp.addInput("INTERPOLATION", "#" + interpSrc.id);
                    samp.addInput("IN_TANGENT", "#" + inTangentSrc.id);
                    samp.addInput("OUT_TANGENT", "#" + outTangentSrc.id);
                    animNode.sampler.Add(samp);

                    // Channel
                    daeChannel chan = new daeChannel();
                    chan.source = "#" + samp.id;
                    chan.target = targetPaths[axis];
                    animNode.channel.Add(chan);

                    dae.library_animations.Add(animNode);
                }
            }
        }
    }
}

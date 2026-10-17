using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoxLorax
{
    // Versioned mesh data produced by tools/create_model.py from the editable model.
    internal sealed class ModelAsset : IDisposable
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<int> materialIds = new List<int>();
        private readonly List<int> groupIds = new List<int>();
        private readonly List<string> groupNames = new List<string>();
        private readonly List<Vector3> pivots = new List<Vector3>();
        internal Vector3[] AnimationAnchors;
        internal Vector3[][] PawVertices;

        internal static ModelAsset Load(string path, bool undead = false)
        {
            var model = new ModelAsset();
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadUInt32() != 0x32584f4c) throw new InvalidDataException("Unsupported model format.");
                    // Valheim's bundle shaders may exist without being registered with Shader.Find.
                    // Resolve after the lox prefab has loaded, not during plugin Awake.
                    var shaders = Resources.FindObjectsOfTypeAll<Shader>();
                    Shader shader = shaders.FirstOrDefault(s => s.name == "Custom/Creature" && s.isSupported)
                        ?? shaders.FirstOrDefault(s => s.name == "Standard" && s.isSupported);
                    if (!shader) throw new InvalidOperationException("No supported creature shader is loaded.");
                    int count = Count(reader, 1, 128);
                    for (int i = 0; i < count; i++)
                    {
                        var material = new Material(shader) { name = "Lorax material " + i };
                        model.owned.Add(material);
                        material.color = new Color(Float(reader), Float(reader), Float(reader), Float(reader));
                        if (undead && (i == 9 || i == 10) && material.HasProperty("_EmissionColor"))
                        {
                            material.EnableKeyword("_EMISSION");
                            material.SetColor("_EmissionColor", new Color(1f, .06f, .65f) * 1.8f);
                        }
                        material.SetFloat("_Glossiness", 0.1f);
                        material.SetFloat("_Metallic", 0f);
                        if (material.HasProperty("_UseStyles")) material.SetFloat("_UseStyles", 0f);
                        if (material.HasProperty("_UseGlossmap")) material.SetFloat("_UseGlossmap", 0f);
                        if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0f);
                        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
                        int bytes = Count(reader, 0, 16 * 1024 * 1024);
                        if (bytes > 0)
                        {
                            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                            model.owned.Add(texture);
                            byte[] data = reader.ReadBytes(bytes);
                            if (data.Length != bytes || !ImageConversion.LoadImage(texture, data, true))
                                throw new InvalidDataException("Invalid model texture.");
                            material.mainTexture = texture;
                        }
                        model.materials.Add(material);
                    }
                    int groupCount = Count(reader, 1, 32);
                    for (int i = 0; i < groupCount; i++)
                    {
                        int length = Count(reader, 1, 128);
                        byte[] name = reader.ReadBytes(length);
                        if (name.Length != length) throw new EndOfStreamException();
                        model.groupNames.Add(Encoding.UTF8.GetString(name));
                        model.pivots.Add(new Vector3(Float(reader), Float(reader), Float(reader)));
                    }
                    model.AnimationAnchors = LoxAnimation.CreateAnchors(model.groupNames, model.pivots);
                    var paws = Enumerable.Range(0, 4).Select(_ => new HashSet<Vector3>()).ToArray();
                    int meshCount = Count(reader, 1, 256);
                    int totalVertices = 0;
                    for (int i = 0; i < meshCount; i++)
                    {
                        int materialId = Count(reader, 0, count - 1);
                        int groupId = Count(reader, 0, groupCount - 1);
                        int vertices = Count(reader, 3, 200000);
                        totalVertices += vertices;
                        if (totalVertices > 500000) throw new InvalidDataException("Model exceeds vertex budget.");
                        var positions = new Vector3[vertices];
                        var normals = new Vector3[vertices];
                        var uv = new Vector2[vertices];
                        for (int v = 0; v < vertices; v++)
                        {
                            positions[v] = new Vector3(Float(reader), Float(reader), Float(reader));
                            normals[v] = new Vector3(Float(reader), Float(reader), Float(reader));
                            uv[v] = new Vector2(Float(reader), Float(reader));
                        }
                        int indexCount = Count(reader, 3, 1500000);
                        if (indexCount % 3 != 0) throw new InvalidDataException("Incomplete triangle.");
                        var indices = new int[indexCount];
                        for (int j = 0; j < indices.Length; j++) indices[j] = Count(reader, 0, vertices - 1);
                        var mesh = new Mesh { name = "Lorax mesh " + i, indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                        model.owned.Add(mesh);
                        mesh.vertices = positions;
                        mesh.normals = normals;
                        mesh.uv = uv;
                        var colors = new Color32[vertices];
                        for (int v = 0; v < vertices; v++) colors[v] = new Color32(255,255,255,255);
                        mesh.colors32 = colors;
                        var weights = new BoneWeight[vertices];
                        for (int v = 0; v < vertices; v++)
                        {
                            weights[v] = LoxAnimation.Weight(positions[v] + model.pivots[groupId], groupId, model.AnimationAnchors);
                            if (groupId >= 1 && groupId <= 4)
                            {
                                int foot = 7 + (groupId - 1) * 3;
                                BoneWeight weight = weights[v];
                                if ((weight.boneIndex0 == foot && weight.weight0 >= .99999f) ||
                                    (weight.boneIndex1 == foot && weight.weight1 >= .99999f))
                                    paws[groupId - 1].Add(positions[v] + model.pivots[groupId] - model.AnimationAnchors[foot]);
                            }
                        }
                        mesh.boneWeights = weights;
                        mesh.bindposes = model.AnimationAnchors.Select(anchor => Matrix4x4.Translate(model.pivots[groupId] - anchor)).ToArray();
                        mesh.triangles = indices;
                        mesh.RecalculateBounds();
                        mesh.UploadMeshData(true);
                        model.meshes.Add(mesh);
                        model.materialIds.Add(materialId);
                        model.groupIds.Add(groupId);
                    }
                    model.PawVertices = paws.Select(paw => paw.ToArray()).ToArray();
                    if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected trailing model data.");
                }
                return model;
            }
            catch { model.Dispose(); throw; }
        }

        private static int Count(BinaryReader reader, int min, int max)
        {
            int value = reader.ReadInt32();
            if (value < min || value > max) throw new InvalidDataException("Model value outside supported range.");
            return value;
        }

        private static float Float(BinaryReader reader)
        {
            float value = reader.ReadSingle();
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite model value.");
            return value;
        }

        internal Transform Instantiate(Transform parent, int layer, bool skinned = true)
        {
            var root = new GameObject("Lorax replacement");
            root.transform.SetParent(parent, false);
            try
            {
                var groups = new Transform[groupNames.Count];
                for (int i = 0; i < groups.Length; i++)
                {
                    groups[i] = new GameObject(groupNames[i]).transform;
                    groups[i].SetParent(root.transform, false);
                    groups[i].localPosition = pivots[i];
                }
                for (int i = 0; i < meshes.Count; i++)
                {
                    var part = new GameObject("Lorax part " + i) { layer = layer };
                    part.transform.SetParent(groups[groupIds[i]], false);
                    Renderer renderer;
                    if (skinned)
                    {
                        var skin = part.AddComponent<SkinnedMeshRenderer>();
                        skin.sharedMesh = meshes[i];
                        skin.quality = SkinQuality.Bone2;
                        // Include the reach of stomps, swimming and head attacks.
                        skin.localBounds = new Bounds(new Vector3(0, .5f, 0) - pivots[groupIds[i]], new Vector3(3f, 3f, 4f));
                        renderer = skin;
                    }
                    else
                    {
                        part.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                        renderer = part.AddComponent<MeshRenderer>();
                    }
                    renderer.sharedMaterial = materials[materialIds[i]];
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                return root.transform;
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }

        public void Dispose()
        {
            foreach (var item in owned) if (item) UnityEngine.Object.Destroy(item);
            owned.Clear();
        }
    }
}

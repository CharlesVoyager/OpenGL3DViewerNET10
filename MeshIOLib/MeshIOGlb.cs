using System.Drawing;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using View3D.model.geom;

namespace OpenGL3DViewerNET10.MeshIOLib
{
    /// <summary>
    /// Save settings for GLB export (read-only for now; export is not implemented).
    /// </summary>
    public class GlbSetting : IMeshOutSetting
    {
        public FormatCode Format { get; set; } = FormatCode.Glb;
    }

    /// <summary>
    /// Loads binary glTF (.glb) files into a <see cref="TopoModel"/>.
    ///
    /// GLB wire format (little-endian):
    ///   [12-byte file header]  magic=0x46546C67 | version=2 | totalLength
    ///   [Chunk 0]  chunkLength | chunkType=0x4E4F534A (JSON) | JSON bytes
    ///   [Chunk 1]  chunkLength | chunkType=0x004E4942 (BIN)  | binary buffer
    ///
    /// Only triangle primitives (mode 4) are imported.
    ///
    /// Color / texture resolution priority (per primitive):
    ///   1. COLOR_0 vertex attribute — per-vertex RGBA
    ///   2. PBR base-color texture   — UV-mapped from TEXCOORD_0
    ///   3. material.pbrMetallicRoughness.baseColorFactor — flat RGBA
    ///   4. Default white [1, 1, 1, 1]
    ///
    /// Full PBR maps extracted per material into <see cref="PbrMaterial"/>:
    ///   • baseColorTexture
    ///   • metallicRoughnessTexture  (G = roughness, B = metallic)
    ///   • normalTexture             (tangent-space XYZ)
    ///   • occlusionTexture          (R channel)
    ///   • emissiveTexture           (sRGB RGB)
    ///
    /// Tangent vectors (TANGENT accessor, VEC4) are read when present and stored
    /// in <see cref="TopoModel.tangents"/> alongside UVs.
    ///
    /// Supported accessor component types:
    ///   Positions / Normals / Tangents : FLOAT (5126)
    ///   Indices   : UNSIGNED_BYTE (5121), UNSIGNED_SHORT (5123), UNSIGNED_INT (5125)
    ///   Colors    : FLOAT (5126), UNSIGNED_BYTE normalized (5121), UNSIGNED_SHORT normalized (5123)
    ///               in either VEC3 (RGB) or VEC4 (RGBA) layout
    ///   TexCoords : FLOAT (5126) VEC2
    /// </summary>
    public class MeshIOGlb : MeshIOBase
    {
        // -----------------------------------------------------------------------
        //  Internal DTOs
        // -----------------------------------------------------------------------

        record ImageInfo(int BufferViewIdx, string? MimeType);
        record TextureInfo(int SourceImageIdx);

        class MaterialInfo
        {
            // Base color
            public float[] BaseColorFactor         = { 1f, 1f, 1f, 1f };
            public int?    BaseColorTextureIndex   = null;

            // Metallic-roughness
            public float   MetallicFactor          = 1f;
            public float   RoughnessFactor         = 1f;
            public int?    MetallicRoughnessTexIdx = null;

            // Normal map
            public int?    NormalTextureIdx        = null;

            // Occlusion
            public int?    OcclusionTextureIdx     = null;

            // Emissive
            public float[] EmissiveFactor          = { 0f, 0f, 0f };
            public int?    EmissiveTextureIdx      = null;
        }

        // -----------------------------------------------------------------------
        //  Public Load overrides
        // -----------------------------------------------------------------------

        public override int Load(string filename, TopoModel model, Action<int> updateRate)
        {
            try   { ImportGlb(filename, model, updateRate); }
            catch { throw; }
            return 0;
        }

        public override int LoadWOCatch(string filename, TopoModel model, Action<int> updateRate)
        {
            ImportGlb(filename, model, updateRate);
            return 0;
        }

        public override int Load(FileStream fs, TopoModel model, Action<int> updateRate)
        {
            ImportGlb(fs, model, updateRate);
            return 0;
        }

        // -----------------------------------------------------------------------
        //  Core parser
        // -----------------------------------------------------------------------

        void ImportGlb(string filename, TopoModel model, Action<int> updateRate)
        {
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read,
                                          FileShare.Read, 1 << 16);
            ImportGlb(fs, model, updateRate);
        }

        void ImportGlb(Stream stream, TopoModel model, Action<int> updateRate)
        {
            model.Clear();

            using var reader = new BinaryReader(stream, Encoding.UTF8, true);

            // ---- File header ----
            uint magic = reader.ReadUInt32();
            if (magic != 0x46546C67) throw new InvalidDataException("Not a GLB file.");
            reader.ReadUInt32(); // version
            reader.ReadUInt32(); // totalLength

            // ---- JSON chunk ----
            int jsonLength = reader.ReadInt32();
            reader.ReadInt32(); // chunkType (JSON)
            var json = reader.ReadBytes(jsonLength);
            var root = JsonDocument.Parse(json).RootElement;

            // ---- BIN chunk (optional) ----
            byte[]? bin = null;
            if (stream.Position < stream.Length)
            {
                int len = reader.ReadInt32();
                reader.ReadInt32(); // chunkType (BIN)
                bin = reader.ReadBytes(len);
            }

            var buffers     = new List<byte[]?> { bin };
            var bufferViews = ParseBufferViews(root);
            var accessors   = ParseAccessors(root);
            var materials   = ParseMaterials(root);
            var images      = ParseImages(root);
            var textures    = ParseTextures(root);

            if (!root.TryGetProperty("meshes", out var meshesEl)) return;

            foreach (var mesh in meshesEl.EnumerateArray())
            {
                foreach (var prim in mesh.GetProperty("primitives").EnumerateArray())
                {
                    int mode = prim.TryGetProperty("mode", out var modeEl) ? modeEl.GetInt32() : 4;
                    if (mode != 4) continue; // triangles only

                    var attrib = prim.GetProperty("attributes");

                    // ---- Required: positions ----
                    var positions = ReadVec3Accessor(
                        attrib.GetProperty("POSITION").GetInt32(),
                        accessors, bufferViews, buffers);

                    // ---- Optional: indices ----
                    int[]? indices = null;
                    if (prim.TryGetProperty("indices", out var idxEl))
                        indices = ReadScalarAccessor(idxEl.GetInt32(), accessors, bufferViews, buffers);

                    // ---- Optional: per-vertex colors ----
                    float[][]? vertexColors = null;
                    if (attrib.TryGetProperty("COLOR_0", out var colEl))
                        vertexColors = ReadColorAccessor(colEl.GetInt32(), accessors, bufferViews, buffers);

                    // ---- Optional: UV coordinates ----
                    float[][]? texcoords = null;
                    if (attrib.TryGetProperty("TEXCOORD_0", out var uvEl))
                        texcoords = ReadVec2Accessor(uvEl.GetInt32(), accessors, bufferViews, buffers);

                    // ---- Optional: tangents (VEC4 FLOAT, w = handedness) ----
                    float[][]? tangents = null;
                    if (attrib.TryGetProperty("TANGENT", out var tanEl))
                        tangents = ReadVec4Accessor(tanEl.GetInt32(), accessors, bufferViews, buffers);

                    // After reading positions, normals, UVs, indices — and when tangents == null:
                    if (tangents == null && texcoords != null)
                        tangents = ComputeTangents(positions, texcoords, indices, positions.Length);

                    // ---- Material ----
                    float[]?    flatColor   = null;
                    PbrMaterial pbrMaterial = new PbrMaterial();

                    if (prim.TryGetProperty("material", out var matIdxEl))
                    {
                        int matIdx = matIdxEl.GetInt32();
                        if (matIdx >= 0 && matIdx < materials.Count)
                        {
                            var mat = materials[matIdx];
                            flatColor = mat.BaseColorFactor;

                            // Build PbrMaterial — all maps resolved through texture → image chain
                            pbrMaterial = new PbrMaterial
                            {
                                BaseColorFactor          = mat.BaseColorFactor,
                                MetallicFactor           = mat.MetallicFactor,
                                RoughnessFactor          = mat.RoughnessFactor,
                                EmissiveFactor           = mat.EmissiveFactor,

                                BaseColorTexture         = LoadBitmapIfValid(mat.BaseColorTextureIndex,   textures, images, bufferViews, buffers),
                                MetallicRoughnessTexture = LoadBitmapIfValid(mat.MetallicRoughnessTexIdx, textures, images, bufferViews, buffers),
                                NormalTexture            = LoadBitmapIfValid(mat.NormalTextureIdx,         textures, images, bufferViews, buffers),
                                OcclusionTexture         = LoadBitmapIfValid(mat.OcclusionTextureIdx,      textures, images, bufferViews, buffers),
                                EmissiveTexture          = LoadBitmapIfValid(mat.EmissiveTextureIdx,       textures, images, bufferViews, buffers),
                            };
                        }
                    }

                    model.materials.Add(pbrMaterial);

                    AddPrimitiveToModel(
                        positions,
                        indices,
                        vertexColors,
                        texcoords,
                        tangents,
                        flatColor,
                        pbrMaterial,
                        model);
                }
            }
        }

        // -----------------------------------------------------------------------
        //  Build triangles from one primitive
        // -----------------------------------------------------------------------

        static readonly float[] DefaultColor = { 1f, 1f, 1f, 1f };

        static void AddPrimitiveToModel(
            RHVector3[]  positions,
            int[]?       indices,
            float[][]?   vertexColors,
            float[][]?   texcoords,
            float[][]?   tangents,
            float[]?     flatColor,
            PbrMaterial  material,
            TopoModel    model)
        {
            int triCount = indices != null ? indices.Length / 3 : positions.Length / 3;

            for (int t = 0; t < triCount; t++)
            {
                int i0 = indices != null ? indices[t * 3]     : t * 3;
                int i1 = indices != null ? indices[t * 3 + 1] : t * 3 + 1;
                int i2 = indices != null ? indices[t * 3 + 2] : t * 3 + 2;

                var p1 = positions[i0];
                var p2 = positions[i1];
                var p3 = positions[i2];

                var normal = p2.Subtract(p1).CrossProduct(p3.Subtract(p2));
                normal.NormalizeSafe();

                // ---- Priority 1: per-vertex colors ----
                if (vertexColors != null)
                {
                    var c0 = vertexColors[i0];
                    var c1 = vertexColors[i1];
                    var c2 = vertexColors[i2];
                    float[] color =
                    {
                        (c0[0]+c1[0]+c2[0]) / 3f,
                        (c0[1]+c1[1]+c2[1]) / 3f,
                        (c0[2]+c1[2]+c2[2]) / 3f,
                        (c0[3]+c1[3]+c2[3]) / 3f,
                    };
                    model.AddTriangle(p1, p2, p3, normal, color);
                }
                // ---- Priority 2: UV-mapped texture (base color or PBR) ----
                else if (texcoords != null && material.BaseColorTexture != null)
                {
                    float[]  uv0  = texcoords[i0];
                    float[]  uv1  = texcoords[i1];
                    float[]  uv2  = texcoords[i2];
                    float[]? tan0 = tangents?[i0];
                    float[]? tan1 = tangents?[i1];
                    float[]? tan2 = tangents?[i2];

                    model.AddTriangle(p1, p2, p3, normal, uv0, uv1, uv2, tan0, tan1, tan2);
                }
                // ---- Priority 3: flat material color ----
                else
                {
                    float[] color = flatColor ?? DefaultColor;
                    model.AddTriangle(p1, p2, p3, normal, color);
                }
            }
        }

        // -----------------------------------------------------------------------
        //  Material parser — full PBR fields
        // -----------------------------------------------------------------------

        static List<MaterialInfo> ParseMaterials(JsonElement root)
        {
            var list = new List<MaterialInfo>();
            if (!root.TryGetProperty("materials", out var matsEl)) return list;

            foreach (var mat in matsEl.EnumerateArray())
            {
                var m = new MaterialInfo();

                // pbrMetallicRoughness block
                if (mat.TryGetProperty("pbrMetallicRoughness", out var pbr))
                {
                    // baseColorFactor
                    if (pbr.TryGetProperty("baseColorFactor", out var bcf))
                    {
                        var vals = bcf.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                        for (int i = 0; i < Math.Min(4, vals.Length); i++)
                            m.BaseColorFactor[i] = vals[i];
                    }

                    // baseColorTexture
                    if (pbr.TryGetProperty("baseColorTexture", out var bct))
                        if (bct.TryGetProperty("index", out var i))
                            m.BaseColorTextureIndex = i.GetInt32();

                    // metallicFactor / roughnessFactor
                    if (pbr.TryGetProperty("metallicFactor",  out var mf)) m.MetallicFactor  = mf.GetSingle();
                    if (pbr.TryGetProperty("roughnessFactor", out var rf)) m.RoughnessFactor = rf.GetSingle();

                    // metallicRoughnessTexture
                    if (pbr.TryGetProperty("metallicRoughnessTexture", out var mrt))
                        if (mrt.TryGetProperty("index", out var i))
                            m.MetallicRoughnessTexIdx = i.GetInt32();
                }

                // normalTexture
                if (mat.TryGetProperty("normalTexture", out var nt))
                    if (nt.TryGetProperty("index", out var i))
                        m.NormalTextureIdx = i.GetInt32();

                // occlusionTexture
                if (mat.TryGetProperty("occlusionTexture", out var ot))
                    if (ot.TryGetProperty("index", out var i))
                        m.OcclusionTextureIdx = i.GetInt32();

                // emissiveFactor
                if (mat.TryGetProperty("emissiveFactor", out var ef))
                {
                    var vals = ef.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                    for (int i = 0; i < Math.Min(3, vals.Length); i++)
                        m.EmissiveFactor[i] = vals[i];
                }

                // emissiveTexture
                if (mat.TryGetProperty("emissiveTexture", out var et))
                    if (et.TryGetProperty("index", out var i))
                        m.EmissiveTextureIdx = i.GetInt32();

                list.Add(m);
            }

            return list;
        }

        // -----------------------------------------------------------------------
        //  Image / texture catalogue parsers
        // -----------------------------------------------------------------------

        static List<ImageInfo> ParseImages(JsonElement root)
        {
            var list = new List<ImageInfo>();
            if (!root.TryGetProperty("images", out var el)) return list;

            foreach (var img in el.EnumerateArray())
            {
                int    bv   = img.TryGetProperty("bufferView", out var b) ? b.GetInt32() : -1;
                string? mime = img.TryGetProperty("mimeType",  out var m) ? m.GetString() : null;
                list.Add(new ImageInfo(bv, mime));
            }
            return list;
        }

        static List<TextureInfo> ParseTextures(JsonElement root)
        {
            var list = new List<TextureInfo>();
            if (!root.TryGetProperty("textures", out var el)) return list;

            foreach (var t in el.EnumerateArray())
            {
                int src = t.TryGetProperty("source", out var s) ? s.GetInt32() : -1;
                list.Add(new TextureInfo(src));
            }
            return list;
        }

        // -----------------------------------------------------------------------
        //  Bitmap loading helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Resolves a glTF texture index to a Bitmap, returning null when:
        ///   • textureIndex is null (map not used by this material)
        ///   • the texture or image index is out of range
        ///   • the image has no embedded bufferView (external URIs not supported)
        ///   • the image bytes cannot be decoded
        /// </summary>
        static Bitmap? LoadBitmapIfValid(
            int?                 textureIndex,
            List<TextureInfo>    textures,
            List<ImageInfo>      images,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers)
        {
            if (!textureIndex.HasValue)
                return null;

            int texIdx = textureIndex.Value;
            if (texIdx < 0 || texIdx >= textures.Count)
                return null;

            int imgIdx = textures[texIdx].SourceImageIdx;
            if (imgIdx < 0 || imgIdx >= images.Count)
                return null;

            var img = images[imgIdx];

            // External URI images (bufferView == -1) are not supported
            if (img.BufferViewIdx < 0 || img.BufferViewIdx >= bufferViews.Count)
                return null;

            try
            {
                return LoadBitmapFromImage(img, bufferViews, buffers);
            }
            catch
            {
                // Corrupt or unsupported image format — skip silently
                return null;
            }
        }

        static Bitmap LoadBitmapFromImage(
            ImageInfo            img,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers)
        {
            var bytes = GetImageBytes(img, bufferViews, buffers);
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }

        static byte[] GetImageBytes(
            ImageInfo            img,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers)
        {
            var bv  = bufferViews[img.BufferViewIdx];
            var buf = buffers[bv.BufferIdx]!;
            var data = new byte[bv.ByteLength];
            Array.Copy(buf, bv.ByteOffset, data, 0, bv.ByteLength);
            return data;
        }

        // -----------------------------------------------------------------------
        //  Accessor readers
        // -----------------------------------------------------------------------

        /// <summary>Reads a VEC3 FLOAT accessor → RHVector3[].</summary>
        static RHVector3[] ReadVec3Accessor(
            int accessorIdx, List<AccessorInfo> accessors,
            List<BufferViewInfo> bufferViews, List<byte[]?> buffers)
        {
            var acc = accessors[accessorIdx];
            if (acc.Type != "VEC3")       throw new InvalidDataException($"Expected VEC3, got {acc.Type}.");
            if (acc.ComponentType != 5126) throw new NotSupportedException("Only FLOAT (5126) is supported for VEC3.");

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, 12);
            var result         = new RHVector3[acc.Count];

            for (int i = 0; i < acc.Count; i++)
            {
                int o = acc.ByteOffset + i * stride;
                result[i] = new RHVector3(
                    BitConverter.ToSingle(data, o),
                    BitConverter.ToSingle(data, o + 4),
                    BitConverter.ToSingle(data, o + 8));
            }
            return result;
        }

        /// <summary>Reads a VEC2 FLOAT accessor → float[][2] (u,v per vertex).</summary>
        static float[][] ReadVec2Accessor(
            int accessorIdx, List<AccessorInfo> accessors,
            List<BufferViewInfo> bufferViews, List<byte[]?> buffers)
        {
            var acc = accessors[accessorIdx];
            if (acc.Type != "VEC2")       throw new InvalidDataException($"Expected VEC2, got {acc.Type}.");
            if (acc.ComponentType != 5126) throw new NotSupportedException("Only FLOAT (5126) is supported for TEXCOORD_0.");

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, 8);
            var result         = new float[acc.Count][];

            for (int i = 0; i < acc.Count; i++)
            {
                int o = acc.ByteOffset + i * stride;
                result[i] = new float[]
                {
                    BitConverter.ToSingle(data, o),
                    BitConverter.ToSingle(data, o + 4)
                };
            }
            return result;
        }

        /// <summary>
        /// Reads a VEC4 FLOAT accessor → float[][4].
        /// Used for TANGENT (x,y,z,w) where w = handedness (±1).
        /// </summary>
        static float[][] ReadVec4Accessor(
            int accessorIdx, List<AccessorInfo> accessors,
            List<BufferViewInfo> bufferViews, List<byte[]?> buffers)
        {
            var acc = accessors[accessorIdx];
            if (acc.Type != "VEC4")       throw new InvalidDataException($"Expected VEC4, got {acc.Type}.");
            if (acc.ComponentType != 5126) throw new NotSupportedException("Only FLOAT (5126) is supported for TANGENT.");

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, 16);
            var result         = new float[acc.Count][];

            for (int i = 0; i < acc.Count; i++)
            {
                int o = acc.ByteOffset + i * stride;
                result[i] = new float[]
                {
                    BitConverter.ToSingle(data, o),
                    BitConverter.ToSingle(data, o + 4),
                    BitConverter.ToSingle(data, o + 8),
                    BitConverter.ToSingle(data, o + 12)
                };
            }
            return result;
        }

        /// <summary>Reads a SCALAR accessor (indices) → int[].</summary>
        static int[] ReadScalarAccessor(
            int accessorIdx, List<AccessorInfo> accessors,
            List<BufferViewInfo> bufferViews, List<byte[]?> buffers)
        {
            var acc = accessors[accessorIdx];
            if (acc.Type != "SCALAR") throw new InvalidDataException($"Expected SCALAR, got {acc.Type}.");

            int elementSize = acc.ComponentType switch
            {
                5121 => 1, // UNSIGNED_BYTE
                5123 => 2, // UNSIGNED_SHORT
                5125 => 4, // UNSIGNED_INT
                _    => throw new NotSupportedException($"Unsupported index component type {acc.ComponentType}.")
            };

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, elementSize);
            var result         = new int[acc.Count];

            for (int i = 0; i < acc.Count; i++)
            {
                int o = acc.ByteOffset + i * stride;
                result[i] = acc.ComponentType switch
                {
                    5121 => data[o],
                    5123 => BitConverter.ToUInt16(data, o),
                    5125 => (int)BitConverter.ToUInt32(data, o),
                    _    => 0
                };
            }
            return result;
        }

        /// <summary>
        /// Reads a COLOR_0 accessor → float[][4] RGBA in [0,1].
        /// Supports VEC3/VEC4 × FLOAT/UNSIGNED_BYTE/UNSIGNED_SHORT.
        /// </summary>
        static float[][] ReadColorAccessor(
            int accessorIdx, List<AccessorInfo> accessors,
            List<BufferViewInfo> bufferViews, List<byte[]?> buffers)
        {
            var  acc    = accessors[accessorIdx];
            bool isVec4 = acc.Type == "VEC4";

            int elementSize = acc.ComponentType switch
            {
                5126 => isVec4 ? 16 : 12,
                5121 => isVec4 ?  4 :  3,
                5123 => isVec4 ?  8 :  6,
                _    => throw new NotSupportedException($"Unsupported COLOR_0 component type {acc.ComponentType}.")
            };

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, elementSize);
            var result         = new float[acc.Count][];

            for (int i = 0; i < acc.Count; i++)
            {
                int   o = acc.ByteOffset + i * stride;
                float r, g, b, a = 1f;

                switch (acc.ComponentType)
                {
                    case 5126: // FLOAT
                        r = BitConverter.ToSingle(data, o);
                        g = BitConverter.ToSingle(data, o + 4);
                        b = BitConverter.ToSingle(data, o + 8);
                        if (isVec4) a = BitConverter.ToSingle(data, o + 12);
                        break;
                    case 5121: // UNSIGNED_BYTE normalized
                        r = data[o]     / 255f;
                        g = data[o + 1] / 255f;
                        b = data[o + 2] / 255f;
                        if (isVec4) a = data[o + 3] / 255f;
                        break;
                    case 5123: // UNSIGNED_SHORT normalized
                        r = BitConverter.ToUInt16(data, o)     / 65535f;
                        g = BitConverter.ToUInt16(data, o + 2) / 65535f;
                        b = BitConverter.ToUInt16(data, o + 4) / 65535f;
                        if (isVec4) a = BitConverter.ToUInt16(data, o + 6) / 65535f;
                        break;
                    default:
                        r = g = b = 1f;
                        break;
                }

                result[i] = new float[] { r, g, b, a };
            }
            return result;
        }

        // -----------------------------------------------------------------------
        //  Buffer/accessor utilities
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns (bufferSlice, elementStride) for an accessor.
        /// The returned slice starts at the bufferView's byteOffset.
        /// The accessor's own byteOffset is NOT baked in — callers apply it as a base.
        /// </summary>
        static (byte[] data, int stride) GetAccessorBytes(
            AccessorInfo         acc,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers,
            int                  elementSize)
        {
            if (acc.BufferViewIdx < 0)
                throw new NotSupportedException("Sparse accessors without a bufferView are not supported.");

            var bv  = bufferViews[acc.BufferViewIdx];
            var buf = buffers[bv.BufferIdx]
                      ?? throw new InvalidDataException(
                          $"Buffer {bv.BufferIdx} is missing (external URIs are not supported).");

            var slice = new byte[bv.ByteLength];
            Array.Copy(buf, bv.ByteOffset, slice, 0, bv.ByteLength);

            int stride = bv.ByteStride > 0 ? bv.ByteStride : elementSize;
            return (slice, stride);
        }

        // -----------------------------------------------------------------------
        //  JSON structure parsers
        // -----------------------------------------------------------------------

        record BufferViewInfo(int BufferIdx, int ByteOffset, int ByteLength, int ByteStride);
        record AccessorInfo(int BufferViewIdx, int ByteOffset, int ComponentType, int Count, string Type);

        static List<BufferViewInfo> ParseBufferViews(JsonElement root)
        {
            var list = new List<BufferViewInfo>();
            if (!root.TryGetProperty("bufferViews", out var el)) return list;

            foreach (var bv in el.EnumerateArray())
            {
                int bufIdx     = bv.GetProperty("buffer").GetInt32();
                int byteOffset = bv.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
                int byteLen    = bv.GetProperty("byteLength").GetInt32();
                int byteStride = bv.TryGetProperty("byteStride", out var bs) ? bs.GetInt32() : 0;
                list.Add(new BufferViewInfo(bufIdx, byteOffset, byteLen, byteStride));
            }
            return list;
        }

        static List<AccessorInfo> ParseAccessors(JsonElement root)
        {
            var list = new List<AccessorInfo>();
            if (!root.TryGetProperty("accessors", out var el)) return list;

            foreach (var ac in el.EnumerateArray())
            {
                int    bvIdx         = ac.TryGetProperty("bufferView", out var bv) ? bv.GetInt32() : -1;
                int    byteOffset    = ac.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
                int    componentType = ac.GetProperty("componentType").GetInt32();
                int    count         = ac.GetProperty("count").GetInt32();
                string type          = ac.GetProperty("type").GetString()!;
                list.Add(new AccessorInfo(bvIdx, byteOffset, componentType, count, type));
            }
            return list;
        }

        // -----------------------------------------------------------------------
        //  Utility: count triangle primitives (for progress reporting)
        // -----------------------------------------------------------------------

        static int CountTrianglePrimitives(JsonElement meshesEl)
        {
            int count = 0;
            foreach (var m in meshesEl.EnumerateArray())
                if (m.TryGetProperty("primitives", out var prims))
                    foreach (var p in prims.EnumerateArray())
                    {
                        int mode = p.TryGetProperty("mode", out var mEl) ? mEl.GetInt32() : 4;
                        if (mode == 4) count++;
                    }
            return count;
        }

        // -----------------------------------------------------------------------
        //  Transform helpers (node hierarchy → per-mesh world matrix)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Walks the node tree and produces a dictionary mapping mesh index →
        /// world-space column-major 4×4 float[16] transform.
        /// Only the first node referencing each mesh is recorded.
        /// </summary>
        static Dictionary<int, float[]> BuildNodeTransforms(JsonElement root)
        {
            var result = new Dictionary<int, float[]>();

            if (!root.TryGetProperty("nodes", out var nodesEl)) return result;

            var nodeArray = nodesEl.EnumerateArray().ToArray();
            int nodeCount = nodeArray.Length;

            var parentIdx = new int[nodeCount];
            Array.Fill(parentIdx, -1);

            for (int ni = 0; ni < nodeCount; ni++)
                if (nodeArray[ni].TryGetProperty("children", out var childrenEl))
                    foreach (var childEl in childrenEl.EnumerateArray())
                        parentIdx[childEl.GetInt32()] = ni;

            var localMatrices = new float[nodeCount][];
            for (int ni = 0; ni < nodeCount; ni++)
                localMatrices[ni] = NodeLocalMatrix(nodeArray[ni]);

            var worldMatrices = new float[nodeCount][];

            float[] GetWorldMatrix(int idx)
            {
                if (worldMatrices[idx] != null) return worldMatrices[idx];
                if (parentIdx[idx] == -1)
                    return worldMatrices[idx] = localMatrices[idx];
                return worldMatrices[idx] = MultiplyMatrix(GetWorldMatrix(parentIdx[idx]), localMatrices[idx]);
            }

            for (int ni = 0; ni < nodeCount; ni++)
                if (nodeArray[ni].TryGetProperty("mesh", out var meshIdxEl))
                {
                    int meshIdx = meshIdxEl.GetInt32();
                    if (!result.ContainsKey(meshIdx))
                        result[meshIdx] = GetWorldMatrix(ni);
                }

            return result;
        }

        static float[] NodeLocalMatrix(JsonElement nodeEl)
        {
            if (nodeEl.TryGetProperty("matrix", out var matEl))
            {
                var vals = matEl.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                if (vals.Length == 16) return vals;
            }

            var t = nodeEl.TryGetProperty("translation", out var tEl)
                ? tEl.EnumerateArray().Select(e => e.GetSingle()).ToArray()
                : new float[] { 0, 0, 0 };

            var r = nodeEl.TryGetProperty("rotation", out var rEl)
                ? rEl.EnumerateArray().Select(e => e.GetSingle()).ToArray()
                : new float[] { 0, 0, 0, 1 };

            var s = nodeEl.TryGetProperty("scale", out var sEl)
                ? sEl.EnumerateArray().Select(e => e.GetSingle()).ToArray()
                : new float[] { 1, 1, 1 };

            return TrsToMatrix(t, r, s);
        }

        /// <summary>Applies a column-major 4×4 matrix (float[16]) to a position vector.</summary>
        static RHVector3 ApplyTransform(RHVector3 v, float[]? m)
        {
            if (m == null) return v;
            double x = m[0]*v.x + m[4]*v.y + m[8] *v.z + m[12];
            double y = m[1]*v.x + m[5]*v.y + m[9] *v.z + m[13];
            double z = m[2]*v.x + m[6]*v.y + m[10]*v.z + m[14];
            return new RHVector3(x, y, z);
        }

        /// <summary>Builds a column-major 4×4 matrix from glTF TRS components.</summary>
        static float[] TrsToMatrix(float[] t, float[] r, float[] s)
        {
            float qx = r[0], qy = r[1], qz = r[2], qw = r[3];
            float x2 = qx+qx, y2 = qy+qy, z2 = qz+qz;
            float xx = qx*x2, xy = qx*y2, xz = qx*z2;
            float yy = qy*y2, yz = qy*z2, zz = qz*z2;
            float wx = qw*x2, wy = qw*y2, wz = qw*z2;
            float sx = s[0],  sy = s[1],  sz = s[2];

            return new float[16]
            {
                (1-(yy+zz))*sx, (xy+wz)*sx,    (xz-wy)*sx,    0,
                (xy-wz)*sy,    (1-(xx+zz))*sy, (yz+wx)*sy,    0,
                (xz+wy)*sz,    (yz-wx)*sz,     (1-(xx+yy))*sz,0,
                t[0],           t[1],           t[2],          1
            };
        }

        /// <summary>Multiplies two column-major 4×4 matrices: result = a × b.</summary>
        static float[] MultiplyMatrix(float[] a, float[] b)
        {
            var c = new float[16];
            for (int col = 0; col < 4; col++)
                for (int row = 0; row < 4; row++)
                {
                    float sum = 0;
                    for (int k = 0; k < 4; k++)
                        sum += a[row + k*4] * b[k + col*4];
                    c[row + col*4] = sum;
                }
            return c;
        }

        static float[][] ComputeTangents(RHVector3[] positions, float[][] texcoords, int[]? indices, int vertCount)
        {
            var tangentAccum = new Vector3[vertCount];
            var bitangentAccum = new Vector3[vertCount];
            int triCount = indices != null ? indices.Length / 3 : vertCount / 3;

            for (int t = 0; t < triCount; t++)
            {
                int i0 = indices != null ? indices[t * 3] : t * 3;
                int i1 = indices != null ? indices[t * 3 + 1] : t * 3 + 1;
                int i2 = indices != null ? indices[t * 3 + 2] : t * 3 + 2;

                var p0 = positions[i0]; var p1 = positions[i1]; var p2 = positions[i2];
                float[] uv0 = texcoords[i0], uv1 = texcoords[i1], uv2 = texcoords[i2];

                var edge1 = new Vector3((float)(p1.x - p0.x), (float)(p1.y - p0.y), (float)(p1.z - p0.z));
                var edge2 = new Vector3((float)(p2.x - p0.x), (float)(p2.y - p0.y), (float)(p2.z - p0.z));
                float du1 = uv1[0] - uv0[0], dv1 = uv1[1] - uv0[1];
                float du2 = uv2[0] - uv0[0], dv2 = uv2[1] - uv0[1];
                float r = 1f / (du1 * dv2 - du2 * dv1 + 1e-8f);

                var T = (edge1 * dv2 - edge2 * dv1) * r;
                var B = (edge2 * du1 - edge1 * du2) * r;

                tangentAccum[i0] += T; tangentAccum[i1] += T; tangentAccum[i2] += T;
                bitangentAccum[i0] += B; bitangentAccum[i1] += B; bitangentAccum[i2] += B;
            }

            var result = new float[vertCount][];
            for (int i = 0; i < vertCount; i++)
            {
                // Gram-Schmidt orthogonalize against the vertex normal (requires normals array)
                var t = Vector3.Normalize(tangentAccum[i]);
                var b = bitangentAccum[i];
                float w = Vector3.Dot(Vector3.Cross(t, b), /* normal[i] */ Vector3.UnitZ) < 0 ? -1f : 1f;
                result[i] = new float[] { t.X, t.Y, t.Z, w };
            }
            return result;
        }

    }
}

using System.Drawing;
using System.IO;
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
    /// Color resolution priority (per primitive):
    ///   1. COLOR_0 vertex attribute  – per-vertex RGBA, averaged across the 3 triangle vertices
    ///   2. material.pbrMetallicRoughness.baseColorFactor – flat RGBA for the whole primitive
    ///   3. Default white [1, 1, 1, 1]
    ///
    /// The resolved RGBA float[4] is stored on each TopoTriangle via
    /// model.AddTriangle(p1, p2, p3, normal, color).
    ///
    /// Supported accessor component types:
    ///   Positions : FLOAT (5126)
    ///   Indices   : UNSIGNED_BYTE (5121), UNSIGNED_SHORT (5123), UNSIGNED_INT (5125)
    ///   Colors    : FLOAT (5126), UNSIGNED_BYTE normalized (5121), UNSIGNED_SHORT normalized (5123)
    ///               in either VEC3 (RGB) or VEC4 (RGBA) layout
    /// </summary>
    public class MeshIOGlb : MeshIOBase
    {
        // ========================= NEW TYPES =========================

        record ImageInfo(int BufferViewIdx, string? MimeType);
        record TextureInfo(int SourceImageIdx);

        class MaterialInfo
        {
            public float[] BaseColorFactor = new float[] { 1, 1, 1, 1 };
            public int? BaseColorTextureIndex = null;
        }

        // ------------------------------------------------------------------ //
        //  Public Load overrides
        // ------------------------------------------------------------------ //

        public override int Load(string filename, TopoModel model, Action<int> updateRate)
        {
            try
            {
                ImportGlb(filename, model, updateRate);
            }
            catch
            {
                throw;
            }
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

        // ------------------------------------------------------------------ //
        //  Core parser
        // ------------------------------------------------------------------ //

        void ImportGlb(string filename, TopoModel model, Action<int> updateRate)
        {
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            ImportGlb(fs, model, updateRate);
        }

        void ImportGlb(Stream stream, TopoModel model, Action<int> updateRate)
        {
            model.Clear();

            using var reader = new BinaryReader(stream, Encoding.UTF8, true);

            uint magic = reader.ReadUInt32();
            if (magic != 0x46546C67) throw new InvalidDataException();

            reader.ReadUInt32(); // version
            reader.ReadUInt32(); // length

            // JSON chunk
            int jsonLength = reader.ReadInt32();
            reader.ReadInt32();
            var json = reader.ReadBytes(jsonLength);

            var root = JsonDocument.Parse(json).RootElement;

            // BIN chunk
            byte[]? bin = null;
            if (stream.Position < stream.Length)
            {
                int len = reader.ReadInt32();
                reader.ReadInt32();
                bin = reader.ReadBytes(len);
            }

            var buffers = new List<byte[]?> { bin };

            var bufferViews = ParseBufferViews(root);
            var accessors = ParseAccessors(root);

            // ⭐ NEW
            var materials = ParseMaterials(root);
            var images = ParseImages(root);
            var textures = ParseTextures(root);

            if (!root.TryGetProperty("meshes", out var meshesEl)) return;

            foreach (var mesh in meshesEl.EnumerateArray())
            {
                foreach (var prim in mesh.GetProperty("primitives").EnumerateArray())
                {
                    var attrib = prim.GetProperty("attributes");

                    var positions = ReadVec3Accessor(
                        attrib.GetProperty("POSITION").GetInt32(),
                        accessors, bufferViews, buffers);

                    int[]? indices = null;
                    if (prim.TryGetProperty("indices", out var idx))
                        indices = ReadScalarAccessor(idx.GetInt32(), accessors, bufferViews, buffers);

                    float[][]? vertexColors = null;
                    if (attrib.TryGetProperty("COLOR_0", out var col))
                        vertexColors = ReadColorAccessor(col.GetInt32(), accessors, bufferViews, buffers);

                    float[][]? texcoords = null;
                    if (attrib.TryGetProperty("TEXCOORD_0", out var uv))
                        texcoords = ReadVec2Accessor(uv.GetInt32(), accessors, bufferViews, buffers);

                    float[]? flatColor = null;
                    Bitmap? textureBitmap = null;

                    // ⭐ UPDATED MATERIAL HANDLING
                    if (prim.TryGetProperty("material", out var matIdxEl))
                    {
                        int matIdx = matIdxEl.GetInt32();
                        if (matIdx < materials.Count)
                        {
                            var mat = materials[matIdx];
                            flatColor = mat.BaseColorFactor;

                            if (mat.BaseColorTextureIndex.HasValue)
                            {
                                int texIdx = mat.BaseColorTextureIndex.Value;
                                if (texIdx < textures.Count)
                                {
                                    int imgIdx = textures[texIdx].SourceImageIdx;
                                    if (imgIdx >= 0 && imgIdx < images.Count)
                                    {
                                        textureBitmap = LoadBitmapFromImage(
                                            images[imgIdx], bufferViews, buffers);
                                    }
                                }
                            }
                        }
                    }

                    AddPrimitiveToModel(
                     positions,
                     indices,
                     vertexColors,
                     texcoords,   // ⭐ NEW
                     flatColor,
                     textureBitmap,
                     model);
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Build triangles from one primitive and add to model
        // ------------------------------------------------------------------ //

        static readonly float[] DefaultColor = new float[] { 1f, 1f, 1f, 1f };

        // ========================= TRIANGLES =========================
        static void AddPrimitiveToModel(
                    RHVector3[] positions,
                    int[]? indices,
                    float[][]? vertexColors,
                    float[][]? texcoords,   // ⭐ NEW
                    float[]? flatColor,
                    Bitmap? texture,
                    TopoModel model)
        {
            int triCount = indices != null ? indices.Length / 3 : positions.Length / 3;

            for (int t = 0; t < triCount; t++)
            {
                int i0 = indices != null ? indices[t * 3] : t * 3;
                int i1 = indices != null ? indices[t * 3 + 1] : t * 3 + 1;
                int i2 = indices != null ? indices[t * 3 + 2] : t * 3 + 2;

                var p1 = positions[i0];
                var p2 = positions[i1];
                var p3 = positions[i2];

                var normal = p2.Subtract(p1).CrossProduct(p3.Subtract(p2));
                normal.NormalizeSafe();

                float[] color;

                // ✅ Priority 1: Vertex color
                if (vertexColors != null)
                {
                    var c0 = vertexColors[i0];
                    var c1 = vertexColors[i1];
                    var c2 = vertexColors[i2];

                    color = new float[]
                    {
                (c0[0]+c1[0]+c2[0])/3f,
                (c0[1]+c1[1]+c2[1])/3f,
                (c0[2]+c1[2]+c2[2])/3f,
                (c0[3]+c1[3]+c2[3])/3f
                    };
                }
                // ✅ Priority 2: Texture (FIXED)
                else if (texture != null && texcoords != null)
                {
                    var uv0 = texcoords[i0];
                    var uv1 = texcoords[i1];
                    var uv2 = texcoords[i2];

                    // Average UV (simple approximation)
                    float u = (uv0[0] + uv1[0] + uv2[0]) / 3f;
                    float v = (uv0[1] + uv1[1] + uv2[1]) / 3f;

                    // glTF: V is flipped
                    v = 1.0f - v;

                    int x = (int)(u * (texture.Width - 1));
                    int y = (int)(v * (texture.Height - 1));

                    // Clamp (safety)
                    x = Math.Clamp(x, 0, texture.Width - 1);
                    y = Math.Clamp(y, 0, texture.Height - 1);

                    var px = texture.GetPixel(x, y);

                    color = new float[]
                    {
                        px.R / 255f,
                        px.G / 255f,
                        px.B / 255f,
                        px.A / 255f
                    };
                }
                // ✅ Priority 3: Flat color
                else
                {
                    color = flatColor ?? DefaultColor;
                }

                model.AddTriangle(p1, p2, p3, normal, color);
            }
        }
        static float[][] ReadVec2Accessor(  int accessorIdx,
                                            List<AccessorInfo> accessors,
                                            List<BufferViewInfo> bufferViews,
                                            List<byte[]?> buffers)
        {
            var acc = accessors[accessorIdx];

            if (acc.Type != "VEC2")
                throw new InvalidDataException($"Expected VEC2 accessor, got {acc.Type}.");

            if (acc.ComponentType != 5126)
                throw new NotSupportedException("Only FLOAT (5126) is supported for TEXCOORD_0.");

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, 8);

            var result = new float[acc.Count][];

            for (int i = 0; i < acc.Count; i++)
            {
                int offset = acc.ByteOffset + i * stride;

                float u = BitConverter.ToSingle(data, offset);
                float v = BitConverter.ToSingle(data, offset + 4);

                result[i] = new float[] { u, v };
            }

            return result;
        }

        // ========================= MATERIAL =========================

        static List<MaterialInfo> ParseMaterials(JsonElement root)
        {
            var list = new List<MaterialInfo>();

            if (!root.TryGetProperty("materials", out var matsEl))
                return list;

            foreach (var mat in matsEl.EnumerateArray())
            {
                var m = new MaterialInfo();

                if (mat.TryGetProperty("pbrMetallicRoughness", out var pbr))
                {
                    if (pbr.TryGetProperty("baseColorFactor", out var bcf))
                    {
                        var vals = bcf.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                        for (int i = 0; i < Math.Min(4, vals.Length); i++)
                            m.BaseColorFactor[i] = vals[i];
                    }

                    if (pbr.TryGetProperty("baseColorTexture", out var tex))
                    {
                        if (tex.TryGetProperty("index", out var idx))
                            m.BaseColorTextureIndex = idx.GetInt32();
                    }
                }

                list.Add(m);
            }

            return list;
        }

        // ========================= IMAGE / TEXTURE =========================

        static List<ImageInfo> ParseImages(JsonElement root)
        {
            var list = new List<ImageInfo>();
            if (!root.TryGetProperty("images", out var el)) return list;

            foreach (var img in el.EnumerateArray())
            {
                int bv = img.TryGetProperty("bufferView", out var b) ? b.GetInt32() : -1;
                string? mime = img.TryGetProperty("mimeType", out var m) ? m.GetString() : null;
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

        static Bitmap LoadBitmapFromImage(
            ImageInfo img,
            List<BufferViewInfo> bufferViews,
            List<byte[]?> buffers)
        {
            var bytes = GetImageBytes(img, bufferViews, buffers);
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }

        static byte[] GetImageBytes(
            ImageInfo img,
            List<BufferViewInfo> bufferViews,
            List<byte[]?> buffers)
        {
            var bv = bufferViews[img.BufferViewIdx];
            var buf = buffers[bv.BufferIdx]!;

            var data = new byte[bv.ByteLength];
            Array.Copy(buf, bv.ByteOffset, data, 0, bv.ByteLength);
            return data;
        }

        // ------------------------------------------------------------------ //
        //  Material color parser
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Parses materials[] and returns one RGBA float[4] per material.
        /// Falls back to opaque white [1,1,1,1] for any missing fields.
        /// </summary>
        static List<float[]> ParseMaterialColors(JsonElement root)
        {
            var list = new List<float[]>();

            if (!root.TryGetProperty("materials", out var matsEl))
                return list;

            foreach (var mat in matsEl.EnumerateArray())
            {
                float[] rgba = new float[] { 1f, 1f, 1f, 1f }; // default: opaque white

                if (mat.TryGetProperty("pbrMetallicRoughness", out var pbr) &&
                    pbr.TryGetProperty("baseColorFactor", out var bcf))
                {
                    var vals = bcf.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                    if (vals.Length >= 3)
                    {
                        rgba[0] = vals[0];
                        rgba[1] = vals[1];
                        rgba[2] = vals[2];
                        rgba[3] = vals.Length >= 4 ? vals[3] : 1f;
                    }
                }

                list.Add(rgba);
            }

            return list;
        }

        // ------------------------------------------------------------------ //
        //  Color accessor reader
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Reads a COLOR_0 accessor and returns one RGBA float[4] per vertex, all in [0..1].
        ///
        /// Supported layouts:
        ///   Type          : VEC3 (RGB) or VEC4 (RGBA)
        ///   ComponentType : FLOAT (5126)
        ///                   UNSIGNED_BYTE  normalized (5121) → divide by 255
        ///                   UNSIGNED_SHORT normalized (5123) → divide by 65535
        ///
        /// If the accessor is VEC3, the alpha channel is set to 1.0.
        /// </summary>
        static float[][] ReadColorAccessor(
            int                  accessorIdx,
            List<AccessorInfo>   accessors,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers)
        {
            var acc     = accessors[accessorIdx];
            bool isVec4 = acc.Type == "VEC4"; // VEC3 = RGB, VEC4 = RGBA

            int elementSize = acc.ComponentType switch
            {
                5126 => isVec4 ? 16 : 12, // FLOAT          (4 bytes × 3 or 4)
                5121 => isVec4 ?  4 :  3, // UNSIGNED_BYTE  (1 byte  × 3 or 4)
                5123 => isVec4 ?  8 :  6, // UNSIGNED_SHORT (2 bytes × 3 or 4)
                _ => throw new NotSupportedException(
                    $"Unsupported COLOR_0 component type {acc.ComponentType}.")
            };

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, elementSize);
            var result         = new float[acc.Count][];
            int baseOffset     = acc.ByteOffset;

            for (int i = 0; i < acc.Count; i++)
            {
                int   o = baseOffset + i * stride;
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

        // ------------------------------------------------------------------ //
        //  Transform helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Applies a column-major 4×4 matrix stored as float[16] to a position vector.
        /// If no transform is provided the vector is returned unchanged.
        /// </summary>
        static RHVector3 ApplyTransform(RHVector3 v, float[]? m)
        {
            if (m == null) return v;

            // glTF column-major layout:
            //   [ m[0]  m[4]  m[8]  m[12] ]
            //   [ m[1]  m[5]  m[9]  m[13] ]
            //   [ m[2]  m[6]  m[10] m[14] ]
            //   [ m[3]  m[7]  m[11] m[15] ]
            double x = m[0] * v.x + m[4] * v.y + m[8]  * v.z + m[12];
            double y = m[1] * v.x + m[5] * v.y + m[9]  * v.z + m[13];
            double z = m[2] * v.x + m[6] * v.y + m[10] * v.z + m[14];
            return new RHVector3(x, y, z);
        }

        /// <summary>Builds a column-major 4×4 matrix from glTF TRS components.</summary>
        static float[] TrsToMatrix(float[] t, float[] r, float[] s)
        {
            // r = quaternion [x, y, z, w]
            float qx = r[0], qy = r[1], qz = r[2], qw = r[3];

            float x2 = qx + qx, y2 = qy + qy, z2 = qz + qz;
            float xx = qx * x2, xy = qx * y2, xz = qx * z2;
            float yy = qy * y2, yz = qy * z2, zz = qz * z2;
            float wx = qw * x2, wy = qw * y2, wz = qw * z2;

            float sx = s[0], sy = s[1], sz = s[2];

            return new float[16]
            {
                (1 - (yy + zz)) * sx,  (xy + wz)       * sx,  (xz - wy)       * sx,  0,
                (xy - wz)       * sy,  (1 - (xx + zz)) * sy,  (yz + wx)       * sy,  0,
                (xz + wy)       * sz,  (yz - wx)       * sz,  (1 - (xx + yy)) * sz,  0,
                t[0],                  t[1],                  t[2],                   1
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
                        sum += a[row + k * 4] * b[k + col * 4];
                    c[row + col * 4] = sum;
                }
            return c;
        }

        // ------------------------------------------------------------------ //
        //  Node hierarchy → per-mesh world transform
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Walks the node tree and produces a dictionary mapping mesh index →
        /// world-space column-major 4×4 transform matrix (float[16]).
        /// Only the first node referencing each mesh is recorded.
        /// </summary>
        static Dictionary<int, float[]> BuildNodeTransforms(JsonElement root)
        {
            var result = new Dictionary<int, float[]>();

            if (!root.TryGetProperty("nodes", out var nodesEl))
                return result;

            var nodeArray = nodesEl.EnumerateArray().ToArray();
            int nodeCount = nodeArray.Length;

            // parent[i] = index of parent node (-1 for roots)
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

        // ------------------------------------------------------------------ //
        //  JSON structure parsing (bufferViews, accessors)
        // ------------------------------------------------------------------ //

        record BufferViewInfo(int BufferIdx, int ByteOffset, int ByteLength, int ByteStride);
        record AccessorInfo(int BufferViewIdx, int ByteOffset, int ComponentType, int Count, string Type);

        static List<BufferViewInfo> ParseBufferViews(JsonElement root)
        {
            var list = new List<BufferViewInfo>();
            if (!root.TryGetProperty("bufferViews", out var bvEl)) return list;
            foreach (var bv in bvEl.EnumerateArray())
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
            if (!root.TryGetProperty("accessors", out var acEl)) return list;
            foreach (var ac in acEl.EnumerateArray())
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

        // ------------------------------------------------------------------ //
        //  Accessor data readers
        // ------------------------------------------------------------------ //

        /// <summary>Reads a VEC3 FLOAT accessor → array of RHVector3.</summary>
        static RHVector3[] ReadVec3Accessor(
            int                  accessorIdx,
            List<AccessorInfo>   accessors,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers)
        {
            var acc = accessors[accessorIdx];
            if (acc.Type != "VEC3")
                throw new InvalidDataException($"Expected VEC3 accessor, got {acc.Type}.");
            if (acc.ComponentType != 5126)
                throw new NotSupportedException("Only FLOAT (5126) component type is supported for positions.");

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, elementSize: 12);
            var result         = new RHVector3[acc.Count];
            int baseOffset     = acc.ByteOffset;

            for (int i = 0; i < acc.Count; i++)
            {
                int offset = baseOffset + i * stride;
                float x = BitConverter.ToSingle(data, offset);
                float y = BitConverter.ToSingle(data, offset + 4);
                float z = BitConverter.ToSingle(data, offset + 8);
                result[i] = new RHVector3(x, y, z);
            }
            return result;
        }

        /// <summary>Reads a SCALAR accessor (indices) → int array.</summary>
        static int[] ReadScalarAccessor(
            int                  accessorIdx,
            List<AccessorInfo>   accessors,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers)
        {
            var acc = accessors[accessorIdx];
            if (acc.Type != "SCALAR")
                throw new InvalidDataException($"Expected SCALAR accessor, got {acc.Type}.");

            int elementSize = acc.ComponentType switch
            {
                5121 => 1, // UNSIGNED_BYTE
                5123 => 2, // UNSIGNED_SHORT
                5125 => 4, // UNSIGNED_INT
                _    => throw new NotSupportedException($"Unsupported index component type {acc.ComponentType}.")
            };

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, elementSize);
            var result         = new int[acc.Count];
            int baseOffset     = acc.ByteOffset;

            for (int i = 0; i < acc.Count; i++)
            {
                int offset = baseOffset + i * stride;
                result[i] = acc.ComponentType switch
                {
                    5121 => data[offset],
                    5123 => BitConverter.ToUInt16(data, offset),
                    5125 => (int)BitConverter.ToUInt32(data, offset),
                    _    => 0
                };
            }
            return result;
        }

        /// <summary>
        /// Returns (bufferData, elementStride) for an accessor.
        /// The returned slice starts at the bufferView's byteOffset.
        /// The accessor's own byteOffset is NOT baked in — callers apply it as baseOffset.
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
                          $"Buffer {bv.BufferIdx} data is missing (external URIs are not supported).");

            var slice = new byte[bv.ByteLength];
            Array.Copy(buf, bv.ByteOffset, slice, 0, bv.ByteLength);

            int stride = bv.ByteStride > 0 ? bv.ByteStride : elementSize;
            return (slice, stride);
        }

        // ------------------------------------------------------------------ //
        //  Utility
        // ------------------------------------------------------------------ //

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
    }
}

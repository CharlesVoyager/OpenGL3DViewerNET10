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
    /// Supported accessor component types: FLOAT (5126) for positions,
    /// UNSIGNED_BYTE (5121), UNSIGNED_SHORT (5123), UNSIGNED_INT (5125) for indices.
    /// Node hierarchy and transforms are fully resolved.
    /// </summary>
    public class MeshIOGlb : MeshIOBase
    {
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
            Status = STATUS.Busy;
            model.Clear();

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // ── File header ────────────────────────────────────────────────
            uint magic = reader.ReadUInt32();
            if (magic != 0x46546C67)
                throw new InvalidDataException("Not a valid GLB file (bad magic number).");

            uint version = reader.ReadUInt32();
            if (version != 2)
                throw new NotSupportedException($"GLB version {version} is not supported (only version 2).");

            reader.ReadUInt32(); // totalLength – unused

            // ── Chunk 0: JSON ──────────────────────────────────────────────
            uint jsonChunkLength = reader.ReadUInt32();
            uint jsonChunkType   = reader.ReadUInt32();
            if (jsonChunkType != 0x4E4F534A)
                throw new InvalidDataException("Expected JSON chunk (0x4E4F534A) as first GLB chunk.");

            byte[] jsonBytes = reader.ReadBytes((int)jsonChunkLength);
            using var jsonDoc = JsonDocument.Parse(jsonBytes);
            var root = jsonDoc.RootElement;

            // ── Chunk 1: BIN (optional) ────────────────────────────────────
            byte[]? binBuffer = null;
            if (stream.Position < stream.Length)
            {
                uint binChunkLength = reader.ReadUInt32();
                uint binChunkType   = reader.ReadUInt32();
                if (binChunkType == 0x004E4942)
                    binBuffer = reader.ReadBytes((int)binChunkLength);
            }

            // ── Collect all buffers (GLB BIN + external URIs skipped) ──────
            // We only handle the embedded BIN chunk (buffer index 0 in GLB).
            // External URI buffers are intentionally not supported.
            var buffers = new List<byte[]?>();
            if (root.TryGetProperty("buffers", out var buffersEl))
            {
                foreach (var _ in buffersEl.EnumerateArray())
                    buffers.Add(null); // placeholder; replaced below for index 0
                if (buffers.Count > 0)
                    buffers[0] = binBuffer;
            }
            else if (binBuffer != null)
            {
                buffers.Add(binBuffer);
            }

            // ── bufferViews ────────────────────────────────────────────────
            var bufferViews = ParseBufferViews(root);

            // ── accessors ─────────────────────────────────────────────────
            var accessors = ParseAccessors(root);

            // ── Build per-mesh primitive triangle lists ────────────────────
            if (!root.TryGetProperty("meshes", out var meshesEl))
            {
                Status = STATUS.Done;
                return;
            }

            // Collect node transforms so we can apply them per mesh
            var nodeTransforms = BuildNodeTransforms(root);

            // Count total primitives for progress reporting
            int totalPrimitives = CountTrianglePrimitives(meshesEl);
            int processedPrimitives = 0;

            // Walk every mesh
            int meshIndex = 0;
            foreach (var meshEl in meshesEl.EnumerateArray())
            {
                if (!meshEl.TryGetProperty("primitives", out var primitivesEl))
                {
                    meshIndex++;
                    continue;
                }

                // Find transform for this mesh (from first node referencing it)
                float[]? transform = nodeTransforms.TryGetValue(meshIndex, out var t) ? t : null;

                foreach (var primitiveEl in primitivesEl.EnumerateArray())
                {
                    // mode 4 = TRIANGLES (default when omitted)
                    int mode = primitiveEl.TryGetProperty("mode", out var modeEl) ? modeEl.GetInt32() : 4;
                    if (mode != 4)
                        continue;

                    if (!primitiveEl.TryGetProperty("attributes", out var attribEl))
                        continue;
                    if (!attribEl.TryGetProperty("POSITION", out var posAccessorEl))
                        continue;

                    int posAccessorIdx = posAccessorEl.GetInt32();
                    var positions = ReadVec3Accessor(posAccessorIdx, accessors, bufferViews, buffers);

                    int[]? indices = null;
                    if (primitiveEl.TryGetProperty("indices", out var indicesEl))
                    {
                        int idxAccessorIdx = indicesEl.GetInt32();
                        indices = ReadScalarAccessor(idxAccessorIdx, accessors, bufferViews, buffers);
                    }

                    AddPrimitiveToModel(positions, indices, transform, model, ref processedPrimitives,
                                        totalPrimitives, updateRate);

                    if (Command == COMMAND.Abort)
                    {
                        Command = COMMAND.None;
                        Status = STATUS.UserAbort;
                        return;
                    }

                    processedPrimitives++;
                }

                meshIndex++;
            }

            if (Status == STATUS.Busy)
                Status = STATUS.Done;
        }

        // ------------------------------------------------------------------ //
        //  Build triangles from one primitive and add to model
        // ------------------------------------------------------------------ //

        static void AddPrimitiveToModel(
            RHVector3[] positions,
            int[]?      indices,
            float[]?    transform,
            TopoModel   model,
            ref int     processed,
            int         total,
            Action<int> updateRate)
        {
            int triCount = indices != null ? indices.Length / 3 : positions.Length / 3;

            for (int t = 0; t < triCount; t++)
            {
                int i0, i1, i2;
                if (indices != null)
                {
                    i0 = indices[t * 3];
                    i1 = indices[t * 3 + 1];
                    i2 = indices[t * 3 + 2];
                }
                else
                {
                    i0 = t * 3;
                    i1 = t * 3 + 1;
                    i2 = t * 3 + 2;
                }

                var p1 = ApplyTransform(positions[i0], transform);
                var p2 = ApplyTransform(positions[i1], transform);
                var p3 = ApplyTransform(positions[i2], transform);

                var d1     = p2.Subtract(p1);
                var d2     = p3.Subtract(p2);
                var normal = d1.CrossProduct(d2);
                normal.NormalizeSafe();

                model.AddTriangle(p1, p2, p3, normal);

                if (t > 0 && t % 5000 == 0)
                {
                    if (total > 0)
                    {
                        // Progress is a rough per-primitive estimate
                        int pct = (int)((double)processed / total * 100.0);
                        updateRate?.Invoke(pct);
                    }
                }
            }
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

            // glTF uses column-major order:
            //   [ m[0]  m[4]  m[8]  m[12] ]
            //   [ m[1]  m[5]  m[9]  m[13] ]
            //   [ m[2]  m[6]  m[10] m[14] ]
            //   [ m[3]  m[7]  m[11] m[15] ]
            double x = m[0] * v.x + m[4] * v.y + m[8]  * v.z + m[12];
            double y = m[1] * v.x + m[5] * v.y + m[9]  * v.z + m[13];
            double z = m[2] * v.x + m[6] * v.y + m[10] * v.z + m[14];
            return new RHVector3(x, y, z);
        }

        /// <summary>
        /// Builds a 4×4 column-major matrix from glTF TRS components.
        /// </summary>
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
                (1 - (yy + zz)) * sx,  (xy + wz)        * sx,  (xz - wy)        * sx,  0,
                (xy - wz)        * sy,  (1 - (xx + zz)) * sy,  (yz + wx)        * sy,  0,
                (xz + wy)        * sz,  (yz - wx)        * sz,  (1 - (xx + yy)) * sz,  0,
                t[0],                   t[1],                   t[2],                   1
            };
        }

        /// <summary>
        /// Multiplies two column-major 4×4 matrices: result = a × b.
        /// </summary>
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

        static float[] IdentityMatrix() =>
            new float[16] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };

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

            // parent[i] = index of parent node (-1 for roots)
            var nodeArray = nodesEl.EnumerateArray().ToArray();
            int nodeCount = nodeArray.Length;
            var parentIdx = new int[nodeCount];
            Array.Fill(parentIdx, -1);

            // Build parent map from children lists
            for (int ni = 0; ni < nodeCount; ni++)
            {
                if (nodeArray[ni].TryGetProperty("children", out var childrenEl))
                    foreach (var childEl in childrenEl.EnumerateArray())
                        parentIdx[childEl.GetInt32()] = ni;
            }

            // Compute local matrix for each node
            var localMatrices = new float[nodeCount][];
            for (int ni = 0; ni < nodeCount; ni++)
                localMatrices[ni] = NodeLocalMatrix(nodeArray[ni]);

            // Compute world matrix by walking up the parent chain
            var worldMatrices = new float[nodeCount][];
            float[] GetWorldMatrix(int idx)
            {
                if (worldMatrices[idx] != null) return worldMatrices[idx];
                if (parentIdx[idx] == -1)
                    return worldMatrices[idx] = localMatrices[idx];
                return worldMatrices[idx] = MultiplyMatrix(GetWorldMatrix(parentIdx[idx]), localMatrices[idx]);
            }

            for (int ni = 0; ni < nodeCount; ni++)
            {
                if (nodeArray[ni].TryGetProperty("mesh", out var meshIdxEl))
                {
                    int meshIdx = meshIdxEl.GetInt32();
                    if (!result.ContainsKey(meshIdx))
                        result[meshIdx] = GetWorldMatrix(ni);
                }
            }

            return result;
        }

        static float[] NodeLocalMatrix(JsonElement nodeEl)
        {
            // Explicit matrix takes priority
            if (nodeEl.TryGetProperty("matrix", out var matEl))
            {
                var vals = matEl.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                if (vals.Length == 16) return vals;
            }

            // TRS decomposition
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
                int bvIdx         = ac.TryGetProperty("bufferView", out var bv) ? bv.GetInt32() : -1;
                int byteOffset    = ac.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
                int componentType = ac.GetProperty("componentType").GetInt32();
                int count         = ac.GetProperty("count").GetInt32();
                string type       = ac.GetProperty("type").GetString()!;
                list.Add(new AccessorInfo(bvIdx, byteOffset, componentType, count, type));
            }
            return list;
        }

        // ------------------------------------------------------------------ //
        //  Accessor data readers
        // ------------------------------------------------------------------ //

        /// <summary>Reads a VEC3 float accessor → array of RHVector3.</summary>
        static RHVector3[] ReadVec3Accessor(
            int accessorIdx,
            List<AccessorInfo>     accessors,
            List<BufferViewInfo>   bufferViews,
            List<byte[]?>          buffers)
        {
            var acc = accessors[accessorIdx];
            if (acc.Type != "VEC3")
                throw new InvalidDataException($"Expected VEC3 accessor, got {acc.Type}.");
            if (acc.ComponentType != 5126)
                throw new NotSupportedException("Only FLOAT (5126) component type is supported for positions.");

            var (data, stride) = GetAccessorBytes(acc, bufferViews, buffers, elementSize: 12);

            var result = new RHVector3[acc.Count];
            int baseOffset = acc.ByteOffset;
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
            int accessorIdx,
            List<AccessorInfo>     accessors,
            List<BufferViewInfo>   bufferViews,
            List<byte[]?>          buffers)
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

            var result = new int[acc.Count];
            int baseOffset = acc.ByteOffset;
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
        /// The data slice already has the bufferView byteOffset applied;
        /// the accessor's own byteOffset is NOT baked in (callers handle it).
        /// </summary>
        static (byte[] data, int stride) GetAccessorBytes(
            AccessorInfo         acc,
            List<BufferViewInfo> bufferViews,
            List<byte[]?>        buffers,
            int                  elementSize)
        {
            if (acc.BufferViewIdx < 0)
                throw new NotSupportedException("Sparse accessors without a bufferView are not supported.");

            var bv   = bufferViews[acc.BufferViewIdx];
            var buf  = buffers[bv.BufferIdx]
                       ?? throw new InvalidDataException($"Buffer {bv.BufferIdx} data is missing (external URIs are not supported).");

            // Extract the relevant slice from the buffer
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

using OpenTK.Mathematics;
using System.Drawing;

namespace View3D.model.geom
{
    // ---------------------------------------------------------------------------
    // PbrMaterial: Holds all glTF pbrMetallicRoughness textures and scalar factors
    // for a single primitive's material. Null texture fields mean that map is absent
    // and the renderer should fall back to the corresponding *Factor value.
    // ---------------------------------------------------------------------------
    public class PbrMaterial
    {
        // Base color (albedo) — sRGB encoded
        public Bitmap? BaseColorTexture             = null;
        public float[] BaseColorFactor              = { 1f, 1f, 1f, 1f };   // RGBA

        // Metallic-roughness — single texture, two channels:
        //   G channel = roughness   (0 = smooth, 1 = rough)
        //   B channel = metallic    (0 = dielectric, 1 = metal)
        public Bitmap? MetallicRoughnessTexture     = null;
        public float   MetallicFactor               = 1f;
        public float   RoughnessFactor              = 1f;

        // Tangent-space normal map (RGB → XYZ in [-1,1])
        public Bitmap? NormalTexture                = null;

        // Ambient occlusion — R channel only
        public Bitmap? OcclusionTexture             = null;

        // Emissive — sRGB encoded, multiplied by EmissiveFactor
        public Bitmap? EmissiveTexture              = null;
        public float[] EmissiveFactor               = { 0f, 0f, 0f };       // RGB
    }

    public class PrimitiveMaterialRange
    {
        public int StartTriangle { get; set; }
        public int TriangleCount { get; set; }
        public int MaterialIndex { get; set; } = -1;
    }

    // TopoModel: Used to store original STL / GLB file triangle data intact.
    public class TopoModel
    {
#if false //HashSet is fine for uniqueness-style model storage, but not safe for ordered GLB attribute/material binding during draw preparation.
        public HashSet<TopoTriangle> triangles = new HashSet<TopoTriangle>();
#else
        public List<TopoTriangle> drawTriangles = new List<TopoTriangle>();
#endif

        public RHBoundingBox boundingBox = new RHBoundingBox();

        // PBR materials — one entry per GLB primitive (replaces the old flat List<Bitmap>)
        public List<PbrMaterial> materials = new List<PbrMaterial>();

        // UV texture coordinates: 2 floats per vertex (u, v), tightly packed,
        // in the same order as glVertices (i.e. 3 UVs per triangle, no indexing).
        public List<float> texCoords = new List<float>();

        // Tangent vectors: 4 floats per vertex (x, y, z, w) where w = handedness (±1).
        // Same ordering as texCoords — 3 tangents per triangle, matching glVertices.
        public List<float> tangents = new List<float>();
        public List<PrimitiveMaterialRange> primitiveMaterials = new List<PrimitiveMaterialRange>();

        public void Clear()
        {
            drawTriangles.Clear();
            boundingBox.Clear();
            materials.Clear();
            texCoords.Clear();
            tangents.Clear();
            primitiveMaterials.Clear();
        }

        public void EnsureCapacity(int triCount)
        {
            drawTriangles.EnsureCapacity(triCount);
        }

        public void CopyTo(TopoModel newModel)
        {
            foreach (TopoTriangle t in drawTriangles)
                newModel.drawTriangles.Add(new TopoTriangle(t));

            boundingBox.CopyTo(newModel.boundingBox);

            foreach (PbrMaterial m in materials)
            {
                PbrMaterial newMat = new PbrMaterial
                {
                    BaseColorTexture = m.BaseColorTexture,
                    BaseColorFactor = (float[])m.BaseColorFactor.Clone(),
                    MetallicRoughnessTexture = m.MetallicRoughnessTexture,
                    MetallicFactor = m.MetallicFactor,
                    RoughnessFactor = m.RoughnessFactor,
                    NormalTexture = m.NormalTexture,
                    OcclusionTexture = m.OcclusionTexture,
                    EmissiveTexture = m.EmissiveTexture,
                    EmissiveFactor = (float[])m.EmissiveFactor.Clone()
                };
                newModel.materials.Add(newMat);
            }

            newModel.texCoords.AddRange(texCoords);

            newModel.tangents.AddRange(tangents);
            foreach (PrimitiveMaterialRange range in primitiveMaterials)
            {
                newModel.primitiveMaterials.Add(new PrimitiveMaterialRange
                {
                    StartTriangle = range.StartTriangle,
                    TriangleCount = range.TriangleCount,
                    MaterialIndex = range.MaterialIndex
                });
            }
        }

        // ------------------------------------------------------------------
        // AddTriangle overloads
        // ------------------------------------------------------------------

        public void AddTriangle(RHVector3 p1, RHVector3 p2, RHVector3 p3, RHVector3 normal)
        {
            TopoVertex v1 = new TopoVertex(p1);
            TopoVertex v2 = new TopoVertex(p2);
            TopoVertex v3 = new TopoVertex(p3);

            AddTriangleInternal(new TopoTriangle(v1, v2, v3, normal));
            boundingBox.Add(p1);
            boundingBox.Add(p2);
            boundingBox.Add(p3);
        }

        public void AddTriangle(RHVector3 p1, RHVector3 p2, RHVector3 p3, RHVector3 normal, float[] color)
        {
            TopoVertex v1 = new TopoVertex(p1);
            TopoVertex v2 = new TopoVertex(p2);
            TopoVertex v3 = new TopoVertex(p3);

            AddTriangleInternal(new TopoTriangle(v1, v2, v3, normal, color));
            boundingBox.Add(p1);
            boundingBox.Add(p2);
            boundingBox.Add(p3);
        }

        public void AddTriangle(
            RHVector3 p1, RHVector3 p2, RHVector3 p3,
            RHVector3 n0, RHVector3 n1, RHVector3 n2,
            float[]? color = null)
        {
            TopoVertex v1 = new TopoVertex(p1);
            TopoVertex v2 = new TopoVertex(p2);
            TopoVertex v3 = new TopoVertex(p3);

            RHVector3 faceNormal = new RHVector3(
                (n0.x + n1.x + n2.x) / 3.0,
                (n0.y + n1.y + n2.y) / 3.0,
                (n0.z + n1.z + n2.z) / 3.0);
            faceNormal.NormalizeSafe();

            AddTriangleInternal(new TopoTriangle(
                v1, v2, v3,
                new[] { n0, n1, n2 },
                faceNormal,
                color));

            boundingBox.Add(p1);
            boundingBox.Add(p2);
            boundingBox.Add(p3);
        }

        /// <summary>
        /// Adds a textured triangle. uv0/uv1/uv2 are float[2] (u,v) per vertex.
        /// tan0/tan1/tan2 are float[4] (x,y,z,w) tangent vectors per vertex —
        /// pass null for any of them to skip tangent storage.
        /// </summary>
        public void AddTriangle(
            RHVector3 p1, RHVector3 p2, RHVector3 p3, RHVector3 normal,
            float[] uv0,  float[] uv1,  float[] uv2,
            float[]? tan0 = null, float[]? tan1 = null, float[]? tan2 = null)
        {
            TopoVertex v1 = new TopoVertex(p1);
            TopoVertex v2 = new TopoVertex(p2);
            TopoVertex v3 = new TopoVertex(p3);

            AddTriangleInternal(new TopoTriangle(v1, v2, v3, normal));
            boundingBox.Add(p1);
            boundingBox.Add(p2);
            boundingBox.Add(p3);

            // UV coords — always store when UVs are provided
            if (uv0 != null && uv1 != null && uv2 != null)
            {
                texCoords.Add(uv0[0]); texCoords.Add(uv0[1]);
                texCoords.Add(uv1[0]); texCoords.Add(uv1[1]);
                texCoords.Add(uv2[0]); texCoords.Add(uv2[1]);
            }

            // Tangents — store only when all three vertex tangents are provided
            if (tan0 != null && tan1 != null && tan2 != null)
            {
                tangents.Add(tan0[0]); tangents.Add(tan0[1]); tangents.Add(tan0[2]); tangents.Add(tan0[3]);
                tangents.Add(tan1[0]); tangents.Add(tan1[1]); tangents.Add(tan1[2]); tangents.Add(tan1[3]);
                tangents.Add(tan2[0]); tangents.Add(tan2[1]); tangents.Add(tan2[2]); tangents.Add(tan2[3]);
            }
        }

        public void AddTriangle(
            RHVector3 p1, RHVector3 p2, RHVector3 p3,
            RHVector3 n0, RHVector3 n1, RHVector3 n2,
            float[] uv0,  float[] uv1,  float[] uv2,
            float[]? tan0 = null, float[]? tan1 = null, float[]? tan2 = null)
        {
            AddTriangle(p1, p2, p3, n0, n1, n2, null, uv0, uv1, uv2, tan0, tan1, tan2);
        }

        public void AddTriangle(
            RHVector3 p1, RHVector3 p2, RHVector3 p3,
            RHVector3 n0, RHVector3 n1, RHVector3 n2,
            float[]? color,
            float[] uv0,  float[] uv1,  float[] uv2,
            float[]? tan0 = null, float[]? tan1 = null, float[]? tan2 = null)
        {
            AddTriangle(p1, p2, p3, n0, n1, n2, color);

            if (uv0 != null && uv1 != null && uv2 != null)
            {
                texCoords.Add(uv0[0]); texCoords.Add(uv0[1]);
                texCoords.Add(uv1[0]); texCoords.Add(uv1[1]);
                texCoords.Add(uv2[0]); texCoords.Add(uv2[1]);
            }

            if (tan0 != null && tan1 != null && tan2 != null)
            {
                tangents.Add(tan0[0]); tangents.Add(tan0[1]); tangents.Add(tan0[2]); tangents.Add(tan0[3]);
                tangents.Add(tan1[0]); tangents.Add(tan1[1]); tangents.Add(tan1[2]); tangents.Add(tan1[3]);
                tangents.Add(tan2[0]); tangents.Add(tan2[1]); tangents.Add(tan2[2]); tangents.Add(tan2[3]);
            }
        }

        // ------------------------------------------------------------------

        private void removeTriangle(TopoTriangle triangle)
        {
            drawTriangles.Remove(triangle);
        }

        private void AddTriangleInternal(TopoTriangle triangle)
        {
            drawTriangles.Add(triangle);
        }

        public double Surface()
        {
            double surface = 0;
            foreach (TopoTriangle t in drawTriangles)
                surface += t.Area();
            return surface;
        }

        public double Volume()
        {
            double volume = 0;
            foreach (TopoTriangle t in drawTriangles)
                volume += t.SignedVolume();
            return Math.Abs(volume);
        }

        public void getTriInWorld(Matrix4 trans, TopoTriangle tInObj, out TopoTriangle tInWorld)
        {
            Vector4 ver1 = tInObj.Vertices[0].pos.asVector4();
            Vector4 ver2 = tInObj.Vertices[1].pos.asVector4();
            Vector4 ver3 = tInObj.Vertices[2].pos.asVector4();

#if false   // OpenTK 3.3.3.0
            ver1 = Vector4.Transform(ver1, trans);
            ver2 = Vector4.Transform(ver2, trans);
            ver3 = Vector4.Transform(ver3, trans);
#else       // OpenTK 4.9.4
            ver1 = ver1 * trans;
            ver2 = ver2 * trans;
            ver3 = ver3 * trans;
#endif

            TopoVertex v1 = new TopoVertex(new RHVector3(ver1.X, ver1.Y, ver1.Z));
            TopoVertex v2 = new TopoVertex(new RHVector3(ver2.X, ver2.Y, ver2.Z));
            TopoVertex v3 = new TopoVertex(new RHVector3(ver3.X, ver3.Y, ver3.Z));
            tInWorld = new TopoTriangle(v1, v2, v3);
        }

        public bool HasColor()
        {
            foreach (TopoTriangle t in drawTriangles)
            {
                if (t.Color != null)
                    return true;
                else
                    return false;
            }
            return false;
        }
    }
}

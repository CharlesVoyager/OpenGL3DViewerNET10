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

    // TopoModel: Used to store original STL / GLB file triangle data intact.
    public class TopoModel
    {
        public HashSet<TopoTriangle> triangles = new HashSet<TopoTriangle>();

        public RHBoundingBox boundingBox = new RHBoundingBox();

        // PBR materials — one entry per GLB primitive (replaces the old flat List<Bitmap>)
        public List<PbrMaterial> materials = new List<PbrMaterial>();

        // UV texture coordinates: 2 floats per vertex (u, v), tightly packed,
        // in the same order as glVertices (i.e. 3 UVs per triangle, no indexing).
        public List<float> texCoords = new List<float>();

        // Tangent vectors: 4 floats per vertex (x, y, z, w) where w = handedness (±1).
        // Same ordering as texCoords — 3 tangents per triangle, matching glVertices.
        public List<float> tangents = new List<float>();

        public void Clear()
        {
            triangles.Clear();
            boundingBox.Clear();
            materials.Clear();
            texCoords.Clear();
            tangents.Clear();
        }

        public void EnsureCapacity(int triCount)
        {
            triangles.EnsureCapacity(triCount);
        }

        public void CopyTo(TopoModel newModel)
        {
            foreach (TopoTriangle t in triangles)
                newModel.triangles.Add(new TopoTriangle(t));
        }

        // ------------------------------------------------------------------
        // AddTriangle overloads
        // ------------------------------------------------------------------

        public void AddTriangle(RHVector3 p1, RHVector3 p2, RHVector3 p3, RHVector3 normal)
        {
            TopoVertex v1 = new TopoVertex(p1);
            TopoVertex v2 = new TopoVertex(p2);
            TopoVertex v3 = new TopoVertex(p3);

            triangles.Add(new TopoTriangle(v1, v2, v3, normal));
            boundingBox.Add(p1);
            boundingBox.Add(p2);
            boundingBox.Add(p3);
        }

        public void AddTriangle(RHVector3 p1, RHVector3 p2, RHVector3 p3, RHVector3 normal, float[] color)
        {
            TopoVertex v1 = new TopoVertex(p1);
            TopoVertex v2 = new TopoVertex(p2);
            TopoVertex v3 = new TopoVertex(p3);

            triangles.Add(new TopoTriangle(v1, v2, v3, normal, color));
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

            triangles.Add(new TopoTriangle(v1, v2, v3, normal));
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

        // ------------------------------------------------------------------

        private void removeTriangle(TopoTriangle triangle)
        {
            triangles.Remove(triangle);
        }

        public double Surface()
        {
            double surface = 0;
            foreach (TopoTriangle t in triangles)
                surface += t.Area();
            return surface;
        }

        public double Volume()
        {
            double volume = 0;
            foreach (TopoTriangle t in triangles)
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
            foreach (TopoTriangle t in triangles)
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

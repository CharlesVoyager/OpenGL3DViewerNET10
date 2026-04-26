#nullable disable

namespace View3D.model.geom
{
    public class Submesh
    {
        public class DrawRange
        {
            public int StartVertex;
            public int VertexCount;
            public int MaterialIndex = -1;
        }

        public bool selected = false;

        public float[] glVertices = null; // [x y z]
        public float[] glNormals = null;  // [nx ny nz]
        public float[] glColors = null;
        public List<DrawRange> DrawRanges = new List<DrawRange>();

        int idxVertices = 0;
        int idxNormals = 0;
        int idxColors = 0;

        public void EnsureCapacity(int triCount, bool hasColor)
        {
            glVertices = new float[triCount * 3 * 3];   // Triangle has 3 vertices, every vertex has 3 floats for position.
            glNormals = new float[triCount * 3 * 3];    // Triangle has 3 vertices, every vertex has 3 floats for normal.
            if (hasColor)
                glColors = new float[triCount * 3 * 3]; // Triangle has 3 vertices, every vertex has 3 floats for color.
        }

        public void Clear()
        {
            glVertices = null;
            glNormals = null;
            glColors = null;
            DrawRanges.Clear();
            idxVertices = 0;
            idxNormals = 0;
            idxColors = 0;
        }

        public void CopyTo(Submesh newMesh)
        {
            newMesh.glVertices = new float[glVertices.Length];
            Array.Copy(glVertices, newMesh.glVertices, glVertices.Length);

            newMesh.glNormals = new float[glNormals.Length];
            Array.Copy(glNormals, newMesh.glNormals, glNormals.Length);

            if (glColors != null)
            {
                newMesh.glColors = new float[glColors.Length];
                Array.Copy(glColors, newMesh.glColors, glColors.Length);
            }

            foreach (DrawRange range in DrawRanges)
            {
                newMesh.DrawRanges.Add(new DrawRange
                {
                    StartVertex = range.StartVertex,
                    VertexCount = range.VertexCount,
                    MaterialIndex = range.MaterialIndex
                });
            }
        }

        public void AddTriangle(RHVector3 v1, RHVector3 v2, RHVector3 v3, RHVector3 n, float[] color)
        {
            AddTriangle(v1, v2, v3, n, n, n, color);
        }

        public void AddTriangle(RHVector3 v1, RHVector3 v2, RHVector3 v3, RHVector3 n0, RHVector3 n1, RHVector3 n2, float[] color)
        {
            if (idxVertices + 9 > glVertices.Length)
                throw new Exception("Too many triangles added to submesh vertices.");

            if (idxNormals + 9 > glNormals.Length)
                throw new Exception("Too many triangles added to submesh normals.");

            glVertices[idxVertices++] = (float)v1.x;
            glVertices[idxVertices++] = (float)v1.y;
            glVertices[idxVertices++] = (float)v1.z;

            glNormals[idxNormals++] = (float)n0.x;
            glNormals[idxNormals++] = (float)n0.y;
            glNormals[idxNormals++] = (float)n0.z;

            glVertices[idxVertices++] = (float)v2.x;
            glVertices[idxVertices++] = (float)v2.y;
            glVertices[idxVertices++] = (float)v2.z;

            glNormals[idxNormals++] = (float)n1.x;
            glNormals[idxNormals++] = (float)n1.y;
            glNormals[idxNormals++] = (float)n1.z;

            glVertices[idxVertices++] = (float)v3.x;
            glVertices[idxVertices++] = (float)v3.y;
            glVertices[idxVertices++] = (float)v3.z;

            glNormals[idxNormals++] = (float)n2.x;
            glNormals[idxNormals++] = (float)n2.y;
            glNormals[idxNormals++] = (float)n2.z;

            if (color != null)
            {
                if (idxColors + 9 > glColors.Length)
                    throw new Exception("Too many colors added to submesh.");

                glColors[idxColors++] = color[0];
                glColors[idxColors++] = color[1];
                glColors[idxColors++] = color[2];

                glColors[idxColors++] = color[0];
                glColors[idxColors++] = color[1];
                glColors[idxColors++] = color[2];

                glColors[idxColors++] = color[0];
                glColors[idxColors++] = color[1];
                glColors[idxColors++] = color[2];
            }
        }
    }
}

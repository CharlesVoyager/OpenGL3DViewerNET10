using OpenTK.Mathematics;

namespace View3D.model.geom
{
   
    public class Submesh
    {
        public bool selected = false;

        // [x y z nx ny nz]
        public float[] glVertices = null;

        public float[] glColors = null;

        int idx = 0;

        public void EnsureCapacity(int triCount)
        {
            glVertices = new float[triCount * 3 * 6];
        }


        public void Clear()
        {
            glVertices = null;

            glColors = null;
        }

        public void CopyTo(Submesh newMesh)
        {
            newMesh.glVertices = new float[glVertices.Length];
            newMesh.glColors = new float[glColors.Length];

            Array.Copy(glVertices,          newMesh.glVertices,         glVertices.Length);
            Array.Copy(glColors,            newMesh.glColors,           glColors.Length);
        }


        public void AddTriangle(RHVector3 v1, RHVector3 v2, RHVector3 v3, RHVector3 n, float[] color)
        {
            if (idx + 18 > glVertices.Length)
            {
                throw new Exception("Too many triangles added to submesh");
            }
            glVertices[idx++] = (float)v1.x;
            glVertices[idx++] = (float)v1.y;
            glVertices[idx++] = (float)v1.z;

            glVertices[idx++] = (float)n.x;
            glVertices[idx++] = (float)n.y;
            glVertices[idx++] = (float)n.z;

            glVertices[idx++] = (float)v2.x;
            glVertices[idx++] = (float)v2.y;
            glVertices[idx++] = (float)v2.z;

            glVertices[idx++] = (float)n.x;
            glVertices[idx++] = (float)n.y;
            glVertices[idx++] = (float)n.z;


            glVertices[idx++] = (float)v3.x;
            glVertices[idx++] = (float)v3.y;
            glVertices[idx++] = (float)v3.z;

            glVertices[idx++] = (float)n.x;
            glVertices[idx++] = (float)n.y;
            glVertices[idx++] = (float)n.z;
        }
    }
}

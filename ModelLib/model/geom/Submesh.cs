using OpenTK.Mathematics;

namespace View3D.model.geom
{
   
    public class Submesh
    {
        public bool selected = false;

        // [x y z nx ny nz]
        public float[] glVertices = null;

        public float[] glColors = null;

        int idxVertices = 0;
        int idxColors = 0;

        public void EnsureCapacity(int triCount)
        {
            glVertices = new float[triCount * 3 * 6];
            glColors = new float[triCount * 3];
        }

        public void Clear()
        {
            glVertices = null;

            glColors = null;
        }

        public void CopyTo(Submesh newMesh)
        {
            newMesh.glVertices = new float[glVertices.Length];
            Array.Copy(glVertices,          newMesh.glVertices,         glVertices.Length);

            if (glColors != null)
            {
                newMesh.glColors = new float[glColors.Length];
                Array.Copy(glColors,            newMesh.glColors,           glColors.Length);
            }
        }


        public void AddTriangle(RHVector3 v1, RHVector3 v2, RHVector3 v3, RHVector3 n, float[] color)
        {
            if (idxVertices + 18 > glVertices.Length)
            {
                throw new Exception("Too many triangles added to submesh");
            }
            glVertices[idxVertices++] = (float)v1.x;
            glVertices[idxVertices++] = (float)v1.y;
            glVertices[idxVertices++] = (float)v1.z;

            glVertices[idxVertices++] = (float)n.x;
            glVertices[idxVertices++] = (float)n.y;
            glVertices[idxVertices++] = (float)n.z;

            glVertices[idxVertices++] = (float)v2.x;
            glVertices[idxVertices++] = (float)v2.y;
            glVertices[idxVertices++] = (float)v2.z;

            glVertices[idxVertices++] = (float)n.x;
            glVertices[idxVertices++] = (float)n.y;
            glVertices[idxVertices++] = (float)n.z;

            glVertices[idxVertices++] = (float)v3.x;
            glVertices[idxVertices++] = (float)v3.y;
            glVertices[idxVertices++] = (float)v3.z;

            glVertices[idxVertices++] = (float)n.x;
            glVertices[idxVertices++] = (float)n.y;
            glVertices[idxVertices++] = (float)n.z;

            if (color != null)
            {
                if (idxColors + 3 > glColors.Length)
                {
                    throw new Exception("Too many triangles added to submesh");
                }

                glColors[idxColors++] = color[0];
                glColors[idxColors++] = color[1];
                glColors[idxColors++] = color[2];
            }
        }
    }
}

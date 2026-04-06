using OpenTK.Mathematics;

namespace View3D.model.geom
{
    public class SubmeshEdge
    {
        public int vertex1, vertex2;
        public int color;
        public SubmeshEdge(int v1, int v2, int col)
        {
            vertex1 = v1;
            vertex2 = v2;
            color = col;
        }
    }

    public class SubmeshTriangle
    {
        public int vertex1, vertex2, vertex3;
        public int color;

        public SubmeshTriangle(int v1, int v2, int v3, int col)
        {
            vertex1 = v1;
            vertex2 = v2;
            vertex3 = v3;
            color = col;
        }

        public void Normal(Submesh mesh, out float nx, out float ny, out float nz)
        {
            Vector3 v0 = mesh.vertices[vertex1];
            Vector3 v1 = mesh.vertices[vertex2];
            Vector3 v2 = mesh.vertices[vertex3];
            float a1 = v1.X - v0.X;
            float a2 = v1.Y - v0.Y;
            float a3 = v1.Z - v0.Z;
            float b1 = v2.X - v1.X;
            float b2 = v2.Y - v1.Y;
            float b3 = v2.Z - v1.Z;
            nx = a2 * b3 - a3 * b2;
            ny = a3 * b1 - a1 * b3;
            nz = a1 * b2 - a2 * b1;
            float length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length == 0)
            {
                nx = ny = 0;
                nz = 1;
            }
            else
            {
                nx /= length;
                ny /= length;
                nz /= length;
            }
        }
    }
    public class Submesh
    {
        public enum MeshColor {
            FrontBack   = -1,
            ErrorFace   = -2,
            ErrorEdge   = -3,
            OutSide     = -4,
            EdgeLoop    = -5,
            CutEdge     = -6,
            Normal      = -7,
            Edge        = -8,
            Back        = -9,
            TransBlue   = -10,
            OverhangLv1 = -11,
            OverhangLv2 = -12,
            OverhangLv3 = -13,
        };

        public const int MESHCOLOR_FRONTBACK = -1;
        public const int MESHCOLOR_ERRORFACE = -2;
        public const int MESHCOLOR_ERROREDGE = -3;
        public const int MESHCOLOR_OUTSIDE = -4;
        public const int MESHCOLOR_EDGE_LOOP = -5;
        public const int MESHCOLOR_CUT_EDGE = -6;
        public const int MESHCOLOR_NORMAL = -7;
        public const int MESHCOLOR_EDGE = -8;
        public const int MESHCOLOR_BACK = -9;
        public const int MESHCOLOR_TRANS_BLUE = -10;
        public const int MESHCOLOR_PINK = -11;
        public const int MESHCOLOR_LIGHTPINK = -12;
        public const int MESHCOLOR_LIGHTPINK_WHITE = -13;

        public List<Vector3> vertices = new List<Vector3>();
        public List<SubmeshTriangle> triangles = new List<SubmeshTriangle>();
        public bool selected = false;

        public float[] glVertices = null;
        public float[] glNormals = null;
        public int[] glColors = null;
        public int[] glTriangles = null;

        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();

            glVertices = null;
            glNormals = null;
            glColors = null;
            glTriangles = null;
        }

        /// <summary>
        /// Remove unneded temporary data
        /// </summary>
        public void Compress()
        {
            glVertices = new float[3 * vertices.Count];
            glNormals = new float[3 * vertices.Count];
            glColors = new int[vertices.Count];
            glTriangles = new int[triangles.Count * 3];

            UpdateDrawLists();
            vertices.Clear();
        }

        public void CopyTo(Submesh newMesh)
        {
            newMesh.glVertices = new float[glVertices.Length];
            newMesh.glNormals = new float[glNormals.Length];
            newMesh.glColors = new int[glColors.Length];
            newMesh.glTriangles = new int[glTriangles.Length];

            Array.Copy(glVertices,          newMesh.glVertices,         glVertices.Length);
            Array.Copy(glNormals,           newMesh.glNormals,          glNormals.Length);
            Array.Copy(glColors,            newMesh.glColors,           glColors.Length);
            Array.Copy(glTriangles,         newMesh.glTriangles,        glTriangles.Length);
        }

        public int VertexId(RHVector3 v)
        {
            int pos = vertices.Count;
            vertices.Add(new Vector3((float)v.x, (float)v.y, (float)v.z));
            return pos;
        }

        public int VertexId(double x, double y, double z)
        {
            int pos = vertices.Count;
            vertices.Add(new Vector3((float)x, (float)y, (float)z));
            return pos;
        }



        public void AddTriangle(RHVector3 v1, RHVector3 v2, RHVector3 v3, int color)
        {
            triangles.Add(new SubmeshTriangle(VertexId(v1), VertexId(v2), VertexId(v3), color));
        }

        private void UpdateDrawLists()
        {
            int idx = 0;
            foreach (SubmeshTriangle t in triangles)
            {
                int n1 = 3 * t.vertex1;
                int n2 = 3 * t.vertex2;
                int n3 = 3 * t.vertex3;
                Vector3 v1 = vertices[t.vertex1];
                Vector3 v2 = vertices[t.vertex2];
                Vector3 v3 = vertices[t.vertex3];
                t.Normal(this, out glNormals[n1], out glNormals[n1 + 1], out glNormals[n1 + 2]);
                glNormals[n2] = glNormals[n3] = glNormals[n1];
                glNormals[n2 + 1] = glNormals[n3 + 1] = glNormals[n1 + 1];
                glNormals[n2 + 2] = glNormals[n3 + 2] = glNormals[n1 + 2];
                glVertices[n1++] = v1.X;
                glVertices[n1++] = v1.Y;
                glVertices[n1] = v1.Z;
                glVertices[n2++] = v2.X;
                glVertices[n2++] = v2.Y;
                glVertices[n2] = v2.Z;
                glVertices[n3++] = v3.X;
                glVertices[n3++] = v3.Y;
                glVertices[n3] = v3.Z;
                glTriangles[idx++] = t.vertex1;
                glTriangles[idx++] = t.vertex2;
                glTriangles[idx++] = t.vertex3;
            }
        }
    }
}

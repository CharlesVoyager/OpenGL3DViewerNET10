#nullable disable

namespace View3D.model.geom
{
    public class TopoTriangle
    {
        public TopoVertex[] Vertices = new TopoVertex[3];
        public RHVector3 Normal;
        public RHVector3[] VertexNormals = new RHVector3[3];
        public float[] Color;

        public TopoTriangle(TopoTriangle t)
        {
            for (int i = 0; i < 3; i++)
            {
                Vertices[i] = new TopoVertex(new RHVector3(t.Vertices[i].pos.x, t.Vertices[i].pos.y, t.Vertices[i].pos.z));
                VertexNormals[i] = new RHVector3(t.VertexNormals[i].x, t.VertexNormals[i].y, t.VertexNormals[i].z);
            }

            Normal = new RHVector3(t.Normal.x, t.Normal.y, t.Normal.z);
            Color = t.Color != null ? (float[])t.Color.Clone() : null;
        }

        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3)
        {
            Vertices[0] = v1;
            Vertices[1] = v2;
            Vertices[2] = v3;
            RecomputeNormal();
            SetVertexNormalsFromFaceNormal();
            Color = null;
        }

        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3, RHVector3 n)
        {
            Vertices[0] = v1;
            Vertices[1] = v2;
            Vertices[2] = v3;
            Normal = n;
            SetVertexNormalsFromFaceNormal();
            Color = null;
        }

        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3, RHVector3 n, float[] color)
        {
            Vertices[0] = v1;
            Vertices[1] = v2;
            Vertices[2] = v3;
            Normal = n;
            SetVertexNormalsFromFaceNormal();
            Color = color;
        }

        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3, RHVector3[] vertexNormals, RHVector3 faceNormal, float[] color = null)
        {
            Vertices[0] = v1;
            Vertices[1] = v2;
            Vertices[2] = v3;
            Normal = faceNormal;
            for (int i = 0; i < 3; i++)
                VertexNormals[i] = new RHVector3(vertexNormals[i].x, vertexNormals[i].y, vertexNormals[i].z);
            Color = color;
        }


        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is TopoTriangle)) return false;
            return (((TopoTriangle)obj).Vertices[0] == this.Vertices[0] && ((TopoTriangle)obj).Vertices[1] == this.Vertices[1] && ((TopoTriangle)obj).Vertices[2] == this.Vertices[2]);
        }

        // This function GetHashCode() is necessary. DO NOT DELETE IT.
        // For example, in the case: Dictionary<TopoTriangle, int>, it needs GetHashCode() to check if the keys are duplicate!!!
        public override int GetHashCode()
        {
            return ((this.Vertices[0].pos.x + this.Vertices[0].pos.y + this.Vertices[0].pos.z) * 5915587277 +
                   (this.Vertices[1].pos.x + this.Vertices[1].pos.y + this.Vertices[1].pos.z) * 1500450271 +
                   (this.Vertices[2].pos.x + this.Vertices[2].pos.y + this.Vertices[2].pos.z) * 3267000013).GetHashCode();
        }

        public void FlipDirection()
        {
            Normal.Scale(-1);
            for (int i = 0; i < 3; i++)
                VertexNormals[i].Scale(-1);
            TopoVertex v = Vertices[0];
            Vertices[0] = Vertices[1];
            Vertices[1] = v;

            RHVector3 n = VertexNormals[0];
            VertexNormals[0] = VertexNormals[1];
            VertexNormals[1] = n;
        }

        public void RecomputeNormal()
        {
            RHVector3 d1 = Vertices[1].pos.Subtract(Vertices[0].pos);
            RHVector3 d2 = Vertices[2].pos.Subtract(Vertices[1].pos);
            Normal = d1.CrossProduct(d2);
            Normal.NormalizeSafe();
            SetVertexNormalsFromFaceNormal();
        }

        private void SetVertexNormalsFromFaceNormal()
        {
            for (int i = 0; i < 3; i++)
                VertexNormals[i] = new RHVector3(Normal.x, Normal.y, Normal.z);
        }

        public int VertexIndexFor(TopoVertex test)
        {
            if (test == Vertices[0]) return 0;
            if (test == Vertices[1]) return 1;
            if (test == Vertices[2]) return 2;
            return -1;
        }

        public double SignedVolume()
        {
            return Vertices[0].pos.ScalarProduct(Vertices[1].pos.CrossProduct(Vertices[2].pos)) / 6.0;
        }

        public double Area()
        {
            RHVector3 d1 = Vertices[1].pos.Subtract(Vertices[0].pos);
            RHVector3 d2 = Vertices[2].pos.Subtract(Vertices[1].pos);
            return 0.5 * d1.CrossProduct(d2).Length;
        }

        public bool IsDegenerated()
        {
            if (Vertices[0] == Vertices[1] || Vertices[1] == Vertices[2] || Vertices[2] == Vertices[0])
                return true;
            return false;
        }

        /// <summary>
        /// Checks if all Vertices are colinear preventing a Normal computation. If point are coliniear the center vertex is
        /// moved in the direction of the edge to allow Normal computations.
        /// </summary>
        /// <returns></returns>
        public bool CheckIfColinear()
        {
            RHVector3 zero = new RHVector3(0, 0, 0);
            RHVector3 d1 = Vertices[1].pos.Subtract(Vertices[0].pos);
            RHVector3 d2 = Vertices[2].pos.Subtract(Vertices[1].pos);

            if (!d1.CrossProduct(d2).Equals(zero))
                return false;
            else
                return true;
        }

        public int NumberOfSharedVertices(TopoTriangle tri)
        {
            int sameVertices = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (Vertices[i] == tri.Vertices[j])
                    {
                        sameVertices++;
                        break;
                    }
                }
            }
            return sameVertices;
        }

        public bool SameNormalOrientation(TopoTriangle test)
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (Vertices[i] == test.Vertices[j] && Vertices[(i + 1) % 3] == test.Vertices[(j + 2) % 3])
                        return true;
                }
            }
            return false;
        }

        public RHVector3 Center
        {
            get
            {
                RHVector3 c = Vertices[0].pos.Add(Vertices[1].pos).Add(Vertices[2].pos);
                c.Scale(1.0 / 3.0);
                return c;
            }
        }

        public override string ToString()
        {
            string output = string.Empty;
            for (int i = 0;i < 3; i++)
            {
                output += "V" + i.ToString() +": " + Vertices[i].pos.x.ToString("0.0") + " " + Vertices[i].pos.y.ToString("0.0") + " " + Vertices[i].pos.z.ToString("0.0");
                output += "\n";
            }
            return output;
        }
    }
}

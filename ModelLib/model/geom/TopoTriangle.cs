
namespace View3D.model.geom
{
    public class TopoTriangle
    {
        public TopoVertex[] vertices = new TopoVertex[3];
        public RHVector3 normal;
        public float[] Color;

        public TopoTriangle(TopoTriangle t)
        {
            for (int i = 0; i < 3; i++)
                vertices[i] = new TopoVertex(new RHVector3(t.vertices[i].pos.x, t.vertices[i].pos.y, t.vertices[i].pos.z));

            normal = new RHVector3(t.normal.x, t.normal.y, t.normal.z);
        }

        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3)
        {
            vertices[0] = v1;
            vertices[1] = v2;
            vertices[2] = v3;
            RecomputeNormal();
        }

        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3, RHVector3 n)
        {
            vertices[0] = v1;
            vertices[1] = v2;
            vertices[2] = v3;
            normal = n;
            Color = new float[] { 1f, 1f, 1f, 1f }; // RGBA [0..1]
        }

        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3, RHVector3 n, float[] color)
        {
            vertices[0] = v1;
            vertices[1] = v2;
            vertices[2] = v3;
            normal = n;
            Color = color;
        }


        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is TopoTriangle)) return false;
            return (((TopoTriangle)obj).vertices[0] == this.vertices[0] && ((TopoTriangle)obj).vertices[1] == this.vertices[1] && ((TopoTriangle)obj).vertices[2] == this.vertices[2]);
        }

        // This function GetHashCode() is necessary. DO NOT DELETE IT.
        // For example, in the case: Dictionary<TopoTriangle, int>, it needs GetHashCode() to check if the keys are duplicate!!!
        public override int GetHashCode()
        {
            return ((this.vertices[0].pos.x + this.vertices[0].pos.y + this.vertices[0].pos.z) * 5915587277 +
                   (this.vertices[1].pos.x + this.vertices[1].pos.y + this.vertices[1].pos.z) * 1500450271 +
                   (this.vertices[2].pos.x + this.vertices[2].pos.y + this.vertices[2].pos.z) * 3267000013).GetHashCode();
        }

        public void FlipDirection()
        {
            normal.Scale(-1);
            TopoVertex v = vertices[0];
            vertices[0] = vertices[1];
            vertices[1] = v;
        }

        public void RecomputeNormal()
        {
            RHVector3 d1 = vertices[1].pos.Subtract(vertices[0].pos);
            RHVector3 d2 = vertices[2].pos.Subtract(vertices[1].pos);
            normal = d1.CrossProduct(d2);
            normal.NormalizeSafe();
        }

        public int VertexIndexFor(TopoVertex test)
        {
            if (test == vertices[0]) return 0;
            if (test == vertices[1]) return 1;
            if (test == vertices[2]) return 2;
            return -1;
        }

        public double SignedVolume()
        {
            return vertices[0].pos.ScalarProduct(vertices[1].pos.CrossProduct(vertices[2].pos)) / 6.0;
        }

        public double Area()
        {
            RHVector3 d1 = vertices[1].pos.Subtract(vertices[0].pos);
            RHVector3 d2 = vertices[2].pos.Subtract(vertices[1].pos);
            return 0.5 * d1.CrossProduct(d2).Length;
        }

        public bool IsDegenerated()
        {
            if (vertices[0] == vertices[1] || vertices[1] == vertices[2] || vertices[2] == vertices[0])
                return true;
            return false;
        }

        /// <summary>
        /// Checks if all vertices are colinear preventing a normal computation. If point are coliniear the center vertex is
        /// moved in the direction of the edge to allow normal computations.
        /// </summary>
        /// <returns></returns>
        public bool CheckIfColinear()
        {
            RHVector3 zero = new RHVector3(0, 0, 0);
            RHVector3 d1 = vertices[1].pos.Subtract(vertices[0].pos);
            RHVector3 d2 = vertices[2].pos.Subtract(vertices[1].pos);
            //double angle = d1.Angle(d2);
            //if (angle > 0.001 && angle<Math.PI-0.001) 
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
                    if (vertices[i] == tri.vertices[j])
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
                    if (vertices[i] == test.vertices[j] && vertices[(i + 1) % 3] == test.vertices[(j + 2) % 3])
                        return true;
                }
            }
            return false;
        }

        public RHVector3 Center
        {
            get
            {
                RHVector3 c = vertices[0].pos.Add(vertices[1].pos).Add(vertices[2].pos);
                c.Scale(1.0 / 3.0);
                return c;
            }
        }

        public override string ToString()
        {
            string output = string.Empty;
            for (int i = 0;i < 3; i++)
            {
                output += "V" + i.ToString() +": " + vertices[i].pos.x.ToString("0.0") + " " + vertices[i].pos.y.ToString("0.0") + " " + vertices[i].pos.z.ToString("0.0");
                output += "\n";
            }
            return output;
        }
    }
}

using System;
using System.Collections.Generic;

namespace View3D.model.geom
{
    public class TopoTriangle
    {
        public TopoVertex[] vertices = new TopoVertex[3];
        public int id ;
        public RHVector3 normal;

        public TopoTriangle(int _id,TopoVertex v1, TopoVertex v2, TopoVertex v3)
        {
            id = _id;
            vertices[0] = v1;
            vertices[1] = v2;
            vertices[2] = v3;
            RecomputeNormal();
        }
        //---
        public TopoTriangle(TopoVertex v1, TopoVertex v2, TopoVertex v3)
        {
            vertices[0] = v1;
            vertices[1] = v2;
            vertices[2] = v3;
            RecomputeNormal();
        }
        //---

        public TopoTriangle(TopoModel model, TopoVertex v1, TopoVertex v2, TopoVertex v3, double nx, double ny, double nz)
        {
            vertices[0] = v1;
            vertices[1] = v2;
            vertices[2] = v3;
            normal = new RHVector3(nx, ny, nz);
            //RHVector3 normalTest = new RHVector3(nx, ny, nz);
        }

        public TopoTriangle(TopoModel model, TopoVertex v1, TopoVertex v2, TopoVertex v3)
        {
            vertices[0] = v1;
            vertices[1] = v2;
            vertices[2] = v3;
            RecomputeNormal();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is TopoTriangle)) return false;

            //if (((TopoTriangle)obj).vertices[0] == this.vertices[0])
            //    System.Console.WriteLine("Same 0!");

            //if (((TopoTriangle)obj).vertices[1] == this.vertices[1])
            //    System.Console.WriteLine("Same 1!");

            //if (((TopoTriangle)obj).vertices[2] == this.vertices[2])
            //    System.Console.WriteLine("Same 2!");

            return (((TopoTriangle)obj).vertices[0] == this.vertices[0] && ((TopoTriangle)obj).vertices[1] == this.vertices[1] && ((TopoTriangle)obj).vertices[2] == this.vertices[2]);
        }

        public override int GetHashCode()
        {
            //return (Convert.ToInt64(this.vertices[0].pos.x * 1000000) ^ Convert.ToInt64(this.vertices[0].pos.y * 1000000) ^ Convert.ToInt64(this.vertices[0].pos.z * 1000000) ^
            //       Convert.ToInt64(this.vertices[1].pos.x * 1000000) ^ Convert.ToInt64(this.vertices[1].pos.y * 1000000) ^ Convert.ToInt64(this.vertices[1].pos.z * 1000000) ^
            //       Convert.ToInt64(this.vertices[2].pos.x * 1000000) ^ Convert.ToInt64(this.vertices[2].pos.y * 1000000) ^ Convert.ToInt64(this.vertices[2].pos.z * 1000000)).GetHashCode();


            return ((this.vertices[0].pos.x + this.vertices[0].pos.y + this.vertices[0].pos.z) * 5915587277 +
                    (this.vertices[1].pos.x + this.vertices[1].pos.y + this.vertices[1].pos.z) * 1500450271 +
                    (this.vertices[2].pos.x + this.vertices[2].pos.y + this.vertices[2].pos.z) * 3267000013).GetHashCode();
        }

        public void Unlink(TopoModel model)
        {
            //edges[0].disconnectFace(this,model);
            //edges[1].disconnectFace(this,model);
            //edges[2].disconnectFace(this,model);
            //vertices[0].disconnectFace(this);
            //vertices[1].disconnectFace(this);
            //vertices[2].disconnectFace(this);
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
    }

    public class TopoTriangleDistance : IComparer<TopoTriangleDistance>, IComparable<TopoTriangleDistance>
    {
        public double distance;
        public TopoTriangle triangle;

        public TopoTriangleDistance(double dist, TopoTriangle tri)
        {
            triangle = tri;
            distance = dist;
        }

        public int Compare(TopoTriangleDistance td1, TopoTriangleDistance td2)
        {
            return -td1.distance.CompareTo(td2.distance);
        }

        public int CompareTo(TopoTriangleDistance td)
        {
            return -distance.CompareTo(td.distance);
        }
    }
}

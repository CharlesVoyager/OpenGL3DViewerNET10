namespace View3D.model.geom
{
    public class RHBoundingBox
    {
        static double epsilon = 1e-7;
        public RHVector3 minPoint = null;
        public RHVector3 maxPoint = null;

 		//--- MODEL_SLA	// milton
        public TopoTriangle[] getBoundingTri()
        {
            TopoTriangle[] triangles = new TopoTriangle[12];
            TopoVertex[] vertices = getVertices();

            // one box contain 6 surfaces (12 triangles)
            triangles[0] = new TopoTriangle(vertices[0], vertices[1], vertices[3]);
            triangles[1] = new TopoTriangle(vertices[0], vertices[3], vertices[2]);

            triangles[2] = new TopoTriangle(vertices[4], vertices[6], vertices[7]);
            triangles[3] = new TopoTriangle(vertices[4], vertices[7], vertices[5]);

            triangles[4] = new TopoTriangle(vertices[2], vertices[3], vertices[7]);
            triangles[5] = new TopoTriangle(vertices[2], vertices[7], vertices[6]);

            triangles[6] = new TopoTriangle(vertices[1], vertices[5], vertices[7]);
            triangles[7] = new TopoTriangle(vertices[1], vertices[7], vertices[3]);

            triangles[8] = new TopoTriangle(vertices[0], vertices[4], vertices[5]);
            triangles[9] = new TopoTriangle(vertices[0], vertices[5], vertices[1]);

            triangles[10] = new TopoTriangle(vertices[0], vertices[2], vertices[6]);
            triangles[11] = new TopoTriangle(vertices[0], vertices[6], vertices[4]);
            return triangles;
        }

        public TopoVertex[] getVertices()
        {
           TopoVertex[] vertices = new TopoVertex[8];
           RHVector3 MAX = maxPoint;
           RHVector3 min = minPoint;
           // one box contain 8 points
           vertices[0] = new TopoVertex(0, new RHVector3(min.x, min.y, min.z));
           vertices[1] = new TopoVertex(1, new RHVector3(min.x, min.y, MAX.z));
           vertices[2] = new TopoVertex(2, new RHVector3(min.x, MAX.y, min.z));
           vertices[3] = new TopoVertex(3, new RHVector3(min.x, MAX.y, MAX.z));

           vertices[4] = new TopoVertex(4, new RHVector3(MAX.x, min.y, min.z));
           vertices[5] = new TopoVertex(5, new RHVector3(MAX.x, min.y, MAX.z));
           vertices[6] = new TopoVertex(6, new RHVector3(MAX.x, MAX.y, min.z));
           vertices[7] = new TopoVertex(7, new RHVector3(MAX.x, MAX.y, MAX.z));
           return vertices;
        }

        public bool containTri(TopoTriangle triangle)
        {
            foreach(TopoVertex ver in triangle.vertices){
                if ( ContainsPoint(ver.pos) )
                    return true;
            }
            return false;
        }

        // Milton:  Efficient AABB/triangle intersection algoirthm 
        // from http://stackoverflow.com/questions/17458562/efficient-aabb-triangle-intersection-in-c-sharp
        public bool overlapTri(TopoTriangle triangle)
        {
            double triangleMin, triangleMax;
            double boxMin, boxMax;

            RHVector3[] boxNormals = {
                new RHVector3(1,0,0),
                new RHVector3(0,1,0),
                new RHVector3(0,0,1)
            };
            Project(triangle.vertices, boxNormals[0], out triangleMin, out triangleMax);
            if (triangleMax < minPoint.x || triangleMin > maxPoint.x )
                return false;
            Project(triangle.vertices, boxNormals[1], out triangleMin, out triangleMax);
            if (triangleMax < minPoint.y || triangleMin > maxPoint.y)
                return false;
            Project(triangle.vertices, boxNormals[2], out triangleMin, out triangleMax);
            if (triangleMax < minPoint.z || triangleMin > maxPoint.z)
                return false;

            double triangleOffset = triangle.normal.ScalarProduct(triangle.vertices[0].pos);
            Project(getVertices(), triangle.normal, out boxMin, out boxMax);
            if (boxMax < triangleOffset || boxMin > triangleOffset)
                return false; // No intersection possible.

            RHVector3[] triangleEdges = {
                triangle.vertices[0].pos.Subtract(triangle.vertices[1].pos),
                triangle.vertices[1].pos.Subtract(triangle.vertices[2].pos),
                triangle.vertices[2].pos.Subtract(triangle.vertices[0].pos)
            };

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    RHVector3 axis = triangleEdges[i].CrossProduct(boxNormals[j]);
                    Project(getVertices(), axis, out boxMin, out boxMax);
                    Project(triangle.vertices, axis, out triangleMin, out triangleMax);
                    if (boxMax < triangleMin || boxMin > triangleMax)
                        return false; // No intersection possible
                }
        
            return true;
        }

        private void Project(TopoVertex[] points, RHVector3 axis, out double min, out double max)
        {
            min = double.PositiveInfinity;
            max = double.NegativeInfinity;
            foreach (TopoVertex p in points)
            {
                //double val = axis.Dot(p);
                double val = axis.ScalarProduct(new RHVector3(p.pos.x, p.pos.y, p.pos.z));
                if (val < min) min = val;
                if (val > max) max = val;
            }
        }
        //---

        public void Add(RHVector3 point)
        {
            if (minPoint == null)
            {
                minPoint = new RHVector3(point);
                maxPoint = new RHVector3(point);
            }
            else
            {
                minPoint.StoreMinimum(point);
                maxPoint.StoreMaximum(point);
            }
        }

        public void Add(double x, double y, double z)
        {
            Add(new RHVector3(x, y, z));
        }

        public void Add(RHBoundingBox box)
        {
            if (box == null || box.minPoint == null) return;
            Add(box.minPoint);
            Add(box.maxPoint);
        }

        public void Clear()
        {
            minPoint = maxPoint = null;
        }

        public bool ContainsPoint(RHVector3 point)
        {
            if (minPoint == null) return false;
            return point.x >= minPoint.x && point.x <= maxPoint.x &&
                point.y >= minPoint.y && point.y <= maxPoint.y &&
                point.z >= minPoint.z && point.z <= maxPoint.z;
        }

        public bool IntersectsBox(RHBoundingBox box)
        {
            if (minPoint == null || box.minPoint == null) return false;
            bool xOverlap = Overlap(minPoint.x, maxPoint.x, box.minPoint.x, box.maxPoint.x);
            bool yOverlap = Overlap(minPoint.y, maxPoint.y, box.minPoint.y, box.maxPoint.y);
            bool zOverlap = Overlap(minPoint.z, maxPoint.z, box.minPoint.z, box.maxPoint.z);
            return xOverlap && yOverlap && zOverlap;
        }

        private bool Overlap(double p1min, double p1max, double p2min, double p2max)
        {
            if (p2min > p1max+epsilon) return false;
            if (p2max+epsilon < p1min) return false;
            return true;
        }

        public double xMin
        {
            get { return MinPoint.x; }
        }

        public double yMin
        {
            get { return MinPoint.y; }
        }

        public double zMin
        {
            get { return MinPoint.z; }
        }

        public double xMax
        {
            get { return MaxPoint.x; }
        }

        public double yMax
        {
            get { return MaxPoint.y; }
        }

        public double zMax
        {
            get { return MaxPoint.z; }
        }

        public RHVector3 MaxPoint 
        {
            get { return (maxPoint == null ? new RHVector3(0,0,0) : maxPoint); }
        }

        public RHVector3 MinPoint
        {
            get { return (minPoint == null ? new RHVector3(0, 0, 0) : minPoint); }
        }

        public RHVector3 Size
        {
            get { return MaxPoint.Subtract(MinPoint); }
        }

        public RHVector3 Center
        {
            get { 
                RHVector3 center = MaxPoint.Add(MinPoint);
#if PRECISION_SINGLE
                center.Scale(0.5F);
#else
                center.Scale(0.5);
#endif
                return center;
            }
        }

        /// <summary>
        /// Convert the box range into bitpattern for a fast intersection test.
        /// 
        /// </summary>
        /// <param name="box"></param>
        /// <returns></returns>
        public int RangeToBits(RHBoundingBox box)
        {
            double dx = (maxPoint.x - minPoint.x) / 10;
            double dy = (maxPoint.y - minPoint.y) / 10;
            double dz = (maxPoint.z - minPoint.z) / 10;
            int p = 0;
            int i;
            double px = minPoint.x;
            double px2 = px+dx;
            double vx = box.minPoint.x;
            double vx2 = box.maxPoint.x;
            double py = minPoint.y;
            double py2 = py + dy;
            double vy = box.minPoint.y;
            double vy2 = box.maxPoint.y;
            double pz = minPoint.z;
            double pz2 = pz + dz;
            double vz = box.minPoint.z;
            double vz2 = box.maxPoint.z;
            for (i = 0; i < 10; i++)
            {
                if (Overlap(px, px2, vx, vx2)) p |= 1 << i;
                if (Overlap(py, py2, vy, vy2)) p |= 1 << (10+i);
                if (Overlap(pz, pz2, vz, vz2)) p |= 1 << (20+i);
                px = px2;
                px2 += dx;
                py = py2;
                py2 += dy;
                pz = pz2;
                pz2 += dz;
            }
            return p;
        }

        static public bool IntersectBits(int a, int b)
        {
            int r = a & b;
            if (r == 0) return false;
            if ((r & 1023) == 0) return false;
            r >>= 10;
            if ((r & 1023) == 0) return false;
            r >>= 10;
            if ((r & 1023) == 0) return false;
            return true; ;
        }
    }
}

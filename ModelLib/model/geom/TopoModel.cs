using OpenTK.Mathematics;

namespace View3D.model.geom
{
    // TopoModel: Used to store orginal STL file triangle data with intact.
    public class TopoModel
    {
        public HashSet<TopoTriangle> triangles = new HashSet<TopoTriangle>();

        public RHBoundingBox boundingBox = new RHBoundingBox();

        public List<float> glVertices = new List<float>(); 

        public void Clear()
        {
            triangles.Clear();

            boundingBox.Clear();

            glVertices.Clear();
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


        private void removeTriangle(TopoTriangle triangle)
        {
            triangles.Remove(triangle);
        }

        
        public double Surface()
        {
            double surface = 0;
            foreach (TopoTriangle t in triangles)
            {
                surface += t.Area();
            }
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
            Vector4 ver1 = tInObj.vertices[0].pos.asVector4();
            Vector4 ver2 = tInObj.vertices[1].pos.asVector4();
            Vector4 ver3 = tInObj.vertices[2].pos.asVector4();

#if false   // OpeenTK 3.3.3.0
            ver1 = Vector4.Transform(ver1, trans);
            ver2 = Vector4.Transform(ver2, trans);
            ver3 = Vector4.Transform(ver3, trans);
#else       // OpenTK 4.9.4.
            ver1 = ver1 * trans;
            ver2 = ver2 * trans;
            ver3 = ver3 * trans;
#endif

            TopoVertex v1 = new TopoVertex(new RHVector3(ver1.X, ver1.Y, ver1.Z));
            TopoVertex v2 = new TopoVertex(new RHVector3(ver2.X, ver2.Y, ver2.Z));
            TopoVertex v3 = new TopoVertex(new RHVector3(ver3.X, ver3.Y, ver3.Z));
            tInWorld = new TopoTriangle(v1, v2, v3);
        }
    }
}

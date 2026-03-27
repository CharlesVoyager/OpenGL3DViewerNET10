using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using View3D.Extensions;

namespace View3D.model.geom
{
    public class TopoModel
    {
        static int MinVertexNumForProress = 2000; // large than all support part vertices number

        public const float epsilon = 0.001f;

        public TopoVertexStorage vertices = new TopoVertexStorage();
        public TopoTriangleStorage triangles = new TopoTriangleStorage();
        public RHBoundingBox boundingBox = new RHBoundingBox();

        // Under construction: To store vertices and normals in glVertices for later binding to GL buffer.
        public List<float> glVertices = new List<float>(); 

        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();
            boundingBox.Clear();

            glVertices.Clear();

            GC.Collect();
        }
        
        public void EnsureCapacity(int triCount) 
        { 
            triangles.EnsureCapacity(triCount);
            // glVertices.EnsureCapacity(triCount * 3 * 6);   // [x y z nx ny nz]
        }

        public TopoModel Copy()
        {
            TopoModel newModel = new TopoModel();
            int nOld = vertices.Count;
            int i = 0;
            List<TopoVertex> vcopy = new List<TopoVertex>(vertices.Count);
            foreach (TopoVertex v in vertices.v)
            {
                v.id = i++;
                TopoVertex newVert = new TopoVertex(v.id, v.pos);
                newModel.addVertex(newVert);
                vcopy.Add(newVert);
            }
            foreach (TopoTriangle t in triangles)
            {
                TopoTriangle triangle = new TopoTriangle(vcopy[t.vertices[0].id], vcopy[t.vertices[1].id], vcopy[t.vertices[2].id], t.normal.x, t.normal.y, t.normal.z);
                newModel.triangles.Add(triangle);
            }
            UpdateVertexNumbers();
            newModel.UpdateVertexNumbers();
            return newModel;
        }

        public void Merge(TopoModel model, Matrix4 trans, Action<double> updateRate)
        {
            int nOld = vertices.Count;
            int i = 0;
            double cnt = 0;

            List<TopoVertex> vcopy = new List<TopoVertex>(model.vertices.Count);
            foreach (TopoVertex v in model.vertices.v)
            {
                v.id = i++;
                TopoVertex newVert = new TopoVertex(v.id, v.pos, trans);
                addVertex(newVert);
                vcopy.Add(newVert);

                cnt++;
                if (updateRate != null && (model.vertices.v.Count > MinVertexNumForProress)
                    && (cnt % (model.vertices.v.Count / 10) == 0))
                {
                    updateRate((double)cnt / model.vertices.v.Count * 50.0);
                }
            }
            cnt = 0;
            foreach (TopoTriangle t in model.triangles)
            {
                TopoTriangle triangle = new TopoTriangle(vcopy[t.vertices[0].id], vcopy[t.vertices[1].id], vcopy[t.vertices[2].id]);
                triangle.RecomputeNormal();
                triangles.Add(triangle);

                cnt++;
                if (updateRate != null && (model.vertices.v.Count > MinVertexNumForProress)
                    && (cnt % (model.triangles.Count / 10) == 0))
                {
                    updateRate((double)cnt / model.triangles.Count * 50.0 + 50);
                }
            }
        }


        public TopoVertex findVertexOrNull(RHVector3 pos)
        {
            return vertices.SearchPoint(pos);
        }

        private void UpdateVertexNumbers()
        {
            int i = 1;
            foreach (TopoVertex v in vertices.v)
            {
                v.id = i++;
            }
        }

        public TopoTriangle addTriangle(double p1x, double p1y, double p1z, double p2x, double p2y, double p2z,
            double p3x, double p3y, double p3z, double nx, double ny, double nz)
        {
            TopoVertex v1 = addVertex(new RHVector3(p1x, p1y, p1z));
            TopoVertex v2 = addVertex(new RHVector3(p2x, p2y, p2z));
            TopoVertex v3 = addVertex(new RHVector3(p3x, p3y, p3z));
            TopoTriangle triangle = addTriangle(new TopoTriangle(v1, v2, v3, nx, ny, nz));

            return triangle;
        }

        public TopoTriangle addTriangle(RHVector3 p1, RHVector3 p2, RHVector3 p3, RHVector3 normal)
        {
            TopoVertex v1 = addVertex(p1);
            TopoVertex v2 = addVertex(p2);
            TopoVertex v3 = addVertex(p3);
            TopoTriangle triangle = addTriangle(new TopoTriangle(v1, v2, v3, normal.x, normal.y, normal.z));

            return triangle;
        }

        private void addVertex(TopoVertex v)
        {
            vertices.Add(v);
            boundingBox.Add(v.pos);
        }

        private TopoVertex addVertex(RHVector3 pos)
        {
            TopoVertex newVertex = findVertexOrNull(pos);
            if (newVertex == null)
            {
                newVertex = new TopoVertex(vertices.Count, pos); // vertex id start from 0
                addVertex(newVertex);
            }
            return newVertex;
        }

        private TopoTriangle addTriangle(TopoTriangle triangle)
        {
            triangles.Add(triangle);
            return triangle;
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

            TopoVertex v1 = new TopoVertex(0, new RHVector3(ver1.X, ver1.Y, ver1.Z));
            TopoVertex v2 = new TopoVertex(1, new RHVector3(ver2.X, ver2.Y, ver2.Z));
            TopoVertex v3 = new TopoVertex(2, new RHVector3(ver3.X, ver3.Y, ver3.Z));
            tInWorld = new TopoTriangle(v1, v2, v3);
        }

        public void FillMeshCheckRAM(Submesh mesh, int defaultColor)
        {
            int cnt = 0;
            foreach (TopoTriangle t in triangles)
            {
                if (0 == cnt % 50000)
                {
                    if (!Utils.RamTools.IsRamSizeValid())
                    {
                        throw new System.OutOfMemoryException();
                    }
                }
                mesh.AddTriangle(t.vertices[0].pos, t.vertices[1].pos, t.vertices[2].pos, defaultColor);
                cnt++;
            }
        }

        public void FillMesh(Submesh mesh, int defaultColor)
        {
            foreach (TopoTriangle t in triangles)
                mesh.AddTriangle(t.vertices[0].pos, t.vertices[1].pos, t.vertices[2].pos, defaultColor);
        }
    }
}

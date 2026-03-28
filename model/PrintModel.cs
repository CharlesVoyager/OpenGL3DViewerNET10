using OpenGL3DViewerNET10.Draw;
using OpenTK.Mathematics;
using System.Diagnostics;
using View3D.model.geom;
using View3D.ModelObjectTool;

namespace View3D.model
{
    public delegate void PrintModelChangedEvent(PrintModel model);
    public delegate LinkedList<PrintModel> ListviewGetModelsDelegate(bool selected);
    public partial class PrintModel : ThreeDModel
    {
        public TopoModel Model;         // Original STL file triangles data.

        public Submesh Mesh;            // Centerized triangles data.

        public ModelGLDraw Drawer;

        public RHBoundingBox BoundingBox;

        public string name = "Unknown";
        public bool outside = false;

        public Matrix4 trans;

        public double maxScaleVector = 0;
        public double minScaleVector = 0;
        public double m = 0;
        public double b = 0;

        public PrintModel()
        {            
            Model = new TopoModel();
            Mesh = new Submesh();
            Drawer = new ModelGLDraw(this);
            BoundingBox = new RHBoundingBox();
        }

        public virtual object cloneWithModel()
        {
            PrintModel stl = new PrintModel();
            Model.CopyTo(stl.Model);   // NOTE: Just clone Model is enough. Drawer/BoundingBox do not need to clone.
            Mesh.CopyTo(stl.Mesh);
            stl.name = name;
            stl.Position.x = Position.x;
            stl.Position.y = Position.y;
            stl.Position.z = Position.z;
            stl.Scale.x = Scale.x;
            stl.Scale.y = Scale.y;
            stl.Scale.z = Scale.z;
            stl.Rotation.x = Rotation.x;
            stl.Rotation.y = Rotation.y;
            stl.Rotation.z = Rotation.z;
            stl.trans = trans;
            stl.Selected = false;
            return stl;
        }

        public virtual void Clear()
        {
            Model.Clear();
            Mesh.Clear();
            BoundingBox.Clear();

            name = null;
            outside = false;

            // It should dispose drawer in main thread. But, it causes black flash when deleting model. So, we just let GC to dispose it.
#if false
            MainWindow.main.threeDControl.InvokeGL(() =>
            {
                Drawer.Dispose();
            });
#endif
        }

        public override string ToString()
        {
            return name;
        }

        /// <summary>
        /// Translate Object, so that the lowest point is 0.
        /// </summary>
        public void Land()
        {
            Position.z = (float)(BoundingBox.Size.z / 2);
            UpdateBoundingBoxByMatrix();
        }

        // Keep same height to the printer base after rotation.
        public void LandToZ(float oriZmin)
        {
            if (Math.Abs(oriZmin - zMin) < 0.001) return;
            Position.z += oriZmin - zMin;

            UpdateBoundingBoxByMatrix();
        }

        // Scale → Rotate → Translate (applied right-to-left in matrix multiplication):
        private void updateMatrix()
        {
            float x = Rotation.x;
            float y = Rotation.y;
            float z = Rotation.z;

            Matrix4 scale = Matrix4.CreateScale(
                     Scale.x != 0 ? Scale.x : 1,
                     Scale.y != 0 ? Scale.y : 1,
                     Scale.z != 0 ? Scale.z : 1
            );

            Matrix4 rotX = Matrix4.CreateRotationX(x * (float)Math.PI / 180.0f);
            Matrix4 rotY = Matrix4.CreateRotationY(y * (float)Math.PI / 180.0f);
            Matrix4 rotZ = Matrix4.CreateRotationZ(z * (float)Math.PI / 180.0f);

            Matrix4 transl = Matrix4.CreateTranslation(Position.x, Position.y, Position.z);

            // Combine: Scale → RotX → RotY → RotZ → Translate
            trans = scale * rotX * rotY * rotZ * transl;
        }

        private unsafe void updateBoundingBox()
        {
            //Stopwatch sw = Stopwatch.StartNew();

            BoundingBox.Clear();

            if (Mesh.glVertices.Length == 0)
                return;

            ModelMatrix mtx = ModelObjectToolHelper.ToModelMatrix(trans);
            fixed (float* ptr = &Mesh.glVertices[0])
            {
                BoundingBox3 box3 = ModelObjectToolWrapper.Instance.Tool.GetBoundingBox(mtx, ptr, Mesh.glVertices.Length / 3);
                BoundingBox.Add(box3.MaxX, box3.MaxY, box3.MaxZ);
                BoundingBox.Add(box3.MinX, box3.MinY, box3.MinZ);
            }

            //Debug.WriteLine("[PrintModel.calcBoundingBox]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void UpdateBoundingBoxAndMatrix()
        {
            //Stopwatch sw = Stopwatch.StartNew();

            updateMatrix(); // Must update trans Matrix before updating Bounding Box.

            updateBoundingBox();
            xMin = (float)BoundingBox.xMin;
            xMax = (float)BoundingBox.xMax;
            yMin = (float)BoundingBox.yMin;
            yMax = (float)BoundingBox.yMax;
            zMin = (float)BoundingBox.zMin;
            zMax = (float)BoundingBox.zMax;

            //Debug.WriteLine("[PrintModel.UpdateBoundingBoxAndMatrix]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        // This function is used when moving the object for saving bounding box compuation.
        // NOTE THAT: If the model is rotated, the bounding box can not be obtained just through trans matrix but compute all vertices in regular way.
        public void UpdateBoundingBoxByMatrix()
        {
            //Stopwatch sw = Stopwatch.StartNew();

            updateMatrix(); // Must update trans Matrix before updating Bounding Box.

            // Centerized current bounding box.
            float xMinCenter = (float)(BoundingBox.xMin - BoundingBox.Center.x);
            float yMinCenter = (float)(BoundingBox.yMin - BoundingBox.Center.y);
            float zMinCenter = (float)(BoundingBox.zMin - BoundingBox.Center.z);

            float xMaxCenter = (float)(BoundingBox.xMax - BoundingBox.Center.x);
            float yMaxCenter = (float)(BoundingBox.yMax - BoundingBox.Center.y);
            float zMaxCenter = (float)(BoundingBox.zMax - BoundingBox.Center.z);

            Vector4 minVertex = new Vector4(xMinCenter, yMinCenter, zMinCenter, 1.0f);
            Vector4 maxVertex = new Vector4(xMaxCenter, yMaxCenter, zMaxCenter, 1.0f);

            Matrix4 transl = Matrix4.CreateTranslation(Position.x, Position.y, Position.z);

            Vector4 newMinVertex = minVertex * transl;
            Vector4 newMaxVertex = maxVertex * transl;

            BoundingBox.minPoint.x = newMinVertex.X;
            BoundingBox.minPoint.y = newMinVertex.Y;
            BoundingBox.minPoint.z = newMinVertex.Z;

            BoundingBox.maxPoint.x = newMaxVertex.X;
            BoundingBox.maxPoint.y = newMaxVertex.Y;
            BoundingBox.maxPoint.z = newMaxVertex.Z;

            xMin = (float)BoundingBox.xMin;
            xMax = (float)BoundingBox.xMax;
            yMin = (float)BoundingBox.yMin;
            yMax = (float)BoundingBox.yMax;
            zMin = (float)BoundingBox.zMin;
            zMax = (float)BoundingBox.zMax;

            //Debug.WriteLine("[PrintModel.UpdateBoundingBoxByMatrix]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void TransformPoint(ref Vector3 v, out float x, out float y, out float z)
        {
            Vector4 v4 = new Vector4(v, 1);
            x = Vector4.Dot(trans.Column0, v4);
            y = Vector4.Dot(trans.Column1, v4);
            z = Vector4.Dot(trans.Column2, v4);
        }

        public void TransformPoint(RHVector3 v, out float x, out float y, out float z)
        {
            Vector4 v4 = v.asVector4();
            x = Vector4.Dot(trans.Column0, v4);
            y = Vector4.Dot(trans.Column1, v4);
            z = Vector4.Dot(trans.Column2, v4);
        }

        public void TransformPoint(RHVector3 v, RHVector3 outv)
        {
            Vector4 v4 = v.asVector4();
            outv.x = Vector4.Dot(trans.Column0, v4);
            outv.y = Vector4.Dot(trans.Column1, v4);
            outv.z = Vector4.Dot(trans.Column2, v4);
        }


        public void ModelToMesh()
        {
            //Stopwatch sw = Stopwatch.StartNew();

            Mesh.Clear();

            // Fill Mesh with checking RAM 
            int cnt = 0;
            foreach (TopoTriangle t in Model.triangles)
            {
                if (0 == cnt % 50000)
                {
                    if (!Utils.RamTools.IsRamSizeValid())
                    {
                        throw new System.OutOfMemoryException();
                    }
                }
                Mesh.AddTriangle(   t.vertices[0].pos.Subtract(Model.boundingBox.Center),
                                    t.vertices[1].pos.Subtract(Model.boundingBox.Center),
                                    t.vertices[2].pos.Subtract(Model.boundingBox.Center),
                                    Submesh.MESHCOLOR_FRONTBACK);
                cnt++;
            }
            // <>

            Mesh.selected = Selected;
            Mesh.Compress();

            //Debug.WriteLine("[PrintModel.Paint]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void CopyTopoModelBoundingBoxToPrintModel()
        {
            BoundingBox.Add(Model.boundingBox);
            xMin = (float)BoundingBox.xMin;
            xMax = (float)BoundingBox.xMax;
            yMin = (float)BoundingBox.yMin;
            yMax = (float)BoundingBox.yMax;
            zMin = (float)BoundingBox.zMin;
            zMax = (float)BoundingBox.zMax;
        }
    }
}

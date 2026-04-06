using OpenGL3DViewerNET10.Draw;
using OpenTK.Mathematics;
using View3D.model.geom;
using View3D.ModelObjectTool;

namespace OpenGL3DViewerNET10.ModelLib.model
{
    public delegate void PrintModelChangedEvent(PrintModel model);
    public delegate LinkedList<PrintModel> ListviewGetModelsDelegate(bool selected);
    public partial class PrintModel : ThreeDModel
    {
        public TopoModel Model;         // Original STL file triangles data.

        public Submesh Mesh;            // Centerized triangles data.

        public ModelGLDraw Drawer;

        public RHBoundingBox BoundingBox;

        public string Name = "Unknown";
        public bool outside = false;

        public Matrix4 trans;

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
            stl.Name = Name;
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

            Name = "Unknown";
            outside = false;

            // It should dispose drawer in main thread. But, it causes black flash when deleting model. So, we just let GC to dispose it.
#if false
            MainWindow.main.threeDControl.InvokeGL(() =>
            {
                Drawer.Dispose();
            });
#endif
        }

        /// <summary>
        /// Translate Object, so that the lowest point is 0.
        /// </summary>
        public void Land()
        {
            float shiftZ = -zMin;
            Position.z += shiftZ;
            UpdateTransMatrix();
            UpdateBoundingBoxByShift(0, 0, shiftZ);
        }

        // Keep same height to the printer base after rotation.
        public void LandToMinZ(float targetMinZ)
        {
            if (Math.Abs(targetMinZ - zMin) < 0.001) return;

            float shiftZ = targetMinZ - zMin;
            Position.z += shiftZ;

            UpdateTransMatrix();
            UpdateBoundingBoxByShift(0, 0, shiftZ);
        }

        // Scale → Rotate → Translate (applied right-to-left in matrix multiplication):
        public void UpdateTransMatrix()
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

            //Debug.WriteLine("[PrintModel.updateBoundingBox]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void UpdateBoundingBoxAndMatrix()
        {
            //Stopwatch sw = Stopwatch.StartNew();

            UpdateTransMatrix(); // Must update trans Matrix before updating Bounding Box.

            updateBoundingBox();

            //Debug.WriteLine("[PrintModel.UpdateBoundingBoxAndMatrix]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        // This function is used when moving the object for saving bounding box compuation.
        // NOTE NOTE NOTE: If the model is rotated, the bounding box can not be obtained just through trans matrix but compute all vertices in regular way.
        // Import Test Case: Rotate the model 40 degress -> Move the object to check if the bounding box is align correctly.
        public void UpdateBoundingBoxByShift(float shiftX, float shiftY, float shiftZ)
        {
            BoundingBox.MaxPoint.x += shiftX;
            BoundingBox.MinPoint.x += shiftX;

            BoundingBox.MaxPoint.y += shiftY;
            BoundingBox.MinPoint.y += shiftY;

            BoundingBox.MaxPoint.z += shiftZ;
            BoundingBox.MinPoint.z += shiftZ;
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

            Mesh.EnsureCapacity(Model.triangles.Count);

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
                Mesh.AddTriangle(   t.Vertices[0].pos.Subtract(Model.boundingBox.Center),
                                    t.Vertices[1].pos.Subtract(Model.boundingBox.Center),
                                    t.Vertices[2].pos.Subtract(Model.boundingBox.Center),
                                    t.Normal,
                                    t.Color);
                cnt++;
            }
            // <>

            Mesh.selected = Selected;


            //Debug.WriteLine("[PrintModel.Paint]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void CopyTopoModelBoundingBoxToPrintModel()
        {
            BoundingBox.Add(Model.boundingBox); // Copy TopoModel's Bounding Box to PrintModel.
        }

        public float xMin
        {
            get { return (float)BoundingBox.MinPoint.x; }
        }

        public float yMin
        {
            get { return (float)BoundingBox.MinPoint.y; }
        }

        public float zMin
        {
            get { return (float)BoundingBox.MinPoint.z; }
        }

        public float xMax
        {
            get { return (float)BoundingBox.MaxPoint.x; }
        }

        public float yMax
        {
            get { return (float)BoundingBox.MaxPoint.y; }
        }

        public float zMax
        {
            get { return (float)BoundingBox.MaxPoint.z; }
        }
    }
}

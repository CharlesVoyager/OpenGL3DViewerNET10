using OpenGL3DViewerNET10.Draw;
using OpenTK.Mathematics;
using System.Diagnostics;
using View3D.model.geom;
using View3D.ModelObjectTool;

namespace OpenGL3DViewerNET10.ModelLib.model
{
    public class Coord3D
    {
        double x = 0, y = 0, z = 0;
       
        private readonly Action<double, double, double> updateBoundingBoxByShift;
        public Coord3D(Action<double, double, double> operation)
        {
            updateBoundingBoxByShift = operation;
        }

        public double X
        {
            get { return x; }
            set 
            {
                double old = x;
                x = value;
                updateBoundingBoxByShift(x - old, 0, 0);
            }
        }

        public double Y
        {
            get { return y; }
            set
            {
                double old = y;
                y = value;
                updateBoundingBoxByShift(0, y - old, 0);
            }
        }

        public double Z
        {
            get { return z; }
            set
            {
                double old = z;
                z = value;
                updateBoundingBoxByShift(0, 0, z - old);
            }
        }
    }


    public class ThreeDModel
    {
        private bool selected = false;

        private Coord3D position = null;   
        private RHVector3 rotation = new RHVector3(0, 0, 0);    
        private RHVector3 scale = new RHVector3(1, 1, 1);


        public RHVector3 InitialPosition = new RHVector3(0, 0, 0);


        public TopoModel Model;         // Original triangles data from 3D Model file (Stl or Glb).

        public Submesh Mesh;            // Centerized triangles data.

        public ModelGLDraw Drawer;

        public RHBoundingBox BoundingBox;

        public string Name = "Unknown";

        public bool outside = false;
        public Matrix4 trans;

        public ThreeDModel()
        {
            position = new Coord3D(updateBoundingBoxByShift);
            Model = new TopoModel();
            Mesh = new Submesh();
            Drawer = new ModelGLDraw(this);
            BoundingBox = new RHBoundingBox();
        }

        public void CopyTo(ThreeDModel stl)
        {
            Model.CopyTo(stl.Model);   // NOTE: Just clone Model is enough. Drawer/BoundingBox do not need to clone.
            Mesh.CopyTo(stl.Mesh);
            stl.Name = Name;
            stl.position.X = position.X;
            stl.position.Y = position.Y;
            stl.position.Z = position.Z;
            stl.Scale.x = Scale.x;
            stl.Scale.y = Scale.y;
            stl.Scale.z = Scale.z;
            stl.Rotation.x = Rotation.x;
            stl.Rotation.y = Rotation.y;
            stl.Rotation.z = Rotation.z;
            stl.trans = trans;
            stl.Selected = false;
            BoundingBox.CopyTo(stl.BoundingBox);    // NOTE: This must be after copying position becuse setting position will update bounding box.
        }

        public void Clear()
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
            Position.Z += shiftZ;
            UpdateTransMatrix();
        }

        // Keep same height to the printer base after rotation.
        public void LandToMinZ(float targetMinZ)
        {
            if (Math.Abs(targetMinZ - zMin) < 0.001) return;

            float shiftZ = targetMinZ - zMin;
            Position.Z += shiftZ;

            UpdateTransMatrix();
        }

        // Scale → Rotate → Translate (applied right-to-left in matrix multiplication):
        public void UpdateTransMatrix()
        {
            Matrix4 scale = Matrix4.CreateScale(
                     (float)(Scale.x != 0 ? Scale.x : 1),
                     (float)(Scale.y != 0 ? Scale.y : 1),
                     (float)(Scale.z != 0 ? Scale.z : 1)
            );

            Matrix4 rotX = Matrix4.CreateRotationX((float)(Rotation.x * Math.PI / 180.0));
            Matrix4 rotY = Matrix4.CreateRotationY((float)(Rotation.y * Math.PI / 180.0));
            Matrix4 rotZ = Matrix4.CreateRotationZ((float)(Rotation.z * Math.PI / 180.0));

            Matrix4 transl = Matrix4.CreateTranslation((float)Position.X, (float)Position.Y, (float)Position.Z);

            // Combine: Scale → RotX → RotY → RotZ → Translate
            trans = scale * rotX * rotY * rotZ * transl;
        }

        private unsafe void updateBoundingBox()
        {
            Stopwatch sw = Stopwatch.StartNew();

            BoundingBox.Clear();

            if (Mesh.glVertices.Length == 0)
                return;

            ModelMatrix mtx = ModelObjectToolHelper.ToModelMatrix(trans);
            fixed (float* ptr = &Mesh.glVertices[0])
            {
                BoundingBox3 box3 = ModelObjectToolWrapper.Instance.Tool.GetBoundingBox(mtx, ptr, Mesh.glVertices.Length);
                BoundingBox.Add(box3.MaxX, box3.MaxY, box3.MaxZ);
                BoundingBox.Add(box3.MinX, box3.MinY, box3.MinZ);
            }

            Debug.WriteLine("[ThreeDModel.updateBoundingBox]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void UpdateBoundingBoxAndMatrix()
        {
            //Stopwatch sw = Stopwatch.StartNew();

            UpdateTransMatrix(); // Must update trans Matrix before updating Bounding Box.

            updateBoundingBox();

            //Debug.WriteLine("[ThreeDModel.UpdateBoundingBoxAndMatrix]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        // This function is used when moving the object for saving bounding box compuation.
        // NOTE NOTE NOTE: If the model is rotated, the bounding box can not be obtained just through trans matrix but compute all vertices in regular way.
        // Import Test Case: Rotate the model 40 degress -> Move the object to check if the bounding box is align correctly.
        void updateBoundingBoxByShift(double shiftX, double shiftY, double shiftZ)
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

            Mesh.EnsureCapacity(Model.triangles.Count, Model.HasColor());

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
                Mesh.AddTriangle(t.Vertices[0].pos.Subtract(Model.boundingBox.Center),
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

        public bool Selected
        {
            get { return selected; }
            set { selected = value; }
        }
        public Coord3D Position
        {
            get { return position; }
        }

        public RHVector3 Rotation
        {
            get { return rotation; }
        }

        public RHVector3 Scale
        {
            get { return scale; }
        }
    }
}

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
        public TopoModel Model;

        public Submesh Mesh;
        public ModelGLDraw Drawer;

        private RHBoundingBox bbox;
        private List<Vector3> convexVectorList;

        public string name = "Unknown";
        public bool outside = false;

        public Matrix4 trans;

        public int extruder = 0;
        public double maxScaleVector = 0;
        public double minScaleVector = 0;
        public double m = 0;
        public double b = 0;

        public PrintModel()
        {            
            Model = new TopoModel();
            Mesh = new Submesh();
            Drawer = new ModelGLDraw(this);
            bbox = new RHBoundingBox();

            convexVectorList = new List<Vector3>();
        }

        public virtual object cloneWithModel()
        {
            PrintModel stl = new PrintModel();
            stl.Model = Model.Copy();   // NOTE: Just clone Model is enough. Mesh/Drawer/bbox do not need to clone.
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
            bbox.Clear();
            convexVectorList.Clear();

            name = null;
            outside = false;

            extruder = 0;

            // It should dispose drawer in main thread. But, it causes black flash when deleting model. So, we just let GC to dispose it.
#if false
            MainWindow.main.threeDControl.InvokeGL(() =>
            {
                Drawer.Dispose();
            });
#endif
            GC.Collect();
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
            UpdateBoundingBoxAndMatrix();
            Position.z -= zMin;
            UpdateBoundingBoxAndMatrix();
        }

        public void LandToZ(float oriZmin)
        {
            if (Math.Abs(oriZmin - zMin) < 0.001) return;
            Position.z += oriZmin - zMin;

            UpdateBoundingBoxAndMatrix();
        }

        public void Center(float x, float y)
        {
            Land();
            RHVector3 center = bbox.Center;
            Position.x += x - (float)center.x;
            Position.y += y - (float)center.y;
        }

        public void CenterWOLand(float x, float y)
        {
            RHVector3 center = bbox.Center;
            Position.x += x - (float)center.x;
            Position.y += y - (float)center.y;
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

            bbox.Clear();
            convexVectorList.Clear();
            convexVectorList.EnsureCapacity(Model.triangles.Count * 3);
            foreach (var t in Model.triangles)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 vector3 = new Vector3(  (float)t.vertices[i].pos.x,
                                                    (float)t.vertices[i].pos.y,
                                                    (float)t.vertices[i].pos.z);
                    convexVectorList.Add(vector3);
                }
            }
            Vector3[] vec = convexVectorList.ToArray();

            if (vec.Length == 0)
                return;

            ModelMatrix mtx = ModelObjectToolHelper.ToModelMatrix(trans);
            fixed (float* ptr = &vec[0].X)
            {
                BoundingBox3 box3 = ModelObjectToolWrapper.Instance.Tool.GetBoundingBox(mtx, ptr, vec.Length);
                bbox.Add(box3.MaxX, box3.MaxY, box3.MaxZ);
                bbox.Add(box3.MinX, box3.MinY, box3.MinZ);
            }

            //Debug.WriteLine("[PrintModel.calcBoundingBox]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void UpdateBoundingBoxAndMatrix()
        {
            updateMatrix(); // Must update trans Matrix before updating Bounding Box.

            updateBoundingBox();
            xMin = (float)bbox.xMin;
            xMax = (float)bbox.xMax;
            yMin = (float)bbox.yMin;
            yMax = (float)bbox.yMax;
            zMin = (float)bbox.zMin;
            zMax = (float)bbox.zMax;
        }

        // This function only works for the model without scaling and rotation.
        // If the model just moves without scaling and rotation, this functon is useful for updating bounding box very quickly.
        public void UpdateBoundingBoxByMatrix()
        {
            updateMatrix(); // Must update trans Matrix before updating Bounding Box.

#if false   // TopoModel boundingBox is original bounding box of the model without rotation.
            float xMinCenter = (float)(Model.boundingBox.xMin - Model.boundingBox.Center.x);
            float yMinCenter = (float)(Model.boundingBox.yMin - Model.boundingBox.Center.y);
            float zMinCenter = (float)(Model.boundingBox.zMin - Model.boundingBox.Center.z);

            float xMaxCenter = (float)(Model.boundingBox.xMax - Model.boundingBox.Center.x);
            float yMaxCenter = (float)(Model.boundingBox.yMax - Model.boundingBox.Center.y);
            float zMaxCenter = (float)(Model.boundingBox.zMax - Model.boundingBox.Center.z);
#else

            float xMinCenter = (float)(bbox.xMin - bbox.Center.x);
            float yMinCenter = (float)(bbox.yMin - bbox.Center.y);
            float zMinCenter = (float)(bbox.zMin - bbox.Center.z);

            float xMaxCenter = (float)(bbox.xMax - bbox.Center.x);
            float yMaxCenter = (float)(bbox.yMax - bbox.Center.y);
            float zMaxCenter = (float)(bbox.zMax - bbox.Center.z);
#endif

            Vector4 minVertex = new Vector4(xMinCenter, yMinCenter, zMinCenter, 1.0f);
            Vector4 maxVertex = new Vector4(xMaxCenter, yMaxCenter, zMaxCenter, 1.0f);

            Matrix4 scale = Matrix4.CreateScale(
                     Scale.x != 0 ? Scale.x : 1,
                     Scale.y != 0 ? Scale.y : 1,
                     Scale.z != 0 ? Scale.z : 1
            );
            Matrix4 transl = Matrix4.CreateTranslation(Position.x, Position.y, Position.z);

            Vector4 newMinVertex = minVertex * scale * transl;
            Vector4 newMaxVertex = maxVertex * scale * transl;

            bbox.minPoint.x = newMinVertex.X;
            bbox.minPoint.y = newMinVertex.Y;
            bbox.minPoint.z = newMinVertex.Z;

            bbox.maxPoint.x = newMaxVertex.X;
            bbox.maxPoint.y = newMaxVertex.Y;
            bbox.maxPoint.z = newMaxVertex.Z;

            xMin = (float)bbox.xMin;
            xMax = (float)bbox.xMax;
            yMin = (float)bbox.yMin;
            yMax = (float)bbox.yMax;
            zMin = (float)bbox.zMin;
            zMax = (float)bbox.zMax;
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


        public override void Paint()
        {
            //Stopwatch sw = Stopwatch.StartNew();

            Mesh.Clear();

            Model.FillMeshCheckRAM(Mesh, outside ? Submesh.MESHCOLOR_OUTSIDE : Submesh.MESHCOLOR_FRONTBACK);

            Mesh.selected = Selected;
            Mesh.extruder = extruder;
            Mesh.Compress();

            //Debug.WriteLine("[PrintModel.Paint]==> Elapsed Time: " + sw.ElapsedMilliseconds.ToString());
        }

        public void CopyTopoModelBoundingBoxToPrintModel()
        {
            bbox.Add(Model.boundingBox);
            xMin = (float)bbox.xMin;
            xMax = (float)bbox.xMax;
            yMin = (float)bbox.yMin;
            yMax = (float)bbox.yMax;
            zMin = (float)bbox.zMin;
            zMax = (float)bbox.zMax;
        }
        /// <summary>
        /// Get bounding box of model with support
        /// </summary>
        /// <returns></returns>
        public RHBoundingBox BoundingBox
        {
            get
            {
                RHBoundingBox return_bbox = new RHBoundingBox();
                return_bbox.Add(xMin, yMin, zMin);
                return_bbox.Add(xMax, yMax, zMax);

                return return_bbox;
            }

            protected set
            {
                bbox.Clear();
                bbox.Add(value);
            }
        }

        /// <summary>
        /// Get bounding box of model without support
        /// </summary>
        /// <returns></returns>
        public RHBoundingBox BoundingBoxWOSupport
        {
            get
            {
                // not copy data, may be modified by reference
                return bbox;
            }
        }
    }
}

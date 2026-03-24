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
        public Submesh submesh;
        public ModelGLDraw Drawer;
        public List<Vector3> convexVectorList;

        public string name = "Unknown";
        public bool outside = false;
        public int mid = 0;     // model id
        public int serNum = 0;

        public Matrix4 trans, invTrans;

        protected RHBoundingBox bbox = new RHBoundingBox();
        public int extruder = 0;
        public double maxScaleVector = 0;
        public double minScaleVector = 0;
        public double m = 0;
        public double b = 0;

        public RHVector3[] vtxPosWorldCor;
        public RHVector3[] triNormalWorldCor;

        public PrintModel()
        {
            submesh = new Submesh();
            Model = new TopoModel();
            Drawer = new ModelGLDraw(this);
            convexVectorList = new List<Vector3>();
        }

        /// <summary>
        /// Transfer triangle vertex coordinate to world coordinate. Before transferring, the origin is at the center of model.
        /// </summary>
        /// <param name="triIdx">index of triangle</param>
        /// <returns>world coordinate of triangle vertex</returns>
        public TopoTriangle getTriWorByMesh(int triIdx)
        {
            if (triIdx > (submesh.triangles.Count - 1)) return null;

            TopoVertex[] verWorArr = new TopoVertex[3]; // variable for world coordinate

            // triangles紀錄XYZ的值(vertex1~3)
            int n1 = 3 * submesh.triangles[triIdx].vertex1;
            int n2 = 3 * submesh.triangles[triIdx].vertex2;
            int n3 = 3 * submesh.triangles[triIdx].vertex3;

            // glVettices紀錄個別X/Y/Z座標的值
            verWorArr[0] = new TopoVertex(0, new RHVector3(submesh.glVertices[n1], submesh.glVertices[n1 + 1], submesh.glVertices[n1 + 2]));
            verWorArr[1] = new TopoVertex(1, new RHVector3(submesh.glVertices[n2], submesh.glVertices[n2 + 1], submesh.glVertices[n2 + 2]));
            verWorArr[2] = new TopoVertex(2, new RHVector3(submesh.glVertices[n3], submesh.glVertices[n3 + 1], submesh.glVertices[n3 + 2]));

            for (int i = 0; i < verWorArr.Count(); i++)
            {
                RHVector3 verWor = new RHVector3(0, 0, 0);
                TransformPoint(verWorArr[i].pos, verWor);       // 轉為實際座標,轉換前以中心點(64,64)當作原點(0,0)
                verWorArr[i] = new TopoVertex(i, verWor);       // 此時verWorArr放的是實際座標
            }
            return new TopoTriangle(verWorArr[0], verWorArr[1], verWorArr[2]);
        }
        public void ResetVertexPosToBBox()
        {
            foreach (TopoVertex v in Model.vertices.v)
            {
                v.pos.x -= Model.boundingBox.Center.x;
                v.pos.y -= Model.boundingBox.Center.y;
                v.pos.z -= Model.boundingBox.Center.z;
            }
        }
        public virtual object cloneWithModel()
        {
            PrintModel stl = new PrintModel();
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
            stl.curPos = curPos;
            stl.preRX = preRX;
            stl.preRX2 = preRX2;
            stl.preRY = preRY;
            stl.preRY2 = preRY2;
            stl.preRZ = preRZ;
            stl.preRZ2 = preRZ2;
            stl.invTrans = invTrans;
            stl.trans = trans;
            stl.Selected = false;

            // NOTE: Don't need to clone Drawer.
            stl.Model = Model.Copy();
            return stl;
        }

        public virtual void Clear()
        {
            name = null;
            outside = false;
            if (null != submesh)
                submesh.Clear();
            submesh = null;
            bbox.Clear();
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

        public override Vector3 getCenter()
        {
            return bbox.Center.asVector3();
        }

        private double mxDist(Matrix4 mx1, Matrix4 mx2)
        {
            return Vector4.Subtract(mx1.Row0, mx2.Row0).LengthSquared +
                    Vector4.Subtract(mx1.Row1, mx2.Row1).LengthSquared +
                    Vector4.Subtract(mx1.Row2, mx2.Row2).LengthSquared +
                    Vector4.Subtract(mx1.Row3, mx2.Row3).LengthSquared;
        }

        private void UpdateMatrix()
        {
            Matrix4 oldTrans = trans;

            float x = Rotation.x;
            float y = Rotation.y;
            float z = Rotation.z;
            x -= preRX2;
            y -= preRY2;
            z -= preRZ2;

            Matrix4 identity = Matrix4.Identity;
            Matrix4 transl = Matrix4.CreateTranslation(Position.x, Position.y, Position.z);
            Matrix4 scale = Matrix4.CreateScale(Scale.x != 0 ? Scale.x : 1, Scale.y != 0 ? Scale.y : 1, Scale.z != 0 ? Scale.z : 1);

            Matrix4 rotx = Matrix4.CreateRotationX(x * (float)Math.PI / 180.0f);
            trans = Matrix4.Mult(identity, rotx);
            Matrix4 roty = Matrix4.CreateRotationY(y * (float)Math.PI / 180.0f);
            trans = Matrix4.Mult(trans, roty);
            Matrix4 rotz = Matrix4.CreateRotationZ(z * (float)Math.PI / 180.0f);
            trans = Matrix4.Mult(trans, rotz);

            preRX2 = Rotation.x;
            preRY2 = Rotation.y;
            preRZ2 = Rotation.z;

            if (reset == false)
                curPos = Matrix4.Mult(trans, curPos);
            else
                curPos = Matrix4.Identity;

            Matrix4 cT = curPos;
            cT.Transpose();
            trans = Matrix4.Mult(scale, cT);
            trans = Matrix4.Mult(trans, transl);
            invTrans = trans;
            invTrans.Invert();
        }

        public unsafe void CalcBoundingBox()
        {
            ConvexVector();
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
        }

        private void ConvexVector()
        {
            convexVectorList.Clear();
            for (int i = 0; i < Model.vertices.Count; i++)
            {
                Vector3 vector3 = new Vector3((float)Model.vertices.v[i].pos.x,
                                            (float)Model.vertices.v[i].pos.y,
                                            (float)Model.vertices.v[i].pos.z);
                convexVectorList.Add(vector3);
            }
        }

        public void UpdateBoundingBoxAndMatrix()
        {
            UpdateMatrix();
            bbox.Clear();

            CalcBoundingBox();

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

        public void ReverseTransformPoint(RHVector3 v, RHVector3 outv)
        {
            Vector4 v4 = v.asVector4();
            outv.x = Vector4.Dot(invTrans.Column0, v4);
            outv.y = Vector4.Dot(invTrans.Column1, v4);
            outv.z = Vector4.Dot(invTrans.Column2, v4);
        }

        public override void Paint()
        {
            submesh.Clear();

            Model.FillMeshCheckRAM(this.trans, submesh, outside ? Submesh.MESHCOLOR_OUTSIDE : Submesh.MESHCOLOR_FRONTBACK);

            submesh.selected = Selected;
            submesh.extruder = extruder;
            submesh.Compress();
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

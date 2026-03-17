using OpenTK.Mathematics;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using View3D.model.geom;
using View3D.ModelObjectTool;

namespace View3D.model
{
    public delegate void PrintModelChangedEvent(PrintModel model);
    public delegate LinkedList<PrintModel> ListviewGetModelsDelegate(bool selected);
    public partial class PrintModel : ThreeDModel
    {
        public int modelDataId = 0; // model data id
        public TopoModel originalModel = new TopoModel();
        public TopoModel repairedModel = null;
        public int activeModel = 0;
        public string name = "Unknown";
        public bool outside = false, oldOutside = false;
        public bool modifiedM = false;
        public bool modifiedR = false;
        public bool modifiedS = false;

        public Matrix4 trans, invTrans;
        public Submesh submesh;
        public int activeSubmesh = -1;
        protected RHBoundingBox bbox = new RHBoundingBox();
        public event PrintModelChangedEvent printModelChangedEvent = null;
        public ListviewGetModelsDelegate ListviewGetModels = null;
        public int extruder = 0;
        public double maxScaleVector = 0;
        public double minScaleVector = 0;
        public double m = 0;
        public double b = 0;
        public int mid = 0; // model id
        public int serNum = 0;

        public IDraw Drawer { get { return drawer; } }
        protected IDraw drawer;

        public RHVector3[] vtxPosWorldCor;
        public RHVector3[] triNormalWorldCor;
        private bool dirtySpaceInfo = true; // flag for need to update space structure information
        public float storedLayerValue = 999f;
        public float storedMaxLayer = 0;
        public float storedMinLayer = 0;
        public bool storedLayerChanged = false;
        public RHVector3 OriginalBboxMax;
        public RHVector3 OriginalBboxMin;

        public List<Vector3> convexVectorList;

        public PrintModel()
        {
            this.submesh = new Submesh();
        }

        public PrintModel(IDraw newDrawer)
        {
            this.drawer = newDrawer;
            this.submesh = new Submesh();
            this.submesh.SetDrawer(newDrawer);
        }

        public PrintModel(IDraw newDrawer, TopoModel model)
        {
            this.originalModel = null;
            this.originalModel = model;
            this.drawer = newDrawer;
            this.submesh = new Submesh();
            this.submesh.SetDrawer(newDrawer);

            this.convexVectorList = new List<Vector3>();
        }

        public virtual void SetDrawer(IDraw newDrawer)
        {
            this.drawer = newDrawer;
            this.submesh.SetDrawer(newDrawer);
        }

        /// <summary>
        /// Calculate world coordinate of all vertexes
        /// </summary>
        public void calVtxWorldCoordinate()
        {
            int vId;
            TopoTriangle triWor;
            int totalTriangle;
            int processing_step;

            if (null != vtxPosWorldCor)
                Array.Clear(vtxPosWorldCor, 0, vtxPosWorldCor.Length);
            if (null != triNormalWorldCor)
                Array.Clear(triNormalWorldCor, 0, triNormalWorldCor.Length);

            vtxPosWorldCor = new RHVector3[originalModel.vertices.Count];
            triNormalWorldCor = new RHVector3[submesh.triangles.Count];
            totalTriangle = submesh.triangles.Count;
            processing_step = (totalTriangle >= 32) ? (totalTriangle / 32) : 1;
            for (int triIdx = 0; triIdx < totalTriangle; triIdx++)    // 模型總三角網格數
            {
                triWor = getTriWorByMesh(triIdx);
                triNormalWorldCor[triIdx] = triWor.normal;

                for (int i = 0; i < triWor.vertices.Count(); i++)
                {
                    // store vertex position by vertex id
                    vId = originalModel.triangles.triangles[triIdx].vertices[i].id;
                    vtxPosWorldCor[vId] = triWor.vertices[i].pos;
                }

                if (triIdx % processing_step == 0)
                {
                    //MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = ((double)triIdx / totalTriangle) * 15.0;
                    //Application.DoEvents();
                    int percentage = (int)(((double)triIdx / totalTriangle) * 15.0);
                }
            }
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

        public TopoModel Model
        {
            get
            {
                return (activeModel == 0 ? originalModel : repairedModel);
            }
        }

        public TopoModel ActiveModel
        {
            get
            {
                if (activeModel == 0 || repairedModel == null) return originalModel;
                return repairedModel;
            }
        }

        public void Reset()
        {
            repairedModel = originalModel.Copy();
            //repairedModel.Analyse();
            //ShowRepaired(true);
        }

        public void ResetWoCopy()
        {
            repairedModel = originalModel;
        }

        public void ResetVertexPosToBBox()
        {
            foreach (TopoVertex v in originalModel.vertices.v)
            {
                v.pos.x -= originalModel.boundingBox.Center.x;
                v.pos.y -= originalModel.boundingBox.Center.y;
                v.pos.z -= originalModel.boundingBox.Center.z;
            }
        }

        public void ResetVertexPosToBBoxZmin()
        {
            foreach (TopoVertex v in repairedModel.vertices.v)
            {
                v.pos.x -= originalModel.boundingBox.Center.x;
                v.pos.y -= originalModel.boundingBox.Center.y;
                //v.pos.z -= originalModel.boundingBox.Center.z;
                v.pos.z -= originalModel.boundingBox.zMin;
            }
        }

        public bool FixNormals()
        {
            if (repairedModel == null)
                repairedModel = originalModel.Copy();
            repairedModel.UpdateNormals();
            bool isValid = repairedModel.Analyse();
            //repairedModel.updateBad();
            ShowRepaired(true);
            return isValid;
        }

        public void ShowRepaired(bool showRepaired)
        {
            if (showRepaired)
            {
                activeModel = 1;
            }
            else
            {
                activeModel = 0;
            }
            ForceViewRegeneration();
            //MainWindow.main.threedview.Refresh();
            if (printModelChangedEvent != null)
                printModelChangedEvent(this);
        }

        public void RunTest()
        {
        }

        public PrintModel copyPrintModel()
        {
            PrintModel stl = new PrintModel(this.drawer);
            //stl.filename = filename;
            stl.name = name;
            stl.Position.x = Position.x;
            stl.Position.y = Position.y + 5 + yMax - yMin;
            stl.Position.z = Position.z;
            stl.Scale.x = Scale.x;
            stl.Scale.y = Scale.y;
            stl.Scale.z = Scale.z;
            stl.Rotation.x = Rotation.x;
            stl.Rotation.y = Rotation.y;
            stl.Rotation.z = Rotation.z;
            stl.Selected = false;
            stl.activeModel = activeModel;
            stl.originalModel = originalModel.Copy();
            if (repairedModel != null)
                stl.repairedModel = repairedModel.Copy();
            else
                stl.repairedModel = null;
            stl.UpdateBoundingBox();
            stl.ListviewGetModels += ListviewGetModels;
            return stl;
        }

        public virtual object cloneWithModel()//(int idx)
        {
            PrintModel stl = new PrintModel(this.drawer, this.originalModel);
            //stl.filename = filename;
            stl.name = name;// + " (" + idx + ")";
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
            stl.activeModel = activeModel;
            stl.ListviewGetModels += ListviewGetModels;
            return stl;
        }

        public virtual void Clear()
        {
            activeModel = 0;
            name = null;
            outside = false;
            oldOutside = false;
            if (null != submesh)
                submesh.Clear();
            submesh = null;
            activeSubmesh = -1;
            bbox = null;
            printModelChangedEvent = null;
            extruder = 0;
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
            UpdateBoundingBox();
            Position.z -= zMin;
        }

        public void LandToZ(float oriZmin)
        {
            if (Math.Abs(oriZmin - zMin) < 0.001) return;
            Position.z += oriZmin - zMin;
            //Console.WriteLine("zMin="+zMin);
            UpdateBoundingBox();
        }

        public void LandUpdateBB()
        {
            UpdateBoundingBox();
            Position.z -= zMin;
            UpdateBoundingBox();
        }

        public void LandUpdateBBNoPreUpdate()
        {
            Position.z -= zMin;
            UpdateBoundingBox();
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

        public void Center2(float x, float y)
        {
            double centerX = 0.0, centerY = 0.0;
            int objectCount = 0;
            LinkedList<PrintModel> models = null;
            if (ListviewGetModels != null)
                models = ListviewGetModels(false);
            Debug.Assert(models != null);

            foreach (PrintModel stl in models)
            {
                objectCount++;
                centerX += stl.originalModel.boundingBox.Center.x;
                centerY += stl.originalModel.boundingBox.Center.y;
            }

            centerX = centerX / objectCount;
            centerY = centerY / objectCount;
            UpdateBoundingBox();
            if (ListviewGetModels != null)
                models = ListviewGetModels(false);
            Debug.Assert(models != null);

            foreach (PrintModel stl in models)
            {
                stl.Position.x = x - (float)centerX;
                stl.Position.y = y - (float)centerY;
            }
            UpdateBoundingBox();
        }

        public void Center3(float x, float y, PrintModel stl)
        {
            double centerX = 0.0, centerY = 0.0;
            int objectCount = 0;
            LinkedList<PrintModel> models = null;
            if (ListviewGetModels != null)
                models = ListviewGetModels(false);
            Debug.Assert(models != null);
            foreach (PrintModel stl1 in models)
            {
                objectCount++;
                centerX += stl1.originalModel.boundingBox.Center.x;
                centerY += stl1.originalModel.boundingBox.Center.y;
            }

            centerX = centerX / objectCount;
            centerY = centerY / objectCount;

            UpdateBoundingBox();

            stl.Position.x = x - (float)centerX;
            stl.Position.y = y - (float)centerY;
            UpdateBoundingBox();
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
            for (int i = 0; i < ActiveModel.vertices.Count; i++)
            {
                Vector3 vector3 = new Vector3((float)ActiveModel.vertices.v[i].pos.x,
                                            (float)ActiveModel.vertices.v[i].pos.y,
                                            (float)ActiveModel.vertices.v[i].pos.z);
                convexVectorList.Add(vector3);
            }
        }

        public void UpdateBoundingBox()
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

        public void ForceViewRegeneration()
        {
            ForceRefresh = true;
        }

        bool lastShowEdges = false;
        int lastRendered = -1; // 0 = all , 1 = mesh
        bool lastSelected = false;
        public bool ForceRefresh = false;
        public bool ForceRefreshOneTime = false;

        public override void Paint()
        {
            TopoModel model = ActiveModel;

            RHVector3 cutPos;
            RHVector3 cutDir;
            bool cutFaceEnabled;
            bool showEdges;
            bool cutFaceUpdated;
            if (drawer != null)
            {
                cutPos = drawer.GetCutPosition();
                cutDir = drawer.GetCutDirection();
                cutFaceEnabled = drawer.IsCutFaceEnabled();
                showEdges = drawer.IsEdgeShowEnabled();
                cutFaceUpdated = drawer.IsCutFaceUpdated();
            }
            else
            {
                cutPos = new RHVector3(0, 0, 0);
                cutDir = new RHVector3(0, 0, 0);
                cutFaceEnabled = false;
                showEdges = false;
                cutFaceUpdated = false;
            }
            if (cutFaceEnabled)
            {
                if (ForceRefresh || cutFaceUpdated || lastRendered != 1 || activeModel != activeSubmesh || lastShowEdges != showEdges || lastSelected != Selected)
                {
                    RHVector3 normpoint = cutPos.Add(cutDir);
                    RHVector3 point = new RHVector3(0, 0, 0);
                    ReverseTransformPoint(cutPos, point);
                    ReverseTransformPoint(normpoint, normpoint);
                    RHVector3 normal = normpoint.Subtract(point);

                    submesh.Clear();
                    model.CutMesh(submesh, normal, point, outside ? Submesh.MESHCOLOR_OUTSIDE : Submesh.MESHCOLOR_FRONTBACK);
                    submesh.selected = Selected;
                    submesh.extruder = extruder;
                    submesh.Compress();
                    lastShowEdges = showEdges;
                    lastSelected = Selected;
                    activeSubmesh = activeModel;
                    lastRendered = 1;
                }
            }
            else
            {
                if (ForceRefresh || ForceRefreshOneTime || cutFaceUpdated || lastRendered != 0 || activeModel != activeSubmesh || lastShowEdges != showEdges)
                {
                    submesh.Clear();

                    model.FillMeshCheckRAM(this.trans, submesh, outside ? Submesh.MESHCOLOR_OUTSIDE : Submesh.MESHCOLOR_FRONTBACK);

                    submesh.selected = Selected;
                    submesh.extruder = extruder;
                    submesh.Compress();
                    lastShowEdges = showEdges;
                    lastSelected = Selected;
                    activeSubmesh = activeModel;
                    lastRendered = 0;

                    if (ForceRefreshOneTime)
                        ForceRefreshOneTime = false;
                }
            }

            Vector3 translateVec;
            if (drawer != null)
                translateVec = drawer.GetTranslateVector();
            else
                translateVec = new Vector3(0, 0, 0);

            submesh.Draw(submesh, 2, translateVec);

            ForceRefresh = false;
        }

        /// <summary>
        /// Get bounding box of model with support
        /// </summary>
        /// <returns></returns>
        public RHBoundingBox BoundingBox
        {
            get
            {
                RHBoundingBox bbox = new RHBoundingBox();
                bbox.Add(xMin, yMin, zMin);
                bbox.Add(xMax, yMax, zMax);

                return bbox;
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

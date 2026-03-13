using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using View3D.model;
using View3D.model.geom;

namespace View3D.MeshInOut
{
    public class StlSetting : Setting
    {
        public bool Binary { get; set; }
        public bool RepairModel { get; set; }

        public StlSetting()
        {
            Format = FormatCode.Stl;
        }
    }

    public partial class MeshIOStl : MeshIOBase
    {
        public enum FileType { Unknown, Binary, UTF8 };

        public override int Load(string filename, TopoModel model, Action<int> updateRate)
        {
            importSTL(filename, model, updateRate);
            return 0;
        }

        public override int LoadWOCatch(string filename, TopoModel model, Action<int> updateRate)
        {
            importSTLWOCatch(filename, model, updateRate);
            return 0;
        }

        public int LoadByteWOCatch(byte[] STLByte, TopoModel model, Action<int> updateRate)
        {
            importByteArray(ref STLByte, model, updateRate);
            return 0;
        }

        public override void Save(string filename, TopoModel model, IMeshOutSetting outSetting, Action<int> updateRate)
        {
            exportSTL(filename, model, updateRate, (outSetting as StlSetting).Binary, (outSetting as StlSetting).RepairModel);
        }

        public override void Save(FileStream fs, TopoModel model, IMeshOutSetting outSetting, Action<int> updateRate)
        {
            exportSTL(fs, model, updateRate, (outSetting as StlSetting).Binary);
        }

        /// <summary>
        /// export all objects to a STL file
        /// </summary>
        /// <param name="filename">output filename</param>
        /// <param name="binary">output binary format</param>
        /// <param name="DomodelRepair">reduce the number of facet of object</param>
        public void exportSTL(string filename, TopoModel model, Action<int> updateRate, bool binary, bool DomodelRepair)
        {
            FileStream fs = File.Open(filename, FileMode.Create);

            Status = STATUS.Busy;

            if (binary)
            {
                exportSTLBinary(fs, model, updateRate);
            }
            else
            {
                exportSTLAscii(fs, model, updateRate);
            }
            fs.Close();

            if (DomodelRepair)
            {
                try
                {
                }
                catch { }
            }

            if (Status == STATUS.Busy)
                Status = STATUS.Done;

        }

        protected void exportSTL(FileStream fs, TopoModel model, Action<int> updateRate, bool binary)
        {
            Status = STATUS.Busy;

            if (binary)
            {
                exportSTLBinary(fs, model, updateRate);
            }
            else
            {
                exportSTLAscii(fs, model, updateRate);
            }

            if (Status == STATUS.Busy)
                Status = STATUS.Done;

        }

        private void exportSTLBinary(FileStream fs, TopoModel model, Action<int> updateRate)
        {
            int count = 0;
            BinaryWriter w = new BinaryWriter(fs);
            int i;
            for (i = 0; i < 20; i++) w.Write((int)0);
            w.Write(model.triangles.Count);
            foreach (TopoTriangle t in model.triangles)
            {
                w.Write((float)t.normal.x);
                w.Write((float)t.normal.y);
                w.Write((float)t.normal.z);
                for (i = 0; i < 3; i++)
                {
                    w.Write((float)t.vertices[i].pos.x);
                    w.Write((float)t.vertices[i].pos.y);
                    w.Write((float)t.vertices[i].pos.z);
                }
                w.Write((short)0);

                count++;
                if (count % 5000 == 0)
                {
                    //TODO: check 2 stage loading
                    if (updateRate != null)
                        updateRate( (int)(((double)count / model.triangles.Count) * 100.0) );
                    ////if (MainWindow.main.threedview.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                    ////    MainWindow.main.threedview.ui.BusyWindow.increment != 0.0)
                    ////{
                    ////    MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value =
                    ////            ((double)count / model.triangles.Count) * (100.0 - MainWindow.main.threedview.ui.BusyWindow.firstStagePercent) + MainWindow.main.threedview.ui.BusyWindow.firstStagePercent;
                    ////}
                    ////Application.DoEvents();
                    //Fix_save_issue_160829
                    if (Command == COMMAND.Abort)
                    {
                        Command = COMMAND.None;
                        Status = STATUS.UserAbort;
                        return;
                    }
                }
            }
            //w.Close();
            w.Flush(); //below .Net 4.5, not close stream for write support point
        }

        private void exportSTLAscii(FileStream fs, TopoModel model, Action<int> updateRate)
        {
            int count = 0;
            TextWriter w = new EnglishStreamWriter(fs);
            w.WriteLine("solid XYZ");
            foreach (TopoTriangle t in model.triangles)
            {
                w.Write("  facet normal ");
                w.Write(t.normal.x);
                w.Write(" ");
                w.Write(t.normal.y);
                w.Write(" ");
                w.WriteLine(t.normal.z);
                w.WriteLine("    outer loop");
                w.Write("      vertex ");
                w.Write(t.vertices[0].pos.x);
                w.Write(" ");
                w.Write(t.vertices[0].pos.y);
                w.Write(" ");
                w.WriteLine(t.vertices[0].pos.z);
                w.Write("      vertex ");
                w.Write(t.vertices[1].pos.x);
                w.Write(" ");
                w.Write(t.vertices[1].pos.y);
                w.Write(" ");
                w.WriteLine(t.vertices[1].pos.z);
                w.Write("      vertex ");
                w.Write(t.vertices[2].pos.x);
                w.Write(" ");
                w.Write(t.vertices[2].pos.y);
                w.Write(" ");
                w.WriteLine(t.vertices[2].pos.z);
                w.WriteLine("    endloop");
                w.WriteLine("  endfacet");

                count++;
                if (count % 5000 == 0)
                {
                    //TODO: check 2 stage loading
                    ////if (MainWindow.main.threedview.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                    ////    MainWindow.main.threedview.ui.BusyWindow.increment != 0.0)
                    ////{
                    ////    MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value =
                    ////    ((double)count / model.triangles.Count) * (100.0 - MainWindow.main.threedview.ui.BusyWindow.firstStagePercent) + MainWindow.main.threedview.ui.BusyWindow.firstStagePercent;
                    ////    Application.DoEvents();
                    ////}
                    if (updateRate != null)
                        updateRate( (int)(((double)count / model.triangles.Count) * 100.0) );
                    ////if (model.IsActionStopped()) return;
                    ////if (MainWindow.main.threedview.ui.BusyWindow.killed) return;
                    if (Command == COMMAND.Abort)
                    {
                        Command = COMMAND.None;
                        Status = STATUS.UserAbort;
                        return;
                    }
                }
            }
            w.WriteLine("endsolid XYZware_Nobel");
            //w.Close();
            w.Flush(); //below .Net 4.5, not close stream for write support point
        }

        private RHVector3 extractVector(string s)
        {
            RHVector3 v = new RHVector3(0,0,0);
            s = s.Trim().Replace("\t", " ");
            int p = s.IndexOf(' ');
            if (p < 0) throw new Exception("Format error");
#if PRECISION_SINGLE
            float.TryParse(s.Substring(0, p), NumberStyles.Float, NumFormat, out v.x);
#else
            double.TryParse(s.Substring(0, p), NumberStyles.Float, NumFormat, out v.x);
#endif
            s = s.Substring(p).Trim();
            p = s.IndexOf(' ');
            if (p < 0) throw new Exception("Format error");
#if PRECISION_SINGLE
            float.TryParse(s.Substring(0, p), NumberStyles.Float, NumFormat, out v.y);
#else
            double.TryParse(s.Substring(0, p), NumberStyles.Float, NumFormat, out v.y);
#endif
            s = s.Substring(p).Trim();
#if PRECISION_SINGLE
            float.TryParse(s, NumberStyles.Float, NumFormat, out v.z);
#else
            double.TryParse(s, NumberStyles.Float, NumFormat, out v.z);
#endif
            return v;
        }

        private void ReadArray(Stream stream, byte[] data)
        {
            int offset = 0;
            int remaining = data.Length;
            try
            {
                while (remaining > 0)
                {
                    int read = stream.Read(data, offset, remaining);
                    if (read <= 0)
                        throw new EndOfStreamException
                            (String.Format("End of stream reached with {0} bytes left to read", remaining));
                    remaining -= read;
                    offset += read;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// import text STL file
        /// </summary>
        /// <param name="filename">input filename</param>
        /// <remarks>
        ///     ASCII STL
        ///     ====================
        ///     solid name
        ///     facet normal ni nj nk
        ///         outer loop
        ///             vertex v1x v1y v1z
        ///             vertex v2x v2y v2z
        ///             vertex v3x v3y v3z
        ///         endloop
        ///     endfacet
        ///     endsolid name
        /// </remarks>
        //TODO: check public or private
        protected void importSTLAscii(string filename, TopoModel model, Action<int> updateRate)
        {
            string text = System.IO.File.ReadAllText(filename);
            int lastP = 0, p, pend, normal, outer, vertex, vertex2;
            int count = 0,max = text.Length;

            while ((p = text.IndexOf("facet", lastP)) > 0)
            {
                count++;

                if (count % 4000 == 0)
                {
                    ////MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = ((double)lastP / max) * 100.0;
                    ////Application.DoEvents();
                    if (updateRate != null)
                        updateRate((int)(((double)lastP / max) * 100.0));
                    ////if (IsActionStopped()) return;
                    ////if (MainWindow.main.threedview.ui.BusyWindow.killed) return;
                    if (Command == COMMAND.Abort)
                    {
                        Command = COMMAND.None;
                        Status = STATUS.UserAbort;
                        return;
                    }
                }

                pend = text.IndexOf("endfacet", p + 5);
                normal = text.IndexOf("normal", p) + 6;
                outer = text.IndexOf("outer loop", normal);
                RHVector3 normalVect = extractVector(text.Substring(normal, outer - normal));
                normalVect.NormalizeSafe();
                outer += 10;
                vertex = text.IndexOf("vertex", outer) + 6;
                vertex2 = text.IndexOf("vertex", vertex);
                RHVector3 p1 = extractVector(text.Substring(vertex, vertex2 - vertex));
                vertex2 += 7;
                vertex = text.IndexOf("vertex", vertex2);
                RHVector3 p2 = extractVector(text.Substring(vertex2, vertex - vertex2));
                vertex += 7;
                vertex2 = text.IndexOf("endloop", vertex);
                RHVector3 p3 = extractVector(text.Substring(vertex, vertex2 - vertex));
                lastP = pend + 8;
                model.addTriangle(p1, p2, p3, normalVect);
            }
        }

        /// <summary>
        /// import binary STL file
        /// </summary>
        /// <param name="filename">input filename</param>
        /// <remarks>
        ///     ASCII STL
        ///     ====================
        ///     solid name
        ///     facet normal ni nj nk
        ///         outer loop
        ///             vertex v1x v1y v1z
        ///             vertex v2x v2y v2z
        ///             vertex v3x v3y v3z
        ///         endloop
        ///     endfacet
        ///     endsolid name
        ///     
        /// 
        ///     Binary STL format:
        ///     ====================
        ///     80 byte header
        ///     4 byte triangle count
        ///     For each triangle:
        ///     normal(3 floats)
        ///     vertex1(3 floats)
        ///     vertex2(3 floats)
        ///     vertex3(3 floats)
        ///     attribute byte count(2 bytes)
        /// </remarks>
        protected void importSTLWOCatch(string filename, TopoModel model, Action<int> updateRate)
        {
            Status = STATUS.Busy;

            ////model.StartAction("L_LOADING...");
            model.Clear();
            FileStream f = null;
            BinaryReader r = null;

            try
            {
                f = File.OpenRead(filename);
                byte[] header = new byte[80];
                ReadArray(f, header);
                r = new BinaryReader(f);
                int nTri = r.ReadInt32();
                if (f.Length != 84 + nTri * 50)
                {
                    r.Close();
                    f.Close();
                    importSTLAscii(filename, model, updateRate);
                }
                else
                {
                    for (int i = 0; i < nTri; i++)
                    {
                        if (i > 0 && i % 4000 == 0)
                        {
                            if (updateRate != null)
                                updateRate((int)(((double)i / nTri) * 100.0));

                            if (Command == COMMAND.Abort)
                            {
                                Command = COMMAND.None;
                                Status = STATUS.UserAbort;
                                return;
                            }

                            if (!Utils.RamTools.IsRamSizeValid())
                            {
                                throw new System.OutOfMemoryException();
                            }
                        }

                        RHVector3 normal = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        RHVector3 p1 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        RHVector3 p2 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        RHVector3 p3 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        
                        TopoVertex v1 = new TopoVertex(0, p1);
                        TopoVertex v2 = new TopoVertex(1, p2);
                        TopoVertex v3 = new TopoVertex(2, p3);


                        RHVector3 d1 = v2.pos.Subtract(v1.pos);
                        RHVector3 d2 = v3.pos.Subtract(v2.pos);
                        RHVector3 normal2 = d1.CrossProduct(d2);
                        normal2.NormalizeSafe();

                        model.addTriangle(p1, p2, p3, normal2);
                        r.ReadUInt16();
                    }
                }
            } // let the upper methods catch the exception
            finally
            {
                if (r != null)
                    r.Close();

                if (f != null)
                    f.Close();
            }

            if (Status == STATUS.Busy)
                Status = STATUS.Done;
        }

        protected void importSTL(string filename, TopoModel model, Action<int> updateRate)
        {
            Status = STATUS.Busy;

            ////model.StartAction("L_LOADING...");
            model.Clear();

            try
            {
                FileStream f = File.OpenRead(filename);
                byte[] header = new byte[80];
                ReadArray(f, header);
                BinaryReader r = new BinaryReader(f);
                int nTri = r.ReadInt32();
                if (f.Length != 84 + nTri * 50)
                {
                    r.Close();
                    f.Close();
                    importSTLAscii(filename, model, updateRate);
                }
                else
                {
                    for (int i = 0; i < nTri; i++)
                    {
                        //--- MODEL_SLA	// milton
                        if (i > 0 && i % 4000 == 0)
                        {
                            ////MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = ((double)i / nTri) * 100.0;
                            ////Application.DoEvents();
                            if (updateRate != null)
                                updateRate((int)(((double)i / nTri) * 100.0));
                            ////if (model.IsActionStopped()) return;
                            ////if (MainWindow.main.threedview.ui.BusyWindow.killed) return;
                            if (Command == COMMAND.Abort)
                            {
                                Command = COMMAND.None;
                                Status = STATUS.UserAbort;
                                return;
                            }
                        }
                        //---

                        //timer.Start();
                        RHVector3 normal = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        RHVector3 p1 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        RHVector3 p2 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        RHVector3 p3 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        normal.NormalizeSafe();
                        model.addTriangle(p1, p2, p3, normal);
                        //timer.Stop();
                        r.ReadUInt16();
                    }
                    r.Close();
                    f.Close();
                    //showTime("addTriangle(p1, p2, p3, normal)");
                    //stopWatch.Reset();
                }
            }
            catch
            {
                throw;
                ////MessageBox.Show(Trans.T("M_LOAD_STL_FILE_ERROR"), Trans.T("W_LOAD_STL_FILE_ERROR"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (Status == STATUS.Busy)
                Status = STATUS.Done;

        }

        public void importByteArray(ref byte[] stlArr, TopoModel model, Action<int> updateRate = null)
        {
            MemoryStream stream = new MemoryStream();

            stream.Write(stlArr, 0, stlArr.Length);          
            stream.Position = 0;

            byte[] header = new byte[80];
            ReadArray(stream, header);
            BinaryReader r = new BinaryReader(stream);
            int nTri = r.ReadInt32();
            
            try
            {
                for (int i = 0; i < nTri; i++)
                {
                    ////if (i>0 && i % 4000 == 0)
                    ////{
                    ////    MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = ((double)i / nTri) * 100.0;
                    ////    Application.DoEvents();
                    ////    ////if(model.IsActionStopped()) return;
                    ////    if (MainWindow.main.threedview.ui.BusyWindow.killed) return;
                    ////}
                    //timer.Start();
                    
                    if (Command == COMMAND.Abort)
                    {
                        Command = COMMAND.None;
                        Status = STATUS.UserAbort;
                        return;
                    }

                    if (updateRate != null)
                        updateRate((int)(((double)i / nTri) * 100.0));

                    RHVector3 normal = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    RHVector3 p1 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    RHVector3 p2 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    RHVector3 p3 = new RHVector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    normal.NormalizeSafe();
                    model.addTriangle(p1, p2, p3, normal);
                    //timer.Stop();
                    r.ReadUInt16();
                }
                r.Close();
                stream.Close();
                //showTime("addTriangle(p1, p2, p3, normal)");
                //stopWatch.Reset();
            }
            catch
            {
                throw;
                ////MessageBox.Show(Trans.T("M_LOAD_STL_FILE_ERROR"), Trans.T("W_LOAD_STL_FILE_ERROR"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }

}

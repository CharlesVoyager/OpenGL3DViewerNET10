using System.IO;
using View3D.model.geom;

namespace OpenGL3DViewerNET10.MeshIOLib
{
    public class StlSetting : IMeshOutSetting
    {
        public bool Binary { get; set; }
        public bool RepairModel { get; set; }
        public FormatCode Format { get; set; }

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
        void exportSTL(string filename, TopoModel model, Action<int> updateRate, bool binary, bool DomodelRepair)
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

        void exportSTL(FileStream fs, TopoModel model, Action<int> updateRate, bool binary)
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

        void exportSTLBinary(FileStream fs, TopoModel model, Action<int> updateRate)
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
                    if (updateRate != null)
                        updateRate( (int)(((double)count / model.triangles.Count) * 100.0) );

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

        void exportSTLAscii(FileStream fs, TopoModel model, Action<int> updateRate)
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

        void ReadArray(Stream stream, byte[] data)
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
        void importSTLAscii(string filename, TopoModel model, Action<int> updateRate)
        {
            long fileSize = new System.IO.FileInfo(filename).Length;
            long bytesRead = 0;
            int count = 0;

            using (var reader = new System.IO.StreamReader(filename))
            {
                string line;
                RHVector3 normalVect = null;
                RHVector3 p1 = null, p2 = null;
                int vertexIndex = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    bytesRead += line.Length + 1; // approximate
                    line = line.TrimStart(); // remove leading whitespace only

                    if (line.StartsWith("facet normal", StringComparison.OrdinalIgnoreCase))
                    {
                        // Parse: "facet normal x y z"
                        normalVect = ParseVector(line, 12); // skip "facet normal"
                        normalVect?.NormalizeSafe();
                        vertexIndex = 0;

                        count++;
                        if (count % 4000 == 0)
                        {
                            updateRate?.Invoke((int)((bytesRead / (double)fileSize) * 100.0));

                            if (Command == COMMAND.Abort)
                            {
                                Command = COMMAND.None;
                                Status = STATUS.UserAbort;
                                return;
                            }
                        }
                    }
                    else if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
                    {
                        // Parse: "vertex x y z"
                        var v = ParseVector(line, 6); // skip "vertex"
                        if (vertexIndex == 0) p1 = v;
                        else if (vertexIndex == 1) p2 = v;
                        else if (vertexIndex == 2)
                        {
                            model.AddTriangle(p1, p2, v, normalVect);
                        }
                        vertexIndex++;
                    }
                }
            }

            //Console.WriteLine("Finished reading ASCII STL. Total triangles: " + count);
        }

        // Replaces extractVector() — parses "x y z" starting at offset, no Substring allocation
        RHVector3 ParseVector(string line, int startIndex)
        {
            ReadOnlySpan<char> span = line.AsSpan(startIndex);

            // Skip leading spaces
            int i = 0;
            while (i < span.Length && span[i] == ' ') i++;
            span = span.Slice(i);

            // Parse three space-separated floats
            double x = 0, y = 0, z = 0;
            span = ParseDouble(span, out x);
            span = ParseDouble(span.TrimStart(), out y);
            ParseDouble(span.TrimStart(), out z);

            return new RHVector3(x, y, z);
        }

        ReadOnlySpan<char> ParseDouble(ReadOnlySpan<char> span, out double value)
        {
            int end = span.IndexOf(' ');
            if (end < 0) end = span.Length;

#if NET5_0_OR_GREATER
            double.TryParse(span.Slice(0, end), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
#else
            double.TryParse(span.Slice(0, end).ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
#endif

            return end < span.Length ? span.Slice(end) : ReadOnlySpan<char>.Empty;
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
        void importSTLWOCatch(string filename, TopoModel model, Action<int> updateRate)
        {
            Status = STATUS.Busy;

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
                    r.Close();
                    f.Close();
                    importSTLBinary(filename, model, updateRate);
                }
            }
            finally
            {
                if (r != null)
                    r.Close();

                if (f != null)
                    f.Close();
            }
        }
        void importSTLBinary(string filename, TopoModel model, Action<int> updateRate)
        {
            Status = STATUS.Busy;
            model.Clear();

            try
            {
                using var f = new FileStream(filename, FileMode.Open, FileAccess.Read,
                                             FileShare.Read, 1 << 16); // 64KB buffer

                // Read 80-byte header
                byte[] header = new byte[80];
                f.Read(header, 0, 80);

                // Read triangle count
                byte[] countBuf = new byte[4];
                f.Read(countBuf, 0, 4);
                int nTri = BitConverter.ToInt32(countBuf, 0);

                // Read all triangle data at once
                byte[] data = new byte[nTri * 50];
                f.Read(data, 0, data.Length);

                model.EnsureCapacity(nTri); // if supported

                for (int i = 0; i < nTri; i++)
                {
                    if (i > 0 && i % 20000 == 0)
                    {
                        updateRate?.Invoke((int)((double)i / nTri * 100.0));

                        if (Command == COMMAND.Abort)
                        {
                            Command = COMMAND.None;
                            Status = STATUS.UserAbort;
                            return;
                        }
                        if (!ModelLib.Utils.RamTools.IsRamSizeValid())
                            throw new OutOfMemoryException();
                    }

                    int o = i * 50;
                    // Skip file normal (o+0..o+11) — we recalculate below
                    var p1 = new RHVector3(BitConverter.ToSingle(data, o + 12),
                                           BitConverter.ToSingle(data, o + 16),
                                           BitConverter.ToSingle(data, o + 20));
                    var p2 = new RHVector3(BitConverter.ToSingle(data, o + 24),
                                           BitConverter.ToSingle(data, o + 28),
                                           BitConverter.ToSingle(data, o + 32));
                    var p3 = new RHVector3(BitConverter.ToSingle(data, o + 36),
                                           BitConverter.ToSingle(data, o + 40),
                                           BitConverter.ToSingle(data, o + 44));
                    // attribute bytes at o+48..o+49 skipped

                    RHVector3 d1 = p2.Subtract(p1);
                    RHVector3 d2 = p3.Subtract(p2);
                    RHVector3 normal = d1.CrossProduct(d2);
                    normal.NormalizeSafe();

                    model.AddTriangle(p1, p2, p3, normal);
                }
            }
            finally
            {
                // FileStream disposed by `using`
            }

            if (Status == STATUS.Busy)
                Status = STATUS.Done;
        }

        void importSTL(string filename, TopoModel model, Action<int> updateRate)
        {
            Status = STATUS.Busy;

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
                        model.AddTriangle(p1, p2, p3, normal);
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

        void importByteArray(ref byte[] stlArr, TopoModel model, Action<int> updateRate = null)
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
                    model.AddTriangle(p1, p2, p3, normal);
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

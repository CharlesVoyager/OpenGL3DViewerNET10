using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using View3D.model.geom;

namespace View3D.MeshInOut
{

    public enum FormatCode { UnKnown, Stl, Obj, _3ws };
    public enum ErrorCode {
        ERR_NONE = 0,
        ERR_NOT_VALID_FILE = -1000,
        ERR_NOT_SUPPORT_VER = -1001,
    }

    public class MeshFormat
    {
        public FormatCode Format { get; set; }
        public string Extension;
    }

    public class Setting : IMeshOutSetting
    {
        public FormatCode Format { get; set; }
    }

    public class MeshIOBase : IMeshInOut
    {
        protected NumberFormatInfo NumFormat { get; private set; }

        #region User Command
        public enum STATUS { Idle, Busy, Done, UserAbort }
        public enum COMMAND { None, Abort }

        public STATUS Status { get; protected set; }
        public COMMAND Command { get; set; }
        #endregion

        public MeshIOBase()
        {
            NumFormat = CultureInfo.InvariantCulture.NumberFormat;
        }

        public virtual int Load(string filename, TopoModel model, Action<int> updateRate)
        {
            Debug.Assert(false);
            return -1;
        }
        public virtual int LoadWOCatch(string filename, TopoModel model, Action<int> updateRate)
        {
            Debug.Assert(false);
            return -1;
        }
        public virtual int Load(FileStream fs, TopoModel model, Action<int> updateRate)
        {
            Debug.Assert(false);
            return -1;
        }

        public virtual void Save(string filename, TopoModel stl, IMeshOutSetting outSetting, Action<int> updateRate)
        {
            Debug.Assert(false);
        }
        public virtual void Save(FileStream fs, TopoModel stl, IMeshOutSetting outSetting, Action<int> updateRate)
        {
            Debug.Assert(false);
        }

        public void TaskAbort(object sender, EventArgs e)
        {
            Command = COMMAND.Abort;
        }
    }
}

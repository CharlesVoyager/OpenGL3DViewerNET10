using System;
using System.IO;
using View3D.model.geom;

namespace View3D.MeshInOut
{
    public interface IMeshInOut
    {
        MeshIOBase.COMMAND Command { get; set; }
        MeshIOBase.STATUS Status { get; }

        int Load(string filename, TopoModel model, Action<int> updateRate);
        int Load(FileStream fs, TopoModel model, Action<int> updateRate);
        int LoadWOCatch(string filename, TopoModel model, Action<int> updateRate);
        void Save(string filename, TopoModel stl, IMeshOutSetting outSetting, Action<int> updateRate);
        void Save(FileStream fs, TopoModel stl, IMeshOutSetting outSetting, Action<int> updateRate);
        void TaskAbort(object sender, EventArgs e);
    }
}
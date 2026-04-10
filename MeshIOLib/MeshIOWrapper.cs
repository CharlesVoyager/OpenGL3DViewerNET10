using View3D.model.geom;
using View3D;

namespace OpenGL3DViewerNET10.MeshIOLib
{
    public class MeshIOWrapper
    {
        public void LoadWOCatch(string file, TopoModel model)
        {
            string lname = file.ToLower();

            IMeshInOut fileMesh;
            Action<int> updateRateFunc;

            if (lname.EndsWith(".stl"))
                fileMesh = new MeshIOStl();
            else if (lname.EndsWith(".glb"))
                fileMesh = new MeshIOGlb();
            else
                fileMesh = new MeshIOBase();

            updateRateFunc = OnProcessUpdate;
            MainWindow.main.BusyWindow.AbortTask += fileMesh.TaskAbort;

            fileMesh.LoadWOCatch(file, model, updateRateFunc);

            MainWindow.main.BusyWindow.AbortTask -= fileMesh.TaskAbort;
        }

        public void OnProcessUpdate(int rate)
        {
            MainWindow.main.Dispatcher.InvokeAsync(() =>
            {
                if (MainWindow.main.BusyWindow.Visibility == System.Windows.Visibility.Visible)
                    MainWindow.main.BusyWindow.busyProgressbar.Value = rate;
            });
        }
    }
}

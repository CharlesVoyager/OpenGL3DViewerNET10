using View3D.model.geom;
using View3D.MeshInOut;

namespace View3D.view
{
    public class ModelInOut
    {
        event EventHandler AbortTask;

        public void LoadWOCatch(string file, TopoModel model)
        {
            string lname = file.ToLower();

            IMeshInOut fileMesh;
            Action<int> updateRateFunc;

            if (lname.EndsWith(".stl"))
                fileMesh = new MeshIOStl();
            else
                fileMesh = new MeshIOBase();

            updateRateFunc = OnProcessUpdate;
            AbortTask += fileMesh.TaskAbort;
            fileMesh.LoadWOCatch(file, model, updateRateFunc);
            AbortTask -= fileMesh.TaskAbort;
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

using System;
using System.IO;
using View3D.model;
using View3D.model.geom;
using View3D.MeshInOut;

namespace View3D.view
{
    public class ModelInOut
    {
        public bool TwoStageUpdateProcess { get; set; }
        public event EventHandler AbortTask;
        private int mergedCount, mergeCountTotal;

        public ModelInOut()
        {
            TwoStageUpdateProcess = false;
        }

        public void LoadWOCatch(string file, TopoModel model)
        {
            string lname = file.ToLower();

            IMeshInOut fileMesh;
            Action<int> updateRateFunc;
            if (lname.EndsWith(".stl"))
            {
                fileMesh = new MeshIOStl();
                updateRateFunc = OnProcessUpdate;
            }
            else
            {
                fileMesh = new MeshIOBase();
                updateRateFunc = OnProcessUpdate;
            }
            AbortTask += fileMesh.TaskAbort;
            fileMesh.LoadWOCatch(file, model, updateRateFunc);
            AbortTask -= fileMesh.TaskAbort;
        }
        public void Save(string filename, TopoModel model, Setting outSetting) { }

        #region EvenHandler
        private void EnableBusyWindowNoCancleButton()
        {
            if (MainWindow.main == null) return;
            if (MainWindow.main.threeDControl == null) return;

            // BusyWindow start
            MainWindow.main.BusyWindow.labelBusyMessage.Text = Trans.T("L_MODELING");
            MainWindow.main.BusyWindow.killed = false;
            MainWindow.main.BusyWindow.Visibility = System.Windows.Visibility.Visible;
            MainWindow.main.BusyWindow.busyProgressbar.IsIndeterminate = false;
            MainWindow.main.BusyWindow.busyProgressbar.Maximum = 100;
            MainWindow.main.BusyWindow.busyProgressbar.Value = 0; 
            MainWindow.main.BusyWindow.AbortTask += OnUIAbort;
            MainWindow.main.BusyWindow.StartTimer();
        }

        public void OnProcessUpdate(int rate)
        {

            MainWindow.main.Dispatcher.InvokeAsync(() =>
            {
                if (MainWindow.main.BusyWindow.Visibility == System.Windows.Visibility.Visible)
                    MainWindow.main.BusyWindow.busyProgressbar.Value = rate;
            });
        }

        public void OnProcessUpdate3wsLoadStageLoadStl(int rate)
        {
            if (MainWindow.main.BusyWindow.Visibility == System.Windows.Visibility.Visible)
                MainWindow.main.BusyWindow.busyProgressbar.Value = rate / 2; 
        }

        public void OnProcessUpdateSaveStageMerge(double value)
        {
            if (MainWindow.main.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                MainWindow.main.BusyWindow.increment != 0.0)
            {
                if (MainWindow.main.BusyWindow.busyProgressbar.Value < MainWindow.main.BusyWindow.firstStagePercent)
                {
                    MainWindow.main.BusyWindow.busyProgressbar.Value = ((value + mergedCount * 100) / mergeCountTotal) * MainWindow.main.BusyWindow.firstStagePercent / 100;
                }
            }
        }

        public void OnProcessUpdateSaveStage2nd(int rate)
        {
            if (MainWindow.main.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                MainWindow.main.BusyWindow.increment != 0.0)
            {
                MainWindow.main.BusyWindow.busyProgressbar.Value =
                        (rate) * (100.0 - MainWindow.main.BusyWindow.firstStagePercent)
                        + MainWindow.main.BusyWindow.firstStagePercent;
            }
        }

        public void OnUIAbort(object sender, EventArgs e)
        {
            if (AbortTask != null)
                AbortTask(sender, e);
        }
        #endregion

    }
}

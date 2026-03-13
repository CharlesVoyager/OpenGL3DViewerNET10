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
        private bool saveTaskAbort;
        private int mergedCount, mergeCountTotal;

        public ModelInOut()
        {
            TwoStageUpdateProcess = false;
        }
        public void Load(string file, ModelData model)
        {
            string lname = model.FileName.ToLower();
            if (lname.EndsWith(".stl"))
            {
                MeshIOStl fileMesh = new MeshIOStl();
                AbortTask += fileMesh.TaskAbort;

                try
                {
                    fileMesh.Load(model.FileName, model.originalModel, OnProcessUpdate);
                }
                catch
                {
                    ///MessageBox.Show(Trans.T("M_LOAD_STL_FILE_ERROR"), Trans.T("W_LOAD_STL_FILE_ERROR"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                AbortTask -= fileMesh.TaskAbort;
            }
            FileInfo info = new FileInfo(file);
            model.name = info.Name;
        }

        public void LoadWOCatch(string file, ModelData model)
        {
            string lname = model.FileName.ToLower();

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
            fileMesh.LoadWOCatch(model.FileName, model.originalModel, updateRateFunc);
            AbortTask -= fileMesh.TaskAbort;

            FileInfo info = new FileInfo(file);
            model.name = info.Name;
        }
        public void Save(string filename, TopoModel model, Setting outSetting) { }

        #region EvenHandler
        private void EnableBusyWindowNoCancleButton()
        {
            if (MainWindow.main == null) return;
            if (MainWindow.main.threedview == null) return;

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
            saveTaskAbort = true;

            if (AbortTask != null)
                AbortTask(sender, e);
        }
        #endregion

    }
}

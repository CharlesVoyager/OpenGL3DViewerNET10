 using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using View3D.model;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for BusyWindow.xaml
    /// </summary>
    public partial class BusyWindow : System.Windows.Controls.UserControl
    {
        public event EventHandler AbortTask;

        public bool killed = false;
        public double increment = 0;
        public double firstStagePercent = 20.0;
        private string progressStatus = "";

        public BusyWindow()
        {
            InitializeComponent();

            try
            {
                MainWindow.main.languageChanged += translate;
            }
            catch { }
        }

        private void translate()
        {
            //labelBusyMessage.Text = Trans.T("L_MODELING");
            labelElapsedTime.Text = Trans.T("L_ELAPSED_TIME");
        }

        public void StartTimer()
        {
            progressStatus = "";
            textBlock_time.Text = "00:00:00";
            timer = new DispatcherTimer();
            timer.Tick += dispatcherTimerTick_;
            timer.Interval = new TimeSpan(0, 0, 1);
            stopWatch = new Stopwatch();

            stopWatch.Start();
            timer.Start();
        }
        //Change_Brush_Time
        public void StopTimer()
        {
            textBlock_time.Text = "00:00:00";
            stopWatch.Stop();
            timer.Stop();
        }
        //


        public void progressUpdate(int curLayer, int totalLayer)
        {

#if DEBUG_MODE
            progressStatus = "(" + curLayer + "/" + totalLayer + ")";
#endif
#if !DEBUG_MODE
            progressStatus = ((double)curLayer / totalLayer).ToString("P0");
#endif
            
        }
        
        private DispatcherTimer timer;
        private Stopwatch stopWatch;

        public int getStopWatch()
        {
            return Convert.ToInt16(stopWatch.Elapsed.Seconds);
        }

        private void dispatcherTimerTick_(object sender, EventArgs e)
        {
            textBlock_time.Text = progressStatus + "   " + stopWatch.Elapsed.Hours.ToString("00")
                + ":" + stopWatch.Elapsed.Minutes.ToString("00")
                + ":" + stopWatch.Elapsed.Seconds.ToString("00");

        }

        public void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            // importing
            killed = true;

            if (AbortTask != null)
                AbortTask(this, new EventArgs());
        }

        public void EnableBusyWindow()
        {
            labelBusyMessage.Text = Trans.T("L_MODELING");
            killed = false;
            Visibility = Visibility.Visible;
            buttonCancel.Visibility = Visibility.Visible;
            busyProgressbar.IsIndeterminate = false;
            busyProgressbar.Maximum = 100;
            busyProgressbar.Value = 0;
            StartTimer();
        }

        public void DisableBusyWindow()
        {
            Visibility = Visibility.Hidden;
        }
    }
}

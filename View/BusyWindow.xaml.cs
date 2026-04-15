using OpenGL3DViewerNET10.ModelLib.Utils;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

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
            labelElapsedTime.Text = Trans.T("L_ELAPSED_TIME");
        }

        public void StartTimer()
        {
            textBlock_time.Text = "00:00:00";
            timer = new DispatcherTimer();
            timer.Tick += dispatcherTimerTick_;
            timer.Interval = new TimeSpan(0, 0, 1);
            stopWatch = new Stopwatch();

            stopWatch.Start();
            timer.Start();
        }

        public void StopTimer()
        {
            textBlock_time.Text = "00:00:00";
            stopWatch.Stop();
            timer.Stop();
        }
        
        private DispatcherTimer timer;
        private Stopwatch stopWatch;

        public int getStopWatch()
        {
            return Convert.ToInt16(stopWatch.Elapsed.Seconds);
        }

        private void dispatcherTimerTick_(object sender, EventArgs e)
        {
            textBlock_time.Text = stopWatch.Elapsed.Hours.ToString("00")
                + ":" + stopWatch.Elapsed.Minutes.ToString("00")
                + ":" + stopWatch.Elapsed.Seconds.ToString("00");

        }

        public void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            killed = true;

            if (AbortTask != null)
                AbortTask(this, new EventArgs());
        }

        public void EnableBusyWindow()
        {
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

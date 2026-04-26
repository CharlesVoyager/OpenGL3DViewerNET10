using OpenGL3DViewerNET10.ModelLib.Utils;
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
        public event EventHandler? AbortTask;
        DispatcherTimer? timer;
        Stopwatch? stopWatch;

        public bool killed = false;
        public double increment = 0;
        public double firstStagePercent = 20.0;

        public BusyWindow()
        {
            InitializeComponent();

            try
            {
                stopWatch = new Stopwatch();

                MainWindow.main.languageChanged += translate;
                timer = new DispatcherTimer();
                timer.Tick += dispatcherTimerTick_;
                timer.Interval = new TimeSpan(0, 0, 1);
            }
            catch { }
        }

        private void translate()
        {
            labelElapsedTime.Text = Trans.T("L_ELAPSED_TIME");
        }

        public int getStopWatch()
        {
            if (stopWatch == null) return 0;
            return Convert.ToInt16(stopWatch.Elapsed.Seconds);
        }

        private void dispatcherTimerTick_(object sender, EventArgs e)
        {
            if (stopWatch == null) return;

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

            if (stopWatch == null || timer == null) return;

            textBlock_time.Text = "00:00:00";
            stopWatch.Reset();
            stopWatch.Start();
            timer.Start();
        }

        public void DisableBusyWindow()
        {
            Visibility = Visibility.Hidden;

            if (stopWatch == null || timer == null) return;

            textBlock_time.Text = "00:00:00";
            stopWatch.Stop();
            timer.Stop();
        }
    }
}

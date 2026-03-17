using System.IO;
using View3D;
using View3D.view;

namespace OpenGL3DViewerNET10
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    public partial class App : System.Windows.Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            MainWindow? mainWindow = null;
            // Launch WPF on a dedicated STA background thread
            var wpfThread = new Thread(() =>
            {
                var app = new App();
                mainWindow = new MainWindow();
                app.Run(mainWindow);  // WPF message pump runs here
            });
            wpfThread.SetApartmentState(ApartmentState.STA);
            wpfThread.IsBackground = false;
            wpfThread.Name = "WPF-Thread";
            wpfThread.Start();

            // Wait until MainWindow is ready before starting OpenTK
            View3D.MainWindow._mainWindowReady.Wait();

            // OpenTK GameWindow runs on the main thread (required by GLFW)
            mainWindow.threeDControl = new ThreeDControl();
            mainWindow.threeDControl.SetComp(mainWindow.stlComposer);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessCommandLine(mainWindow);
            });

            // Wait until STL model data is ready if import STL file through command line before starting rendering loop
            STLComposer._stlModelDataReady.Wait();

            // Blocks main thread for lifetime of GL window — correct!
            mainWindow.threeDControl.Run();

            Console.WriteLine("Exit the program.");
        }

        // Command line argument example: @"..\..\..\Stl\10_10_10.stl"
        private static void ProcessCommandLine(MainWindow main)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < Math.Min(2, args.Length); i++)
            {
                string file = args[i];
                if (File.Exists(file))
                    main.LoadGCodeOrSTL(file);
            }
        }
    }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using View3D.model;
using View3D.view;

namespace View3D
{
    public delegate void languageChangedEvent();

    public partial class MainWindow : Window
    {
        public event languageChangedEvent? languageChanged = null;

        public static MainWindow? main = null;

        public ThreeDSettings? threeDSettings = null;
        public ThreeDControl? threeDControl = null;
        public STLComposer? stlComposer = null;
        public ThreeDCamera? threeDCamera = null;

        public Trans? trans = null;

        public double dpiX, dpiY;

        #region Print Area settings
        public float PrintAreaWidth = 256;  // x-axis direction
        public float PrintAreaDepth = 256;  // y-axis direction
        public float PrintAreaHeight = 200; // z-axis direction
        double epsilon = 1e-4; // 0.0001

        public bool PointInside(float x, float y, float z)
        {
            if (z < -0.1 || z > PrintAreaHeight)
                return false;

            if (x < -epsilon || x > PrintAreaWidth + epsilon) return false;
            if (y < -epsilon || y > PrintAreaDepth + epsilon) return false;

            return true;
        }
        #endregion

        public static readonly ManualResetEventSlim _mainWindowReady = new ManualResetEventSlim(false);

        public MainWindow()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US", false);

            main = this;

            // Translator
            trans = new Trans(AppDomain.CurrentDomain.BaseDirectory + "Resources");

            // Retrieve DPI from WPF presentation source after initialization
            Loaded += (s, e) =>
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                }
            };

            // ThreeDSettings
            threeDSettings = new ThreeDSettings();
            threeDSettings.Hide();

            // STLComposer
            stlComposer = new STLComposer();
            stlComposer.Hide();

            // Camera
            threeDCamera = new ThreeDCamera();
            threeDCamera.SetCameraDefaults();
            threeDCamera.OrientIsometric();

            InitializeComponent();
            UI();

            if (languageChanged != null)
                languageChanged();

            _mainWindowReady.Set();
        }

        private void MainWindow_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            bool canSupport = true;

            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (!file.ToUpper().EndsWith(".STL"))
                    {
                        canSupport = false;
                        break;
                    }
                }
                e.Effects = canSupport ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                foreach (string file in files)
                    LoadGCodeOrSTL(file);
            }
        }

        public void LoadGCodeOrSTL(string file)
        {
            if (!File.Exists(file)) return;

            string fileLow = file.ToLower();
            if (fileLow.EndsWith(".stl"))
                stlComposer.openAndAddObject(file);
        }

        public void Update3D()
        {
            if (threeDControl != null)
                threeDControl.UpdateChanges();
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Delete)
                {
                    stlComposer.buttonRemoveSTL_Click(null, null);
                    stlComposer.updateSTLState(null);
                    threeDControl.UpdateChanges();
                }
            }
            catch { }
        }

        public void UpdateLocation(double x, double y)
        {
            Left = x / dpiX * 96;
            Top = y / dpiY * 96;
        }

        public void UpdateSize(double width, double height)
        {
            Width = width / dpiX * 96;
            Height = height / dpiY * 96 + 28;
        }

        //── UI (WPF) ────────────────────────────────────────────────
        private ContextMenu? _contextMenu;
        public void UI()
        {
            VisualStateManager.GoToState(UI_view, "State2", true);
            VisualStateManager.GoToState(UI_move, "State2", true);
            VisualStateManager.GoToState(UI_rotate, "State2", true);
            VisualStateManager.GoToState(UI_resize_advance, "State2", true);
            VisualStateManager.GoToState(UI_object_information, "State2", true);

            UI_resize_advance.btn_Scale.FontSize = 12;
            UI_resize_advance.button_mmtoinch.FontSize = 12;
            UI_resize_advance.button_inchtomm.FontSize = 12;
            UI_resize_advance.lbl_Size.FontSize = 12;

            move_toggleButton.FontSize = 12;
            import_button.FontSize = 12;

            languageChanged += translate;

            // Retrieve the context menu from resources
            _contextMenu = (System.Windows.Controls.ContextMenu)this.Resources["ViewerContextMenu"];

            // Wire up click handlers
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[0]).Click += (s, e) => OnLandObject();
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[1]).Click += (s, e) => OnResetObject();
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[2]).Click += (s, e) => OnRemoveObject();
            // index 3 is Separator
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[4]).Click += (s, e) => OnMmToInch();
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[5]).Click += (s, e) => OnInchToMm();
            // index 6 is Separator
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[7]).Click += (s, e) => OnClone();
            // index 8 is Separator
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[9]).Click += (s, e) => stlComposer.Show();
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[10]).Click += (s, e) => threeDSettings.Show();

            // About
            gridAbout.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Called from ThreeDControl (GL thread) via Dispatcher.InvokeAsync.
        /// hasModel controls which items are visible.
        /// </summary>
        public void ShowContextMenu(bool isModelSelected)
        {
            if (isModelSelected == false) return;

            // Must be called on the WPF thread
            _contextMenu.Items.Cast<FrameworkElement>()
                .Where(item => item is System.Windows.Controls.MenuItem)
                .ToList()
                .ForEach(item => item.Visibility =
                    isModelSelected ? Visibility.Visible : Visibility.Collapsed);

            _contextMenu.IsOpen = true;
        }

        // Actions — forward to ThreeDControl
        private void OnLandObject() => threeDControl.ContextMenu_LandObject();
        private void OnResetObject() => threeDControl.ContextMenu_ResetObject();
        private void OnRemoveObject() => threeDControl.ContextMenu_RemoveObject();
        private void OnMmToInch() => threeDControl.ContextMenu_MmToInch();
        private void OnInchToMm() => threeDControl.ContextMenu_InchToMm();
        private void OnClone() => threeDControl.ContextMenu_Clone();

        private void translate()
        {
            view_toggleButton.ToolTip = Trans.T("B_VIEW");
            move_toggleButton.ToolTip = Trans.T("B_MOVE");
            rotate_toggleButton.ToolTip = Trans.T("B_ROTATE");
            resize_toggleButton.ToolTip = Trans.T("B_SCALE");
            info_toggleButton.ToolTip = Trans.T("B_INFO");
            remove_toggleButton.ToolTip = Trans.T("B_REMOVE");
            import_button.ToolTip = Trans.T("B_IMPORT");
            about_button.ToolTip = Trans.T("B_ABOUT");

            view_toggleButton.Content = Trans.T("B_VIEW");
            move_toggleButton.Content = Trans.T("B_MOVE");
            rotate_toggleButton.Content = Trans.T("B_ROTATE");
            resize_toggleButton.Content = Trans.T("B_SCALE");
            info_toggleButton.Content = Trans.T("B_INFO");
            remove_toggleButton.Content = Trans.T("B_REMOVE");
            import_button.Content = Trans.T("B_IMPORT");
            about_button.Content = Trans.T("B_ABOUT");
        }

        public void setbuttonVisable(bool flag)
        {
            if (flag == true)
            {
                view_toggleButton.Visibility = Visibility.Visible;
                move_toggleButton.Visibility = Visibility.Visible;
                rotate_toggleButton.Visibility = Visibility.Visible;
                resize_toggleButton.Visibility = Visibility.Visible;
                info_toggleButton.Visibility = Visibility.Visible;
                remove_toggleButton.Visibility = Visibility.Visible;

                view_toggleButton.IsChecked = false;
                move_toggleButton.IsChecked = false;
                rotate_toggleButton.IsChecked = false;
                resize_toggleButton.IsChecked = false;
                info_toggleButton.IsChecked = false;
                remove_toggleButton.IsChecked = false;
            }
            else
            {
                move_toggleButton.Visibility = Visibility.Hidden;
                rotate_toggleButton.Visibility = Visibility.Hidden;
                resize_toggleButton.Visibility = Visibility.Hidden;
                info_toggleButton.Visibility = Visibility.Hidden;
                remove_toggleButton.Visibility = Visibility.Hidden;

                VisualStateManager.GoToState(UI_move, "State2", true);
                VisualStateManager.GoToState(UI_rotate, "State2", true);
                VisualStateManager.GoToState(UI_resize_advance, "State2", true);
                VisualStateManager.GoToState(UI_object_information, "State2", true);
            }
        }

        private void view_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_view, "State1", true);

            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;
        }

        private void view_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_view, "State2", true);
            Focus();
        }

        public void move_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_move, "State1", true);
            view_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            UI_move.Initial();
        }

        public void move_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_move, "State2", true);
            Focus();
        }

        private void import_button_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            openFileDialog.Title = "Select a File";
            openFileDialog.Filter = "STL Files (*.stl)|*.stl";

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string filePath = openFileDialog.FileName;

                string fileLow = filePath.ToLower();
                if (fileLow.EndsWith(".stl"))
                {
                    stlComposer.openAndAddObject(filePath);
                    threeDControl.InvokeGL(() =>
                    {
                        STLComposer._stlModelDataReady.Wait();
                        threeDControl.UploadMeshToGPU();
                    });
                }
            }
        }

        private void about_button_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            gridAbout.Visibility = Visibility.Visible;

            DebugLog();
        }

        private void rotate_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            UI_rotate.sliderX.Value = Convert.ToDouble(stlComposer.textRotX.Text);
            UI_rotate.sliderY.Value = Convert.ToDouble(stlComposer.textRotY.Text);
            UI_rotate.sliderZ.Value = Convert.ToDouble(stlComposer.textRotZ.Text);
            VisualStateManager.GoToState(UI_rotate, "State1", true);
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;
        }

        // Scale
        public void resize_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_resize_advance, "State1", true);
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            PrintModel stl = stlComposer.SingleSelectedModel;
            if (stl == null) return;
            UI_move.slider_moveZ.Maximum = 1000;

            model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
            UI_resize_advance.bboxnow = bbox.Size.x / Convert.ToDouble(stlComposer.textScaleX.Text);
            UI_resize_advance.bboynow = bbox.Size.y / Convert.ToDouble(stlComposer.textScaleY.Text);
            UI_resize_advance.bboznow = bbox.Size.z / Convert.ToDouble(stlComposer.textScaleZ.Text);

            UI_resize_advance.gIsShow = true;
            UI_resize_advance.dimX = bbox.Size.x;
            UI_resize_advance.updateTxt(Enums.Axis.X);
            UI_resize_advance.dimY = bbox.Size.y;
            UI_resize_advance.updateTxt(Enums.Axis.Y);
            UI_resize_advance.dimZ = bbox.Size.z;
            UI_resize_advance.updateTxt(Enums.Axis.Z);

            if (Convert.ToDouble(stlComposer.textRotX.Text) != 0 ||
                Convert.ToDouble(stlComposer.textRotY.Text) != 0 ||
                Convert.ToDouble(stlComposer.textRotZ.Text) != 0)
            {
                UI_resize_advance.chk_Uniform.IsChecked = true;
                UI_resize_advance.chk_Uniform.IsEnabled = false;
            }
            else
            {
                UI_resize_advance.chk_Uniform.IsEnabled = true;
            }

            if (UI_resize_advance.chk_Uniform.IsChecked == true)
            {
                UI_resize_advance.chk_Uniform_Checked(null, null);
            }

            UI_resize_advance.gIsShow = false;
            UI_resize_advance.button_mmtoinch.IsEnabled = true;
            UI_resize_advance.button_inchtomm.IsEnabled = false;
            UI_resize_advance.lbl_XUnits.Content = Trans.T("L_MM");
            UI_resize_advance.lbl_YUnits.Content = Trans.T("L_MM");
            UI_resize_advance.lbl_ZUnits.Content = Trans.T("L_MM");

            UI_resize_advance.txt_Scale.Text = "";
            UI_resize_advance.button_Reset.ToolTip = Trans.T("B_RESET");
            UI_resize_advance.button_Reset.Content = Trans.T("B_RESET");
            UI_resize_advance.lbl_Uniform.Content = Trans.T("L_UNIFORM");
            UI_resize_advance.lbl_Size.Content = Trans.T("L_SIZE");
            UI_resize_advance.btn_Scale.Content = Trans.T("B_APPLY");
            UI_resize_advance.button_mmtoinch.Content = Trans.T("B_SCALE_UP") + " (" + Trans.T("L_MM") + "->" + Trans.T("L_INCH") + ")";
            UI_resize_advance.button_inchtomm.Content = Trans.T("B_SCALE_DOWN") + " (" + Trans.T("L_INCH") + "->" + Trans.T("L_MM") + ")";
        }

        private void rotate_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_rotate, "State2", true);
            Focus();
        }

        private void resize_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_resize_advance, "State2", true);
            Focus();
        }

        private void info_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_object_information, "State1", true);
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
        }

        private void info_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_object_information, "State2", true);
            Focus();
        }

        public void remove_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            UI_move.slider_moveX.Minimum = -1000;
            UI_move.slider_moveX.Maximum = 1000;
            UI_move.slider_moveY.Minimum = -1000;
            UI_move.slider_moveY.Maximum = 1000;

            OutofBound.Visibility = Visibility.Hidden;

            stlComposer.buttonRemoveSTL_Click(null, null);
            stlComposer.updateSTLState(null);
            threeDControl.UpdateChanges();

            if (stlComposer.listObjects.Items.Count > 0)
                stlComposer.updateSTLState(stlComposer.SingleSelectedModel);
            Focus();
        }

        private void zoomin_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            threeDControl.button_zoomIn_Click(null, null);
            Focus();
        }

        private void zoomout_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            threeDControl.button_zoomOut_Click(null, null);
            Focus();
        }

        private void remove_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            remove_toggleButton.IsChecked = false;
        }

        private void button_closeAbout_Click(object sender, RoutedEventArgs e)
        {
            gridAbout.Visibility = Visibility.Hidden;
        }

        void DebugLog()
        {
            foreach(var m in stlComposer.models)
            {
                System.Diagnostics.Debug.WriteLine($"Model: {m.name}, Position: {m.Position.ToString()}, Rotation: {m.Rotation.ToString()}, Scale: {m.Scale.ToString()}");
                System.Diagnostics.Debug.WriteLine("curPos: " + m.curPos.Row0.ToString());
                System.Diagnostics.Debug.WriteLine("        " + m.curPos.Row1.ToString());
                System.Diagnostics.Debug.WriteLine("        " + m.curPos.Row2.ToString());
                System.Diagnostics.Debug.WriteLine("        " + m.curPos.Row3.ToString());
                // System.Diagnostics.Debug.WriteLine($"  BoundingBox: Min({m.BoundingBox.xMin}, {m.BoundingBox.yMin}, {m.BoundingBox.zMin}), Max({m.BoundingBox.xMax}, {m.BoundingBox.yMax}, {m.BoundingBox.zMax})");
            }
        }
    }
}

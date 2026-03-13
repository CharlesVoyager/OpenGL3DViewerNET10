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
        public ThreeDControl? threedview = null;
        public STLComposer? objectPlacement = null;

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
            objectPlacement = new STLComposer();
            objectPlacement.Hide();

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
                objectPlacement.openAndAddObject(file);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        public void Update3D()
        {
            if (threedview != null)
                threedview.UpdateChanges();
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Delete)
                    threedview.button_remove_Click(null, null);
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
            ((System.Windows.Controls.MenuItem)_contextMenu.Items[9]).Click += (s, e) => objectPlacement.Show();
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

        // Expose a property so ThreeDControl can check items exist
        public System.Windows.Controls.ContextMenu ContextMenuItems => _contextMenu;

        // Actions — forward to ThreeDControl
        private void OnLandObject() => threedview.ContextMenu_LandObject();
        private void OnResetObject() => threedview.ContextMenu_ResetObject();
        private void OnRemoveObject() => threedview.ContextMenu_RemoveObject();
        private void OnMmToInch() => threedview.ContextMenu_MmToInch();
        private void OnInchToMm() => threedview.ContextMenu_InchToMm();
        private void OnClone() => threedview.ContextMenu_Clone();

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

            PrintModel stl = objectPlacement.SingleSelectedModel;
            if (stl == null) return;

            UI_move.slider_moveX.Maximum = 1000;
            UI_move.slider_moveX.Minimum = -1000;
            UI_move.slider_moveY.Maximum = 1000;
            UI_move.slider_moveY.Minimum = -1000;
            UI_move.slider_moveZ.Maximum = 1000;
            UI_move.slider_moveZ.Minimum = -1000;
            UI_move.slider_moveX.Value = stl.Position.x;
            UI_move.slider_moveY.Value = stl.Position.y;
            UI_move.slider_moveZ.Value = stl.Position.z;

            double moveMax, moveMin;

            moveMax = (int)Math.Floor((PrintAreaWidth - (stl.BoundingBox.xMax - stl.Position.x)) * 100) * 0.01;
            moveMin = (int)Math.Ceiling((stl.Position.x - stl.BoundingBox.xMin) * 100) * 0.01;

            double a = PrintAreaWidth - (stl.BoundingBox.xMax - stl.Position.x);
            double b = stl.Position.x - stl.BoundingBox.xMin;

            a += 0;
            b += 0;

            //module is out of bound,it cannot move. 
            if (moveMin > moveMax)
                UI_move.slider_moveX.Value = (float)(moveMin + moveMax) / 2;
            else if (moveMin <= stl.Position.x && stl.Position.x <= moveMax)//module is in of bound.
                UI_move.slider_moveX.Value = stl.Position.x;
            else if (stl.Position.x > moveMax)//model is out of bound(too big), but it can move.
                UI_move.slider_moveX.Value = moveMax;
            else // (moveMin > stl.Position.x)//model is out of bound(too small), but it can move.
                UI_move.slider_moveX.Value = moveMin;

            if (moveMin > moveMax)
            {
                UI_move.slider_moveX.Maximum = (float)(moveMin + moveMax) / 2;
                UI_move.slider_moveX.Minimum = (float)(moveMin + moveMax) / 2;
            }
            else
            {
                UI_move.slider_moveX.Maximum = moveMax;
                UI_move.slider_moveX.Minimum = moveMin;
            }


            moveMax = (int)Math.Floor((PrintAreaDepth - (stl.BoundingBox.yMax - stl.Position.y)) * 100) * 0.01;
            moveMin = (int)Math.Ceiling((stl.Position.y - stl.BoundingBox.yMin) * 100) * 0.01;

            //module is out of bound,it can not move. 
            if (moveMin > moveMax)
                UI_move.slider_moveY.Value = (float)(moveMin + moveMax) / 2;
            else if (moveMin <= stl.Position.y && stl.Position.y <= moveMax)//module is in of bound.
                UI_move.slider_moveY.Value = stl.Position.y;
            else if (stl.Position.y > moveMax)//model is out of bound(too big), but it can move.
                UI_move.slider_moveY.Value = moveMax;
            else // (moveMin > stl.Position.y)//model is out of bound(too small), but it can move.
                UI_move.slider_moveY.Value = moveMin;

            if (moveMin > moveMax)
            {
                UI_move.slider_moveY.Maximum = (float)(moveMin + moveMax) / 2;
                UI_move.slider_moveY.Minimum = (float)(moveMin + moveMax) / 2;
            }
            else
            {
                UI_move.slider_moveY.Maximum = moveMax;
                UI_move.slider_moveY.Minimum = moveMin;
            }

            moveMax = PrintAreaHeight - (stl.BoundingBoxWOSupport.zMax - stl.Position.z);
            moveMin = stl.Position.z - stl.BoundingBoxWOSupport.zMin;
            if (moveMin > moveMax)
                moveMin = moveMax;
            if (moveMin <= stl.Position.z && moveMax >= stl.Position.z)
                UI_move.slider_moveZ.Value = stl.Position.z;
            else if (moveMax < stl.Position.z)
                UI_move.slider_moveZ.Value = moveMax;
            else // (moveMin > stl.Position.z)
                UI_move.slider_moveZ.Value = moveMin;
            UI_move.slider_moveZ.Maximum = moveMax;
            UI_move.slider_moveZ.Minimum = moveMin;

            UI_move.moveZ_textbox.Text = (Math.Round(UI_move.slider_moveZ.Value - UI_move.slider_moveZ.Minimum, 3)).ToString();
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
                    objectPlacement.openAndAddObject(filePath);
                    threedview.InvokeGL(() =>
                    {
                        STLComposer._stlModelDataReady.Wait();
                        threedview.UploadMeshToGPU();
                    });
                }
            }
        }

        public void IsTabStopAllToggleButton(object control, bool pIsTabStop)
        {
            if (control is ToggleButton)
                ((ToggleButton)control).IsTabStop = pIsTabStop;
            else
                if (control is Grid)
                    foreach (UIElement child in ((Grid)control).Children)
                        IsTabStopAllToggleButton(child, pIsTabStop);
            if (control is StackPanel)
                foreach (UIElement child in ((StackPanel)control).Children)
                    IsTabStopAllToggleButton(child, pIsTabStop);
        }

        private void about_button_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            gridAbout.Visibility = Visibility.Visible;
        }

        private void rotate_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            UI_rotate.sliderX.Value = Convert.ToDouble(objectPlacement.textRotX.Text);
            UI_rotate.sliderY.Value = Convert.ToDouble(objectPlacement.textRotY.Text);
            UI_rotate.sliderZ.Value = Convert.ToDouble(objectPlacement.textRotZ.Text);
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

            PrintModel stl = objectPlacement.SingleSelectedModel;
            if (stl == null) return;
            UI_move.slider_moveZ.Maximum = 1000;

            model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
            UI_resize_advance.bboxnow = bbox.Size.x / Convert.ToDouble(objectPlacement.textScaleX.Text);
            UI_resize_advance.bboynow = bbox.Size.y / Convert.ToDouble(objectPlacement.textScaleY.Text);
            UI_resize_advance.bboznow = bbox.Size.z / Convert.ToDouble(objectPlacement.textScaleZ.Text);

            UI_resize_advance.gIsShow = true;
            UI_resize_advance.dimX = bbox.Size.x;
            UI_resize_advance.updateTxt(Enums.Axis.X);
            UI_resize_advance.dimY = bbox.Size.y;
            UI_resize_advance.updateTxt(Enums.Axis.Y);
            UI_resize_advance.dimZ = bbox.Size.z;
            UI_resize_advance.updateTxt(Enums.Axis.Z);

            if (Convert.ToDouble(objectPlacement.textRotX.Text) != 0 ||
                Convert.ToDouble(objectPlacement.textRotY.Text) != 0 ||
                Convert.ToDouble(objectPlacement.textRotZ.Text) != 0)
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

            OutofBound.Visibility = System.Windows.Visibility.Hidden;
            threedview.button_remove_Click(null, null);
            if (objectPlacement.listObjects.Items.Count > 0)
                objectPlacement.updateSTLState(objectPlacement.SingleSelectedModel);
            Focus();
        }

        private void zoomin_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            threedview.button_zoomIn_Click(null, null);
            Focus();
        }

        private void zoomout_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            threedview.button_zoomOut_Click(null, null);
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

        public static ManualResetEvent allDone = new ManualResetEvent(false);
    }

    public static class MouseDownHelper
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
        typeof(bool), typeof(MouseDownHelper), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnNotifyPropertyChanged)));

        private static void OnNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as UIElement;
            if (element != null && e.NewValue != null)
            {
                if ((bool)e.NewValue)
                {
                    Register(element);
                }
                else
                {
                    UnRegister(element);
                }
            }
        }

        private static void Register(UIElement element)
        {
            element.MouseDown += element_MouseDown;
            element.MouseLeftButtonDown += element_MouseLeftButtonDown;
            element.MouseLeave += element_MouseLeave;
            element.MouseUp += element_MouseUp;
        }

        private static void UnRegister(UIElement element)
        {
            element.MouseDown -= element_MouseDown;
            element.MouseLeftButtonDown -= element_MouseLeftButtonDown;
            element.MouseLeave -= element_MouseLeave;
            element.MouseUp -= element_MouseUp;
        }

        private static void element_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseDown(element, true);
            }
        }

        private static void element_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseLeftButtonDown(element, true);
            }
        }

        private static void element_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseDown(element, false);
                SetIsMouseLeftButtonDown(element, false);
            }
        }

        private static void element_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseDown(element, false);
                SetIsMouseLeftButtonDown(element, false);
            }
        }

        internal static readonly DependencyPropertyKey IsMouseDownPropertyKey = DependencyProperty.RegisterAttachedReadOnly("IsMouseDown",
        typeof(bool), typeof(MouseDownHelper), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty IsMouseDownProperty = IsMouseDownPropertyKey.DependencyProperty;

        internal static void SetIsMouseDown(UIElement element, bool value)
        {
            element.SetValue(IsMouseDownPropertyKey, value);
        }

        internal static readonly DependencyPropertyKey IsMouseLeftButtonDownPropertyKey = DependencyProperty.RegisterAttachedReadOnly("IsMouseLeftButtonDown",
        typeof(bool), typeof(MouseDownHelper), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty IsMouseLeftButtonDownProperty = IsMouseLeftButtonDownPropertyKey.DependencyProperty;

        internal static void SetIsMouseLeftButtonDown(UIElement element, bool value)
        {
            element.SetValue(IsMouseLeftButtonDownPropertyKey, value);
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

#nullable disable

namespace View3D.view
{
    public class BrushToFloatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                var color = brush.Color;
                // Convert 0-255 byte values to 0.0-1.0 floats
                float r = color.R / 255f;
                float g = color.G / 255f;
                float b = color.B / 255f;
                return $"{r:F2}, {g:F2}, {b:F2}";
            }
            return "0.00, 0.00, 0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class AppSettings
    {
        // Printer area diemnsions in millimeters.  These are used to draw the printer bed and frame,
        public uint PrintAreaWidth { get; set; } = 256;     // x-axis direction
        public uint PrintAreaDepth { get; set; } = 256;     // y-axis direction
        public uint PrintAreaHeight { get; set; } = 200;    // z-axis direction
        // <>

        // Initial OpenGL Client Size
        public int InitialClientSizeWidth { get; set; } = 1024;
        public int InitialClientSizeHeight { get; set; } = 768;
        // <>

        // Minimum OpenGL Client Size to prevent extremely small windows that can cause rendering issues
        public int MinClientSizeWidth { get; set; } = 830;
        public int MinClientSizeHeight { get; set; } = 700;
        // <>

        // UseVBOs and OpenGLVersion will be updated in OnLoad of ThreeDControl.
        public bool UseVBOs { get; set; } = false;
        public float OpenGLVersion { get; set; } = 1.0f; // Version for feature detection
        // <>

        public uint BackgroundTopColor { get; set; } = 0xFFF5F5F5;
        public uint BackgroundBottomColor { get; set; } = 0xFF000000;
        public uint FacesColor { get; set; } = 0xFF4169E1;
        public uint EdgesColor { get; set; } = 0xFFA9A9A9;
        public uint SelectedFacesColor { get; set; } = 0xFF6495ED;
        public uint PrinterBaseColor { get; set; } = 0xFFDCDCDC;
        public uint PrinterFrameColor { get; set; } = 0xFF000000;
        public uint OutsidePrintbedColor { get; set; } = 0xFFFF0000;

        public bool ShowEdges { get; set; } = false;
        public bool ShowFaces { get; set; } = true;
        public bool ShowPrintbed { get; set; } = true;

        public uint SelectionBoxColor { get; set; } = 0xFFFFFFFF;
        public uint ErrorModelColor { get; set; } = 0xFFFF0000;
        public uint InsideFacesColor { get; set; } = 0xFF000000;

        public uint ModelColor { get; set; } = 0xFF6BA3C6;
        
        public float KeyDirX { get; set; } = -0.6f;
        public float KeyDirY { get; set; } = 1.0f;
        public float KeyDirZ { get; set; } = 0.0f;
        public uint KeyColor { get; set; } = 0xFFFFFAF2;
        public float KeyStr { get; set; } = 1.8f;

        public float FillDirX { get; set; } = 0.8f;
        public float FillDirY { get; set; } = 0.3f;
        public float FillDirZ { get; set; } = 0.5f;

        public uint FillColor { get; set; } = 0xFFCCE0FF;
        public float FillStr { get; set; } = 1.2f;

        public float BackDirX { get; set; } = 0.1f;
        public float BackDirY { get; set; } = -0.5f;
        public float BackDirZ { get; set; } = -1.0f;
        public uint BackColor { get; set; } = 0xFFE6EBFF;
        public float BackStr { get; set; } = 0.9f;

        public uint SkyColor { get; set; } = 0xFF99B2E6;
        public uint GroundColor { get; set; } = 0xFF40332E;
        public float AmbientStr { get; set; } = 1.2f;
    }

    public class SettingsService
    {
        private static readonly SettingsService _instance = new SettingsService();
        public static SettingsService Instance => _instance;

        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions;
        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;  // EX: OpenGL3DViewerNET10

            // Store in %AppData%\MyApp\settings.json (Windows)
            // or ~/.config/MyApp/settings.json (Linux/macOS)
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var appFolder = Path.Combine(appDataFolder, assemblyName);

            // Create folder if it doesn't exist
            Directory.CreateDirectory(appFolder);

            _settingsPath = Path.Combine(appFolder, "Settings.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            Settings = Load();
        }

        /// <summary>
        /// Loads settings from disk. Returns defaults if file doesn't exist.
        /// </summary>
        private AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Console.WriteLine("No settings file found. Using defaults.");
                    return new AppSettings();
                }

                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
                       ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load settings: {ex.Message}. Using defaults.");
                return new AppSettings();
            }
        }

        /// <summary>
        /// Saves current settings to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(SettingsService.Instance.Settings, _jsonOptions);
                File.WriteAllText(_settingsPath, json);
                Debug.WriteLine($"Settings saved to: {_settingsPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets settings back to defaults and saves.
        /// </summary>
        public void Reset()
        {
            Settings = new AppSettings();
            Save();
            Console.WriteLine("Settings reset to defaults.");
        }

        public string GetSettingsPath() => _settingsPath;
    }

    public partial class ThreeDSettings : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        private bool _isInitializing = true;

        // ── Constructor ──────────────────────────────────────────────────────────
        public ThreeDSettings()
        {
            _isInitializing = true;

            InitializeComponent();

            MainWindow.main.languageChanged += translate;

            loadSettings();

            _isInitializing = false;
        }

        public void translate()
        {
            // Localisation hook — populate as needed.
        }

        void loadSettings()
        {
            try
            {
                txtPrintAreaWidth.TextChanged -= PrintAreaWidth_TextChanged; // Temporarily detach event handlers to prevent triggering on load
                txtPrintAreaDepth.TextChanged -= PrintAreaDepth_TextChanged;
                txtPrintAreaHeight.TextChanged -= PrintAreaHeight_TextChanged;

                txtClientSizeWidth.TextChanged -= ClientSizeWidth_TextChanged;
                txtClientSizeHeight.TextChanged -= ClientSizeHeight_TextChanged;

                keyDirX.TextChanged -= LightSetting_ValueChanged;
                keyDirY.TextChanged -= LightSetting_ValueChanged;
                keyDirY.TextChanged -= LightSetting_ValueChanged;
                keyStr.ValueChanged -= LightSetting_ValueChanged;
                
                fillDirX.TextChanged -= LightSetting_ValueChanged;
                fillDirY.TextChanged -= LightSetting_ValueChanged;
                fillDirZ.TextChanged -= LightSetting_ValueChanged;
                fillStr.ValueChanged -= LightSetting_ValueChanged;
                
                backDirX.TextChanged -= LightSetting_ValueChanged;
                backDirY.TextChanged -= LightSetting_ValueChanged;
                backDirZ.TextChanged -= LightSetting_ValueChanged;
                backStr.ValueChanged -= LightSetting_ValueChanged;

                ambientStr.ValueChanged -= LightSetting_ValueChanged;

                //=================================================================================================

                txtPrintAreaWidth.Text = SettingsService.Instance.Settings.PrintAreaWidth.ToString();
                txtPrintAreaDepth.Text = SettingsService.Instance.Settings.PrintAreaDepth.ToString();
                txtPrintAreaHeight.Text = SettingsService.Instance.Settings.PrintAreaHeight.ToString();

                txtClientSizeWidth.Text = SettingsService.Instance.Settings.InitialClientSizeWidth.ToString();
                txtClientSizeHeight.Text = SettingsService.Instance.Settings.InitialClientSizeHeight.ToString();

                backgroundTop.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.BackgroundTopColor));
                backgroundBottom.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.BackgroundBottomColor));
                faces.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.FacesColor));
                edges.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.EdgesColor));
                selectedFaces.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.SelectedFacesColor));
                printerBase.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.PrinterBaseColor));
                printerFrame.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.PrinterFrameColor));
                outsidePrintbed.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.OutsidePrintbedColor));

                showEdges.IsChecked = SettingsService.Instance.Settings.ShowEdges;
                showFaces.IsChecked = SettingsService.Instance.Settings.ShowFaces;
                showPrintbed.IsChecked  = SettingsService.Instance.Settings.ShowPrintbed;

                selectionBox.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.SelectionBoxColor));
                errorModel.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.ErrorModelColor));
                insideFaces.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.InsideFacesColor));

                modelColor.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.ModelColor));

                keyDirX.Text = SettingsService.Instance.Settings.KeyDirX.ToString();
                keyDirY.Text = SettingsService.Instance.Settings.KeyDirY.ToString();
                keyDirZ.Text = SettingsService.Instance.Settings.KeyDirZ.ToString();
                keyColor.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.KeyColor));
                keyStr.Value = SettingsService.Instance.Settings.KeyStr;

                fillDirX.Text = SettingsService.Instance.Settings.FillDirX.ToString();
                fillDirY.Text = SettingsService.Instance.Settings.FillDirY.ToString();
                fillDirZ.Text = SettingsService.Instance.Settings.FillDirZ.ToString();
                fillColor.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.FillColor));
                fillStr.Value = SettingsService.Instance.Settings.FillStr;

                backDirX.Text = SettingsService.Instance.Settings.BackDirX.ToString();
                backDirY.Text = SettingsService.Instance.Settings.BackDirY.ToString();
                backDirZ.Text = SettingsService.Instance.Settings.BackDirZ.ToString();
                backColor.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.BackColor));
                backStr.Value = SettingsService.Instance.Settings.BackStr;

                skyColor.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.SkyColor));
                groundColor.Background = new SolidColorBrush(ArgbToColor(SettingsService.Instance.Settings.GroundColor));
                ambientStr.Value = SettingsService.Instance.Settings.AmbientStr;

                //=================================================================================================

                txtPrintAreaWidth.TextChanged += PrintAreaWidth_TextChanged; // Temporarily detach event handlers to prevent triggering on load
                txtPrintAreaDepth.TextChanged += PrintAreaDepth_TextChanged;
                txtPrintAreaHeight.TextChanged += PrintAreaHeight_TextChanged;

                txtClientSizeWidth.TextChanged += ClientSizeWidth_TextChanged;
                txtClientSizeHeight.TextChanged += ClientSizeHeight_TextChanged;

                keyDirX.TextChanged += LightSetting_ValueChanged;
                keyDirY.TextChanged += LightSetting_ValueChanged;
                keyDirY.TextChanged += LightSetting_ValueChanged;
                keyStr.ValueChanged += LightSetting_ValueChanged;

                fillDirX.TextChanged += LightSetting_ValueChanged;
                fillDirY.TextChanged += LightSetting_ValueChanged;
                fillDirZ.TextChanged += LightSetting_ValueChanged;
                fillStr.ValueChanged += LightSetting_ValueChanged;

                backDirX.TextChanged += LightSetting_ValueChanged;
                backDirY.TextChanged += LightSetting_ValueChanged;
                backDirZ.TextChanged += LightSetting_ValueChanged;
                backStr.ValueChanged += LightSetting_ValueChanged;

                ambientStr.ValueChanged += LightSetting_ValueChanged;
            }
            catch { }
        }

        // ── Color-swatch helpers ─────────────────────────────────────────────────

        /// <summary>Return the ARGB int of a swatch Border's SolidColorBrush background.</summary>
        private static uint ToArgb(Border b)
        {
            if (b.Background is SolidColorBrush scb)
            {
                var c = scb.Color;
                return (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B);
            }
            return 0;
        }
   
        private static Color ArgbToColor(uint argb)
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >>  8) & 0xFF);
            byte b = (byte)( argb        & 0xFF);
            return System.Windows.Media.Color.FromArgb(a, r, g, b);
        }

        // ── Color picker (replaces WinForms ColorDialog) ─────────────────────────
        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
                PickColor(border);
        }

        void PickColor(Border border)
        {
            // Get current color from the border's background
            Color initialColor = Colors.White;
            if (border.Background is SolidColorBrush scb)
            {
                initialColor = scb.Color;
            }

            // Create WPF Color Picker Window
            var colorPickerWindow = new Window
            {
                Title = "Pick a Color",
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(border),
                ResizeMode = ResizeMode.NoResize
            };

            // Selected color (will be updated on confirm)
            System.Windows.Media.Color selectedColor = initialColor;

            // --- Layout ---
            var mainStack = new StackPanel { Margin = new Thickness(15) };

            // Preview Box
            var previewBorder = new Border
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(initialColor)
            };
            mainStack.Children.Add(previewBorder);

            // --- RGB + Hex Inputs ---
            byte r = initialColor.R, g = initialColor.G, b = initialColor.B, a = initialColor.A;

            // Helper: rebuild color and update preview
            Action updatePreview = null;

            // Hex Input
            var hexBox = new TextBox
            {
                Text = $"#{a:X2}{r:X2}{g:X2}{b:X2}",
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(4)
            };

            // Sliders for A, R, G, B
            Slider MakeSlider(byte value) => new Slider
            {
                Minimum = 0,
                Maximum = 255,
                Value = value,
                TickFrequency = 1,
                IsSnapToTickEnabled = true
            };

            TextBox MakeValueBox(byte value) => new TextBox
            {
                Text = value.ToString(),
                Width = 40,
                Padding = new Thickness(2),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var sliderA = MakeSlider(a); var boxA = MakeValueBox(a);
            var sliderR = MakeSlider(r); var boxR = MakeValueBox(r);
            var sliderG = MakeSlider(g); var boxG = MakeValueBox(g);
            var sliderB = MakeSlider(b); var boxB = MakeValueBox(b);

            // Sync slider <-> textbox <-> preview
            bool updating = false;
            updatePreview = () =>
            {
                if (updating) return;
                updating = true;
                selectedColor = System.Windows.Media.Color.FromArgb(
                    (byte)sliderA.Value, (byte)sliderR.Value,
                    (byte)sliderG.Value, (byte)sliderB.Value);
                previewBorder.Background = new SolidColorBrush(selectedColor);
                boxA.Text = ((byte)sliderA.Value).ToString();
                boxR.Text = ((byte)sliderR.Value).ToString();
                boxG.Text = ((byte)sliderG.Value).ToString();
                boxB.Text = ((byte)sliderB.Value).ToString();
                hexBox.Text = $"#{(byte)sliderA.Value:X2}{(byte)sliderR.Value:X2}" +
                              $"{(byte)sliderG.Value:X2}{(byte)sliderB.Value:X2}";
                updating = false;

                // Allow live preview of changes without needing to click OK.
                border.Background = new SolidColorBrush(selectedColor);
                controlsToSettings();
                MainWindow.main.threeDControl.UpdateChanges();
                // <END>
            };

            void BindSliderBox(Slider slider, TextBox box)
            {
                slider.ValueChanged += (_, __) => updatePreview();
                box.TextChanged += (_, __) =>
                {
                    if (updating) return;
                    if (byte.TryParse(box.Text, out byte val))
                    {
                        updating = true;
                        slider.Value = val;
                        updating = false;
                        updatePreview();
                    }
                };
            }

            BindSliderBox(sliderA, boxA);
            BindSliderBox(sliderR, boxR);
            BindSliderBox(sliderG, boxG);
            BindSliderBox(sliderB, boxB);

            hexBox.TextChanged += (_, __) =>
            {
                if (updating) return;
                var hex = hexBox.Text.TrimStart('#');
                if (hex.Length == 8 &&
                    byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte ha) &&
                    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte hr) &&
                    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte hg) &&
                    byte.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out byte hb))
                {
                    updating = true;
                    sliderA.Value = ha; sliderR.Value = hr;
                    sliderG.Value = hg; sliderB.Value = hb;
                    updating = false;
                    updatePreview();
                }
            };

            // Row builder helper
            Grid MakeRow(string label, Slider slider, TextBox box)
            {
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(slider, 1);
                Grid.SetColumn(box, 2);

                grid.Children.Add(lbl);
                grid.Children.Add(slider);
                grid.Children.Add(box);
                return grid;
            }

            mainStack.Children.Add(new TextBlock { Text = "Hex (AARRGGBB):", FontWeight = FontWeights.Bold });
            mainStack.Children.Add(hexBox);
            mainStack.Children.Add(MakeRow("A", sliderA, boxA));
            mainStack.Children.Add(MakeRow("R", sliderR, boxR));
            mainStack.Children.Add(MakeRow("G", sliderG, boxG));
            mainStack.Children.Add(MakeRow("B", sliderB, boxB));

            // OK / Cancel buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var okBtn = new Button
            {
                Content = "OK",
                Width = 75,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(4),
                IsDefault = true
            };

            okBtn.Click += (_, __) => { colorPickerWindow.Close(); };

            btnPanel.Children.Add(okBtn);
            mainStack.Children.Add(btnPanel);

            colorPickerWindow.Content = mainStack;
            colorPickerWindow.ShowDialog();
        }

        // ── Event handlers ───────────────────────────────────────────────────────
        private void CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (sender == showEdges)
                SettingsService.Instance.Settings.ShowEdges = showEdges.IsChecked == true;
            else if (sender == showFaces)
                SettingsService.Instance.Settings.ShowFaces = showFaces.IsChecked == true;
            else if (sender == showPrintbed)
                SettingsService.Instance.Settings.ShowPrintbed = showPrintbed.IsChecked == true;

            if (MainWindow.main.threeDControl != null)
                MainWindow.main.threeDControl.UpdateChanges();
        }

        /// <summary>
        /// Validates that the TextBox contains a valid float.
        /// Mirrors WinForms float_Validating / ErrorProvider pattern using a red border.
        /// </summary>
        private void float_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                bool valid = float.TryParse(tb.Text, out _);
                tb.BorderBrush = valid
                    ? SystemColors.ControlDarkBrush
                    : Brushes.Red;
                tb.ToolTip = valid ? null : "Not a number";
            }
        }

        private void uint_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                bool valid = uint.TryParse(tb.Text, out _);
                tb.BorderBrush = valid
                    ? SystemColors.ControlDarkBrush
                    : Brushes.Red;
                tb.ToolTip = valid ? null : "Not a number";
            }
        }

        private void ThreeDSettings_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true; // Prevent the window from actually closing
            this.Hide();
        }

        /// <summary>
        /// Converts a WPF SolidColorBrush to a WinForms System.Drawing.Color.
        /// Returns Color.Empty or throws if the brush is not a SolidColorBrush.
        /// </summary>
        public System.Drawing.Color ToDrawingColor(Brush wpfBrush)
        {
            if (wpfBrush is SolidColorBrush solidBrush)
            {
                Color c = solidBrush.Color;
                return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }

            // Alternative: Handle Gradients by taking the first stop, 
            // or throw an exception based on your architectural requirements.
            throw new InvalidOperationException("Only SolidColorBrush can be converted to a single Color.");
        }

        // Slider values changed.
        private void LightSetting_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;

            if (sender is Slider slider)
            {
                if (slider == keyStr)
                    SettingsService.Instance.Settings.KeyStr = (float)slider.Value;
                else if (slider == fillStr)
                    SettingsService.Instance.Settings.FillStr = (float)slider.Value;
                else if (slider == backStr)
                    SettingsService.Instance.Settings.BackStr = (float)slider.Value;
                else if (slider == ambientStr)
                    SettingsService.Instance.Settings.AmbientStr = (float)slider.Value; 
            }

            if (MainWindow.main.threeDControl != null)
                MainWindow.main.threeDControl.UpdateChanges();
        }

        // TextBox values changed.
        private void LightSetting_ValueChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (sender is TextBox tb) 
            {
                float value = 0;

                if (tb == keyDirX && float.TryParse(keyDirX.Text, out value)) SettingsService.Instance.Settings.KeyDirX = value;
                else if (tb == keyDirY && float.TryParse(keyDirY.Text, out value)) SettingsService.Instance.Settings.KeyDirY = value;
                else if (tb == keyDirZ && float.TryParse(keyDirZ.Text, out value)) SettingsService.Instance.Settings.KeyDirZ = value;
                else if (tb == fillDirX && float.TryParse(fillDirX.Text, out value)) SettingsService.Instance.Settings.FillDirX = value;
                else if (tb == fillDirY && float.TryParse(fillDirY.Text, out value)) SettingsService.Instance.Settings.FillDirY = value;
                else if (tb == fillDirZ && float.TryParse(fillDirZ.Text, out value)) SettingsService.Instance.Settings.FillDirZ = value;
                else if (tb == backDirX && float.TryParse(backDirX.Text, out value)) SettingsService.Instance.Settings.BackDirX = value;
                else if (tb == backDirY && float.TryParse(backDirY.Text, out value)) SettingsService.Instance.Settings.BackDirY = value;
                else if (tb == backDirZ && float.TryParse(backDirZ.Text, out value)) SettingsService.Instance.Settings.BackDirZ = value;
        
                if (MainWindow.main.threeDControl != null)
                    MainWindow.main.threeDControl.UpdateChanges();
            }
        }

        /*  Light default settings
            // --- Three-point studio rig ---
            const vec3  keyDir   = normalize(vec3(-0.6, 1.0, 0.8));
            const vec3  keyColor = vec3(1.00, 0.98, 0.95);                  //#FFFAF2
            const float keyStr   = 1.8;

            const vec3  fillDir   = normalize(vec3(0.8, 0.3, 0.5));
            const vec3  fillColor = vec3(0.80, 0.88, 1.00);                 //#CCE0FF
            const float fillStr   = 1.2;

            const vec3  backDir   = normalize(vec3(0.1, -0.5, -1.0));
            const vec3  backColor = vec3(0.90, 0.92, 1.00);                 //#E6EBFF
            const float backStr   = 0.9;

            // --- Hemisphere ambient ---
            const vec3  skyColor    = vec3(0.60, 0.70, 0.90);               //#99B2E6
            const vec3  groundColor = vec3(0.25, 0.20, 0.18);               //#40332E
            const float ambientStr  = 1.2;
         */

        private void ResetLightSettingsToDefault_Click(object sender, RoutedEventArgs e)
        {
            AppSettings defaultSettings = new AppSettings();

            keyDirX.Text = defaultSettings.KeyDirX.ToString();
            keyDirY.Text = defaultSettings.KeyDirY.ToString();
            keyDirZ.Text = defaultSettings.KeyDirZ.ToString();
            keyColor.Background = new SolidColorBrush(ArgbToColor(defaultSettings.KeyColor));
            keyStr.Value = defaultSettings.KeyStr;

            fillDirX.Text = defaultSettings.FillDirX.ToString();
            fillDirY.Text = defaultSettings.FillDirY.ToString();
            fillDirZ.Text = defaultSettings.FillDirZ.ToString();
            fillColor.Background = new SolidColorBrush(ArgbToColor(defaultSettings.FillColor));
            fillStr.Value = defaultSettings.FillStr;
         
            backDirX.Text = defaultSettings.BackDirX.ToString();
            backDirY.Text = defaultSettings.BackDirY.ToString();
            backDirZ.Text = defaultSettings.BackDirZ.ToString();
            backColor.Background = new SolidColorBrush(ArgbToColor(defaultSettings.BackColor));
            backStr.Value = defaultSettings.BackStr;

            skyColor.Background = new SolidColorBrush(ArgbToColor(defaultSettings.SkyColor));
            groundColor.Background = new SolidColorBrush(ArgbToColor(defaultSettings.GroundColor));
            ambientStr.Value = defaultSettings.AmbientStr;
        }

        private void ThreeDSettings_Closed(object sender, EventArgs e)
        {
            SettingsService.Instance.Save();
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.Instance.Reset();
            loadSettings();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void PrintAreaWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (uint.TryParse(txtPrintAreaWidth.Text, out uint value))
                SettingsService.Instance.Settings.PrintAreaWidth = value;
        }

        private void PrintAreaDepth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (uint.TryParse(txtPrintAreaDepth.Text, out uint value))
                SettingsService.Instance.Settings.PrintAreaDepth = value;
        }

        private void PrintAreaHeight_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (uint.TryParse(txtPrintAreaHeight.Text, out uint value))
                SettingsService.Instance.Settings.PrintAreaHeight = value;
        }

        private void ClientSizeWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtClientSizeWidth.Text, out int value))
                SettingsService.Instance.Settings.InitialClientSizeWidth = value;
        }

        private void ClientSizeHeight_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtClientSizeHeight.Text, out int value))
                SettingsService.Instance.Settings.InitialClientSizeHeight = value;
        }

        // Updates the SettingsService with current 'Border' control values. Called after color pickers and light setting changes.
        // Other controls update SettingsService directly in their event handlers, so they don't need to call this method.
        void controlsToSettings()
        {
            try
            {
                SettingsService.Instance.Settings.BackgroundTopColor = ToArgb(backgroundTop);
                SettingsService.Instance.Settings.BackgroundBottomColor = ToArgb(backgroundBottom);
                SettingsService.Instance.Settings.FacesColor = ToArgb(faces);
                SettingsService.Instance.Settings.EdgesColor = ToArgb(edges);
                SettingsService.Instance.Settings.SelectedFacesColor = ToArgb(selectedFaces);
                SettingsService.Instance.Settings.PrinterBaseColor = ToArgb(printerBase);
                SettingsService.Instance.Settings.PrinterFrameColor = ToArgb(printerFrame);
                SettingsService.Instance.Settings.OutsidePrintbedColor = ToArgb(outsidePrintbed);

                SettingsService.Instance.Settings.SelectionBoxColor = ToArgb(selectionBox);
                SettingsService.Instance.Settings.ErrorModelColor = ToArgb(errorModel);
                SettingsService.Instance.Settings.InsideFacesColor = ToArgb(insideFaces);

                SettingsService.Instance.Settings.ModelColor = ToArgb(modelColor);

                SettingsService.Instance.Settings.KeyColor = ToArgb(keyColor);
                SettingsService.Instance.Settings.FillColor = ToArgb(fillColor);
                SettingsService.Instance.Settings.BackColor = ToArgb(backColor);

                SettingsService.Instance.Settings.SkyColor = ToArgb(skyColor);
                SettingsService.Instance.Settings.GroundColor = ToArgb(groundColor);
            }
            catch { }
        }


    }
}

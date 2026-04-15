using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using View3D.model.geom;

namespace View3D.view
{
    public class AppSettings
    {
        private static readonly AppSettings _instance = new AppSettings();
        public static AppSettings Instance => _instance;


        public uint PrintAreaWidth { get; set; } = 256;     // x-axis direction
        public uint PrintAreaDepth { get; set; } = 256;     // y-axis direction
        public uint PrintAreaHeight { get; set; } = 200;    // z-axis direction

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
        public int DrawMethod { get; set; } = 0;

        public uint SelectionBoxColor { get; set; } = 0xFFFFFFFF;
        public uint ErrorModelColor { get; set; } = 0xFFFF0000;
        public uint InsideFacesColor { get; set; } = 0xFF000000;

        public string Light1X { get; set; } = "1";
        public string Light1Y { get; set; } = "0.5";
        public string Light1Z { get; set; } = "2";

        public uint ModelColor { get; set; } = 0xFF6BA3C6;
    }

    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public AppSettings Settings { get; private set; }

        public SettingsService(string appName)
        {
            // Store in %AppData%\MyApp\settings.json (Windows)
            // or ~/.config/MyApp/settings.json (Linux/macOS)
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var appFolder = Path.Combine(appDataFolder, appName);

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
                var json = JsonSerializer.Serialize(Settings, _jsonOptions);
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
        SettingsService settingsService = null;

        // ── Fields ───────────────────────────────────────────────────────────────
        int drawMethod = 0;         // 0 = elements, 1 = drawElements, 2 = VBO
        private bool _showEdges = false;
        private bool _showFaces = true;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        // ── Constructor ──────────────────────────────────────────────────────────
        public ThreeDSettings()
        {
            InitializeComponent();

            string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;  // EX: OpenGL3DViewerNET10
            settingsService = new SettingsService(assemblyName);

            loadSettings();

            ResetLightSettingsToDefault_Click(null, null);
            MainWindow.main.languageChanged += translate;
        }

        public void translate()
        {
            // Localisation hook — populate as needed.
        }

        // ── ShowEdges / ShowFaces properties (INotifyPropertyChanged + registry) ─
        public bool ShowEdges
        {
            get => _showEdges;
            set
            {
                if (value == _showEdges) return;
                _showEdges = value;
                settingsService.Settings.ShowEdges = _showEdges;

                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowEdges)));
                MainWindow.main.Update3D();
            }
        }

        public bool ShowFaces
        {
            get => _showFaces;
            set
            {
                if (value == _showFaces) return;
                _showFaces = value;
                settingsService.Settings.ShowFaces = _showFaces;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowFaces)));
                MainWindow.main.Update3D();
            }
        }


        void loadSettings()
        {
            try
            {
                txtPrintAreaWidth.Text = settingsService.Settings.PrintAreaWidth.ToString();
                txtPrintAreaDepth.Text = settingsService.Settings.PrintAreaDepth.ToString();
                txtPrintAreaHeight.Text = settingsService.Settings.PrintAreaHeight.ToString();

                backgroundTop.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.BackgroundTopColor));
                backgroundBottom.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.BackgroundBottomColor));
                faces.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.FacesColor));
                edges.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.EdgesColor));
                selectedFaces.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.SelectedFacesColor));
                printerBase.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.PrinterBaseColor));
                printerFrame.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.PrinterFrameColor));
                outsidePrintbed.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.OutsidePrintbedColor));


                _showEdges = settingsService.Settings.ShowEdges;
                _showFaces = settingsService.Settings.ShowFaces;

                showPrintbed.IsChecked  = settingsService.Settings.ShowPrintbed;

                comboDrawMethod.SelectedIndex = settingsService.Settings.DrawMethod;

                selectionBox.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.SelectionBoxColor));
                errorModel.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.ErrorModelColor));
                insideFaces.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.InsideFacesColor));

                xdir1.Text = settingsService.Settings.Light1X;
                ydir1.Text = settingsService.Settings.Light1Y;
                zdir1.Text = settingsService.Settings.Light1Z;

                modelColor.Background = new SolidColorBrush(ArgbToColor(settingsService.Settings.ModelColor));
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

        private void PickColor(Border border)
        {
            // Get current color from the border's background
            System.Windows.Media.Color initialColor = System.Windows.Media.Colors.White;
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
                MainWindow.main.Update3D();
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

            bool confirmed = false;
            var okBtn = new Button
            {
                Content = "OK",
                Width = 75,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(4),
                IsDefault = true
            };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 75,
                Padding = new Thickness(4),
                IsCancel = true
            };

            okBtn.Click += (_, __) => { confirmed = true; colorPickerWindow.Close(); };
            cancelBtn.Click += (_, __) => { colorPickerWindow.Close(); };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            mainStack.Children.Add(btnPanel);

            colorPickerWindow.Content = mainStack;
            colorPickerWindow.ShowDialog();

            if (confirmed)
            {
                border.Background = new SolidColorBrush(selectedColor);
                MainWindow.main.Update3D();
            }
        }

        // ── Event handlers ───────────────────────────────────────────────────────

        /// <summary>
        /// Single unified handler that mirrors all the WinForms CheckedChanged /
        /// showEdges_CheckedChanged events that simply called MainWindow.main.Update3D().
        /// Also syncs the ShowEdges / ShowFaces backing properties when those
        /// checkboxes are the source.
        /// </summary>
        private void showEdges_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender == showEdges)
                ShowEdges = showEdges.IsChecked == true;
            else if (sender == showFaces)
                ShowFaces = showFaces.IsChecked == true;
            else
                MainWindow.main.Update3D();
        }

        /// <summary>
        /// Validates that the TextBox contains a valid float.
        /// Mirrors WinForms float_Validating / ErrorProvider pattern using a red border.
        /// </summary>
        private void float_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                bool valid = float.TryParse(tb.Text, out _);
                //tb.BorderBrush = valid
                //    ? SystemColors.ControlDarkBrush
                //    : Brushes.Red;
                //tb.ToolTip = valid ? null : Trans.T("L_NOT_A_NUMBER");
            }
        }

        private void ThreeDSettings_Closing(object sender, CancelEventArgs e)
        {
            //RegMemory.StoreWindowPos("threeDSettingsWindow", this, false, false);

            e.Cancel = true; // Prevent the window from actually closing
            this.Hide();
        }

        // ── OpenGL colour helpers ─────────────────────────────────────────────────

        /// <summary>Convert a WPF SolidColorBrush swatch to a normalised OpenGL float[4].</summary>
        private static float[] ToGLColor(Border b)
        {
            if (b.Background is SolidColorBrush scb)
            {
                var c = scb.Color;
                return new float[] { c.R / 255f, c.G / 255f, c.B / 255f, 1f };
            }
            return new float[] { 0f, 0f, 0f, 1f };
        }

        // ── Light direction helpers ───────────────────────────────────────────────

        private static float[] ToDir(System.Windows.Controls.TextBox x, System.Windows.Controls.TextBox y, System.Windows.Controls.TextBox z)
        {
            float.TryParse(x.Text, out float xf);
            float.TryParse(y.Text, out float yf);
            float.TryParse(z.Text, out float zf);
            return new float[] { xf, yf, zf, 0f };
        }

        // ── Public API (identical signatures to the original) ────────────────────
      
        public float[] LightDirection()
        {
            float[] output = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToDir(xdir1, ydir1, zdir1);
            });
            return output;
        }

        public float[] LightColor()
        {
            float[] output = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(lightColor);
            });
            return output;
        }

        public float[] ModelColor()
        {
            float[] output = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(modelColor);
            });
            return output;
        }

        public float GetAmbientIntensity()
        {
            float output = 0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                output = (float)sliderAmbient.Value;
            });
            return output;
        }

        public float GetSpecularIntensity()
        {
            float output = 0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                output = (float)sliderSpecular.Value;
            });
            return output;
        }

        public float GetShininess()
        {
            float output = 0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                output = (float)sliderShininess.Value;
            });
            return output;
        }

        // --------------------------------------------------------------------------------------------

        public System.Drawing.Color InsideFacesBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(insideFaces.Background);
            });
            return color;
        }

        public System.Drawing.Color ErrorModelBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(errorModel.Background);
            });
            return color;
        }

        public System.Drawing.Color ErrorModelEdgeBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(errorModelEdge.Background);
            });
            return color;
        }
        public System.Drawing.Color OutsidePrintbedBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(outsidePrintbed.Background);
            });
            return color;
        }

        public System.Drawing.Color EdgesLoopBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(edges.Background);
            });
            return color;
        }
        public System.Drawing.Color CutFacesBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(cutFaces.Background);
            });
            return color;
        }
        public System.Drawing.Color EdgesBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(edges.Background);
            });
            return color;
        }
        public System.Drawing.Color SelectionBoxBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(selectionBox.Background);
            });
            return color;
        }
        public System.Drawing.Color PrinterFrameBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(printerFrame.Background);
            });
            return color;
        }
        public System.Drawing.Color PrinterBaseBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(printerBase.Background);
            });
            return color;
        }
        public System.Drawing.Color BackgroundTopBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(backgroundTop.Background);
            });
            return color;
        }
        public System.Drawing.Color BackgroundBottomBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                color = ToDrawingColor(backgroundBottom.Background);
            });
            return color;
        }


        /// <summary>
        /// Converts a WPF SolidColorBrush to a WinForms System.Drawing.Color.
        /// Returns Color.Empty or throws if the brush is not a SolidColorBrush.
        /// </summary>
        public System.Drawing.Color ToDrawingColor(System.Windows.Media.Brush wpfBrush)
        {
            if (wpfBrush is System.Windows.Media.SolidColorBrush solidBrush)
            {
                System.Windows.Media.Color c = solidBrush.Color;
                return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }

            // Alternative: Handle Gradients by taking the first stop, 
            // or throw an exception based on your architectural requirements.
            throw new InvalidOperationException("Only SolidColorBrush can be converted to a single Color.");
        }

        public bool IsPrintbed()
        {
            bool result = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                result = showPrintbed.IsChecked == true;
            });
            return result;
        }
        private void LightSetting_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MainWindow.main.Update3D();
        }
        private void LightSetting_ValueChanged(object sender, TextChangedEventArgs e)
        {
            MainWindow.main.Update3D();
        }

        private void ResetLightSettingsToDefault_Click(object sender, RoutedEventArgs e)
        {
            AppSettings defaultSettings = new AppSettings();

            xdir1.Text = defaultSettings.Light1X.ToString();
            ydir1.Text = defaultSettings.Light1Y.ToString();
            zdir1.Text = defaultSettings.Light1Z.ToString();

            lightColor.Background = Brushes.White;

            modelColor.Background = new SolidColorBrush(ArgbToColor(defaultSettings.ModelColor));

            sliderAmbient.Value = 0.3;
            sliderSpecular.Value = 0.3;
            sliderShininess.Value = 16;

            MainWindow.main.Update3D();
        }

        private void ThreeDSettings_Closed(object sender, EventArgs e)
        {
            Debug.WriteLine("Application exiting — saving settings...");
            controlsToSettings();
            settingsService.Save();
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            settingsService.Reset();
            loadSettings();
            MainWindow.main.Update3D();
        }

        private void PrintAreaWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (uint.TryParse(txtPrintAreaWidth.Text, out uint value))
                AppSettings.Instance.PrintAreaWidth = value;
        }

        private void PrintAreaDepth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (uint.TryParse(txtPrintAreaDepth.Text, out uint value))
                AppSettings.Instance.PrintAreaDepth = value;
        }

        private void PrintAreaHeight_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (uint.TryParse(txtPrintAreaHeight.Text, out uint value))
                AppSettings.Instance.PrintAreaHeight = value;
        }

        void controlsToSettings()
        {
            try
            {
                uint value;
                if (uint.TryParse(txtPrintAreaWidth.Text, out value)) settingsService.Settings.PrintAreaWidth = value;
                if (uint.TryParse(txtPrintAreaDepth.Text, out value)) settingsService.Settings.PrintAreaDepth = value;
                if (uint.TryParse(txtPrintAreaHeight.Text, out value)) settingsService.Settings.PrintAreaHeight = value;

                settingsService.Settings.BackgroundTopColor = ToArgb(backgroundTop);
                settingsService.Settings.BackgroundBottomColor = ToArgb(backgroundBottom);
                settingsService.Settings.FacesColor = ToArgb(faces);
                settingsService.Settings.EdgesColor = ToArgb(edges);
                settingsService.Settings.SelectedFacesColor = ToArgb(selectedFaces);
                settingsService.Settings.PrinterBaseColor = ToArgb(printerBase);
                settingsService.Settings.PrinterFrameColor = ToArgb(printerFrame);
                settingsService.Settings.OutsidePrintbedColor = ToArgb(outsidePrintbed);

                settingsService.Settings.ShowEdges = _showEdges;
                settingsService.Settings.ShowFaces = _showFaces;
                settingsService.Settings.ShowPrintbed = (showPrintbed.IsChecked == true ? true : false);

                settingsService.Settings.DrawMethod = comboDrawMethod.SelectedIndex;
                settingsService.Settings.SelectionBoxColor = ToArgb(selectionBox);
                settingsService.Settings.ErrorModelColor = ToArgb(errorModel);
                settingsService.Settings.InsideFacesColor = ToArgb(insideFaces);

                settingsService.Settings.InsideFacesColor = ToArgb(insideFaces);

                settingsService.Settings.Light1X = xdir1.Text;
                settingsService.Settings.Light1Y = ydir1.Text;
                settingsService.Settings.Light1Z = zdir1.Text;
            }
            catch { }
        }
    }
}

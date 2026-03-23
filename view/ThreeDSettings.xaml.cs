using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using View3D.model;
using View3D.model.geom;

namespace View3D.view
{
    public partial class ThreeDSettings : Window, INotifyPropertyChanged
    {
        public float PrintAreaWidth = 256;  // x-axis direction
        public float PrintAreaDepth = 256;  // y-axis direction
        public float PrintAreaHeight = 200; // z-axis direction

        // UseVBOs and OpenGLVersion will be updated in OnLoad of ThreeDControl.
        public bool UseVBOs = false;
        public float OpenGLVersion = 1.0f; // Version for feature detection

        // ── Fields ───────────────────────────────────────────────────────────────
        private RegistryKey threedKey = null;
        public int drawMethod = 0;         // 0 = elements, 1 = drawElements, 2 = VBO
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
            comboDrawMethod.SelectedIndex = 0; // Autodetect best
            RegistryToForm();
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
                threedKey?.SetValue("showEdges", _showEdges ? 1 : 0);
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
                threedKey?.SetValue("showFaces", _showFaces ? 1 : 0);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowFaces)));
                MainWindow.main.Update3D();
            }
        }

        // ── Registry persistence ─────────────────────────────────────────────────

        /// <summary>Persist all current UI values to the registry.</summary>
        public void FormToRegistry()
        {
            if (threedKey == null) return;
            try
            {
                threedKey.SetValue("backgroundTopColor",    ToArgb(backgroundTop));
                threedKey.SetValue("backgroundBottomColor", ToArgb(backgroundBottom));
                threedKey.SetValue("facesColor",            ToArgb(faces));
                threedKey.SetValue("edgesColor",            ToArgb(edges));
                threedKey.SetValue("selectedFacesColor",    ToArgb(selectedFaces));
                threedKey.SetValue("printerBaseColor",      ToArgb(printerBase));
                threedKey.SetValue("printerFrameColor",     ToArgb(printerFrame));
                threedKey.SetValue("outsidePrintbedColor",  ToArgb(outsidePrintbed));
                threedKey.SetValue("showEdges",             _showEdges  ? 1 : 0);
                threedKey.SetValue("showFaces",             _showFaces  ? 1 : 0);
                threedKey.SetValue("showPrintbed",          showPrintbed.IsChecked == true ? 1 : 0);
                threedKey.SetValue("enableLight1",          enableLight1.IsChecked == true ? 1 : 0);
                threedKey.SetValue("enableLight2",          enableLight2.IsChecked == true ? 1 : 0);
                threedKey.SetValue("enableLight3",          enableLight3.IsChecked == true ? 1 : 0);
                threedKey.SetValue("enableLight4",          enableLight4.IsChecked == true ? 1 : 0);
                threedKey.SetValue("drawMethod",            comboDrawMethod.SelectedIndex);

                threedKey.SetValue("ambient1Color",   ToArgb(ambient1));
                threedKey.SetValue("diffuse1Color",   ToArgb(diffuse1));
                threedKey.SetValue("specular1Color",  ToArgb(specular1));
                threedKey.SetValue("ambient2Color",   ToArgb(ambient2));
                threedKey.SetValue("diffuse2Color",   ToArgb(diffuse2));
                threedKey.SetValue("specular2Color",  ToArgb(specular2));
                threedKey.SetValue("ambient3Color",   ToArgb(ambient3));
                threedKey.SetValue("diffuse3Color",   ToArgb(diffuse3));
                threedKey.SetValue("specular3Color",  ToArgb(specular3));
                threedKey.SetValue("ambient4Color",   ToArgb(ambient4));
                threedKey.SetValue("diffuse4Color",   ToArgb(diffuse4));
                threedKey.SetValue("specular4Color",  ToArgb(specular4));
                threedKey.SetValue("selectionBoxColor", ToArgb(selectionBox));
                threedKey.SetValue("errorModelColor",   ToArgb(errorModel));
                threedKey.SetValue("insideFacesColor",  ToArgb(insideFaces));

                threedKey.SetValue("light1X", xdir1.Text);
                threedKey.SetValue("light1Y", ydir1.Text);
                threedKey.SetValue("light1Z", zdir1.Text);
                threedKey.SetValue("light2X", xdir2.Text);
                threedKey.SetValue("light2Y", ydir2.Text);
                threedKey.SetValue("light2Z", zdir2.Text);
                threedKey.SetValue("light3X", xdir3.Text);
                threedKey.SetValue("light3Y", ydir3.Text);
                threedKey.SetValue("light3Z", zdir3.Text);
                threedKey.SetValue("light4X", xdir4.Text);
                threedKey.SetValue("light4Y", ydir4.Text);
                threedKey.SetValue("light4Z", zdir4.Text);
            }
            catch { }
        }

        /// <summary>Restore all UI values from the registry.</summary>
        private void RegistryToForm()
        {
            if (threedKey == null) return;
            try
            {
                SetSwatchColor(backgroundTop,    "backgroundTopColor",    backgroundTop);
                SetSwatchColor(backgroundBottom, "backgroundBottomColor", backgroundBottom);
                SetSwatchColor(faces,            "facesColor",            faces);
                SetSwatchColor(edges,            "edgesColor",            faces);         // original used faces as fallback
                SetSwatchColor(selectedFaces,    "selectedFacesColor",    selectedFaces);
                SetSwatchColor(printerBase,      "printerBaseColor",      printerBase);
                SetSwatchColor(printerFrame,     "printerFrameColor",     printerFrame);
                SetSwatchColor(outsidePrintbed,  "outsidePrintbedColor",  outsidePrintbed);

                _showEdges = 0 != (int)(threedKey.GetValue("showEdges",  _showEdges  ? 1 : 0));
                _showFaces = 0 != (int)(threedKey.GetValue("showFaces",  _showFaces  ? 1 : 0));

                showPrintbed.IsChecked  = 0 != (int)(threedKey.GetValue("showPrintbed",  showPrintbed.IsChecked  == true ? 1 : 0));
                enableLight1.IsChecked  = 0 != (int)(threedKey.GetValue("enableLight1",  enableLight1.IsChecked  == true ? 1 : 0));
                enableLight2.IsChecked  = 0 != (int)(threedKey.GetValue("enableLight2",  enableLight2.IsChecked  == true ? 1 : 0));
                enableLight3.IsChecked  = 0 != (int)(threedKey.GetValue("enableLight3",  enableLight3.IsChecked  == true ? 1 : 0));
                enableLight4.IsChecked  = 0 != (int)(threedKey.GetValue("enableLight4",  enableLight4.IsChecked  == true ? 1 : 0));

                comboDrawMethod.SelectedIndex = (int)(threedKey.GetValue("drawMethod", 0));

                SetSwatchColor(ambient1,  "ambient1Color",  ambient1);
                SetSwatchColor(diffuse1,  "diffuse1Color",  diffuse1);
                SetSwatchColor(specular1, "specular1Color", specular1);
                SetSwatchColor(ambient2,  "ambient2Color",  ambient2);
                SetSwatchColor(diffuse2,  "diffuse2Color",  diffuse2);
                SetSwatchColor(specular2, "specular2Color", specular2);
                SetSwatchColor(ambient3,  "ambient3Color",  ambient3);
                SetSwatchColor(diffuse3,  "diffuse3Color",  diffuse3);
                SetSwatchColor(specular3, "specular3Color", specular3);
                SetSwatchColor(ambient4,  "ambient4Color",  ambient4);
                SetSwatchColor(diffuse4,  "diffuse4Color",  diffuse4);
                SetSwatchColor(specular4, "specular4Color", specular4);
                SetSwatchColor(selectionBox,  "selectionBoxColor", selectionBox);
                SetSwatchColor(errorModel,    "errorModelColor",   errorModel);
                SetSwatchColor(insideFaces,   "insideFacesColor",  insideFaces);

                xdir1.Text = (string)threedKey.GetValue("light1X", xdir1.Text);
                ydir1.Text = (string)threedKey.GetValue("light1Y", ydir1.Text);
                zdir1.Text = (string)threedKey.GetValue("light1Z", zdir1.Text);
                xdir2.Text = (string)threedKey.GetValue("light2X", xdir2.Text);
                ydir2.Text = (string)threedKey.GetValue("light2Y", ydir2.Text);
                zdir2.Text = (string)threedKey.GetValue("light2Z", zdir2.Text);
                xdir3.Text = (string)threedKey.GetValue("light3X", xdir3.Text);
                ydir3.Text = (string)threedKey.GetValue("light3Y", ydir3.Text);
                zdir3.Text = (string)threedKey.GetValue("light3Z", zdir3.Text);
                xdir4.Text = (string)threedKey.GetValue("light4X", xdir4.Text);
                ydir4.Text = (string)threedKey.GetValue("light4Y", ydir4.Text);
                zdir4.Text = (string)threedKey.GetValue("light4Z", zdir4.Text);

                // Migrate legacy key
                if (threedKey.GetValue("backgroundColor", null) != null)
                    threedKey.DeleteValue("backgroundColor");
            }
            catch { }
        }

        // ── Color-swatch helpers ─────────────────────────────────────────────────

        /// <summary>Return the ARGB int of a swatch Border's SolidColorBrush background.</summary>
        private static int ToArgb(Border b)
        {
            if (b.Background is SolidColorBrush scb)
            {
                var c = scb.Color;
                return (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
            }
            return 0;
        }

        /// <summary>
        /// Read an ARGB int from the registry and apply it to <paramref name="target"/>.
        /// Falls back to the current colour of <paramref name="fallback"/> when the key is absent.
        /// </summary>
        private void SetSwatchColor(Border target, string regKey, Border fallback)
        {
            int argb = (int)(threedKey.GetValue(regKey, ToArgb(fallback)));
            target.Background = new SolidColorBrush(ArgbToColor(argb));
        }

        private static System.Windows.Media.Color ArgbToColor(int argb)
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

        private void light_TextChanged(object sender, TextChangedEventArgs e)
        {
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
                //bool valid = float.TryParse(tb.Text, out _);
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

        public bool EnableLight1() 
        {
            bool output = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = (enableLight1.IsChecked == true);
            });
            return output;
        }
        public bool EnableLight2()
        {
            bool output = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = (enableLight2.IsChecked == true);
            });
            return output;
        }

        public bool EnableLight3()
        {
            bool output = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = (enableLight3.IsChecked == true);
            });
            return output;
        }

        public bool EnableLight4()
        {
            bool output = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = (enableLight4.IsChecked == true);
            });
            return output;
        }

        public float[] Dir1()
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToDir(xdir1, ydir1, zdir1);
            });
            return output;
        }

        public float[] Dir2()
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToDir(xdir2, ydir2, zdir2);
            });
            return output;
        }

        public float[] Dir3()
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToDir(xdir3, ydir3, zdir3);
            });
            return output;
        }

        public float[] Dir4()
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToDir(xdir4, xdir4, xdir4);
            });
            return output;
        }

        public float[] Diffuse1()//  => ToGLColor(diffuse1);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(diffuse1);
            });
            return output;
        }
        public float[] Ambient1()//  => ToGLColor(ambient1);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(ambient1);
            });
            return output;
        }
        public float[] Specular1()// => ToGLColor(specular1);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(specular1);
            });
            return output;
        }
        public float[] Diffuse2()//  => ToGLColor(diffuse2);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(diffuse2);
            });
            return output;
        }
        public float[] Ambient2()//  => ToGLColor(ambient2);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(ambient2);
            });
            return output;
        }
        public float[] Specular2()// => ToGLColor(specular2);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(specular2);
            });
            return output;
        }
        public float[] Diffuse3()//  => ToGLColor(diffuse3);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(diffuse3);
            });
            return output;
        }
        public float[] Ambient3()//  => ToGLColor(ambient3);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(ambient3);
            });
            return output;
        }
        public float[] Specular3()// => ToGLColor(specular3);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(specular3);
            });
            return output;
        }
        public float[] Diffuse4()//  => ToGLColor(diffuse4);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(diffuse4);
            });
            return output;
        }
        public float[] Ambient4()//  => ToGLColor(ambient4);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(ambient4);
            });
            return output;
        }
        public float[] Specular4()// => ToGLColor(specular4);
        {
            float[] output = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                output = ToGLColor(specular4);
            });
            return output;
        }


        public System.Drawing.Color InsideFacesBackgroundColor()
        {
            System.Drawing.Color color = System.Drawing.Color.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
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

        public System.Drawing.Color GetColorSetting(Submesh.MeshColor color, System.Drawing.Color frontBackColor)
        {
            switch (color)
            {
                case Submesh.MeshColor.FrontBack:
                    return frontBackColor;
                case Submesh.MeshColor.Back:
                    return InsideFacesBackgroundColor();
                case Submesh.MeshColor.ErrorFace:
                    return ErrorModelBackgroundColor();
                case Submesh.MeshColor.ErrorEdge:
                    return ErrorModelEdgeBackgroundColor();
                case Submesh.MeshColor.OutSide:
                    return OutsidePrintbedBackgroundColor();
                case Submesh.MeshColor.EdgeLoop:
                    return EdgesLoopBackgroundColor();
                case Submesh.MeshColor.CutEdge:
                    return CutFacesBackgroundColor();
                case Submesh.MeshColor.Normal:
                    return System.Drawing.Color.Blue;
                case Submesh.MeshColor.Edge:
                    return EdgesBackgroundColor();
                case Submesh.MeshColor.TransBlue:
                    return System.Drawing.Color.FromArgb(128, 0, 0, 255);
                case Submesh.MeshColor.OverhangLv1: // pink
                    return System.Drawing.Color.FromArgb(255, 255, 140, 140);
                case Submesh.MeshColor.OverhangLv2: // light pink
                    return System.Drawing.Color.FromArgb(255, 255, 190, 190);
                case Submesh.MeshColor.OverhangLv3: // light pink white
                    return System.Drawing.Color.FromArgb(255, 250, 215, 205);
                default:
                    return System.Drawing.Color.White;
            }
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
    }
}

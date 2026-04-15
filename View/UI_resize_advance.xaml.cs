using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using View3D.model.geom;
using OpenGL3DViewerNET10.ModelLib.model;
using OpenGL3DViewerNET10.ModelLib.Utils;

namespace View3D.view
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public partial class UI_resize_advance : UserControl
    {
        public bool gIsShow = false;
        public double bboxnow;
        public double bboynow;
        public double bboznow;
        public double dimX = 0.0, dimY = 0.0, dimZ = 0.0;

        bool IsScale = false;
        string xyzbind = "x";
        string scaleKeyDown = "";

        public UI_resize_advance()
        {
            InitializeComponent();
            try
            {
                button_mmtoinch.IsEnabled = true;
                button_inchtomm.IsEnabled = false;
                slider_resize.Minimum = 1;  // NOTE: The value of resize cannot be zero; otherwise, exception happens.
                if (MainWindow.main != null)
                    MainWindow.main.languageChanged += translate;
            }
            catch { }
        }

        private void translate()
        {
            lbl_XUnits.Content = Trans.T("L_MM");
            lbl_YUnits.Content = Trans.T("L_MM");
            lbl_ZUnits.Content = Trans.T("L_MM");

            button_Reset.ToolTip = Trans.T("B_RESET");
            button_Reset.Content = Trans.T("B_RESET");
            lbl_Uniform.Content = Trans.T("L_UNIFORM");
            lbl_Size.Content = Trans.T("L_SIZE");
            btn_Scale.Content = Trans.T("B_APPLY");
            button_mmtoinch.Content = Trans.T("B_SCALE_UP") + " (" + Trans.T("L_MM") + "->" + Trans.T("L_INCH") + ")";
            button_inchtomm.Content = Trans.T("B_SCALE_DOWN") + " (" + Trans.T("L_INCH") + "->" + Trans.T("L_MM") + ")";
        }

        public void Init()
        {
            if (MainWindow.main == null) return; // At design time MainWindow.main is null. Add null guards to prevent NullReferenceException.
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            MainWindow.main.UI_move.slider_moveZ.Maximum = 1000;

            RHBoundingBox bbox = stl.BoundingBox;
            bboxnow = bbox.Size.x / Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text);
            bboynow = bbox.Size.y / Convert.ToDouble(MainWindow.main.stlComposer.textScaleY.Text);
            bboznow = bbox.Size.z / Convert.ToDouble(MainWindow.main.stlComposer.textScaleZ.Text);

            gIsShow = true;
            dimX = bbox.Size.x;
            dimY = bbox.Size.y;
            dimZ = bbox.Size.z;
            updateTxt();

            if (Convert.ToDouble(MainWindow.main.stlComposer.textRotX.Text) != 0 ||
                Convert.ToDouble(MainWindow.main.stlComposer.textRotY.Text) != 0 ||
                Convert.ToDouble(MainWindow.main.stlComposer.textRotZ.Text) != 0)
            {
                chk_Uniform.IsChecked = true;
                chk_Uniform.IsEnabled = false;
            }
            else
            {
                chk_Uniform.IsEnabled = true;
            }

            if (chk_Uniform.IsChecked == true)
            {
                chk_Uniform_Checked(null, null);
            }

            gIsShow = false;
        }

        private void txtX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MainWindow.main == null) return; // At design time MainWindow.main is null. Add null guards to prevent NullReferenceException.
            if (gIsShow == true)
                return;

            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            try
            {
                if (dimX == 0)
                {
                    dimX = 0.001;
                    updateTxt();
                }
                else
                {
                    dimX = unit2MMTransform(Convert.ToDouble(txtX.Text));
                        
                    if(dimX == 0)
                    {
                        dimX = 0.001;
                    }
                }

                Double tbefore = Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text);
                Double tTempx = dimX / bboxnow;
                Double tMultScale = 0.0;

                tMultScale = tTempx / tbefore;
                MainWindow.main.stlComposer.textScaleX.Text = tTempx.ToString("0.000");
                if (chk_Uniform.IsChecked == true)
                {
                    Double temp = Convert.ToDouble(MainWindow.main.stlComposer.textScaleY.Text) * tMultScale;
                    if (temp > 0)
                    {
                        MainWindow.main.stlComposer.textScaleY.Text = temp.ToString("0.000");
                    }

                    temp = Convert.ToDouble(MainWindow.main.stlComposer.textScaleZ.Text) * tMultScale;
                    if (temp > 0)
                    {
                        MainWindow.main.stlComposer.textScaleZ.Text = temp.ToString("0.000");
                    }

                    gIsShow = true;
                    IsScale = false;
                    if (xyzbind == "x")
                    {
                        updateSliderValue(Axis.X);
                    }
                    else if (xyzbind == "y")
                    {
                        updateSliderValue(Axis.Y);
                    }
                    else if (xyzbind == "z")
                    {
                        updateSliderValue(Axis.Z);
                    }
                    if (scaleKeyDown != "x")
                    {
                        dimX = stl.BoundingBox.Size.x;
                        updateTxt();
                    }
                    if (scaleKeyDown != "y")
                    {
                        dimY = stl.BoundingBox.Size.y;
                        updateTxt();
                    }
                    if (scaleKeyDown != "z")
                    {
                        dimZ = stl.BoundingBox.Size.z;
                        updateTxt();
                    }
                    gIsShow = false;
                    IsScale = true;
                }
            }
            catch { }
        }

        private void txtY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MainWindow.main == null) return; // At design time MainWindow.main is null. Add null guards to prevent NullReferenceException.
            if (gIsShow == true)
                return;

            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            try
            {
                if (dimY == 0)
                {
                    dimY = 0.001;
                    updateTxt();
                }
                else
                {
                    dimY = unit2MMTransform(Convert.ToDouble(txtY.Text));

                    if (dimY == 0)
                    {
                        dimY = 0.001;
                    }
                }

                Double tbefore = Convert.ToDouble(MainWindow.main.stlComposer.textScaleY.Text);
                Double tTempy = dimY / bboynow;
                Double tMultScale = 0.0;

                tMultScale = tTempy / tbefore;

                MainWindow.main.stlComposer.textScaleY.Text = tTempy.ToString("0.000");
                if (chk_Uniform.IsChecked == true)
                {
                    Double temp = Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text) * tMultScale;
                    if (temp > 0)
                    {
                        MainWindow.main.stlComposer.textScaleX.Text = temp.ToString("0.000");
                    }

                    temp = Convert.ToDouble(MainWindow.main.stlComposer.textScaleZ.Text) * tMultScale;
                    if (temp > 0)
                    {
                        MainWindow.main.stlComposer.textScaleZ.Text = temp.ToString("0.000");
                    }

                    gIsShow = true;
                    IsScale = false;
                    if (xyzbind == "x")
                    {
                        updateSliderValue(Axis.X);
                    }
                    else if (xyzbind == "y")
                    {
                        updateSliderValue(Axis.Y);
                    }
                    else if (xyzbind == "z")
                    {
                        updateSliderValue(Axis.Z);
                    }

                    if (scaleKeyDown != "x")
                    {
                        dimX = stl.BoundingBox.Size.x;
                        updateTxt();
                    }
                    if (scaleKeyDown != "y")
                    {
                        dimY = stl.BoundingBox.Size.y;
                        updateTxt();
                    }
                    if (scaleKeyDown != "z")
                    {
                        dimZ = stl.BoundingBox.Size.z;
                        updateTxt();
                    }
                    gIsShow = false;
                    IsScale = true;
                }
            }
            catch { }
        }

        private void txtZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MainWindow.main == null) return; // At design time MainWindow.main is null. Add null guards to prevent NullReferenceException.

            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
         
            if (stl == null) return;

            if (gIsShow == true)
            {
                MainWindow.main.UI_move.slider_moveZ.Value = stl.Position.Z;
                MainWindow.main.UI_move.slider_moveZ.Minimum = stl.Position.Z - stl.BoundingBox.zMin;
                MainWindow.main.stlComposer.UpdateOutOfBound();
                return;
            }
            try
            { 
                if (dimZ == 0)
                {
                    dimZ = 0.001;
                    updateTxt();
                }
                else
                {
                    dimZ = unit2MMTransform(Convert.ToDouble(txtZ.Text));

                    if (dimY == 0)
                    {
                        dimY = 0.001;
                    }
                }

                Double tbefore = Convert.ToDouble(MainWindow.main.stlComposer.textScaleZ.Text);
                Double tTempz = dimZ / bboznow;
                Double tMultScale = 0.0;

                tMultScale = tTempz / tbefore;

                MainWindow.main.stlComposer.textScaleZ.Text = tTempz.ToString("0.000");
                if (chk_Uniform.IsChecked == true)
                {
                    Double temp = Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text) * tMultScale;
                    if (temp > 0)
                    {
                        MainWindow.main.stlComposer.textScaleX.Text = temp.ToString("0.000");
                    }

                    temp = Convert.ToDouble(MainWindow.main.stlComposer.textScaleY.Text) * tMultScale;
                    if (temp > 0)
                    {
                        MainWindow.main.stlComposer.textScaleY.Text = temp.ToString("0.000");
                    }

                    gIsShow = true;
                    IsScale = false;
                    if (xyzbind == "x")
                    {
                        updateSliderValue(Axis.X);
                    }
                    else if (xyzbind == "y")
                    {
                        updateSliderValue(Axis.Y);
                    }
                    else if (xyzbind == "z")
                    {
                        updateSliderValue(Axis.Z);
                    }

                    if (scaleKeyDown != "x")
                    {
                        dimX = stl.BoundingBox.Size.x;
                        updateTxt();
                    }
                    if (scaleKeyDown != "y")
                    {
                        dimY = stl.BoundingBox.Size.y;
                        updateTxt();
                    }
                    if (scaleKeyDown != "z")
                    {
                        dimZ = stl.BoundingBox.Size.z;
                        updateTxt();
                    }

                    gIsShow = false;
                    IsScale = true;
                }
                stl.Land();
                MainWindow.main.UI_move.slider_moveZ.Minimum = stl.Position.Z - stl.BoundingBox.zMin;
                MainWindow.main.stlComposer.UpdateOutOfBound();
            }
            catch { }
        }

        public void chk_Uniform_Checked(object sender, RoutedEventArgs e)
        {
            if (MainWindow.main == null) return; // At design time MainWindow.main is null. Add null guards to prevent NullReferenceException.
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            try
            {
                slider_resize.IsEnabled = true;
                slider_resizeTemp.IsEnabled = true;
                txt_Scale.IsEnabled = true;
                btn_Scale.IsEnabled = true;
                checkMin();

                if (xyzbind == "x")
                {
                    IsScale = false;
                    updateSliderValue(Axis.X);
                    IsScale = true;
                }
                else if (xyzbind == "y")
                {
                    IsScale = false;
                    updateSliderValue(Axis.Y);
                    IsScale = true;
                }
                else if (xyzbind == "z")
                {
                    IsScale = false;
                    updateSliderValue(Axis.Z);
                    IsScale = true;
                }
            }
            catch { }
        }

        private void chk_Uniform_UnChecked(object sender, RoutedEventArgs e)
        {
            try
            {
                slider_resize.IsEnabled = false;
                slider_resizeTemp.IsEnabled = false;
                txt_Scale.IsEnabled = false;
                btn_Scale.IsEnabled = false;

                txt_Scale.Text = "";
            }
            catch { }
        }

        private void slider_resize_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (MainWindow.main == null) return;
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            if (e.Delta > 0)
                slider_resize.Value += 0.01;
            else
                slider_resize.Value -= 0.01;
            e.Handled = true;
        }

        private void slider_resize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MainWindow.main == null) return;
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            switch (xyzbind)
            {
                case "x":
                    if (IsScale == true)
                    {
                        dimX = unit2MMTransform(slider_resize.Value);
                        updateTxt();
                    }
                    break;
                case "y":
                    if (IsScale == true)
                    {
                        dimY = unit2MMTransform(slider_resize.Value);
                        updateTxt();
                    }
                    break;
                case "z":
                    if (IsScale == true)
                    {
                        dimZ = unit2MMTransform(slider_resize.Value);
                        updateTxt();
                    }
                    break;
            }
            txt_Scale.Text = (Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text) * 100).ToString("0");
        }

        public void checkMin()
        {
            if (MainWindow.main == null) return;
            double txMaxScalableValue = Convert.ToDouble(SettingsService.Instance.Settings.PrintAreaWidth) / dimX;
            double tyMaxScalableValue = Convert.ToDouble(SettingsService.Instance.Settings.PrintAreaDepth) / dimY;
            double tzMaxScalableValue = Convert.ToDouble(SettingsService.Instance.Settings.PrintAreaHeight) / dimZ;
            double tMaxScalableValue = Math.Min(Math.Min(txMaxScalableValue, tyMaxScalableValue), Math.Min(tyMaxScalableValue, tzMaxScalableValue));

            IsScale = false;
            if (txMaxScalableValue == tMaxScalableValue)
            {
                xyzbind = "x";
                slider_resize.Maximum = (double)SettingsService.Instance.Settings.PrintAreaWidth;

                if ((dimY * txMaxScalableValue > (double)SettingsService.Instance.Settings.PrintAreaDepth)
                        || (dimZ * txMaxScalableValue > (double)SettingsService.Instance.Settings.PrintAreaHeight))
                {
                    slider_resize.Maximum = Math.Floor(dimX * Math.Min(tyMaxScalableValue, tzMaxScalableValue) * 1000) / 1000;
                }
            }
            else if (tyMaxScalableValue == tMaxScalableValue)
            {
                xyzbind = "y";
                slider_resize.Maximum = (double)SettingsService.Instance.Settings.PrintAreaDepth;

                if ((dimX * tyMaxScalableValue > (double)SettingsService.Instance.Settings.PrintAreaWidth)
                        || (dimZ * tyMaxScalableValue > (double)SettingsService.Instance.Settings.PrintAreaHeight))
                {
                    slider_resize.Maximum = Math.Floor(dimY * Math.Min(txMaxScalableValue, tzMaxScalableValue) * 1000) / 1000;
                }
            }
            else if (tzMaxScalableValue == tMaxScalableValue)
            {
                xyzbind = "z";
                slider_resize.Maximum = (double)SettingsService.Instance.Settings.PrintAreaHeight;

                if ((dimX * tzMaxScalableValue > (double)SettingsService.Instance.Settings.PrintAreaWidth)
                        || (dimY * tzMaxScalableValue > (double)SettingsService.Instance.Settings.PrintAreaDepth))
                {
                    slider_resize.Maximum = Math.Floor(dimZ * Math.Min(txMaxScalableValue, tyMaxScalableValue) * 1000) / 1000;
                }
            }

            slider_resize.Maximum = unit2InchTransform(slider_resize.Maximum);

            IsScale = true;
        }

        public void button_Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.main == null) return;
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            MainWindow.main.stlComposer.textScaleX.Text = "1";
            MainWindow.main.stlComposer.textScaleY.Text = "1";
            MainWindow.main.stlComposer.textScaleZ.Text = "1";
            bboxnow = stl.BoundingBox.Size.x;
            bboynow = stl.BoundingBox.Size.y;
            bboznow = stl.BoundingBox.Size.z;
            txt_Scale.Text = "100";

            gIsShow = true;
            dimX = stl.BoundingBox.Size.x;
            dimY = stl.BoundingBox.Size.y;
            dimZ = stl.BoundingBox.Size.z;
            updateTxt();
            IsScale = false;
            if (xyzbind == "x")
            {
                updateSliderValue(Axis.X);
            }
            else if (xyzbind == "y")
            {
                updateSliderValue(Axis.Y);
            }
            else if (xyzbind == "z")
            {
                updateSliderValue(Axis.Z);
            }
            IsScale = true;
            gIsShow = false;
            checkMin();
            MainWindow.main.stlComposer.check_stl_size_too_small();
            button_mmtoinch.IsEnabled = true;
            button_inchtomm.IsEnabled = false;
        }

        private void button_mmtoinch_Click(object sender, RoutedEventArgs e)
        {
            button_mmtoinch.IsEnabled = false;
            button_inchtomm.IsEnabled = true;
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            MainWindow.main.stlComposer.DoMmToInch(stl);
            txt_Scale.Text = (Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text) * 100).ToString("0");

            slider_resize.ValueChanged -= slider_resize_ValueChanged;
            slider_resize.Value = Convert.ToDouble(txt_Scale.Text);
            slider_resize.ValueChanged += slider_resize_ValueChanged;

            dimX = stl.BoundingBox.Size.x;
            dimY = stl.BoundingBox.Size.y;
            dimZ = stl.BoundingBox.Size.z;
            updateTxt();
        }

        private void button_inchtomm_Click(object sender, RoutedEventArgs e)
        {
            button_mmtoinch.IsEnabled = true;
            button_inchtomm.IsEnabled = false;
            ThreeDModel model = MainWindow.main.stlComposer.SingleSelectedModel;
            if (model == null) return;
            MainWindow.main.stlComposer.DoInchtomm(model);
            txt_Scale.Text = (Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text) * 100).ToString("0");
            
            slider_resize.ValueChanged -= slider_resize_ValueChanged;
            slider_resize.Value = Convert.ToDouble(txt_Scale.Text);
            slider_resize.ValueChanged += slider_resize_ValueChanged;
        }

        private void btn_Scale_Click(object sender, RoutedEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            model.geom.RHBoundingBox bbox = stl.BoundingBox;

            try
            {
                Double tbeforeX = Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text);
                Double tbeforeY = Convert.ToDouble(MainWindow.main.stlComposer.textScaleY.Text);
                Double tbeforeZ = Convert.ToDouble(MainWindow.main.stlComposer.textScaleZ.Text);
                Double temp = Convert.ToDouble(txt_Scale.Text) / 100;
                Double tAddScaleX = tbeforeX * temp;
                Double tAddScaleY = tbeforeY * temp;
                Double tAddScaleZ = tbeforeZ * temp;

                MainWindow.main.stlComposer.textScaleX.Text = tAddScaleX.ToString("0.000");
                MainWindow.main.stlComposer.textScaleY.Text = tAddScaleY.ToString("0.000");
                MainWindow.main.stlComposer.textScaleZ.Text = tAddScaleZ.ToString("0.000");
                gIsShow = true;
                dimX = stl.BoundingBox.Size.x;
                dimY = stl.BoundingBox.Size.y;
                dimZ = stl.BoundingBox.Size.z;
                updateTxt();
                gIsShow = false;
                //checkMin();
                IsScale = false;
                if (xyzbind == "x")
                {
                    updateSliderValue(Axis.X);
                }
                else if (xyzbind == "y")
                {
                    updateSliderValue(Axis.Y);
                }
                else if (xyzbind == "z")
                {
                    updateSliderValue(Axis.Z);
                }
                IsScale = true;
            }
            catch { }
        }

        private void scaleLostFocus(object sender, RoutedEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            try
            {
                dimX = stl.BoundingBox.Size.x;
                dimY = stl.BoundingBox.Size.y;
                dimZ = stl.BoundingBox.Size.z;
                updateTxt();

                scaleKeyDown = "";
            }
            catch { }
        }

        private void scaleTextBoxKeyBoardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if ((TextBox)sender == txtX)
                scaleKeyDown = "x";
            else if ((TextBox)sender == txtY)
                scaleKeyDown = "y";
            else if ((TextBox)sender == txtZ)
                scaleKeyDown = "z";
        }

        public void updateTxt()
        {
            txtX.Text = dimX.ToString("0.000");
            txtY.Text = dimY.ToString("0.000");
            txtZ.Text = dimZ.ToString("0.000");
        }

        public void updateSliderValue(Axis axis)
        {
            double dimForDisplay = 0.0;
            switch (axis)
            {
                case Axis.X:
                    dimForDisplay = dimX;
                    break;

                case Axis.Y:
                    dimForDisplay = dimY;
                    break;

                case Axis.Z:
                    dimForDisplay = dimZ;
                    break;
            }

            slider_resize.Value = Convert.ToDouble(unit2InchTransform(dimForDisplay).ToString("0.000"));
        }

        // mm -> inch
        // Note: No conversion. Just return the input value. 
        private double unit2InchTransform(double inputValue, int margin = 0)
        {
            double transformedValue = inputValue;
            return transformedValue;
        }

        // inch -> mm
        // Note: No conversion. Just return the input value. 
        private double unit2MMTransform(double inputValue)
        {
            double transformedValue = inputValue;
            return transformedValue;
        }
    }
}

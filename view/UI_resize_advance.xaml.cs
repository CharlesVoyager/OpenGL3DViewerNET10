using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using View3D.model;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for UI_resize.xaml
    /// </summary>
    public partial class UI_resize_advance : System.Windows.Controls.UserControl
    {
        public bool gTextChange = false;
        public string xyzbind = "x";

        public bool gIsShow = false;
        public double bboxnow;
        public double bboynow;
        public double bboznow;
        public bool IsScale = false;
        private string scaleKeyDown = "";
        public double dimX = 0.0, dimY = 0.0, dimZ = 0.0;

        public UI_resize_advance()
        {
            InitializeComponent();
            try
            {
                slider_resize.Minimum = 1;  // NOTE: The value of resize cannot be zero; otherwise, exception happens.
                if (MainWindow.main != null)
                    MainWindow.main.languageChanged += translate;
            }
            catch { }
        }

        private void translate()
        { }

        private void txtX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MainWindow.main == null) return; // At design time MainWindow.main is null. Add null guards to prevent NullReferenceException.
            if (gIsShow == true)
                return;

            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            try
            {
                if (dimX == 0)
                {
                    dimX = 0.001;
                    updateTxt(Enums.Axis.X);
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
                if (chk_Uniform.IsChecked == true)// && gFlag == true)
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
                        updateSliderValue(Enums.Axis.X);
                    }
                    else if (xyzbind == "y")
                    {
                        updateSliderValue(Enums.Axis.Y);
                    }
                    else if (xyzbind == "z")
                    {
                        updateSliderValue(Enums.Axis.Z);
                    }
                    //Fix_resize_issue_160803
                    if (scaleKeyDown != "x")
                    {
                        dimX = stl.BoundingBoxWOSupport.Size.x;
                        updateTxt(Enums.Axis.X);
                    }
                    if (scaleKeyDown != "y")
                    {
                        dimY = stl.BoundingBoxWOSupport.Size.y;
                        updateTxt(Enums.Axis.Y);
                    }
                    if (scaleKeyDown != "z")
                    {
                        dimZ = stl.BoundingBoxWOSupport.Size.z;
                        updateTxt(Enums.Axis.Z);
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

            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            try
            {
                if (dimY == 0)
                {
                    dimY = 0.001;
                    updateTxt(Enums.Axis.Y);
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
                if (chk_Uniform.IsChecked == true)//&& gFlag==true)
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
                        updateSliderValue(Enums.Axis.X);
                    }
                    else if (xyzbind == "y")
                    {
                        updateSliderValue(Enums.Axis.Y);
                    }
                    else if (xyzbind == "z")
                    {
                        updateSliderValue(Enums.Axis.Z);
                    }

                    if (scaleKeyDown != "x")
                    {
                        dimX = stl.BoundingBoxWOSupport.Size.x;
                        updateTxt(Enums.Axis.X);
                    }
                    if (scaleKeyDown != "y")
                    {
                        dimY = stl.BoundingBoxWOSupport.Size.y;
                        updateTxt(Enums.Axis.Y);
                    }
                    if (scaleKeyDown != "z")
                    {
                        dimZ = stl.BoundingBoxWOSupport.Size.z;
                        updateTxt(Enums.Axis.Z);
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

            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
         
            if (stl == null) return;

            if (gIsShow == true)
            {
                stl.LandToZ(stl.zMin);
                MainWindow.main.UI_move.slider_moveZ.Value = stl.Position.z;
                MainWindow.main.UI_move.slider_moveZ.Minimum = stl.Position.z - stl.BoundingBoxWOSupport.zMin;
                MainWindow.main.stlComposer.UpdateOutOfBound();
                return;
            }
            try
            { 
                if (dimZ == 0)
                {
                    dimZ = 0.001;
                    updateTxt(Enums.Axis.Z);
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
                if (chk_Uniform.IsChecked == true)//&& gFlag==true)
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
                        updateSliderValue(Enums.Axis.X);
                    }
                    else if (xyzbind == "y")
                    {
                        updateSliderValue(Enums.Axis.Y);
                    }
                    else if (xyzbind == "z")
                    {
                        updateSliderValue(Enums.Axis.Z);
                    }

                    if (scaleKeyDown != "x")
                    {
                        dimX = stl.BoundingBoxWOSupport.Size.x;
                        updateTxt(Enums.Axis.X);
                    }
                    if (scaleKeyDown != "y")
                    {
                        dimY = stl.BoundingBoxWOSupport.Size.y;
                        updateTxt(Enums.Axis.Y);
                    }
                    if (scaleKeyDown != "z")
                    {
                        dimZ = stl.BoundingBoxWOSupport.Size.z;
                        updateTxt(Enums.Axis.Z);
                    }

                    gIsShow = false;
                    IsScale = true;
                }
                    stl.LandToZ(stl.zMin);
                MainWindow.main.UI_move.slider_moveZ.Minimum = stl.Position.z - stl.BoundingBoxWOSupport.zMin;
                MainWindow.main.stlComposer.UpdateOutOfBound();
            }
            catch { }
        }

        public void chk_Uniform_Checked(object sender, RoutedEventArgs e)
        {
            if (MainWindow.main == null) return; // At design time MainWindow.main is null. Add null guards to prevent NullReferenceException.
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
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
                    updateSliderValue(Enums.Axis.X);
                    IsScale = true;
                }
                else if (xyzbind == "y")
                {
                    IsScale = false;
                    updateSliderValue(Enums.Axis.Y);
                    IsScale = true;
                }
                else if (xyzbind == "z")
                {
                    IsScale = false;
                    updateSliderValue(Enums.Axis.Z);
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
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
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
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            gIsShow = true;
            gIsShow = false;

            switch (xyzbind)
            {
                case "x":
                    if (IsScale == true)
                    {
                        dimX = unit2MMTransform(slider_resize.Value);
                        updateTxt(Enums.Axis.X);
                    }
                    break;
                case "y":
                    if (IsScale == true)
                    {
                        dimY = unit2MMTransform(slider_resize.Value);
                        updateTxt(Enums.Axis.Y);
                    }
                    break;
                case "z":
                    if (IsScale == true)
                    {
                        dimZ = unit2MMTransform(slider_resize.Value);
                        updateTxt(Enums.Axis.Z);
                    }
                    break;
            }

            txt_Scale.Text = (Convert.ToDouble(MainWindow.main.stlComposer.textScaleX.Text) * 100).ToString("0");
        }

        public void checkMin()
        {
            if (MainWindow.main == null) return;
            double txMaxScalableValue = Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaWidth) / dimX;
            double tyMaxScalableValue = Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaDepth) / dimY;
            double tzMaxScalableValue = Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaHeight) / dimZ;
            double tMaxScalableValue = Math.Min(Math.Min(txMaxScalableValue, tyMaxScalableValue), Math.Min(tyMaxScalableValue, tzMaxScalableValue));

            IsScale = false;
            if (txMaxScalableValue == tMaxScalableValue)
            {
                xyzbind = "x";
                slider_resize.Maximum = (double)MainWindow.main.threeDSettings.PrintAreaWidth;

                if ((dimY * txMaxScalableValue > (double)MainWindow.main.threeDSettings.PrintAreaDepth)
                        || (dimZ * txMaxScalableValue > (double)MainWindow.main.threeDSettings.PrintAreaHeight))
                {
                    slider_resize.Maximum = Math.Floor(dimX * Math.Min(tyMaxScalableValue, tzMaxScalableValue) * 1000) / 1000;
                }
            }
            else if (tyMaxScalableValue == tMaxScalableValue)
            {
                xyzbind = "y";
                slider_resize.Maximum = (double)MainWindow.main.threeDSettings.PrintAreaDepth;

                if ((dimX * tyMaxScalableValue > (double)MainWindow.main.threeDSettings.PrintAreaWidth)
                        || (dimZ * tyMaxScalableValue > (double)MainWindow.main.threeDSettings.PrintAreaHeight))
                {
                    slider_resize.Maximum = Math.Floor(dimY * Math.Min(txMaxScalableValue, tzMaxScalableValue) * 1000) / 1000;
                }
            }
            else if (tzMaxScalableValue == tMaxScalableValue)
            {
                xyzbind = "z";
                slider_resize.Maximum = (double)MainWindow.main.threeDSettings.PrintAreaHeight;

                if ((dimX * tzMaxScalableValue > (double)MainWindow.main.threeDSettings.PrintAreaWidth)
                        || (dimY * tzMaxScalableValue > (double)MainWindow.main.threeDSettings.PrintAreaDepth))
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
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
            MainWindow.main.stlComposer.textScaleX.Text = "1";
            MainWindow.main.stlComposer.textScaleY.Text = "1";
            MainWindow.main.stlComposer.textScaleZ.Text = "1";
            bboxnow = bbox.Size.x;
            bboynow = bbox.Size.y;
            bboznow = bbox.Size.z;
            txt_Scale.Text = "";

            gIsShow = true;
            dimX = bbox.Size.x;
            updateTxt(Enums.Axis.X);
            dimY = bbox.Size.y;
            updateTxt(Enums.Axis.Y);
            dimZ = bbox.Size.z;
            updateTxt(Enums.Axis.Z);
            IsScale = false;
            if (xyzbind == "x")
            {
                updateSliderValue(Enums.Axis.X);
            }
            else if (xyzbind == "y")
            {
                updateSliderValue(Enums.Axis.Y);
            }
            else if (xyzbind == "z")
            {
                updateSliderValue(Enums.Axis.Z);
            }
            IsScale = true;
            gIsShow = false;
            checkMin();
            MainWindow.main.stlComposer.check_stl_size_too_small();
        }

        private void button_mmtoinch_Click(object sender, RoutedEventArgs e)
        {
            button_mmtoinch.IsEnabled = false;
            button_inchtomm.IsEnabled = true;
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            MainWindow.main.stlComposer.DoInchOrScale(stl, true);
        }

        private void button_inchtomm_Click(object sender, RoutedEventArgs e)
        {
            button_mmtoinch.IsEnabled = true;
            button_inchtomm.IsEnabled = false;
            PrintModel model = MainWindow.main.stlComposer.SingleSelectedModel;
            if (model == null) return;
            MainWindow.main.stlComposer.DoInchtomm(model);
        }

        private void btn_Scale_Click(object sender, RoutedEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;

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
                dimX = stl.BoundingBoxWOSupport.Size.x;
                updateTxt(Enums.Axis.X);
                dimY = stl.BoundingBoxWOSupport.Size.y;
                updateTxt(Enums.Axis.Y);
                dimZ = stl.BoundingBoxWOSupport.Size.z;
                updateTxt(Enums.Axis.Z);
                gIsShow = false;
                //checkMin();
                IsScale = false;
                if (xyzbind == "x")
                {
                    updateSliderValue(Enums.Axis.X);
                }
                else if (xyzbind == "y")
                {
                    updateSliderValue(Enums.Axis.Y);
                }
                else if (xyzbind == "z")
                {
                    updateSliderValue(Enums.Axis.Z);
                }
                IsScale = true;
            }
            catch { }
        }

        private void scaleLostFocus(object sender, RoutedEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            try
            {
                dimX = stl.BoundingBoxWOSupport.Size.x;
                updateTxt(Enums.Axis.X);
                dimY = stl.BoundingBoxWOSupport.Size.y;
                updateTxt(Enums.Axis.Y);
                dimZ = stl.BoundingBoxWOSupport.Size.z;
                updateTxt(Enums.Axis.Z);

                scaleKeyDown = "";
            }
            catch { }
        }

        private void scaleTextBoxKeyBoardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if ((System.Windows.Controls.TextBox)sender == txtX)
                scaleKeyDown = "x";
            else if ((System.Windows.Controls.TextBox)sender == txtY)
                scaleKeyDown = "y";
            else if ((System.Windows.Controls.TextBox)sender == txtZ)
                scaleKeyDown = "z";
        }

        public void updateTxt(Enums.Axis axis, bool doUnitTransform = true)
        {
            double dimForDisplay = 0.0;

            switch (axis)
            {
                case Enums.Axis.X:
                    dimForDisplay = dimX;
                    if (doUnitTransform)
                    {
                        dimForDisplay = unit2InchTransform(dimForDisplay, (int)MainWindow.main.threeDSettings.PrintAreaWidth);
                    }
                    txtX.Text = dimForDisplay.ToString("0.000");
                    break;

                case Enums.Axis.Y:
                    dimForDisplay = dimY;
                    if (doUnitTransform)
                    {
                        dimForDisplay = unit2InchTransform(dimForDisplay, (int)MainWindow.main.threeDSettings.PrintAreaDepth);                      
                    }
                    txtY.Text = dimForDisplay.ToString("0.000");
                    break;

                case Enums.Axis.Z:
                    dimForDisplay = dimZ;
                    if (doUnitTransform)
                    {
                        dimForDisplay = unit2InchTransform(dimForDisplay, (int)MainWindow.main.threeDSettings.PrintAreaHeight);
                    }
                    txtZ.Text = dimForDisplay.ToString("0.000");
                    break;
            }        
        }

        public void updateSliderValue(Enums.Axis axis)
        {
            double dimForDisplay = 0.0;
            switch (axis)
            {
                case Enums.Axis.X:
                    dimForDisplay = dimX;
                    break;

                case Enums.Axis.Y:
                    dimForDisplay = dimY;
                    break;

                case Enums.Axis.Z:
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenGL3DViewerNET10.ModelLib.model;
using OpenGL3DViewerNET10.ModelLib.Utils;

namespace View3D.view
{
    enum Axis
    {
        X,
        Y,
        Z
    }

    public partial class UI_resize_advance : UserControl
    {
        public bool gIsShow = false;
        Axis xyzbind = Axis.X;

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

            gIsShow = true;
            updateTxt();
            chk_Uniform.IsChecked = true;
            chk_Uniform_Checked(null, null);
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
                double dimX = Convert.ToDouble(txtX.Text);
                if (dimX == 0) dimX = 0.001;

                Double tScalex = dimX / Math.Max(stl.Model.boundingBox.Size.x, 0.0000001);
 
                MainWindow.main.stlComposer.textScaleX.Text = tScalex.ToString("0.000");
                if (chk_Uniform.IsChecked == true)
                {
                    MainWindow.main.stlComposer.textScaleY.Text = tScalex.ToString("0.000");
                    MainWindow.main.stlComposer.textScaleZ.Text = tScalex.ToString("0.000");
     
                    gIsShow = true;
                    updateSliderValue(xyzbind);
                    updateTxt();
                    gIsShow = false;
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
                double dimY = Convert.ToDouble(txtY.Text);
                if (dimY == 0) dimY = 0.001;

                Double tScaley = dimY / Math.Max(stl.Model.boundingBox.Size.y, 0.0000001);
                MainWindow.main.stlComposer.textScaleY.Text = tScaley.ToString("0.000");
                if (chk_Uniform.IsChecked == true)
                {
                    MainWindow.main.stlComposer.textScaleX.Text = tScaley.ToString("0.000");
                    MainWindow.main.stlComposer.textScaleZ.Text = tScaley.ToString("0.000");
  
                    gIsShow = true;
                    updateSliderValue(xyzbind);
                    updateTxt();
                    gIsShow = false;
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
                double dimZ = Convert.ToDouble(txtZ.Text);
                if (dimZ == 0) dimZ = 0.001;

                Double tScalez = dimZ / Math.Max(stl.Model.boundingBox.Size.z, 0.0000001);
                MainWindow.main.stlComposer.textScaleZ.Text = tScalez.ToString("0.000");
                if (chk_Uniform.IsChecked == true)
                {
                    MainWindow.main.stlComposer.textScaleX.Text = tScalez.ToString("0.000");
                    MainWindow.main.stlComposer.textScaleY.Text = tScalez.ToString("0.000");
        
                    gIsShow = true;
                    updateSliderValue(xyzbind);
                    updateTxt();
                    gIsShow = false;
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
                updateSliderValue(xyzbind);
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

        // NOTE: Slider change is always changed in uniform scale.
        private void slider_resize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MainWindow.main == null) return;
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            txt_Scale.Text = slider_resize.Value.ToString("0");

            MainWindow.main.stlComposer.textScaleX.Text = (slider_resize.Value / 100).ToString("0.000");
            MainWindow.main.stlComposer.textScaleY.Text = (slider_resize.Value / 100).ToString("0.000");
            MainWindow.main.stlComposer.textScaleZ.Text = (slider_resize.Value / 100).ToString("0.000");

            gIsShow = true;
            updateTxt();
            gIsShow = false;
        }

        void checkMin()
        {
            if (MainWindow.main == null) return;
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            // Ensure the value of dimension is not zero; otherwise, exception happens when calculating scale.
            double dimX = Math.Max(stl.Model.boundingBox.Size.x, 0.0000001);
            double dimY = Math.Max(stl.Model.boundingBox.Size.y, 0.0000001);
            double dimZ = Math.Max(stl.Model.boundingBox.Size.z, 0.0000001);

            double txMaxScalableValue = Convert.ToDouble(SettingsService.Instance.Settings.PrintAreaWidth) / dimX;
            double tyMaxScalableValue = Convert.ToDouble(SettingsService.Instance.Settings.PrintAreaDepth) / dimY;
            double tzMaxScalableValue = Convert.ToDouble(SettingsService.Instance.Settings.PrintAreaHeight) / dimZ;
            double tMaxScalableValue = Math.Min(Math.Min(txMaxScalableValue, tyMaxScalableValue), Math.Min(tyMaxScalableValue, tzMaxScalableValue));

            slider_resize.ValueChanged -= slider_resize_ValueChanged;
            slider_resize.Maximum = tMaxScalableValue * 100;
            slider_resize.ValueChanged += slider_resize_ValueChanged;

            if (txMaxScalableValue == tMaxScalableValue)
                xyzbind = Axis.X;
            else if (tyMaxScalableValue == tMaxScalableValue)
                xyzbind = Axis.Y;
            else if (tzMaxScalableValue == tMaxScalableValue)
                xyzbind = Axis.Z;
        }

        public void button_Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.main == null) return;
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
         
            MainWindow.main.stlComposer.textScaleX.Text = "1";
            MainWindow.main.stlComposer.textScaleY.Text = "1";
            MainWindow.main.stlComposer.textScaleZ.Text = "1";
            txt_Scale.Text = "100";
            chk_Uniform.IsChecked = true;

            gIsShow = true;
            updateTxt();
            updateSliderValue(xyzbind);
            gIsShow = false;
            checkMin();
            MainWindow.main.stlComposer.check_stl_size_too_small();
            button_mmtoinch.IsEnabled = true;
            button_inchtomm.IsEnabled = false;

            gIsShow = true;
            updateTxt();
            updateSliderValue(xyzbind);
            gIsShow = false;
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

            gIsShow = true;
            updateTxt();
            gIsShow = false;
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

            try
            {
                Double scaleValue = Convert.ToDouble(txt_Scale.Text) / 100;

                MainWindow.main.stlComposer.textScaleX.Text = scaleValue.ToString("0.000");
                MainWindow.main.stlComposer.textScaleY.Text = scaleValue.ToString("0.000");
                MainWindow.main.stlComposer.textScaleZ.Text = scaleValue.ToString("0.000");
            
                gIsShow = true;
                updateTxt();
                gIsShow = false;

                updateSliderValue(xyzbind);
            }
            catch { }
        }

        // Check if values of TextBox is valid.
        private void scaleLostFocus(object sender, RoutedEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            gIsShow = true;
            updateTxt();
            gIsShow = false;
        }

        public void updateTxt()
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            txtX.Text = stl.BoundingBox.Size.x.ToString("0.000");
            txtY.Text = stl.BoundingBox.Size.y.ToString("0.000");
            txtZ.Text = stl.BoundingBox.Size.z.ToString("0.000");
        }

        void updateSliderValue(Axis axis)
        {
            switch (axis)
            {
                case Axis.X:
                    slider_resize.Value = MainWindow.main.stlComposer.SingleSelectedModel.Scale.x * 100;
                    break;

                case Axis.Y:
                    slider_resize.Value = MainWindow.main.stlComposer.SingleSelectedModel.Scale.y * 100;
                    break;

                case Axis.Z:
                    slider_resize.Value = MainWindow.main.stlComposer.SingleSelectedModel.Scale.z * 100;
                    break;
            }
        }
    }
}

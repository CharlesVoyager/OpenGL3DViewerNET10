using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using View3D.model;
using View3D.view;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for UI_resize.xaml
    /// </summary>
    public partial class UI_resize : System.Windows.Controls.UserControl
    {
        public bool gTextChange = false;
        public UI_resize()
        {
            InitializeComponent();
            try
            {
                if (MainWindow.main != null)
                    MainWindow.main.languageChanged += translate;
            }
            catch { }
        }

        private void translate()
        {
            button_resize_reset.ToolTip = Trans.T("B_RESET");
            button_resize_reset.Content = Trans.T("B_RESET");
            button_mmtoinch.Content = Trans.T("L_MM")+" \r\n  > \r\n"+Trans.T("L_INCH")+" \r\n";
            button_inchtomm.Content = Trans.T("L_INCH") + " \r\n  > \r\n" + Trans.T("L_MM") + " \r\n";
        }

        private void slider_resize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (gTextChange == true)
                {
                    gTextChange = false;
                    return;
                }
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;

                //double centerValue = (stl.maxScaleVector + stl.minScaleVector) / 2.0;
                //double scaleValue = stl.Scale.x;

                double m = stl.m;
                double b = stl.b;
                double scaleValue;
                float oriZmin;
                oriZmin = stl.zMin;

                if (slider_resize.Value > 1)
                {
                    scaleValue = m * (slider_resize.Value) + b;
                }
                else
                {
                    scaleValue = slider_resize.Value;
                }

                if (stl.maxScaleVector == 1.0)
                {
                    scaleValue = slider_resize.Value / 2;
                }
                resize_textbox.Text = ((int)(scaleValue * 100)).ToString(CultureInfo.InvariantCulture) ;
            }
            catch { }
        }

        private void slider_resize_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if(stl == null) return;
        }

        private void slider_resize_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            if (e.Delta > 0)
                slider_resize.Value += 0.01;
            else
                slider_resize.Value -= 0.01;
            e.Handled = true;   
        }

        public void button_resize_reset_Click(object sender, RoutedEventArgs e)
        {
            float oriZmin;
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            
            oriZmin = stl.zMin;

            resize_textbox.Text = "100";
            MainWindow.main.stlComposer.textScaleX.Text = "1";
            MainWindow.main.stlComposer.check_stl_size_too_small();
        }

        private void resize_textbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
              
                double m = stl.m;
                double b = stl.b;
                double marginHardLimit = 1.0;
                double dx = (MainWindow.main.threeDSettings.PrintAreaWidth - marginHardLimit * 2) / stl.Model.boundingBox.Size.x;
                double dy = (MainWindow.main.threeDSettings.PrintAreaDepth - marginHardLimit * 2) / stl.Model.boundingBox.Size.y;
                double dz = (MainWindow.main.threeDSettings.PrintAreaHeight - marginHardLimit * 2) / stl.Model.boundingBox.Size.z;

                double maxScaleVector = Math.Min(dx, Math.Min(dy, dz));

                if (maxScaleVector <= 1.0)
                {
                    maxScaleVector = 1.0;
                }

                if (m == 0.0)
                {
                    stl.m = (maxScaleVector - 1) / 1;
                }

                if (b == 0.0)
                {
                    stl.b = maxScaleVector - stl.m * 2;
                }

                double scaleValue = 0.000;
                float oriZmin = stl.zMin;
                MainWindow.main.stlComposer.textScaleX.Text = Convert.ToString(int.Parse(resize_textbox.Text) / 100.00); 
                stl.LandToMinZ(oriZmin);
                MainWindow.main.UI_move.slider_moveZ.Minimum = stl.Position.z - stl.BoundingBox.zMin;
                MainWindow.main.stlComposer.UpdateOutOfBound();


                scaleValue = int.Parse(resize_textbox.Text) / 100.00;
                if (stl.maxScaleVector == 1.0)
                {
                    scaleValue = scaleValue * 2 ;
                }

                if (int.Parse(resize_textbox.Text) / 100.00 > 1)
                {
                    //scaleValue = m * (slider_resize.Value) + b;
                    scaleValue = (scaleValue - b) / m;
                }
                gTextChange = true;
                slider_resize.Value = scaleValue;
            }
            catch { }
        }

        private void button_mmtoinch_Click(object sender, RoutedEventArgs e)
        {
            PrintModel model = MainWindow.main.stlComposer.SingleSelectedModel;
            if (model == null) return;
            MainWindow.main.stlComposer.DoMmToInch(model);
        }

        private void button_inchtomm_Click(object sender, RoutedEventArgs e)
        {
            PrintModel model = MainWindow.main.stlComposer.SingleSelectedModel;
            if (model == null) return;
            MainWindow.main.stlComposer.DoInchtomm(model);
        }
    }
}

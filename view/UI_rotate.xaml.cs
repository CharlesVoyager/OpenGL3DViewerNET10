using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using View3D.model;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for UI_rotate.xaml
    /// </summary>
    /// 
    public partial class UI_rotate : System.Windows.Controls.UserControl
    {
        public bool partBuildInProgress = false;

        public UI_rotate()
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
            button_rotate_reset.ToolTip = Trans.T("B_RESET");
            button_rotate_reset.Content = Trans.T("B_RESET");
        }

        private double ConvertPositionAngel(System.Windows.Point soucePoint, System.Windows.Point targetPoint)
        {
            var res = (Math.Atan2(targetPoint.Y - soucePoint.Y, targetPoint.X - soucePoint.X)) / Math.PI * 180.0;
            res = (int)res;
            return (res >= 0 && res <= 180) ? res += 90 : ((res < 0 && res >= -90) ? res += 90 : res += 450);
        }

        private void StackPanelX_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanelX_MouseMove(sender, e);
        }

        private void StackPanelX_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            label_X.Visibility = Visibility.Hidden;
            textboxX.Visibility = Visibility.Visible;
            labelX.Visibility = Visibility.Visible;
            if (e.LeftButton == MouseButtonState.Pressed && textboxX.IsMouseDirectlyOver == false)
            {
                float oriZmin;
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                oriZmin = stl.zMin;
                sliderX.Value = ConvertPositionAngel(new System.Windows.Point(stackpanelX.Width / 2, stackpanelX.Height / 2), e.GetPosition(stackpanelX));
                //stl.LandToZ(oriZmin);
                //MainWindow.main.stlComposer.updateSTLState(stl);
            }
        }

        private void StackPanelX_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
        }

        private void StackPanelY_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanelY_MouseMove(sender, e);
        }

        private void StackPanelY_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            label_Y.Visibility = Visibility.Hidden;
            textboxY.Visibility = Visibility.Visible;
            labelY.Visibility = Visibility.Visible;
            if (e.LeftButton == MouseButtonState.Pressed && textboxY.IsMouseDirectlyOver == false)
            {
                float oriZmin;
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
     
                oriZmin = stl.zMin;
                sliderY.Value = ConvertPositionAngel(new System.Windows.Point(stackpanelY.Width / 2, stackpanelY.Height / 2), e.GetPosition(stackpanelY));
                //stl.LandToZ(oriZmin);
                //MainWindow.main.stlComposer.updateSTLState(stl);
            }
        }

        private void StackPanelY_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
        }

        private void StackPanelZ_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanelZ_MouseMove(sender, e);
        }

        private void StackPanelZ_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            label_Z.Visibility = Visibility.Hidden;
            textboxZ.Visibility = Visibility.Visible;
            labelZ.Visibility = Visibility.Visible;
            if (e.LeftButton == MouseButtonState.Pressed && textboxZ.IsMouseDirectlyOver == false)
            {
                float oriZmin;
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                oriZmin = stl.zMin;
                sliderZ.Value = ConvertPositionAngel(new System.Windows.Point(stackpanelZ.Width / 2, stackpanelZ.Height / 2), e.GetPosition(stackpanelZ));
            }
        }

        private void StackPanelZ_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
        }

        public void button_rotate_reset_Click(object sender, RoutedEventArgs e)
        {
            float oriZmin = 0.0f;
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            stl.reset = true;
            MainWindow.main.UI_move.button_land_Click(null, null);

            if (sliderX.Value == 0 && sliderY.Value == 0 && sliderZ.Value == 0)
            {
                stl.LandToZ(0);
                stl.Rotation.x = stl.Rotation.y = stl.Rotation.z = 0;
            }
            else
            {
                oriZmin = stl.zMin;
                stl.LandToZ(oriZmin);
            }

            sliderX.Value = 0;
            sliderY.Value = 0;
            sliderZ.Value = 0;
            stl.LandToZ(oriZmin);
            MainWindow.main.stlComposer.UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void stackpanelX_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (textboxX.IsFocused == true)
                return;
            textboxX.Visibility = Visibility.Hidden;
            labelX.Visibility = Visibility.Hidden;
            label_X.Visibility = Visibility.Visible;
        }

        private void stackpanelY_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (textboxY.IsFocused == true)
                return;
            textboxY.Visibility = Visibility.Hidden;
            labelY.Visibility = Visibility.Hidden;
            label_Y.Visibility = Visibility.Visible;
        }

        private void stackpanelZ_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (textboxZ.IsFocused == true)
                return;
            textboxZ.Visibility = Visibility.Hidden;
            labelZ.Visibility = Visibility.Hidden;
            label_Z.Visibility = Visibility.Visible;
        }

        private void orangeX_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            float oriZmin;
            oriZmin = stl.zMin;

            if (e.Delta > 0)
                sliderX.Value++;
            else
                sliderX.Value--;
            e.Handled = true;

            stl.LandToZ(oriZmin);
        }

        private void orangeY_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            float oriZmin;
            oriZmin = stl.zMin;

            if (e.Delta > 0)
                sliderY.Value++;
            else
                sliderY.Value--;
            e.Handled = true;

            stl.LandToZ(oriZmin);
        }

        private void orangeZ_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            float oriZmin;
            oriZmin = stl.zMin;

            if (e.Delta > 0)
                sliderZ.Value++;
            else
                sliderZ.Value--;
            e.Handled = true;

            stl.LandToZ(oriZmin);
        }

        private void textboxX_LostFocus(object sender, RoutedEventArgs e)
        {
            textboxX.Visibility = Visibility.Hidden;
            labelX.Visibility = Visibility.Hidden;
            label_X.Visibility = Visibility.Visible;
        }

        private void textboxY_LostFocus(object sender, RoutedEventArgs e)
        {
            textboxY.Visibility = Visibility.Hidden;
            labelY.Visibility = Visibility.Hidden;
            label_Y.Visibility = Visibility.Visible;
        }

        private void textboxZ_LostFocus(object sender, RoutedEventArgs e)
        {
            textboxZ.Visibility = Visibility.Hidden;
            labelZ.Visibility = Visibility.Hidden;
            label_Z.Visibility = Visibility.Visible;
        }

        private void textboxZ_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Key == Key.Up)
            {
                sliderZ.Value++;
            }
            else if (e.Key == Key.Down)
            {
                sliderZ.Value--;
            }
            else if (e.Key == Key.Enter)
            {
            }
        }

        private void textboxY_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Key == Key.Up)
            {
                sliderY.Value++;
            }
            else if (e.Key == Key.Down)
            {
                sliderY.Value--;
            }
            else if (e.Key == Key.Enter)
            {
            }
        }

        private void textboxX_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Key == Key.Up)
            {
                sliderX.Value++;
            }
            else if (e.Key == Key.Down)
            {
                sliderX.Value--;
            }
            else if (e.Key == Key.Enter)
            {
            }
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            if (e.Text == "")
            {
                return;
            }

            char c = Convert.ToChar(e.Text);
            if (Char.IsNumber(c) || e.Text.Trim() == ".")
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
            if (e.Text.Trim() == ".")
            {
                if (textboxX.IsFocused)
                {
                    if (textboxX.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
                else if (textboxY.IsFocused)
                {
                    if (textboxY.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
                else if (textboxZ.IsFocused)
                {
                    if (textboxZ.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
            }
            base.OnPreviewTextInput(e);
        }

        private void textboxX_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                limitRotateAngle(textboxX);
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                float oriZmin;
                oriZmin = stl.zMin;
                sliderX.Value = Convert.ToDouble(textboxX.Text);
                MainWindow.main.stlComposer.textRotX.Text = textboxX.Text;
                stl.LandToZ(oriZmin);
                stl.UpdateBoundingBoxAndMatrix();
                MainWindow.main.stlComposer.UpdateOutOfBound();
            }
            catch { }
        }

        private void textboxY_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                limitRotateAngle(textboxY);
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                float oriZmin;

                oriZmin = stl.zMin;
                sliderY.Value = Convert.ToDouble(textboxY.Text);
                MainWindow.main.stlComposer.textRotY.Text = textboxY.Text;
                stl.LandToZ(oriZmin);
                stl.UpdateBoundingBoxAndMatrix();
                MainWindow.main.stlComposer.UpdateOutOfBound();
            }
            catch { }
        }

        private void textboxZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                limitRotateAngle(textboxZ);
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                float oriZmin;

                oriZmin = stl.zMin;
                sliderZ.Value = Convert.ToDouble(textboxZ.Text);
                MainWindow.main.stlComposer.textRotZ.Text = textboxZ.Text;
                stl.LandToZ(oriZmin);
                stl.UpdateBoundingBoxAndMatrix();
                MainWindow.main.stlComposer.UpdateOutOfBound();
            }
            catch { }
        }

        // Input angle must between 0~360 degree
        private void limitRotateAngle(System.Windows.Controls.TextBox textbox)
        {
            if (Convert.ToDouble(textbox.Text) >= 360 || textbox.Text.Length > 3)
            {
                textbox.Text = "360";
            }
            else if (Convert.ToDouble(textbox.Text) <= 0)
            {
                textbox.Text = "0";
            }
        }
    }
}


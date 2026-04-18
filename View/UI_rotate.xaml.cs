using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenGL3DViewerNET10.ModelLib.model;
using OpenGL3DViewerNET10.ModelLib.Utils;

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

        private void StackPanelX_MouseMove(object sender, MouseEventArgs e)
        {
            labelX.Visibility = Visibility.Hidden;
            textRotX.Visibility = Visibility.Visible;
            if (e.LeftButton == MouseButtonState.Pressed && textRotX.IsMouseDirectlyOver == false)
            {
                ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                textRotX.Text = ConvertPositionAngel(new Point(stackpanelX.Width / 2, stackpanelX.Height / 2), e.GetPosition(stackpanelX)).ToString();
            }
        }

        private void StackPanelY_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanelY_MouseMove(sender, e);
        }

        private void StackPanelY_MouseMove(object sender, MouseEventArgs e)
        {
            labelY.Visibility = Visibility.Hidden;
            textRotY.Visibility = Visibility.Visible;
            if (e.LeftButton == MouseButtonState.Pressed && textRotY.IsMouseDirectlyOver == false)
            {
                ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                textRotY.Text = ConvertPositionAngel(new Point(stackpanelY.Width / 2, stackpanelY.Height / 2), e.GetPosition(stackpanelY)).ToString();
            }
        }

        private void StackPanelZ_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanelZ_MouseMove(sender, e);
        }

        private void StackPanelZ_MouseMove(object sender, MouseEventArgs e)
        {
            labelZ.Visibility = Visibility.Hidden;
            textRotZ.Visibility = Visibility.Visible;
            if (e.LeftButton == MouseButtonState.Pressed && textRotZ.IsMouseDirectlyOver == false)
            {
                ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                textRotZ.Text = ConvertPositionAngel(new Point(stackpanelZ.Width / 2, stackpanelZ.Height / 2), e.GetPosition(stackpanelZ)).ToString();
            }
        }

        public void button_rotate_reset_Click(object sender, RoutedEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;


            if (textRotX.Text == "0" && textRotY.Text == "0" && textRotZ.Text == "0")
            {
                // Consider the case: Load a model -> Load another model then rotate -> Select a model then reset -> Select second model -> Click reset -> It will hit here...
                textRotX_TextChanged(null, null);
                textRotY_TextChanged(null, null);
                textRotZ_TextChanged(null, null);
            }
            else
            {
                // slider value will trigger textbox value changed event, so no need to update textbox value here
                textRotX.Text = "0";
                textRotY.Text = "0";
                textRotZ.Text = "0";
            }

            stl.UpdateBoundingBoxAndMatrix();
            MainWindow.main.stlComposer.UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void stackpanelX_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (textRotX.IsFocused == true)
                return;
            textRotX.Visibility = Visibility.Hidden;
            labelX.Visibility = Visibility.Visible;
        }

        private void stackpanelY_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (textRotY.IsFocused == true)
                return;
            textRotY.Visibility = Visibility.Hidden;
            labelY.Visibility = Visibility.Visible;
        }

        private void stackpanelZ_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (textRotZ.IsFocused == true)
                return;
            textRotZ.Visibility = Visibility.Hidden;
            labelZ.Visibility = Visibility.Visible;
        }

        private void orangeX_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Delta > 0)
                textRotX.Text = (Convert.ToDouble(textRotX.Text) + 1).ToString();
            else
                textRotX.Text = (Convert.ToDouble(textRotX.Text) - 1).ToString();
            e.Handled = true;
        }

        private void orangeY_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Delta > 0)
                textRotY.Text = (Convert.ToDouble(textRotY.Text) + 1).ToString();
            else
                textRotY.Text = (Convert.ToDouble(textRotY.Text) - 1).ToString();
            e.Handled = true;
        }

        private void orangeZ_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Delta > 0)
                textRotZ.Text = (Convert.ToDouble(textRotZ.Text) + 1).ToString();
            else
                textRotZ.Text = (Convert.ToDouble(textRotZ.Text) - 1).ToString();
            e.Handled = true;
        }

        private void textboxX_LostFocus(object sender, RoutedEventArgs e)
        {
            textRotX.Visibility = Visibility.Hidden;
            labelX.Visibility = Visibility.Visible;
        }

        private void textboxY_LostFocus(object sender, RoutedEventArgs e)
        {
            textRotY.Visibility = Visibility.Hidden;
            labelY.Visibility = Visibility.Visible;
        }

        private void textboxZ_LostFocus(object sender, RoutedEventArgs e)
        {
            textRotZ.Visibility = Visibility.Hidden;
            labelZ.Visibility = Visibility.Visible;
        }

        private void textboxZ_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Key == Key.Up)
            {
                textRotZ.Text = (Convert.ToDouble(textRotZ.Text) + 1).ToString();
            }
            else if (e.Key == Key.Down)
            {
                textRotZ.Text = (Convert.ToDouble(textRotZ.Text) - 1).ToString();
            }
            else if (e.Key == Key.Enter)
            {
            }
        }

        private void textboxY_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Key == Key.Up)
            {
                textRotY.Text = (Convert.ToDouble(textRotY.Text) + 1).ToString();
            }
            else if (e.Key == Key.Down)
            {
                textRotY.Text = (Convert.ToDouble(textRotY.Text) - 1).ToString();
            }
            else if (e.Key == Key.Enter)
            {
            }
        }

        private void textboxX_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
            if (e.Key == Key.Up)
            {
                textRotX.Text = (Convert.ToDouble(textRotX.Text) + 1).ToString();
            }
            else if (e.Key == Key.Down)
            {
                textRotX.Text = (Convert.ToDouble(textRotX.Text) - 1).ToString();
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
                if (textRotX.IsFocused)
                {
                    if (textRotX.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
                else if (textRotY.IsFocused)
                {
                    if (textRotY.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
                else if (textRotZ.IsFocused)
                {
                    if (textRotZ.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
            }
            base.OnPreviewTextInput(e);
        }

        private void textRotX_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                limitRotateAngle(textRotX);
                ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                orangeX.Angle = Convert.ToDouble(textRotX.Text);
                MainWindow.main.stlComposer.textRotX.Text = textRotX.Text;
            }
            catch { }
        }
        private void textRotY_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                limitRotateAngle(textRotY);
                ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                orangeY.Angle = Convert.ToDouble(textRotY.Text);
                MainWindow.main.stlComposer.textRotY.Text = textRotY.Text;
            }
            catch { }
        }

        private void textRotZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                limitRotateAngle(textRotZ);
                ThreeDModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
                orangeZ.Angle = Convert.ToDouble(textRotZ.Text);
                MainWindow.main.stlComposer.textRotZ.Text = textRotZ.Text;
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


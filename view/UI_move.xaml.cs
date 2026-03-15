using OpenTK.Mathematics;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using View3D.model;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for UI_move.xaml
    /// </summary>
    public partial class UI_move : System.Windows.Controls.UserControl
    {
        public UI_move()
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
            button_move_reset.ToolTip = Trans.T("B_RESET");
            button_land.ToolTip = Trans.T("B_LAND");

            button_move_reset.Content = Trans.T("B_RESET");
            button_land.Content = Trans.T("B_LAND");
        }

        protected override void OnPreviewTextInput( TextCompositionEventArgs e)
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
                if (moveX_textbox.IsFocused)
                {
                    if (moveX_textbox.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
                else if (moveY_textbox.IsFocused)
                {
                    if (moveY_textbox.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
                else if (moveZ_textbox.IsFocused)
                {
                    if (moveZ_textbox.Text.IndexOf(".") != -1)
                    {
                        e.Handled = true;
                    }
                }
            }
            base.OnPreviewTextInput(e);
        }

        public void button_move_reset_Click(object sender, RoutedEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            slider_moveX.Value = stl.Position.inix;
            slider_moveY.Value = stl.Position.iniy;
            slider_moveZ.Value = stl.Position.iniz;

            slider_moveX.Value = Math.Round(slider_moveX.Value);
            slider_moveY.Value = Math.Round(slider_moveY.Value);
            if (slider_moveZ.Value != Convert.ToDouble(moveZ_textbox.Text) + slider_moveZ.Minimum)
            {
                slider_moveZ.Value = Convert.ToDouble(moveZ_textbox.Text) + slider_moveZ.Minimum;
            }

            moveX_textbox.Text = slider_moveX.Value.ToString();
            moveY_textbox.Text = slider_moveY.Value.ToString();
            moveZ_textbox.Text = (Math.Round(slider_moveZ.Value - slider_moveZ.Minimum, 3)).ToString();

            MainWindow.main.stlComposer.landModel(stl);
            MainWindow.main.stlComposer.updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();

            stl.modifiedM = false;
        }

        public void button_land_Click(object sender, RoutedEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;


            MainWindow.main.stlComposer.landModel(stl);

            slider_moveX.Value = Math.Round(stl.Position.x);
            slider_moveY.Value = Math.Round(stl.Position.y);
            slider_moveZ.Value = stl.Position.z;
            moveX_textbox.Text = slider_moveX.Value.ToString();
            moveY_textbox.Text = slider_moveY.Value.ToString();

            moveZ_textbox.Text = (Math.Round(slider_moveZ.Value - slider_moveZ.Minimum, 1)).ToString();

            MainWindow.main.stlComposer.updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void slider_moveX_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                slider_moveX.Value++;
            else
                slider_moveX.Value--;
            e.Handled = true;   
        }

        private void slider_moveY_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                slider_moveY.Value++;
            else
                slider_moveY.Value--;
            e.Handled = true;     
        }

        private void slider_moveZ_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                slider_moveZ.Value++;
            else
                slider_moveZ.Value--;
            e.Handled = true;   
        }

        private void slider_moveX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (Math.Abs(e.OldValue - e.NewValue) > 0.01)
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    stl.modifiedM = true;
                    MainWindow.main.stlComposer.textTransX.Text = Convert.ToString(slider_moveX.Value);

                    stl.curPos.Row3 = new Vector4((float)slider_moveX.Value, (float)slider_moveY.Value, (float)slider_moveZ.Value, 1f);
                    MainWindow.main.threeDControl.UpdateChanges();
                }
            }
            catch { }
        }

        private void slider_moveY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (Math.Abs(e.OldValue - e.NewValue) > 0.01)
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    stl.modifiedM = true;
                    MainWindow.main.stlComposer.textTransY.Text = Convert.ToString(slider_moveY.Value);

                    stl.curPos.Row3 = new Vector4((float)slider_moveX.Value, (float)slider_moveY.Value, (float)slider_moveZ.Value, 1f);
                    MainWindow.main.threeDControl.UpdateChanges();
                }
            }
            catch { }
        }

        private void slider_moveZ_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (Math.Abs(e.OldValue - e.NewValue) > 0.0001)
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    stl.modifiedM = true;
                    MainWindow.main.stlComposer.textTransZ.Text = Convert.ToString(slider_moveZ.Value);

                    stl.curPos.Row3 = new Vector4((float)slider_moveX.Value, (float)slider_moveY.Value, (float)slider_moveZ.Value, 1f);
                    MainWindow.main.threeDControl.UpdateChanges();
                }
            }
            catch { }
        }

        private void moveX_textbox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    slider_moveX.Value = Convert.ToDouble(moveX_textbox.Text);
                    if (slider_moveX.Value != Convert.ToDouble(moveX_textbox.Text))
                    {
                        moveX_textbox.Text = slider_moveX.Value.ToString();
                    }
                }
                catch { }
            }
            if (moveX_textbox.Text.Trim() == "")
            {
                moveX_textbox.Text = "";
            }
        }

        private void moveY_textbox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    slider_moveY.Value = Convert.ToDouble(moveY_textbox.Text);
                    if (slider_moveY.Value != Convert.ToDouble(moveY_textbox.Text))
                    {
                        moveY_textbox.Text = slider_moveY.Value.ToString();
                    }
                }
                catch { }
            }
            if (moveY_textbox.Text.Trim() == "")
            {
                moveY_textbox.Text = "";
            }
        }

        private void moveZ_textbox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    Double temp=slider_moveZ.Value;
                        slider_moveZ.Value = Convert.ToDouble(moveZ_textbox.Text) + slider_moveZ.Minimum;
                    if (temp == slider_moveZ.Value)
                    {
                        moveZ_textbox.Text = slider_moveZ.Value.ToString();
                    }
                }
                catch { }
            }
            if (moveZ_textbox.Text.Trim() == "")
            {
                moveZ_textbox.Text = "";
            }
        }

        private void slider_moveX_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
        }

        private void slider_moveY_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
        }

        private void slider_moveZ_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;
        }

        private void slider_moveX_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Down)
            {
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
            }
        }

        private void slider_moveY_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Down)
            {
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
            }
        }

        private void slider_moveZ_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Down)
            {
                PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                if (stl == null) return;
            }
        }

        private void moveX_textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (moveX_textbox.Text.Trim() == "")
            {
                moveX_textbox.Text = slider_moveX.Value.ToString();
            }
        }

        private void moveY_textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (moveY_textbox.Text.Trim() == "")
            {
                moveY_textbox.Text = slider_moveY.Value.ToString();
            }
        }

        private void moveZ_textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (moveZ_textbox.Text.Trim() == "")
            {
                moveZ_textbox.Text = slider_moveZ.Value.ToString();
            }
        }
    }
}
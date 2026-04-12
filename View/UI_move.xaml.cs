using System.Windows;
using System.Windows.Input;
using OpenGL3DViewerNET10.ModelLib.model;
using OpenGL3DViewerNET10.ModelLib.Utils;

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

        public void Initial()
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            slider_moveX.Maximum = 1000;
            slider_moveX.Minimum = -1000;
            slider_moveY.Maximum = 1000;
            slider_moveY.Minimum = -1000;
            slider_moveZ.Maximum = 1000;
            slider_moveZ.Minimum = -1000;
            slider_moveX.Value = stl.Position.x;
            slider_moveY.Value = stl.Position.y;
            slider_moveZ.Value = stl.Position.z;

            double moveMax, moveMin;

            moveMax = (int)Math.Floor((MainWindow.main.threeDSettings.PrintAreaWidth - (stl.BoundingBox.xMax - stl.Position.x)) * 100) * 0.01;
            moveMin = (int)Math.Ceiling((stl.Position.x - stl.BoundingBox.xMin) * 100) * 0.01;

            double a = MainWindow.main.threeDSettings.PrintAreaWidth - (stl.BoundingBox.xMax - stl.Position.x);
            double b = stl.Position.x - stl.BoundingBox.xMin;

            a += 0;
            b += 0;

            //module is out of bound,it cannot move. 
            if (moveMin > moveMax)
                slider_moveX.Value = (float)(moveMin + moveMax) / 2;
            else if (moveMin <= stl.Position.x && stl.Position.x <= moveMax)//module is in of bound.
                slider_moveX.Value = stl.Position.x;
            else if (stl.Position.x > moveMax)//model is out of bound(too big), but it can move.
                slider_moveX.Value = moveMax;
            else // (moveMin > stl.Position.x)//model is out of bound(too small), but it can move.
                slider_moveX.Value = moveMin;

            if (moveMin > moveMax)
            {
                slider_moveX.Maximum = (float)(moveMin + moveMax) / 2;
                slider_moveX.Minimum = (float)(moveMin + moveMax) / 2;
            }
            else
            {
                slider_moveX.Maximum = moveMax;
                slider_moveX.Minimum = moveMin;
            }


            moveMax = (int)Math.Floor((MainWindow.main.threeDSettings.PrintAreaDepth - (stl.BoundingBox.yMax - stl.Position.y)) * 100) * 0.01;
            moveMin = (int)Math.Ceiling((stl.Position.y - stl.BoundingBox.yMin) * 100) * 0.01;

            //module is out of bound,it can not move. 
            if (moveMin > moveMax)
                slider_moveY.Value = (float)(moveMin + moveMax) / 2;
            else if (moveMin <= stl.Position.y && stl.Position.y <= moveMax)//module is in of bound.
                slider_moveY.Value = stl.Position.y;
            else if (stl.Position.y > moveMax)//model is out of bound(too big), but it can move.
                slider_moveY.Value = moveMax;
            else // (moveMin > stl.Position.y)//model is out of bound(too small), but it can move.
                slider_moveY.Value = moveMin;

            if (moveMin > moveMax)
            {
                slider_moveY.Maximum = (float)(moveMin + moveMax) / 2;
                slider_moveY.Minimum = (float)(moveMin + moveMax) / 2;
            }
            else
            {
                slider_moveY.Maximum = moveMax;
                slider_moveY.Minimum = moveMin;
            }

            moveMax = MainWindow.main.threeDSettings.PrintAreaHeight - (stl.BoundingBox.zMax - stl.Position.z);
            moveMin = stl.Position.z - stl.BoundingBox.zMin;
            if (moveMin > moveMax)
                moveMin = moveMax;
            if (moveMin <= stl.Position.z && moveMax >= stl.Position.z)
                slider_moveZ.Value = stl.Position.z;
            else if (moveMax < stl.Position.z)
                slider_moveZ.Value = moveMax;
            else // (moveMin > stl.Position.z)
                slider_moveZ.Value = moveMin;
            slider_moveZ.Maximum = moveMax;
            slider_moveZ.Minimum = moveMin;
        }

        public void button_move_reset_Click(object sender, RoutedEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            slider_moveX.Value = stl.InitialPosition.x;
            slider_moveY.Value = stl.InitialPosition.y;
            slider_moveZ.Value = stl.InitialPosition.z;

            stl.Land();

            MainWindow.main.stlComposer.UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void button_land_Click(object sender, RoutedEventArgs e)
        {
            PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
            if (stl == null) return;

            stl.Land();

            slider_moveX.Value = stl.Position.x;
            slider_moveY.Value = stl.Position.y;
            slider_moveZ.Value = stl.Position.z;
 
            MainWindow.main.stlComposer.UpdateOutOfBound();
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
                    MainWindow.main.stlComposer.textTransX.Text = slider_moveX.Value.ToString("0.000");
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
                    MainWindow.main.stlComposer.textTransY.Text = slider_moveY.Value.ToString("0.000");
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
                    MainWindow.main.stlComposer.textTransZ.Text = slider_moveZ.Value.ToString("0.000");
                }
            }
            catch { }
        }

        private void moveX_textbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    slider_moveX.Value = Convert.ToDouble(moveX_textbox.Text);
                }
                catch { }
            }
        }

        private void moveY_textbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    slider_moveY.Value = Convert.ToDouble(moveY_textbox.Text);
                }
                catch { }
            }
        }

        private void moveZ_textbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                try
                {
                    PrintModel stl = MainWindow.main.stlComposer.SingleSelectedModel;
                    if (stl == null) return;
                    slider_moveZ.Value = Convert.ToDouble(moveZ_textbox.Text);
                }
                catch { }
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
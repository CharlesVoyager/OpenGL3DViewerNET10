using System.Windows;
using System.Windows.Controls;
using View3D.model;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class UI_view : System.Windows.Controls.UserControl
    {
        public UI_view()
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
            top_button.ToolTip = Trans.T("B_TOP");
            left_button.ToolTip = Trans.T("B_LEFT");
            right_button.ToolTip = Trans.T("B_RIGHT");
            front_button.ToolTip = Trans.T("B_FRONT");
            back_button.ToolTip = Trans.T("B_BACK");
            bottom_button.ToolTip = Trans.T("B_BOTTOM");
            view_resetButton.ToolTip = Trans.T("B_RESET");

            top_button.Content = Trans.T("B_TOP");
            left_button.Content = Trans.T("B_LEFT");
            right_button.Content = Trans.T("B_RIGHT");
            front_button.Content = Trans.T("B_FRONT");
            back_button.Content = Trans.T("B_BACK");
            bottom_button.Content = Trans.T("B_BOTTOM");
            view_resetButton.Content = Trans.T("B_RESET");
        }


        private void top_button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.threeDCamera.OnTopView();
        }

        private void bottom_button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.threeDCamera.OnBottomView();
        }

        private void front_button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.threeDCamera.OnFrontView();
        }

        private void back_button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.threeDCamera.OnBackView();
        }

        private void left_button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.threeDCamera.OnLeftView();
        }

        private void right_button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.threeDCamera.OnRightView();
        }

        private void view_resetButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.main.threeDCamera.OnIsometricView();
        }
    }
}

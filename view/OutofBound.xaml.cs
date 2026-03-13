using System.Windows.Controls;
using View3D.model;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for BusyWindow.xaml
    /// </summary>
    public partial class OutofBound : System.Windows.Controls.UserControl
    {
        public OutofBound()
        {
            InitializeComponent();
            try
            {
                MainWindow.main.languageChanged += translate;
            }
            catch { }
        }

        private void translate()
        {
            txt_WarningMsg.Text = Trans.T("L_OUT_OF_BOUNDARY");
        }
    }
}

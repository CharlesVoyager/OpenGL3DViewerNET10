using OpenGL3DViewerNET10.ModelLib.Utils;
using System.Windows.Controls;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for OutofBound.xaml
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

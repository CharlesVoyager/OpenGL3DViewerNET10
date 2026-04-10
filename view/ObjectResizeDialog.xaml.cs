using OpenGL3DViewerNET10.ModelLib.Utils;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for ObjectResizeDialog.xaml
    /// </summary>
    public partial class ObjectResizeDialog : Window
    {
        public bool gIsNo = false;
        public bool gIsInch = false;
        public bool gIsScale = false;

        public ObjectResizeDialog(double originalSizeX, double originalSizeY, double originalSizeZ)
        {
            InitializeComponent();

            MainWindow.main.languageChanged += translate;

            double newSizeMMx = 0, newSizeMMy = 0, newSizeMMz = 0;

            double gx = 0.0;
            double gy = 0.0;
            double gz = 0.0;
  
            gx = originalSizeX;
            gy = originalSizeY;
            gz = originalSizeZ;

            double targetLargestSize = MainWindow.main.threeDSettings.PrintAreaWidth / 2;

            newSizeMMx = gx;
            newSizeMMy = gy;
            newSizeMMz = gz;

            double Max = Math.Max(Math.Max(gx, gy), gz);

            if ((gx == gy) && (gy == gz))
            {
                newSizeMMx = targetLargestSize;
                newSizeMMy = targetLargestSize;
                newSizeMMz = targetLargestSize;
            }
            else
            {
                if (gx == Max)
                {
                    newSizeMMx = targetLargestSize;
                    newSizeMMy = targetLargestSize / gx * newSizeMMy;
                    newSizeMMz = targetLargestSize / gx * newSizeMMz;
                }
                else if (gy == Max)
                {
                    newSizeMMx = targetLargestSize / gy * newSizeMMx;
                    newSizeMMy = targetLargestSize;
                    newSizeMMz = targetLargestSize / gy * newSizeMMz;
                }
                else if (gz == Max)
                {
                    newSizeMMx = targetLargestSize / gz * newSizeMMx;
                    newSizeMMy = targetLargestSize / gz * newSizeMMy;
                    newSizeMMz = targetLargestSize;
                }
            }
            txtOriginalSize.Text = txtOriginalSize.Text.Replace("§", " ");
            txtInchScale.Text = txtInchScale.Text.Replace("§", " ");
            txtAutoScale.Text = txtAutoScale.Text.Replace("§", " ");
            string tOriginalSize = gx.ToString("0.000") + " X " + gy.ToString("0.000") + " X " + gz.ToString("0.000") + " mm\u00B3";
            string tInchScale = gx.ToString("0.000") + " X " + gy.ToString("0.000") + " X " + gz.ToString("0.000") + " inch\u00B3";
            string tAutoScale = newSizeMMx.ToString("0.000") + " X " + newSizeMMy.ToString("0.000") + " X " + newSizeMMz.ToString("0.000") + " mm\u00B3";
            txtOriginalSize.Inlines.Add(new Run(tOriginalSize.ToString(CultureInfo.InvariantCulture)) { FontWeight = FontWeights.Bold });
            txtInchScale.Inlines.Add(new Run(tInchScale.ToString(CultureInfo.InvariantCulture)) { FontWeight = FontWeights.Bold });
            txtAutoScale.Inlines.Add(new Run(tAutoScale.ToString(CultureInfo.InvariantCulture)) { FontWeight = FontWeights.Bold });
        }

        private void translate()
        {
            txtTitle.Text = Trans.T("W_OBJ_TOO_SMALL");
            txtContent.Text = Trans.T("M_OBJ_SCALE_YES_NO");
            txtOriginalSize.Text = Trans.T("M_OBJ_ORI_SIZE");
            txtInchScale.Text = Trans.T("M_INCH_SIZE");
            txtAutoScale.Text = Trans.T("M_AUTO_SCALE_SIZE");
            Button_No.Content = Trans.T("B_NO");
            Button_AutoScale.Content = Trans.T("B_AUTO_SCALE");
            Button_Inch.Content = Trans.T("B_IMPORT_INCH");
        }

        private void Button_No_Click(object sender, RoutedEventArgs e)
        {
            gIsNo = true;
            this.Close();
        }

        private void Button_Inch_Click(object sender, RoutedEventArgs e)
        {
            gIsInch = true;
            this.Close();
        }

        private void Button_AutoScale_Click(object sender, RoutedEventArgs e)
        {
            gIsScale = true;
            this.Close();
        }
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using View3D.model;

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

        public double newSizeMMx = 0, newSizeMMy = 0, newSizeMMz = 0;

        double gx = 0.0;
        double gy = 0.0;
        double gz = 0.0;

        public ObjectResizeDialog(double px, double py, double pz)
        {
            InitializeComponent();

            MainWindow.main.languageChanged += translate;
  
            gx = px;
            gy = py;
            gz = pz;

            string tx = gx.ToString("0.000");
            string ty = gy.ToString("0.000");
            string tz = gz.ToString("0.000");
            newSizeMMx = gx;
            newSizeMMy = gy;
            newSizeMMz = gz;
            double Max = Math.Max(Math.Max(gx, gy), gz);
            if ((gx == gy) && (gy == gz))
            {
                newSizeMMx = 25.000;
                newSizeMMy = 25.000;
                newSizeMMz = 25.000;
            }
            else
            {
                if (gx == Max)
                {
                    newSizeMMx = 25.000;
                    newSizeMMy = 25.000 / gx * newSizeMMy;
                    newSizeMMz = 25.000 / gx * newSizeMMz;
                }
                else if (gy == Max)
                {
                    newSizeMMx = 25.000 / gy * newSizeMMx;
                    newSizeMMy = 25.000;
                    newSizeMMz = 25.000 / gy * newSizeMMz;
                }
                else if (gz == Max)
                {
                    newSizeMMx = 25.000 / gz * newSizeMMx;
                    newSizeMMy = 25.000 / gz * newSizeMMy;
                    newSizeMMz = 25.000;
                }
            }
            txtOriginalSize.Text = txtOriginalSize.Text.Replace("§", " ");
            txtInchScale.Text = txtInchScale.Text.Replace("§", " ");
            txtAutoScale.Text = txtAutoScale.Text.Replace("§", " ");
            string tOriginalSize = tx.ToString() + " X " + ty.ToString() + " X " + tz.ToString() + " mm\u00B3";
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

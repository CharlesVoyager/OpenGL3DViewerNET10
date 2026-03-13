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
        public bool gIsNo =false;
        public bool gIsInch = false;
        public bool gIsScale = false;

        public static double scaleInchx = 0, scaleInchy = 0, scaleInchz = 0,scaleMMx = 0, scaleMMy = 0, scaleMMz = 0;

        double gx = 0.0;
        double gy = 0.0;
        double gz = 0.0;

        public ObjectResizeDialog(double px,double py, double pz)
        {
            InitializeComponent();

            MainWindow.main.languageChanged += translate;
  
            gx = px;
            gy = py;
            gz = pz;

            string tx = gx.ToString("0.000");
            string ty = gy.ToString("0.000");
            string tz = gz.ToString("0.000");
            double inchx = gx * 25.4;
            double inchy = gy * 25.4;
            double inchz = gz * 25.4;
            double scalex = gx;
            double scaley = gy;
            double scalez = gz;
            double Max = Math.Max(Math.Max(gx, gy), gz);
            if ((gx == gy) && (gy == gz))
            {
                scalex = 25.000;
                scaley = 25.000;
                scalez = 25.000;
            }
            else
            {
                if (gx == Max)
                {
                    scalex = 25.000;
                    scaley = 25.000 / gx * scaley;
                    scalez = 25.000 / gx * scalez;
                }
                else if (gy == Max)
                {
                    scalex = 25.000 / gy * scalex;
                    scaley = 25.000;
                    scalez = 25.000 / gy * scalez;
                }
                else if (gz == Max)
                {
                    scalex = 25.000 / gz * scalex;
                    scaley = 25.000 / gz * scaley;
                    scalez = 25.000;
                }
            }
            txtOriginalSize.Text = txtOriginalSize.Text.Replace("§", " ");
            txtInchScale.Text = txtInchScale.Text.Replace("§", " ");
            txtAutoScale.Text = txtAutoScale.Text.Replace("§", " ");
            string tOriginalSize = tx.ToString() + " X " + ty.ToString() + " X " + tz.ToString() + " mm\u00B3";
            //string tInchScale = inchx.ToString("0.000") + " X " + inchy.ToString("0.000") + " X " + inchz.ToString("0.000") + " mm\u00B3";
            string tInchScale = gx.ToString("0.000") + " X " + gy.ToString("0.000") + " X " + gz.ToString("0.000") + " inch\u00B3";     // According to DQA's suggestion, follow JetR's design
            string tAutoScale = scalex.ToString("0.000") + " X " + scaley.ToString("0.000") + " X " + scalez.ToString("0.000") + " mm\u00B3";
            txtOriginalSize.Inlines.Add(new Run(tOriginalSize.ToString(CultureInfo.InvariantCulture)) { FontWeight = FontWeights.Bold });
            txtInchScale.Inlines.Add(new Run(tInchScale.ToString(CultureInfo.InvariantCulture)) { FontWeight = FontWeights.Bold });
            txtAutoScale.Inlines.Add(new Run(tAutoScale.ToString(CultureInfo.InvariantCulture)) { FontWeight = FontWeights.Bold });
            scaleInchx = inchx; scaleInchy = inchy; scaleInchz = inchz;
            scaleMMx = scalex; scaleMMy = scaley; scaleMMz = scalez;
        }

        private void translate()
        {
            txtTitle.Text = Trans.T("W_OBJ_TOO_SMALL");
            txtContent.Text = Trans.T("M_OBJ_SCALE_YES_NO");
            txtOriginalSize.Text = Trans.T("M_OBJ_ORI_SIZE");
            txtInchScale.Text = Trans.T("M_INCH_SIZE");
            txtAutoScale.Text = Trans.T("M_AUTO_SCALE_SIZE");
            Button_No.Content = Trans.T("B_NO");
            Button_Yes.Content = Trans.T("B_AUTO_SCALE");
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

        private void Button_Yes_Click(object sender, RoutedEventArgs e)
        {
            gIsScale = true;
            this.Close();
        }
    }
}

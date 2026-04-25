using OpenGL3DViewerNET10.ModelLib.model;
using View3D.model.geom;

namespace View3D.view
{
    /// <summary>
    /// Interaction logic for UI_object_information.xaml
    /// </summary>
    public partial class UI_object_information : System.Windows.Controls.UserControl
    {
        public UI_object_information()
        {
            InitializeComponent();

            try
            {
                if (MainWindow.main != null)
                    MainWindow.main.languageChanged += translate;
            }
            catch { }
        }

        public void translate()
        {
        }

        public void Analyse(ThreeDModel pm)
        {
            //Stopwatch sw = Stopwatch.StartNew();

            if (pm == null || pm.Model == null)
            {
                txtVolume.Text = "0.000 cm³";
                txtSizeX.Text = "0.000 mm";
                txtSizeY.Text = "0.000 mm";
                txtSizeZ.Text = "0.000 mm";
                txtCollision.Text = "";
                txtFilename.Text = "";
                txtPosX.Text = "0.000";
                txtPosY.Text = "0.000";
                txtPosZ.Text = "0.000";
                return;
            }

            double volume = 0;
            foreach (TopoTriangle t in pm.Model.drawTriangles)
                volume += t.SignedVolume();

            volume = volume * pm.Scale.x * pm.Scale.y * pm.Scale.z;

            RHBoundingBox bbox = pm.BoundingBox;

            string CubicCM = (0.001 * Math.Abs(volume)).ToString("0.000");
            if (CubicCM == "0.000")
            { CubicCM = "0.001"; }

            txtVolume.Text = CubicCM + " cm³";
            txtSizeX.Text = bbox.Size.x.ToString("0.000") + " mm";
            txtSizeY.Text = bbox.Size.y.ToString("0.000") + " mm";
            txtSizeZ.Text = bbox.Size.z.ToString("0.000") + " mm";

            txtCollision.Text = pm.outside.ToString();
            txtFilename.Text = pm.Name;
            txtPosX.Text = pm.Position.X.ToString("0.000");
            txtPosY.Text = pm.Position.Y.ToString("0.000");
            txtPosZ.Text = pm.Position.Z.ToString("0.000");

            //Debug.WriteLine("Elapsed time for Analyse: " + sw.ElapsedMilliseconds + " ms");
        }
    }
}

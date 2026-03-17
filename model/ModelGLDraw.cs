using OpenTK.Mathematics;
using System.Drawing;
using View3D.model.geom;
using System.Diagnostics;

namespace View3D.model
{
    public class ModelGLDraw : IDraw
    {
        #region Draw Function
        public void Draw(Submesh mesh, int method, Vector3 edgetrans, bool forceFaces = false)
        {
            if (method == 2)    // VBOs (fastest)
            {

            }
        }

        public Vector3 GetTranslateVector()
        {
            return MainWindow.main.threeDControl.cam.EdgeTranslation();
        }
#endregion

        #region Color Setting
        public GetColorSettingHandler GetColorSetting { get; set; }

        public virtual int GetColorRGBA(Submesh.MeshColor colorCode, Color frontBackColor)
        {
            int idx = (int)colorCode;

            if (idx >= 0)
                return 255 << 24 | idx;

            Color color;
            if (GetColorSetting != null)
                color = GetColorSetting(colorCode, frontBackColor);
            else
                color = Color.Wheat;

            return ColorToRgba32(color);
        }

        private int ColorToRgba32(Color c)
        {
            return (int)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
        }
        #endregion
    }
}

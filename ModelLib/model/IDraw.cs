using OpenTK.Mathematics;
using System;
using System.Drawing;
using View3D.model.geom;

namespace View3D.model
{
    //public delegate Color GetColorSettingHandler(Submesh.MeshColor color);
    public delegate Color GetColorSettingHandler(Submesh.MeshColor color, Color fontBackColor);

    public interface IDraw
    {
        // mesh draw function
        void Draw(Submesh mesh, int method, Vector3 edgetrans, bool forceFaces = false);
        Vector3 GetTranslateVector();

        // mesh color setting
        GetColorSettingHandler GetColorSetting { get; set; }
        int GetColorRGBA(Submesh.MeshColor color, Color frontBackColor);
    }
}

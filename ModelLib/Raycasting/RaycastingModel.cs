using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using View3D.Primitive;
using View3D.Extensions;

namespace View3D.Raycasting
{
    public class RaycastingModel : RaycastingTriangle
    {
        public override bool PickTest(Vector2 mousePos, ref List<string> key, ref Vector3 p, ref Vector3 normal, ref Vector3 normal_NoRotation)
        {
            bool isSelect = false;

            return isSelect;
        }
    }
}

using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace View3D.Raycasting
{
    public class RaycastingAABB : RaycastingBase
    {
        public override bool PickTest(Vector2 mousePos, ref List<string> keys, ref Vector3 p, ref Vector3 normal, ref Vector3 normal_NoRotation)
        {
            bool isSelect = false;
            return isSelect;
        }
    }
}

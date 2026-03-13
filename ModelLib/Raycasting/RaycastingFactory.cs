using System;
using System.Collections.Generic;
using View3D.Enumeration;
using OpenTK.Mathematics;

namespace View3D.Raycasting
{
    public class RaycastingFactory
    {
        public static bool Raycast(RaycastType type, Vector2 mousePos, RascastingArgs args)
        {
            Raycasting raycasting = new Raycasting();
            switch (type)
            {
                case RaycastType.AABB:
                    {
                        raycasting.SetCasting(new RaycastingAABB());
                        return raycasting.Intersection(mousePos, args);
                    }
                case RaycastType.TRIANGLE:
                    {
                        raycasting.SetCasting(new RaycastingTriangle());
                        return raycasting.Intersection(mousePos, args);
                    }
                case RaycastType.MODEL:
                    {
                        raycasting.SetCasting(new RaycastingModel());
                        return raycasting.Intersection(mousePos, args);
                    }
                default:
                    {
                        throw new ArgumentException(string.Format("{0} is not supported format.", type));
                    }
            }
        }
    }
}

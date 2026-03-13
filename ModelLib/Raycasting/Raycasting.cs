using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace View3D.Raycasting
{
    public class RascastingArgs : EventArgs
    {
        public List<string> Key { get;  set; }
        public Vector3 Point { get;  set; }
        public Vector3 Normal_World { get;  set; }
        public Vector3 Normal { get;  set; }
        public int TriId { get; set; }

        public RascastingArgs()
        {
            Key = new List<string>();
            Point = new Vector3();
            Normal = new Vector3();
            Normal_World = new Vector3();
        }

        public RascastingArgs(List<string> key, Vector3 p, Vector3 normal, Vector3 normal_World)
        {
            Key = key;
            Point = p;
            Normal = normal;
            Normal_World = normal_World;
        }

    }

    class Raycasting
    {
        private RaycastingBase _RaycastingStrategy = null;

        public Raycasting() { ;}

        public Raycasting(RaycastingBase raycastingStrategy)
        {
            _RaycastingStrategy = raycastingStrategy;
        }

        public void SetCasting(RaycastingBase raycastingStrategy)
        {
            _RaycastingStrategy = raycastingStrategy;
        }

        public bool Intersection(Vector2 mousePos, RascastingArgs args)
        {
            List<string> key = args.Key;
            Vector3 p = args.Point;
            Vector3 normal = args.Normal;
            Vector3 normal_World = args.Normal_World;
            
            bool result = _RaycastingStrategy.PickTest(mousePos, ref key, ref p, ref normal_World, ref normal);

            args.Key = key;
            args.Normal = normal;
            args.Normal_World = normal_World;
            args.Point = p;
            args.TriId = GetTriangleId(_RaycastingStrategy);

            return result;
        }

        private int GetTriangleId(RaycastingBase inst)
        {
            if (inst is RaycastingTriangle)
            {
                return (inst as RaycastingTriangle).TriangleId;
            }
            else
                return -1;
        }
    }
}

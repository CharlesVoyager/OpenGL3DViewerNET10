using System;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace View3D.Primitive
{
    public class Ray
    {
        public Vector3 Position;
        private Vector3 _normal;

        public Vector3 Normal
        {
            get
            {
                return _normal;
            }
            set
            {
                _normal = value;
                _normal.Normalize();
            }
        }

        public Ray()
        {
            //ray at origin
            //points down to z axis
            Position = new Vector3(0, 0, 0);
            _normal = new Vector3(0, 0, 1);
        }

        public Ray(Vector3 p, Vector3 dir)
        {
            //normalize!
            Position = p;
            _normal = dir;
            _normal.Normalize();
        }

        public override string ToString()
        {
            string result = "P: (" + Position.X + ", " + Position.Y + ", " + Position.Z + "), ";
            result += "D: ( " + _normal.X + ", " + _normal.Y + ", " + _normal.Z + ")";
            return result;
        }
    }
}

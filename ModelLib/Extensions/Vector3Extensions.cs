using System;
using OpenTK.Mathematics;

namespace View3D.Extensions
{
    public static class Vector3Extension
    {
        public static Vector4 ToVector4(this Vector3 vec)
        {
            return new Vector4(vec, 1);
        }

        public static Vector3 ToVector3(this Vector4 vec)
        {
            return new Vector3(vec.X, vec.Y, vec.Z);
        }

        public static Vector3 Mult(this Vector3 v, Matrix4 m)
        {
            return new Vector3(
                m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z + m.M41 * 1,
                m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z + m.M42 * 1,
                m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z + m.M43 * 1);
        }

        public static Vector4 Mult4(this Vector4 v, Matrix4 m)
        {
            return new Vector4(
                m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z + m.M41 * v.W,
                m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z + m.M42 * v.W,
                m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z + m.M43 * v.W,
                m.M14 * v.X + m.M24 * v.Y + m.M34 * v.Z + m.M44 * v.W);
        }

        public static Vector3 ToRound(this Vector3 vec)
        {
            try
            {
                return new Vector3(float.Parse(vec.X.ToString("0.000")), float.Parse(vec.Y.ToString("0.000")), float.Parse(vec.Z.ToString("0.000")));
            }
            catch (Exception)
            {
                return new Vector3(vec);
            }
        }

        public static Vector4 Mult(this Vector4 v, Matrix4 m)
        {
            return new Vector4(
                m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z + m.M41 * v.W,
                m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z + m.M42 * v.W,
                m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z + m.M43 * v.W,
                m.M14 * v.X + m.M24 * v.Y + m.M34 * v.Z + m.M44 * v.W);
        }
    }
}

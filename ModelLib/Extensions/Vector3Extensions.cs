using System;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

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

        public static Vector3 Unproject(this Vector3 windowCoords, Matrix4 modelView, Matrix4 projection, float[] viewPort)
        {
            // First, convert from window coordinates to NDC coordinates
            Vector4 ndcCoords = new Vector4(windowCoords.X, windowCoords.Y, windowCoords.Z, 1.0f);
            ndcCoords.X = (ndcCoords.X - viewPort[0]) / viewPort[2]; // Range 0 to 1: (windowX - viewX) / viewWidth
            ndcCoords.Y = (ndcCoords.Y - viewPort[1]) / viewPort[3]; // Range 0 to 1: (windowY - viewY) / viewHeight
            // Remember, NDC ranges from -1 to 1, not 0 to 1
            ndcCoords.X = ndcCoords.X * 2f - 1f; // Range: -1 to 1
            ndcCoords.Y = 1f - ndcCoords.Y * 2f; // Range: -1 to 1 - Flipped!
            ndcCoords.Z = ndcCoords.Z * 2f - 1f; // Range: -1 to 1

            // Next, from NDC space to eye / view space.
            // Note, this leaves a scalar in the W component!

#if false   // OpenTK 3.3.3.0
            Vector4 eyeCoords = new Vector4();
            Matrix4 projInv = Matrix4.Invert(projection);
            Vector4.Transform(ref ndcCoords, ref projInv, out eyeCoords);
#else       // OpenTK 4.X.
            Matrix4 projInv = Matrix4.Invert(projection);
            Vector4 eyeCoords = projInv * ndcCoords;
#endif

            // eye space to world space.
            // Remember, eye space assumes the camera is at the center of the world,
            // this is not the case, let's move the actual point into world space

#if false   // OpenTK 3.3.3.0
            Vector4 worldCoords = new Vector4();
            Matrix4 viewInv = Matrix4.Invert(modelView);
            Vector4.Transform(ref ndcCoords, ref viewInv, out worldCoords);
#else       // OpenTK 4.X.
            Matrix4 viewInv = Matrix4.Invert(modelView);
            Vector4 worldCoords = viewInv * ndcCoords;
#endif

            // Finally, undo the perspective divide!
            // When we multiplied by the inverse of the projection matrix, that
            // multiplication left the inverse of the perspective divide in the 
            // W component of the resulting vector. This could be 0
            if (Math.Abs(0.0f - worldCoords.W) > 0.00001f)
            {
                // This is the same as dividing every component by W
                worldCoords *= 1.0f / worldCoords.W;
            }

            // Now we have a proper 4D vector with a W of 1 (or 0)
            return new Vector3(worldCoords.X, worldCoords.Y, worldCoords.Z);
        }

        public static Vector3 ToEulerAngles(this Quaternion q)
        {
            // Store the Euler angles in radians
            Vector3 pitchYawRoll = new Vector3();

            double sqw = q.W * q.W;
            double sqx = q.X * q.X;
            double sqy = q.Y * q.Y;
            double sqz = q.Z * q.Z;

            // If quaternion is normalised the unit is one, otherwise it is the correction factor
            double unit = sqx + sqy + sqz + sqw;
            double test = q.X * q.Y + q.Z * q.W;

            if (test > 0.4999f * unit)                              // 0.4999f OR 0.5f - EPSILON
            {
                // Singularity at north pole
                pitchYawRoll.Y = 2f * (float)Math.Atan2(q.X, q.W);  // Yaw
                pitchYawRoll.Z = (float)Math.PI * 0.5f;                         // Pitch
                pitchYawRoll.X = 0f;                                // Roll
                return pitchYawRoll;
            }
            else if (test < -0.4999f * unit)                        // -0.4999f OR -0.5f + EPSILON
            {
                // Singularity at south pole
                pitchYawRoll.Y = -2f * (float)Math.Atan2(q.X, q.W); // Yaw
                pitchYawRoll.Z = -(float)Math.PI * 0.5f;                        // Pitch
                pitchYawRoll.X = 0f;                                // Roll
                return pitchYawRoll;
            }
            else
            {
                pitchYawRoll.Y = (float)Math.Atan2(2f * q.Y * q.W - 2f * q.X * q.Z, sqx - sqy - sqz + sqw);       // Yaw
                pitchYawRoll.Z = (float)Math.Asin(2f * test / unit);                                             // Pitch
                pitchYawRoll.X = (float)Math.Atan2(2f * q.X * q.W - 2f * q.Y * q.Z, -sqx + sqy - sqz + sqw);      // Roll
            }

            return pitchYawRoll * (float)(180 / Math.PI);
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

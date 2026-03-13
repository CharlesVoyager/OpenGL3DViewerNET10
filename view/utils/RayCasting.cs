using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System;
using View3D.Extensions;
using View3D.model;

namespace View3D.view.utils
{
    class Line
    {
        public Vector3 Start;
        public Vector3 End;
        public float Length
        {
            get
            {
                Vector3 line = End - Start;
                return line.Length;
            }
        }

        public float LengthSq
        {
            get
            {
                Vector3 line = End - Start;
                return line.LengthSquared;
            }
        }

        public Line(Line copy)
        {
            Start = new Vector3(copy.Start.X, copy.Start.Y, copy.Start.Z);
            End = new Vector3(copy.End.X, copy.End.Y, copy.End.Z);
        }

        public Line(Vector3 p1, Vector3 p2)
        {
            Start = p1;
            End = p2;
        }

        public Vector3 ToVector()
        {
            return new Vector3(End.X - Start.X, End.Y - Start.Y, End.Z - Start.Z);
        }

        public override string ToString()
        {
            string result = "Start: (" + Start.X + ", " + Start.Y + ", " + Start.Z + "), ";
            result += "End: ( " + End.X + ", " + End.Y + ", " + End.Z + ")";
            return result;
        }
    }

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

    public class BoundingBox
    {
        private Vector3 minPoint = Vector3.One * float.MaxValue;
        private Vector3 maxPoint = Vector3.One * float.MinValue;

        public Vector3 MinPoint3
        {
            get { return minPoint; }
            set { minPoint = value; }
        }
        public Vector3 MaxPoint3
        {
            get { return maxPoint; }
            set { maxPoint = value; }
        }
    }

    public class RayCasting
    {
        public static Ray GenerateRay(int X, int Y, out Vector3 near, out Vector3 far)
        {
            float[] viewport = new float[4];
            Matrix4 modelViewMatrix, projectionMatrix, modelViewProjectionMatrix;
            GL.GetFloat(GetPName.ModelviewMatrix, out modelViewMatrix);
            GL.GetFloat(GetPName.ProjectionMatrix, out projectionMatrix);
            GL.GetFloat(GetPName.Viewport, viewport);
            modelViewProjectionMatrix = modelViewMatrix * projectionMatrix;
            near = Vector3Extension.Unproject(new Vector3(X, Y, 0.0f), modelViewProjectionMatrix, Matrix4.Identity, viewport);
            far = Vector3Extension.Unproject(new Vector3(X, Y, 1.0f), modelViewProjectionMatrix, Matrix4.Identity, viewport);

            return new Ray(near, Vector3.Normalize(far - near));
        }

        public static bool RaycastTriangle(Ray ray, Vector3[] triangle, out Vector3 p)
        {
            float t = -1;
            bool result = RaycastTriangle(ray, triangle, out t);
            p = ray.Position + ray.Normal * t;
            return result;
        }

        private static bool RaycastTriangle(Ray ray, Vector3[] triangle, out float t)
        {
            if (!RayIntersectTriangle(ray, triangle, out t))
            {
                return false;
            }

            if (t > 0.00001)
                return true;
            else
                return false;
        }

        private static bool RayIntersectTriangle(Ray ray, Vector3[] triangle, out float t)
        {
            t = -1;
            Line ba = new Line(triangle[1], triangle[0]);
            Line ca = new Line(triangle[2], triangle[0]);
            Vector3 baVec = ba.ToVector();
            Vector3 caVec = ca.ToVector();

            Vector3 pVec = new Vector3();
            Vector3 rayNorm = ray.Normal;
            Vector3.Cross(ref rayNorm, ref caVec, out pVec);
            float det = Vector3.Dot(baVec, pVec);

            if (det > -0.00001 && det < 0.00001)
                return false;

            float invDet = 1 / det;
            Line pv0 = new Line(ray.Position, triangle[0]);
            Vector3 tVec = pv0.ToVector();
            float u = invDet * Vector3.Dot(tVec, pVec);

            if (u < 0.0 || u > 1.0)
                return false;

            Vector3 qVec = new Vector3();
            Vector3.Cross(ref tVec, ref baVec, out qVec);
            float v = invDet * Vector3.Dot(rayNorm, qVec);

            if (v < 0.0 || u + v > 1.0)
                return false;

            t = -invDet * Vector3.Dot(caVec, qVec);
            return true;
        }

        public static bool RaycastAABB(Ray ray, PrintModel md)
        {
            BoundingBox aabb = new BoundingBox();
            aabb.MinPoint3 = new Vector3((float)md.BoundingBox.minPoint.x, (float)md.BoundingBox.minPoint.y, (float)md.BoundingBox.minPoint.z);
            aabb.MaxPoint3 = new Vector3((float)md.BoundingBox.maxPoint.x, (float)md.BoundingBox.maxPoint.y, (float)md.BoundingBox.maxPoint.z);

            float t1 = (aabb.MinPoint3.X - ray.Position.X) / ray.Normal.X;
            float t2 = (aabb.MaxPoint3.X - ray.Position.X) / ray.Normal.X;
            float t3 = (aabb.MinPoint3.Y - ray.Position.Y) / ray.Normal.Y;
            float t4 = (aabb.MaxPoint3.Y - ray.Position.Y) / ray.Normal.Y;
            float t5 = (aabb.MinPoint3.Z - ray.Position.Z) / ray.Normal.Z;
            float t6 = (aabb.MaxPoint3.Z - ray.Position.Z) / ray.Normal.Z;

            float tmin = Math.Max(Math.Max(Math.Min(t1, t2), Math.Min(t3, t4)), Math.Min(t5, t6));
            float tmax = Math.Min(Math.Min(Math.Max(t1, t2), Math.Max(t3, t4)), Math.Max(t5, t6));

            if (tmax < 0)
            {
                return false;
            }

            if (tmin > tmax)
            {
                return false;
            }
            return true;
        }
    }
}

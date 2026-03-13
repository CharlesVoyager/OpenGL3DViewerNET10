using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using View3D.Primitive;

namespace View3D.Raycasting
{
    public class RaycastingTriangle: RaycastingBase
    {
        public int TriangleId { get; private set; }

        public override bool PickTest(Vector2 mousePos, ref List<string> key, ref Vector3 p, ref Vector3 normal, ref Vector3 normal_NoRotation)
        {
            bool isSelect = false;
            return isSelect;
        }

        protected bool RaycastTriangle(Ray ray, Vector3[] triangle, out Vector3 p)
        {
            float t = -1;
            bool result = RaycastTriangle(ray, triangle, out t);
            p = ray.Position + ray.Normal * t;
            return result;
        }

        protected bool RaycastTriangle(Ray ray, Vector3[] triangle, out float t)
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

        protected bool RayIntersectTriangle(Ray ray, Vector3[] triangle, out float t)
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
    }
}

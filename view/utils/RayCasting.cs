using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System;
using View3D.Extensions;
using View3D.Primitive;
using OpenGL3DViewerNET10.ModelLib.model;

namespace View3D.view.utils
{
    public class RayCasting
    {
        // OpenTK 3.3.3.0
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

        // OpenTK 4.9.4
        public static Ray GenerateRay(int mouseX, int mouseY, Matrix4 view, Matrix4 projection, Vector2i windowSize, out Vector3 nearPos, out Vector3 farPos)
        {
            // 1. Flip Y coordinate (Window top-left to OpenGL bottom-left)
            float x = (2.0f * mouseX) / windowSize.X - 1.0f;
            float y = 1.0f - (2.0f * mouseY) / windowSize.Y;

            // 2. Create the View-Projection matrix and invert it
            Matrix4 viewProjectionInv = Matrix4.Invert(view * projection);

            // 3. Unproject the Near and Far points
            // Near plane is Z = -1 in NDC, Far plane is Z = 1
            Vector4 nearNDC = new Vector4(x, y, -1.0f, 1.0f);
            Vector4 farNDC = new Vector4(x, y, 1.0f, 1.0f);

            Vector4 nearWorld = nearNDC * viewProjectionInv;
            Vector4 farWorld = farNDC * viewProjectionInv;

            // 4. Perspective divide (W-divide)
            nearPos = nearWorld.Xyz / nearWorld.W;
            farPos = farWorld.Xyz / farWorld.W;

            return new Ray(nearPos, Vector3.Normalize(farPos - nearPos));
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
            if (md.BoundingBox.minPoint == null || md.BoundingBox.maxPoint == null)
                return false;

            Vector3 aabbMinPoint3 = new Vector3((float)md.BoundingBox.minPoint.x, (float)md.BoundingBox.minPoint.y, (float)md.BoundingBox.minPoint.z);
            Vector3 aabbMaxPoint3 = new Vector3((float)md.BoundingBox.maxPoint.x, (float)md.BoundingBox.maxPoint.y, (float)md.BoundingBox.maxPoint.z);

            float t1 = (aabbMinPoint3.X - ray.Position.X) / ray.Normal.X;
            float t2 = (aabbMaxPoint3.X - ray.Position.X) / ray.Normal.X;
            float t3 = (aabbMinPoint3.Y - ray.Position.Y) / ray.Normal.Y;
            float t4 = (aabbMaxPoint3.Y - ray.Position.Y) / ray.Normal.Y;
            float t5 = (aabbMinPoint3.Z - ray.Position.Z) / ray.Normal.Z;
            float t6 = (aabbMaxPoint3.Z - ray.Position.Z) / ray.Normal.Z;

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

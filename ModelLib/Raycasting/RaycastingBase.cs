using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using View3D.Primitive;
using View3D.Extensions;

namespace View3D.Raycasting
{
    public abstract class RaycastingBase
    {
        public abstract bool PickTest(Vector2 mousePos, ref List<string> key, ref Vector3 p, ref Vector3 normal, ref Vector3 normal_NoRotation);

        protected Ray GenerateRay(Vector2 mousePos, out Vector3 near, out Vector3 far)
        {
            float[] viewport = new float[4];
            Matrix4 modelViewMatrix, projectionMatrix, modelViewProjectionMatrix;
            GL.GetFloat(GetPName.ModelviewMatrix, out modelViewMatrix);
            GL.GetFloat(GetPName.ProjectionMatrix, out projectionMatrix);
            GL.GetFloat(GetPName.Viewport, viewport);
            modelViewProjectionMatrix = modelViewMatrix * projectionMatrix;
            near = Vector3Extension.Unproject(new Vector3(mousePos.X, mousePos.Y, 0.0f), modelViewProjectionMatrix, Matrix4.Identity, viewport);
            far = Vector3Extension.Unproject(new Vector3(mousePos.X, mousePos.Y, 1.0f), modelViewProjectionMatrix, Matrix4.Identity, viewport);

            return new Ray(near, Vector3.Normalize(far - near));
        }
    }
}

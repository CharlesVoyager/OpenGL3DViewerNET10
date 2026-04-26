using OpenTK.Mathematics;
using View3D.Extensions;
using View3D.Primitive;

namespace View3D.ModelObjectTool
{
    public class ModelObjectToolNormal : ModelObjectToolBase
    {
        public override unsafe BoundingBox3 GetBoundingBox(ModelMatrix matrix, float* vertexArray, int vertexCount)
        {            
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float nowX = 0;
            float nowY = 0;
            float nowZ = 0;
            int arrayCount = vertexCount;
            for (int n = 0; n < arrayCount; n += 3)
            {
                float x = *vertexArray++;
                float y = *vertexArray++;
                float z = *vertexArray++;
                nowX = matrix.X[0] * x + matrix.Y[0] * y + matrix.Z[0] * z + matrix.W[0];
                nowY = matrix.X[1] * x + matrix.Y[1] * y + matrix.Z[1] * z + matrix.W[1];
                nowZ = matrix.X[2] * x + matrix.Y[2] * y + matrix.Z[2] * z + matrix.W[2];
                maxX = Math.Max(maxX, nowX);
                minX = Math.Min(minX, nowX);
                maxY = Math.Max(maxY, nowY);
                minY = Math.Min(minY, nowY);
                maxZ = Math.Max(maxZ, nowZ);
                minZ = Math.Min(minZ, nowZ);
            }
            return new BoundingBox3(minX, maxX, minY, maxY, minZ, maxZ);
        }
        public unsafe override BoundingBox3 GetBoundingBox(ModelMatrix matrix, float[] vertexArray)
        {           
            fixed(float *ptr = &vertexArray[0])
            {
                return GetBoundingBox(matrix, ptr, vertexArray.Length / 3);
            }
        }

        public override unsafe bool Select(ModelMatrix matrix, float* vertices, int vertexCount, float[] ray_Position, float[] ray_Normal)
        {            
            return false;
        }
        public override bool Select(ModelMatrix matrix, float[] vertices, float[] ray_Position, float[] ray_Normal)
        {
            throw new NotImplementedException();
        }

        public override unsafe void ToWorldCoordinate(ModelMatrix matrix, float* sourceVertexArray, ref float[] destVertexArray)
        {
            Matrix4 mat = ModelObjectToolHelper.ToMatrix4(matrix);
        
            Vector3 newPoint3 = new Vector3();
            Vector3 temp = new Vector3();
            for (int i = 0; i < destVertexArray.Length; i+=3)
            {
                temp.X = *sourceVertexArray++;
                temp.Y = *sourceVertexArray++;
                temp.Z = *sourceVertexArray++;

                newPoint3 = temp.ToVector4().Mult(mat).ToVector3();

                destVertexArray[i] = newPoint3.X;
                destVertexArray[i + 1] = newPoint3.Y;
                destVertexArray[i + 2] = newPoint3.Z;
            }
        }
        public override void ToWorldCoordinate(ModelMatrix matrix, float[] sourceVertexArray, ref float[] destVertexArray)
        {
            throw new NotImplementedException();
        }

        public override unsafe void ToWorldCoordinate(ModelMatrix matrix, float* sourceVertexArray, ref double[] destVertexArray)
        {
            Matrix4 mat = ModelObjectToolHelper.ToMatrix4(matrix);

            Vector3 newPoint3 = new Vector3();
            Vector3 temp = new Vector3();
            for (int i = 0; i < destVertexArray.Length; i += 3)
            {
                temp.X = *sourceVertexArray++;
                temp.Y = *sourceVertexArray++;
                temp.Z = *sourceVertexArray++;

                newPoint3 = temp.ToVector4().Mult(mat).ToVector3();

                destVertexArray[i] = newPoint3.X;
                destVertexArray[i + 1] = newPoint3.Y;
                destVertexArray[i + 2] = newPoint3.Z;
            }
        }
        public override void ToWorldCoordinate(ModelMatrix matrix, float[] sourceVertexArray, ref double[] destVertexArray)
        {
            throw new NotImplementedException();
        }
        
        public override unsafe void DetectCubesOverlappedWithModel(ModelMatrix matrix, ref bool[] output, float* vertices, int vertexCount,
            float* minCube, float* maxCube, int cubeCount,
            Vector3 cubeSizeXYZ, Vector3 cubeCountXYZ, Vector3 boundingBoxMinXYZ)
        {
            throw new NotImplementedException();
        }

        public override unsafe bool RayIntersectTriangle(ModelMatrix matrix, float[] vertices, float[] ray_Position, float[] ray_Normal,
            out int id, out float output)
        {
            fixed (float* ptr = &vertices[0])
            {
                return RayIntersectTriangle(matrix, ptr, vertices.Length / 3, ray_Position, ray_Normal, out id, out output);
            }
        }

        public override unsafe bool RayIntersectTriangle(ModelMatrix matrix, float* vertices, int vertexCount, float[] ray_Position, float[] ray_Normal,
            out int id, out float output)
        {
            output = -1;
            id = -1;
            bool selected = false;
            Vector3[] triangle = new Vector3[3];
            Vector3[] mdVertices = new Vector3[3];
            Matrix4 mat = ModelObjectToolHelper.ToMatrix4(matrix);            
            float length = float.MaxValue;            
            Ray ray = new Ray();
            ray.Normal = new Vector3(ray_Normal[0], ray_Normal[1], ray_Normal[2]);
            ray.Position = new Vector3(ray_Position[0], ray_Position[1], ray_Position[2]);
            int triId = 0;
            for (int i = 0; i < vertexCount; i += 3)
            {
                for (int n = 0; n < 3; n++ )
                {
                    mdVertices[n].X = *vertices++;
                    mdVertices[n].Y = *vertices++;
                    mdVertices[n].Z = *vertices++;
                }

                triangle[0] = mdVertices[0].ToVector4().Mult(mat).ToVector3();
                triangle[1] = mdVertices[1].ToVector4().Mult(mat).ToVector3();
                triangle[2] = mdVertices[2].ToVector4().Mult(mat).ToVector3();
                float temp;
                if (RaycastTriangle(ray, triangle, out temp))
                {                    
                    if (temp <= length)
                    {                        
                        length = temp;
                        id = triId;
                        selected = true;
                        output = length;
                        return true;
                    }
#if DEBUG_MODE
                        Console.WriteLine("Pos: " + hitP + ", Dist: " + line.Length);
#endif
                }
                triId++;
            }
            return selected;
        }

        private bool RaycastTriangle(Ray ray, Vector3[] triangle, out float t)
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

        private bool RayIntersectTriangle(Ray ray, Vector3[] triangle, out float t)
        {
            t = -1;
            Line ba = new Line(triangle[1], triangle[0]);
            Line ca = new Line(triangle[2], triangle[0]);
            Vector3 baVec = ba.ToVector();
            Vector3 caVec = ca.ToVector();

            Vector3 pVec = new Vector3();
            Vector3 rayNorm = ray.Normal;
            Vector3.Cross(in rayNorm, in caVec, out pVec);
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
            Vector3.Cross(in tVec, in baVec, out qVec);
            float v = invDet * Vector3.Dot(rayNorm, qVec);

            if (v < 0.0 || u + v > 1.0)
                return false;

            t = -invDet * Vector3.Dot(caVec, qVec);
            return true;
        }

        public override Ray GenerateRay(int mouseX, int mouseY, Matrix4 view, Matrix4 projection, Vector2i windowSize, out Vector3 nearPos, out Vector3 farPos)
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

        public override bool RaycastAABB(Ray ray, Vector3 aabbMinPoint3, Vector3 aabbMaxPoint3)
        {
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

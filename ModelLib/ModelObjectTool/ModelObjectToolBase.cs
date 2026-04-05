using OpenTK.Mathematics;
using View3D.Primitive;

namespace View3D.ModelObjectTool
{
    public class ModelMatrix
    {
        private float[] _X;

        private float[] _Y;

        private float[] _Z;

        private float[] _W;

        public unsafe float* PtrW
        {
            get
            {
                fixed (float* result = &_W[0])
                {
                    return result;
                }
            }
        }

        public unsafe float* PtrZ
        {
            get
            {
                fixed (float* result = &_Z[0])
                {
                    return result;
                }
            }
        }

        public unsafe float* PtrY
        {
            get
            {
                fixed (float* result = &_Y[0])
                {
                    return result;
                }
            }
        }

        public unsafe float* PtrX
        {
            get
            {
                fixed (float* result = &_X[0])
                {
                    return result;
                }
            }
        }

        public float[] W => _W;

        public float[] Z => _Z;

        public float[] Y => _Y;

        public float[] X => _X;

        public ModelMatrix(float[] x, float[] y, float[] z, float[] w)
        {
            _X = x;
            _Y = y;
            _Z = z;
            _W = w;
        }

        public ModelMatrix()
        {
            _X = new float[4] { 1f, 0f, 0f, 0f };
            _Y = new float[4] { 0f, 1f, 0f, 0f };
            _Z = new float[4] { 0f, 0f, 1f, 0f };
            _W = new float[4] { 0f, 0f, 0f, 1f };
        }
    }
    public class BoundingBox3
    {
        public float MinX;

        public float MaxX;

        public float MinY;

        public float MaxY;

        public float MinZ;

        public float MaxZ;

        public BoundingBox3(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
        }
    }

    public abstract class ModelObjectToolBase
    {
        public abstract unsafe BoundingBox3 GetBoundingBox(ModelMatrix matrix, float* vertexArray, int vertexCount);
        public abstract BoundingBox3 GetBoundingBox(ModelMatrix matrix, float[] vertexArray);

        public abstract unsafe bool Select(ModelMatrix matrix, float* vertices, int vertexCount, float[] ray_Position, float[] ray_Normal);
        public abstract bool Select(ModelMatrix matrix, float[] vertices, float[] ray_Position, float[] ray_Normal);

        public abstract unsafe void ToWorldCoordinate(ModelMatrix matrix, float* sourceVertexArray, ref float[] destVertexArray);
        public abstract void ToWorldCoordinate(ModelMatrix matrix, float[] sourceVertexArray, ref float[] destVertexArray);

        public abstract unsafe void ToWorldCoordinate(ModelMatrix matrix, float* sourceVertexArray, ref double[] destVertexArray);
        public abstract void ToWorldCoordinate(ModelMatrix matrix, float[] sourceVertexArray, ref double[] destVertexArray);

        
        public abstract unsafe void DetectCubesOverlappedWithModel(ModelMatrix matrix, ref bool[] output, float* vertices, int vertexCount,
            float* minCube, float* maxCube, int cubeCount,
            Vector3 cubeSizeXYZ, Vector3 cubeCountXYZ, Vector3 boundingBoxMinXYZ);

        public abstract unsafe bool RayIntersectTriangle(ModelMatrix matrix, float* vertices, int vertexCount, float[] ray_Position, float[] ray_Normal,
            out int id, out float output);
        public abstract unsafe bool RayIntersectTriangle(ModelMatrix matrix, float[] vertices, float[] ray_Position, float[] ray_Normal,
            out int id, out float output);

        public abstract Ray GenerateRay(int mouseX, int mouseY, Matrix4 view, Matrix4 projection, Vector2i windowSize, out Vector3 nearPos, out Vector3 farPos);

        public abstract bool RaycastAABB(Ray ray, Vector3 aabbMinPoint3, Vector3 aabbMaxPoint3);
    }
}

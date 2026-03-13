using OpenTK.Mathematics;

namespace View3D.ModelObjectTool
{
    public class ModelObjectToolHelper
    {
        public static ModelMatrix ToModelMatrix(Matrix4 worldMat)
        {
            float[] matrixX, matrixY, matrixZ, matrixW;            

            matrixX = new float[] {worldMat.Column0.X, worldMat.Column1.X,
                                   worldMat.Column2.X, worldMat.Column3.X};

            matrixY = new float[] {worldMat.Column0.Y, worldMat.Column1.Y,
                                   worldMat.Column2.Y, worldMat.Column3.Y};

            matrixZ = new float[] {worldMat.Column0.Z, worldMat.Column1.Z,
                                   worldMat.Column2.Z, worldMat.Column3.Z};

            matrixW = new float[] {worldMat.Column0.W, worldMat.Column1.W,
                                   worldMat.Column2.W, worldMat.Column3.W};

            return new ModelMatrix(matrixX, matrixY, matrixZ, matrixW);
        }

        public static Matrix4 ToMatrix4(ModelMatrix worldMat)
        {
            return new Matrix4(worldMat.X[0], worldMat.X[1], worldMat.X[2], worldMat.X[3],
                               worldMat.Y[0], worldMat.Y[1], worldMat.Y[2], worldMat.Y[3],
                               worldMat.Z[0], worldMat.Z[1], worldMat.Z[2], worldMat.Z[3],
                               worldMat.W[0], worldMat.W[1], worldMat.W[2], worldMat.W[3]);
        }
    }
}

using OpenTK.Mathematics;

namespace View3D.Primitive
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
}

namespace OpenGL3DViewerNET10.ModelLib.model
{
    public class Coord3D
    {
        public float x = 0, y = 0, z = 0;

        public float inix = 0, iniy = 0, iniz = 0;  // remember initial position for reset

        public Coord3D() { }
        public Coord3D(float _x, float _y, float _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }

        public override string ToString()
        {
            return x.ToString("0") + " " + y.ToString("0") + " " + z.ToString("0");
        }
    }
    public abstract class ThreeDModel
    {
        private bool selected = false;
        private Coord3D position = new Coord3D();      // shift position
        private Coord3D rotation = new Coord3D();      // rotate vector
        private Coord3D scale = new Coord3D(1, 1, 1);  // scaler magnitude

        public bool Selected
        {
            get { return selected; }
            set { selected = value; }
        }
        public Coord3D Position
        {
            get { return position; }
            set { position = value; }
        }

        public Coord3D Rotation
        {
            get { return rotation; }
            set { rotation = value; }
        }

        public Coord3D Scale
        {
            get { return scale; }
            set { scale = value; }
        }

        public virtual void ResetQuality() { }
        /// <summary>
        /// Has the model changed since last paint?
        /// </summary>
        public virtual bool Changed
        {
            get { return false; }
        }

        public virtual void Clear() 
        {
            // Console.Write("ThreeDModel:Clear()");
        }
    }
}

using View3D.model.geom;

namespace OpenGL3DViewerNET10.ModelLib.model
{
    public abstract class ThreeDModel
    {
        private bool selected = false;
        private RHVector3 position = new RHVector3(0, 0, 0);   
        private RHVector3 rotation = new RHVector3(0, 0, 0);    
        private RHVector3 scale = new RHVector3(1, 1, 1);


        public RHVector3 InitialPosition = new RHVector3(0, 0, 0);

        public bool Selected
        {
            get { return selected; }
            set { selected = value; }
        }
        public RHVector3 Position
        {
            get { return position; }
            set { position = value; }
        }

        public RHVector3 Rotation
        {
            get { return rotation; }
            set { rotation = value; }
        }

        public RHVector3 Scale
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

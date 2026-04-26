
namespace View3D.ModelObjectTool
{

    public class ModelObjectToolWrapper
    {
        private static ModelObjectToolWrapper _Instance = new ModelObjectToolWrapper();

        public static ModelObjectToolWrapper Instance
        {
            get { return _Instance; }
        }

        private ModelObjectToolBase _ModelObjectTool;

        public ModelObjectToolBase Tool
        {
            get { return _ModelObjectTool; }
        }

        private ModelObjectToolWrapper()
        {
            _ModelObjectTool = new ModelObjectToolNormal();
        }
    }
}

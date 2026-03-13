using View3D.Enumeration;

namespace View3D.ModelObjectTool
{
    public class ModelObjectToolWrapper
    {
        private static ModelObjectToolWrapper _Instance = new ModelObjectToolWrapper();

        public static ModelObjectToolWrapper Instance
        {
            get { return _Instance; }
        }

        private ModelObjectToolType _ToolType = ModelObjectToolType.Normal;
        private ModelObjectToolBase _ModelObjectTool;

        public ModelObjectToolBase Tool
        {
            get { return _ModelObjectTool; }
        }

        private ModelObjectToolWrapper()
        {
            ChangeToSIMDType();

            CreateModelObjectTool(_ToolType);
        }

        public void ChangeType(ModelObjectToolType type)
        {
            if (_ToolType != type)
            {
                if (type == ModelObjectToolType.SIMD)
                    ChangeToSIMDType();

                CreateModelObjectTool(_ToolType);
            }
        }

        private void ChangeToSIMDType()
        {
        }

        private void CreateModelObjectTool(ModelObjectToolType type)
        {
            _ModelObjectTool = new ModelObjectToolNormal();
        }
    }
}

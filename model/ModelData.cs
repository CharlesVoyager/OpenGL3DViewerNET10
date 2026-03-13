using System;
using View3D.model.geom;

namespace View3D.model
{
    public class ModelData
    {
        public TopoModel originalModel = new TopoModel();
        private String _FileName = "";
        public int Used = 0;
        public string name = "Unknown";
        public ModelData(String fileName)
        {
            _FileName = fileName;
            Used = 1;
        }

        public String FileName
        {
            get
            {
                return _FileName;
            }
        }

        public void RemoveOne()
        {
            Used--;
            if (Used == 0)
                Clear();
        }

        private void Clear()
        {
            originalModel.Clear();
        }

        public TopoModel CloneModel()
        {
            Used++;
            return originalModel;
        }
    }
}

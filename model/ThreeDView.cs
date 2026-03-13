using System.Collections.Generic;
using View3D.model;

namespace View3D.view
{
    public delegate void onObjectMoved(float dx, float dy);
    public delegate void onObjectSelected(ThreeDModel selModel);

    public class ThreeDView
    {
        public onObjectMoved eventObjectMoved;
        public onObjectSelected eventObjectSelected;
        public bool autoupdateable = false;
        public bool objectsSelected = false;
        public LinkedList<ThreeDModel> models;

        public ThreeDView()
        {
            models = new LinkedList<ThreeDModel>();
        }
    }
}

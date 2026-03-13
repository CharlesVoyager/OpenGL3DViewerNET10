namespace View3D.model.geom
{
    public class TopoTriangleStorage
    {
        private Dictionary<TopoTriangle, int> _idDict = new Dictionary<TopoTriangle, int>();
        private List<TopoTriangle> _triangles = new List<TopoTriangle>();

        public void Add(TopoTriangle triangle)
        {
            int id;
            if (!_idDict.TryGetValue(triangle, out id))
            {
                id = _triangles.Count;
                _idDict.Add(triangle, id);
                _triangles.Add(triangle);
            }
        }

        public bool Remove(TopoTriangle triangle)
        {
            int id;

            if (_idDict.TryGetValue(triangle, out id))
            {
                _idDict.Remove(triangle);
                _triangles.RemoveAt(id);
                return true;
            }
            return false;
        }

        public System.Collections.IEnumerator GetEnumerator()
        {
            foreach (TopoTriangle t in triangles)
               yield return t;
        }

        public void Clear()
        {
            _triangles.Clear();
            _idDict.Clear();
        }

        public bool Contains(TopoTriangle test)
        {
            bool exist;
            int id;
            exist = _idDict.TryGetValue(test, out id);
            return exist;
        }

        public List<TopoTriangle> triangles
        {
            get { return _triangles; }
        }

        public int Count
        {
            get { return _triangles.Count; }
        }
    }
}

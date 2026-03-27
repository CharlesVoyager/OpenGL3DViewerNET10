using System;
using System.Collections;
using System.Collections.Generic;

namespace View3D.model.geom
{
    public class TopoVertexStorage
    {
        public List<TopoVertex> v = new List<TopoVertex>();

        Dictionary<Int64, int> hash = new Dictionary<Int64, int>();
        int count = 0;

        public int Count
        {
            get { return count; }
        }

        public void Clear()
        {
            v.Clear(); 
            hash.Clear();  
            count = 0;
        }

        public void Add(TopoVertex vertex)
        {
            Add(vertex, 0);
        }

        public void Add(TopoVertex vertex, int level)
        {        
            Int64 temp = Convert.ToInt64(Math.Floor(vertex.pos.x * 100000)) * 5915587277 + Convert.ToInt64(Math.Floor(vertex.pos.y * 100000)) * 1500450271 + Convert.ToInt64(Math.Floor(vertex.pos.z * 100000)) * 3267000013;
            if (hash.ContainsKey(temp) == false)
            {
                hash.Add(temp, count);
                v.Add(vertex);
                count++;
            }
        }

        public TopoVertex SearchPoint(RHVector3 vertex)
        {
            Int64 temp = Convert.ToInt64(Math.Floor(vertex.x * 100000)) * 5915587277 + Convert.ToInt64(Math.Floor(vertex.y * 100000)) * 1500450271 + Convert.ToInt64(Math.Floor(vertex.z * 100000)) * 3267000013;
            if (hash.ContainsKey(temp)) 
                return v[Convert.ToInt32(hash[temp])];
            else return null;
        }
    }
}

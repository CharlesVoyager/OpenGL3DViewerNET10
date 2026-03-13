using System;
using System.Collections;
using System.Collections.Generic;

namespace View3D.model.geom
{
    public class TopoVertexStorage
    {
        TopoVertexStorage left = null, right = null;
        TopoVertexStorageLeaf leaf = null;
        public List<TopoVertex> v = new List<TopoVertex>();
        Dictionary<Int64, int> hash = new Dictionary<Int64, int>();

        int splitDimension = -1;
        private int count = 0;
        double splitPosition = 0;
        
        public bool IsLeaf
        {
            get { return leaf != null; }
        }

        public int Count
        {
            get { return count; }
        }

        public void Clear()
        {
            left = right = null;
            leaf = null;
            count = 0;
        }

        public void ChangeCoordinates(TopoVertex vertex, RHVector3 newPos)
        {
            Remove(vertex);
            vertex.pos = new RHVector3(newPos);
            Add(vertex);
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

        public void Remove(TopoVertex vertex)
        {
            if (leaf == null && left == null) return;
            if (RemoveTraverse(vertex)) count--;
        }

        private bool RemoveTraverse(TopoVertex vertex)
        {
            if (IsLeaf)
            {
                if (leaf.vertices.Remove(vertex))
                    return true;
                else
                    return false; // should not happen
            }
            if (vertex.pos[splitDimension] < splitPosition)
                return left.RemoveTraverse(vertex);
            else
                return right.RemoveTraverse(vertex);
        }

        public System.Collections.IEnumerator GetEnumerator()
        {
            if (left != null)
            {
                foreach (TopoVertex v in left)
                    yield return v;
                foreach (TopoVertex v in right)
                    yield return v;
            }
            if (leaf!=null)
            {
                foreach (TopoVertex v in leaf.vertices)
                    yield return v;
            }
        }

        public HashSet<TopoVertex> SearchBox(RHBoundingBox box)
        {
            HashSet<TopoVertex> set = new HashSet<TopoVertex>();
            if(leaf!=null || left!=null)
                SearchBoxTraverse(box,set);
            return set;
        }

        private void SearchBoxTraverse(RHBoundingBox box,HashSet<TopoVertex> set) {
            if (IsLeaf)
            {
                foreach (TopoVertex v in leaf.vertices)
                {
                    if (box.ContainsPoint(v.pos))
                        set.Add(v);
                }
                return;
            }
        }
    }

    public class TopoVertexStorageLeaf
    {
        public RHBoundingBox box = new RHBoundingBox();
        public List<TopoVertex> vertices = new List<TopoVertex>();

        public void Add(TopoVertex vertex)
        {
            vertices.Add(vertex);
            box.Add(vertex.pos);
        }

        public int LargestDimension()
        {
            RHVector3 size = box.Size;
            if (size.x > size.y && size.x > size.z) return 0;
            if (size.y > size.z) return 1;
            return 2;
        }

        public TopoVertex SearchPoint(RHVector3 vertex)
        {
            foreach (TopoVertex v in vertices)
            {
                if (vertex.Distance(v.pos) < TopoModel.epsilon)
                    return v;
            }
            return null;
        }
    }
}

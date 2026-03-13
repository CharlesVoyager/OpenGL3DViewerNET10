using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace View3D.view.utils
{
    public class RegMemory
    {
        public static int GetInt(string r, int def)
        {
            return 0;

        }
        public static void SetInt(string r, int val)
        {

        }
        public static string GetString(string r, string def)
        {
            return "";
        }
        public static void SetString(string r, string val)
        {
        }
       
        public class HistoryFile
        {
            public string file;
            public HistoryFile(string fname)
            {
                file = fname;
            }
            public override string ToString()
            {
                int p = file.LastIndexOf(Path.DirectorySeparatorChar);
                if (p < 0) return file;
                return file.Substring(p + 1);
            }
        }
        public class FilesHistory
        {
            public LinkedList<HistoryFile> list = new LinkedList<HistoryFile>();
            string name;
            int maxLength;
            public FilesHistory(string id, int max)
            {
                name = id;
                maxLength = max;
                string l = RegMemory.GetString(name, "");
                foreach (string fn in l.Split('|'))
                {
                    if (fn.Length > 0 && File.Exists(fn))
                    {
                        list.AddLast(new HistoryFile(fn));
                        if (list.Count == max) break;
                    }
                }
            }
            public void Save(string fname)
            {
                if (list.Count > 0 && list.First.Value.file == fname) return;
                foreach (HistoryFile f in list)
                {
                    if (f.file == fname)
                    {
                        list.Remove(f);
                        break;
                    }
                }
                list.AddFirst(new HistoryFile(fname));
                while (list.Count > maxLength)
                    list.RemoveLast();
                // Build string
                string store = "";
                foreach (HistoryFile f in list)
                    store += "|" + f.file;
                RegMemory.SetString(name, store.Substring(1));
            }
        }
    }
}

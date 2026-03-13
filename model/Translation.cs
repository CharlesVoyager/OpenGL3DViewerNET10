using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Xml;
using View3D.view.utils;

namespace View3D.model
{
    public class Translation
    {
        public string language;
        public string file;
        public string fileshort;
        public Dictionary<string, string> trans;

        public Translation(string _file,string _fileShort)
        {
            file = _file;
            fileshort = _fileShort;
            language = "unknown";
            trans = new Dictionary<string, string>();
            if (!File.Exists(file)) return;

            try
            {
                //Load the the document with the last book node.
                XmlTextReader reader = new XmlTextReader(file);
                XmlDocument doc = new XmlDocument();
                doc.Load(reader);
                XmlNodeList rootlist = doc.GetElementsByTagName("translation");
                foreach (XmlNode n in rootlist)
                {
                    XmlAttribute att = n.Attributes["language"];
                    if (att != null) language = att.InnerText;
                }
                foreach (XmlNode n in doc.GetElementsByTagName("t"))
                {
                    XmlAttribute att = n.Attributes["id"];
                    if (att == null) continue; // missing id!
                    string id = att.InnerText;
                    string value = n.InnerText.Trim();
                    if (trans.ContainsKey(id))
                        Console.WriteLine("Double id:" + id);
                    else
                        trans.Add(id, value);
                }
            }
            catch(Exception e) {
                System.Windows.MessageBox.Show("Error reading translation "+_file+":\n"+e.ToString(),"Error",MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public override string ToString()
        {
            return language;
        }
    }

    public class Trans
    {
        private static Trans trans = null;

        private Translation english = null;
        private Translation active = null;
        private SortedList<string, Translation> translations;

        public Trans(string folder)
        {
            string[] lfiles = Directory.GetFiles(folder, "*.xml");
            string lastactive = "en.xml";
            if (lastactive == null || lastactive == "") lastactive = "en.xml";
            translations = new SortedList<string, Translation>();
            foreach (string l in lfiles)
            {
                try
                {
                    //Console.WriteLine("Adding language " + l);
                    FileInfo f = new FileInfo(l);
                    string shortname = f.Name;
                    Translation t = new Translation(l, shortname);
                    if (shortname == "en.xml")
                    { english = t; }
                    if (shortname == lastactive)
                    { active = t; }
                    translations.Add(t.language, t);
                }
                catch { }
            }

            if (active == null)  // lastLanguage is not support
            { active = english; }

            Trans.trans = this;
        }

        public void selectLanguage(Translation t)
        {
            active = t;

            RegMemory.SetString("lastLanguage", t.fileshort);
        }

        public static string T(string id, bool forcedToEnglish = false)
        {
            string? res = null;
            if (forcedToEnglish)
            {
                res = trans.english.trans[id];
                return res;
            }
            if (trans.active != null && trans.active.trans.ContainsKey(id))
                res = trans.active.trans[id];
            if (res != null) return res;
            if (trans.english != null && trans.english.trans.ContainsKey(id))
                res = trans.english.trans[id];
            if (res != null) return res;
            return id;
        }

        public static string T1(string id, string v1)
        {
            string res = T(id);
            res = res.Replace("$1", v1);
            return res;
        }

        public static string T2(string id, string v1,string v2)
        {
            string res = T(id);
            res = res.Replace("$1", v1);
            res = res.Replace("$2", v2);
            return res;
        }
    }
}

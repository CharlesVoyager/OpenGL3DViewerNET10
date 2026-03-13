using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace View3D.model
{
    public class CommandParameter
    {
        public bool optional = false;
        public string parameter = "";
        public string description = "";

        public CommandParameter(XmlNode n)
        {
            description = n.InnerText;
            parameter = n.Attributes["name"].InnerText;
            string o = n.Attributes["optional"].InnerText;
            if (o.Equals("1")) optional = true;
        }
        public override string ToString()
        {
            if (optional)
                return "[" + parameter + "\\{{\\i " + description + "}\\}] ";
            return parameter + "\\{{\\i " + description + "}\\} ";
        }
    }
    public class CommandDescription
    {
        public string command;
        public string title;
        public LinkedList<CommandParameter> parameter = new LinkedList<CommandParameter>();
        public string description;
        public CommandDescription(XmlNode n)
        {
            try
            {
                command = n.Attributes["name"].InnerText;
                title = n.Attributes["title"].InnerText;
                foreach (XmlNode pn in n.ChildNodes)
                {
                    if (pn.NodeType != XmlNodeType.Element) continue;
                    if (pn.Name.Equals("Param"))
                        parameter.AddLast(new CommandParameter(pn));
                    if (pn.Name.Equals("Description"))
                        description = pn.InnerText;
                }
            }
            catch { }
        }
    }
}

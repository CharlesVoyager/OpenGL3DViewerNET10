using System;
using System.Globalization;
using System.Text;

namespace View3D.model
{
    /// <summary>
    /// Stores the complete data of a gcode command in an easy 
    /// accessible data structure. This structure can be converted
    /// into a binary or ascii representation to be send to a
    /// reprap printer.
    /// </summary>
    public class GCode
    {
        public static NumberFormatInfo format = CultureInfo.InvariantCulture.NumberFormat;
        public static string floatNoExp = "0.#####";
        private ushort fields = 128;
        private ushort g = 0, m = 0;
        private float x, y, z;
        public String orig;

        public bool hasM { get { return (fields & 2) != 0; } }
        public ushort M
        {
            get { return m; }
            set { m = value; fields |= 2; }
        }
        public bool hasG { get { return (fields & 4) != 0; } }
        public ushort G
        {
            get { return g; }
            set { g = value; fields |= 4; }
        }
        public bool hasX { get { return (fields & 8) != 0; } }
        public float X
        {
            get { return x; }
            set { x = value; fields |= 8; }
        }
        public bool hasY { get { return (fields & 16) != 0; } }
        public float Y
        {
            get { return y; }
            set { y = value; fields |= 16; }
        }

        public bool hasL { get { return (fields & 32) != 0; } }

        public float Z
        {
            get { return z; }
            set { z = value; fields |= 32; }
        }

        public String getAscii(bool inclLine, bool inclChecksum)
        {
            StringBuilder s = new StringBuilder();
            if (hasM)
            {
                s.Append("M");
                s.Append(m);
            }
            if (hasG)
            {
                s.Append("G");
                s.Append(g);
            }
            if (hasX)
            {
                s.Append(" X");
                s.Append(x.ToString(floatNoExp, format));
            }
            if (hasY)
            {
                s.Append(" Y");
                s.Append(y.ToString(floatNoExp, format));
            }
            if (hasL)
            {
                s.Append(" Z");
                s.Append(z.ToString(floatNoExp, format));
            }
            if (inclChecksum)
            {
                int check = 0;
                foreach (char ch in s.ToString()) check ^= (ch & 0xff);
                check ^= 32;
                s.Append(" *");
                s.Append(check);
            }
            return s.ToString();
        }

        private void AddCode(char c, string val)
        {
            double d;
            double.TryParse(val, NumberStyles.Float, format, out d);
            switch (c)
            {
                case 'G':
                    G = (ushort)d;
                    break;
                case 'M':
                    M = (ushort)d;
                    break;
                case 'X':
                    X = (float)d;
                    break;
                case 'Y':
                    Y = (float)d;
                    break;
                case 'Z':
                    Z = (float)d;
                    break;
                default:
                    break;
            }
        }

        public void Parse(String line)
        {
            orig = line.Trim();
            fields = 128;
            int l = orig.Length, i;
            int mode = 0; // 0 = search code, 1 = search value
            char code = ';';
            int p1 = 0;
            for (i = 0; i < l; i++)
            {
                char c = orig[i];
                if (mode == 0 && c >= 'a' && c <= 'z')
                {
                    c -= (char)32;
                    orig = orig.Substring(0, i) + c + orig.Substring(i + 1);
                }
                if (mode == 0 && c >= 'A' && c <= 'Z')
                {
                    code = c;
                    mode = 1;
                    p1 = i + 1;
                    continue;
                }
                else if (mode == 1)
                {
                    if (c == ' ' || c == '\t' || c == ';')
                    {
                        AddCode(code, orig.Substring(p1, i - p1));
                        mode = 0;
                    }
                }
                if (c == ';') break;
            }
            if (mode == 1)
                AddCode(code, orig.Substring(p1, orig.Length - p1));
        }

        public override string ToString()
        {
            return getAscii(true, true);
        }
    }
}

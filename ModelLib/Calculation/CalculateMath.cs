using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace View3D.Calculation
{
    class CalculateMath
    {
        public static float Max(float x1, float x2)
        {
            return x1 > x2 ? x1 : x2;
        }

        public static float Min(float x1, float x2)
        {
            return x1 < x2 ? x1 : x2;
        }
    }
}

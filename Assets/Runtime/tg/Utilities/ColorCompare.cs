using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace tg.util
{
    /// <summary>
    /// Contains extension methods for Unity Color's to be compared by visual similarity.
    /// </summary>
    public static class ColorCompare
    {
        public enum ColorCompareMethod { DE76, DE94, DE00 }

        public static float similarity(this Color color0, in Color color1, ColorCompareMethod method = ColorCompareMethod.DE00)
        {
            var lab0 = rgb2lab(color0);
            var lab1 = rgb2lab(color1);

            switch(method)
            {
                case ColorCompareMethod.DE76: return ColorCompare.DE76(lab0, lab1);
                case ColorCompareMethod.DE94: return ColorCompare.DE94(lab0, lab1);
                case ColorCompareMethod.DE00: return ColorCompare.DE00(lab0, lab1);
            }

            return float.NaN;
        }


        // Helper method to convert RGB to Lab color space using SIMD
        private static float3 rgb2lab(in Color color)
        {
            float3 c        = new float3(color.r, color.g, color.b);
            float3 xyz      = new float3(
                math.dot(new float3(0.4124564f, 0.3575761f, 0.1804375f), c),
                math.dot(new float3(0.2126729f, 0.7151522f, 0.0721750f), c),
                math.dot(new float3(0.0193339f, 0.1191920f, 0.9503041f), c)
            ) / 255f;

            float3 fxyz     = math.select(math.pow(xyz / 0.950456f, 1.0f / 3.0f), (xyz * 7.787f) + (16f / 116f), xyz > 0.008856f);
            float3 lab      = new float3((116f * fxyz.y) - 16f, 500f * (fxyz.x - fxyz.y), 200f * (fxyz.y - fxyz.z));

            return lab;
        }

        private static float deltaPrime(float C1Prime, float C2Prime, float h1Prime, float h2Prime)
        {
            float deltaHPrime = h2Prime - h1Prime;

            if (C1Prime * C2Prime == 0)
            {
                // If either C1' or C2' is zero, set deltaH' to 0
                return 0.0f;
            }
            else if (Mathf.Abs(deltaHPrime) <= 180.0f)
            {
                // If the absolute value of deltaH' is less than or equal to 180 degrees, return deltaH'
                return deltaHPrime;
            }
            else if (deltaHPrime > 180.0f)
            {
                // If deltaH' is greater than 180 degrees, subtract 360 degrees
                return deltaHPrime - 360.0f;
            }
            else
            {
                // If deltaH' is less than -180 degrees, add 360 degrees
                return deltaHPrime + 360.0f;
            }
        }

        private static float hPrime(float b, float aPrime)
        {
            float hPrime = Mathf.Atan2(b, aPrime) * 180.0f / Mathf.PI;

            if (hPrime < 0)
            {
                // If h' is negative, add 360 degrees to bring it within the range of 0 to 360 degrees
                hPrime += 360.0f;
            }

            return hPrime;
        }

        private static float DE76(float3 lab1, float3 lab2)
        {
             return Mathf.Sqrt(Mathf.Pow(lab2.x - lab1.x, 2) + Mathf.Pow(lab2.y - lab1.y, 2) + Mathf.Pow(lab2.z - lab1.z, 2));
        }
        
        private static float DE94(float3 lab1, float3 lab2)
        {

            // Calculate the color difference using the CIE94 formula
            float deltaL = lab2.x - lab1.x;
            float deltaA = lab2.y - lab1.y;
            float deltaB = lab2.z - lab1.z;

            float deltaC = Mathf.Sqrt(deltaA * deltaA + deltaB * deltaB);
            float deltaH = Mathf.Sqrt(deltaA * deltaA + deltaB * deltaB - deltaC * deltaC);

            float Sl = 1.0f;
            float K1 = 0.045f;
            float K2 = 0.015f;

            float Sc = 1.0f + K1 * deltaC;
            float Sh = 1.0f + K2 * deltaC;

            float deltaE = Mathf.Sqrt(
                Mathf.Pow(deltaL / Sl, 2) +
                Mathf.Pow(deltaC / Sc, 2) +
                Mathf.Pow(deltaH / Sh, 2)
            );
            
            return float.IsNaN(deltaE) ? 0.0f : deltaE;
        }
        
        private static float DE00(float3 ca, float3 cb, float kL = 1.0f, float kC = 1.0f, float kH = 1.0f)
        {
            float L1 = ca.x; float L2 = cb.x;
            float a1 = ca.y; float a2 = cb.y;
            float b1 = ca.z; float b2 = cb.z;


            float C1 = Mathf.Sqrt(a1 * a1 + b1 * b1);
            float C2 = Mathf.Sqrt(a2 * a2 + b2 * b2);
            float barC = (C1 + C2) * 0.5f;

            float G = 0.5f * (1.0f - Mathf.Sqrt(Mathf.Pow(barC, 7.0f) / (Mathf.Pow(barC, 7.0f) + Mathf.Pow(25.0f, 7.0f))));
            float a1Prime = (1.0f + G) * a1;
            float a2Prime = (1.0f + G) * a2;

            float C1Prime = Mathf.Sqrt(a1Prime * a1Prime + b1 * b1);
            float C2Prime = Mathf.Sqrt(a2Prime * a2Prime + b2 * b2);

            float h1Prime = hPrime(b1, a1Prime);
            float h2Prime = hPrime(b2, a2Prime);

            float deltaLPrime = L2 - L1;
            float deltaCPrime = C2Prime - C1Prime;
            float deltahPrime = deltaPrime(C1Prime, C2Prime, h1Prime, h2Prime);
            float deltaHPrimeBar = 2.0f * Mathf.Sqrt(C1Prime * C2Prime) * Mathf.Sin(deltahPrime * 0.5f * Mathf.Deg2Rad);

            float LPrimeBar = (L1 + L2) * 0.5f;
            float CPrimeBar = (C1Prime + C2Prime) * 0.5f;

            float hPrimeBar = Mathf.Abs(h1Prime - h2Prime) <= 180.0f ? (h1Prime + h2Prime) * 0.5f : (h1Prime + h2Prime + 360.0f) * 0.5f;

            float T = 1.0f -
                0.17f * Mathf.Cos((hPrimeBar - 30.0f)           * Mathf.Deg2Rad) +
                0.24f * Mathf.Cos(2.0f * hPrimeBar              * Mathf.Deg2Rad) +
                0.32f * Mathf.Cos((3.0f * hPrimeBar + 6.0f)     * Mathf.Deg2Rad) -
                0.20f * Mathf.Cos((4.0f * hPrimeBar - 63.0f)    * Mathf.Deg2Rad);

            float SL = 1.0f + (0.015f * ((LPrimeBar - 50.0f) * (LPrimeBar - 50.0f))) / Mathf.Sqrt(20.0f + ((LPrimeBar - 50.0f) * (LPrimeBar - 50.0f)));
            float SC = 1.0f + 0.045f * CPrimeBar;
            float SH = 1.0f + 0.015f * CPrimeBar * T;

            float deltaTheta = 30.0f * Mathf.Exp(-Mathf.Pow((hPrimeBar - 275.0f) / 25.0f, 2.0f));
            float RC = 2.0f * Mathf.Sqrt(Mathf.Pow(CPrimeBar, 7.0f) / (Mathf.Pow(CPrimeBar, 7.0f) + Mathf.Pow(25.0f, 7.0f)));
            float RT = -RC * Mathf.Sin(2.0f * deltaTheta * Mathf.Deg2Rad);

            float deltaE =
                Mathf.Sqrt(
                    Mathf.Pow(deltaLPrime       / (kL * SL), 2.0f) +
                    Mathf.Pow(deltaCPrime       / (kC * SC), 2.0f) +
                    Mathf.Pow(deltaHPrimeBar    / (kH * SH), 2.0f) +
                    RT * (deltaCPrime           / (kC * SC))
                       * (deltaHPrimeBar        / (kH * SH))
                );

            return deltaE;
        }
    }
}

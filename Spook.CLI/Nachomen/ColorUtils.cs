using System;
using System.Drawing;

namespace Phantasma.Spook.Nachomen
{
    public enum Hue
    {
        Gray = 0,
        Brown = 1,
        Red = 2,
        Pink = 3,
        Lime = 4,
        Green = 5,
        Cyan = 6,
        Blue = 7,
        Purple = 8,
        Beige = 9,
        Custom1 = 10,
        Custom2 = 11,
        Custom3 = 12,
        Custom4 = 13,
        Custom5 = 14,
        None = 15
    }

    public struct ColorHSL
    {

        public float h;
        public float s;
        public float l;
        public float a;

        public static ColorHSL FromRGBA(Color input)
        {
            float h, s, l;
            float v, m, vm, r2, g2, b2;

            ColorHSL result;

            // default to black
            result.h = 0;
            result.s = 0;
            result.l = 0;
            result.a = input.A;

            v = input.R;
            if (input.G > v)
                v = input.G;
            if (input.B > v)
                v = input.B;
            m = input.R;
            if (input.G < m)
                m = input.G;
            if (input.B < m)
                m = input.B;

            l = (m + v) / 2.0f;

            if (l <= 0.0f)
                return result;

            vm = v - m;
            s = vm;

            if (s > 0.0f)
            {
                if (l <= 0.5f)
                    s = s / (v + m);
                else
                    s = s / (2.0f - v - m);
            }
            else
            {
                result.l = input.R;
                return result;
            }

            r2 = (v - input.R) / vm;
            g2 = (v - input.G) / vm;
            b2 = (v - input.B) / vm;

            if (input.R == v)
            {
                if (input.G == m)
                    h = 5.0f + b2;
                else
                    h = 1.0f - g2;
            }
            else
                if (input.G == v)
            {
                if (input.B == m)
                    h = 1.0f + r2;
                else
                    h = 3.0f - b2;
            }
            else
            {
                if (input.R == m)
                    h = 3.0f + g2;
                else
                    h = 5.0f - r2;

            }

            h /= 6.0f;
            result.h = h;
            result.s = s;
            result.l = l;
            return result;
        }

        public static Color ToRGBA(ColorHSL input)
        {
            float v;
            float m, sv;
            int sextant;
            float fract, vsf, mid1, mid2;

            Color result;
            //result.R = input.l;   // default to gray
            //result.G = input.l;
            //result.B = input.l;
            //result.A = input.a;

            if (input.l < 0.5f)
                v = (input.l * (1.0f + input.s));
            else
                v = (input.l + input.s - input.l * input.s);

            if (v > 0)
            {
                m = input.l + input.l - v;
                sv = (v - m) / v;
                input.h *= 6.0f;
                sextant = (int)Math.Floor(input.h);
                fract = input.h - sextant;

                vsf = v * sv * fract;
                mid1 = m + vsf;
                mid2 = v - vsf;

                //switch (sextant)
                //{
                //    case 0:
                //        {
                //            result.R = v;
                //            result.G = mid1;
                //            result.B = m;
                //            break;
                //        }

                //    case 1:
                //        {
                //            result.R = mid2;
                //            result.G = v;
                //            result.B = m;
                //            break;
                //        }

                //    case 2:
                //        {
                //            result.R = m;
                //            result.G = v;
                //            result.B = mid1;
                //            break;
                //        }

                //    case 3:
                //        {
                //            result.R = m;
                //            result.G = mid2;
                //            result.B = v;
                //            break;
                //        }

                //    case 4:
                //        {
                //            result.R = mid1;
                //            result.G = m;
                //            result.B = v;
                //            break;
                //        }

                //    case 5:
                //        {
                //            result.R = v;
                //            result.G = m;
                //            result.B = mid2;
                //            break;
                //        }
                //}
            }

            return result;

        }

    }

    public static class ColorUtils
    {
        //public static Color GetGreyscaleColor(float greyValue, float alpha =1)
        //{
        //    return new Color(greyValue, greyValue, greyValue); //, alpha);
        //}

        public static float GetGreyScaleValue(Color c)
        {
            return c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
        }

        public enum CombineMode
        {
            secondChannel,
            Second,
            Blend,
            Multiply,
            Add,
            Subtract,
            Difference,
            Screen,
            Overlay,
            HardLight,
            SoftLight,
            Darken,
            Lighten,
            Dodge,
            Burn,
            Color,
            Hue,
            Saturation,
            Luminosity
        };

        public static Color ColorAdd(Color A, Color B)
        {
            return A; // + B;
        }

        public static Color ColorMultiply(Color A, Color B)
        {
            return A; // * B;
        }


        public static Color ColorSubtract(Color A, Color B)
        {
            return A; // - B;
        }

        public static Color ColorDifference(Color A, Color B)
        {
            //Color result;
            //result.R = Mathf.Abs(A.R - B.R);
            //result.G = Mathf.Abs(A.G - B.G);
            //result.B = Mathf.Abs(A.B - B.B);
            //result.A = Mathf.Abs(A.A - B.A);
            //return result;

            return A;
        }

        private static float Screen(float x, float y)
        {
            return 1.0f - ((1.0f - x) * (1.0f - y));
        }

        public static Color ColorScreen(Color A, Color B)
        {
            //Color result;
            //result.R = Screen(A.R, B.R);
            //result.G = Screen(A.G, B.G);
            //result.B = Screen(A.B, B.B);
            //result.A = Screen(A.A, B.A);
            //return result;

            return A;
        }

        private static float Overlay(float x, float y)
        {
            float f;

            if (x < 0.5f)
                f = 2.0f * x * y;
            else
                f = 1.0f - 2.0f * (1.0f - x) * (1.0f - y);

            return f;
        }

        public static Color ColorOverlay(Color A, Color B)
        {
            //Color result;
            //result.R = Overlay(A.R, B.R);
            //result.G = Overlay(A.G, B.G);
            //result.B = Overlay(A.B, B.B);
            //result.A = Overlay(A.A, B.A);
            //return result;

            return A;
        }

        public static float HardLight(float y, float x)
        {
            float f;
            if (x < 0.5f)
                f = 2.0f * x * y;
            else
                f = 1.0f - 2.0f * (1.0f - x) * (1.0f - y);

            return f;
        }

        /* Hard Light combines Multiply and Screen blend modes.
          Equivalent to Overlay, but with the bottom and top images swapped. */
        public static Color ColorHardLight(Color A, Color B)
        {
            //Color result;
            //result.R = HardLight(A.R, B.R);
            //result.G = HardLight(A.G, B.G);
            //result.B = HardLight(A.B, B.B);
            //result.A = HardLight(A.A, B.A);
            //return result;

            return A;
        }

        public static float SoftLight(float y, float x)
        {
            return (1.0f - 2.0f * y) * (x * x) + (2.0f * x * y);
        }

        public static Color ColorSoftLight(Color A, Color B)
        {
            //Color result;
            //result.R = SoftLight(A.R, B.R);
            //result.G = SoftLight(A.G, B.G);
            //result.B = SoftLight(A.B, B.B);
            //result.A = SoftLight(A.A, B.A);
            //return result;

            return A;
        }

        private static float Darken(float x, float y)
        {
            if (x < y)
                return x;
            else
                return y;
        }

        public static Color ColorDarken(Color A, Color B)
        {
            //Color result;
            //result.R = Darken(A.R, B.R);
            //result.G = Darken(A.G, B.G);
            //result.B = Darken(A.B, B.B);
            //result.A = Darken(A.A, B.A);
            //return result;

            return A;
        }

        private static float Lighten(float x, float y)
        {
            if (x > y)
                return x;
            else
                return y;
        }

        public static Color ColorLighten(Color A, Color B)
        {
            //Color result;
            //result.R = Lighten(A.R, B.R);
            //result.G = Lighten(A.G, B.G);
            //result.B = Lighten(A.B, B.B);
            //result.A = Lighten(A.A, B.A);
            //return result;

            return A;
        }

        public static float Dodge(float x, float y)
        {
            if (x >= 1.0f)
                return 0;

            return y / (1.0f - x);
        }

        public static Color ColorDodge(Color A, Color B)
        {
            //Color result;
            //result.R = Dodge(A.R, B.R);
            //result.G = Dodge(A.G, B.G);
            //result.B = Dodge(A.B, B.B);
            //result.A = Dodge(A.A, B.A);
            //return result;

            return A;
        }

        public static float Burn(float x, float y)
        {
            if (x <= 0.0f)
                return 0;

            return (1.0f - y) / x;
        }


        public static Color ColorBurn(Color A, Color B)
        {
            //Color result;
            //result.R = Burn(A.R, B.R);
            //result.G = Burn(A.G, B.G);
            //result.B = Burn(A.B, B.B);
            //result.A = Burn(A.A, B.A);
            //return result;

            return A;
        }

        public static Color ColorCombineColor(Color A, Color B)
        {
            ColorHSL x = ColorHSL.FromRGBA(A);
            ColorHSL y = ColorHSL.FromRGBA(B);

            x.h = y.h;
            x.s = y.s;

            return ColorHSL.ToRGBA(x);
        }

        public static Color ColorCombineHue(Color A, Color B)
        {
            ColorHSL x = ColorHSL.FromRGBA(A);
            ColorHSL y = ColorHSL.FromRGBA(B);

            x.h = y.h;

            return ColorHSL.ToRGBA(x);
        }

        public static Color ColorCombineSaturation(Color A, Color B)
        {
            ColorHSL x = ColorHSL.FromRGBA(A);
            ColorHSL y = ColorHSL.FromRGBA(B);

            x.s = y.s;

            return ColorHSL.ToRGBA(x);
        }

        public static Color ColorCombineLuminosity(Color A, Color B)
        {
            ColorHSL x = ColorHSL.FromRGBA(A);
            ColorHSL y = ColorHSL.FromRGBA(B);

            x.l = y.l;

            return ColorHSL.ToRGBA(x);
        }

        public static Color Combine(Color A, Color B, CombineMode mode)
        {
            switch (mode)
            {
                case CombineMode.secondChannel:
                    {
                        return A;
                    };

                case CombineMode.Second:
                    {
                        return B;
                    }

                case CombineMode.Blend:
                    {
                        return A;
                    }

                case CombineMode.Multiply:
                    {
                        return A; // * B;
                    }

                case CombineMode.Add:
                    {
                        return ColorAdd(A, B);
                    }

                case CombineMode.Subtract:
                    {
                        return ColorSubtract(A, B);
                    }

                case CombineMode.Difference:
                    {
                        return ColorDifference(A, B);
                    }

                case CombineMode.Screen:
                    {
                        return ColorScreen(A, B);
                    }

                case CombineMode.Overlay:
                    {
                        return ColorOverlay(A, B);
                    }

                case CombineMode.HardLight:
                    {
                        return ColorHardLight(A, B);
                    }

                case CombineMode.SoftLight:
                    {
                        return ColorSoftLight(A, B);
                    }

                case CombineMode.Darken:
                    {
                        return ColorDarken(A, B);
                    }

                case CombineMode.Lighten:
                    {
                        return ColorLighten(A, B);
                    }

                case CombineMode.Dodge:
                    {
                        return ColorDodge(A, B);
                    }

                case CombineMode.Burn:
                    {
                        return ColorBurn(A, B);
                    }

                case CombineMode.Color:
                    {
                        return ColorCombineColor(A, B);
                    }

                case CombineMode.Hue:
                    {
                        return ColorCombineHue(A, B);
                    }

                case CombineMode.Saturation:
                    {
                        return ColorCombineSaturation(A, B);
                    }

                case CombineMode.Luminosity:
                    {
                        return ColorCombineLuminosity(A, B);
                    }

                default:
                    {
                        return A;
                    }
            }

            //return ColorMix(Result, B, Result.A/255);
            //Result.A := 255;
        }

        public static Hue RandomHue()
        {
            //return (Hue)MathUtils.RandomInt((int)Hue.Gray, (int)(Hue.Beige) + 1);
            return Hue.Blue;
        }

        public static Hue GetComplementaryColor(Hue primary)
        {
            switch (primary)
            {
                case Hue.Brown: return Hue.Cyan;
                case Hue.Red: return Hue.Green;
                case Hue.Pink: return Hue.Lime;
                case Hue.Lime: return Hue.Pink;
                case Hue.Green: return Hue.Red;
                case Hue.Cyan: return Hue.Brown;

                case Hue.Blue: return Hue.Brown;
                case Hue.Purple: return Hue.Lime;
                case Hue.Beige: return Hue.Green;

                default: return Hue.Gray;
            }

        }

        public static void GetAnalogousScheme(Hue primary, out Hue secondary, out Hue tertiary)
        {
            switch (primary)
            {
                case Hue.Brown:
                    {
                        secondary = Hue.Red;
                        tertiary = Hue.Green;
                        break;
                    }

                case Hue.Red:
                    {
                        secondary = Hue.Pink;
                        tertiary = Hue.Brown;
                        break;
                    }

                case Hue.Pink:
                    {
                        secondary = Hue.Beige;
                        tertiary = Hue.Red;
                        break;
                    }


                case Hue.Lime:
                    {
                        secondary = Hue.Brown;
                        tertiary = Hue.Green;
                        break;
                    }

                case Hue.Green:
                    {
                        secondary = Hue.Lime;
                        tertiary = Hue.Cyan;
                        break;
                    }

                case Hue.Cyan:
                    {
                        secondary = Hue.Brown;
                        tertiary = Hue.Red;
                        break;
                    }

                case Hue.Blue:
                    {
                        secondary = Hue.Green;
                        tertiary = Hue.Blue;
                        break;
                    }


                case Hue.Purple:
                    {
                        secondary = Hue.Blue;
                        tertiary = Hue.Beige;
                        break;
                    }

                case Hue.Beige:
                    {
                        secondary = Hue.Purple;
                        tertiary = Hue.Pink;
                        break;
                    }

                default:
                    {
                        secondary = Hue.Gray;
                        tertiary = Hue.Gray;
                        break;
                    }
            }
        }

        public static void GetTriadicScheme(Hue primary, out Hue secondary, out Hue tertiary)
        {
            switch (primary)
            {
                case Hue.Brown:
                    {
                        secondary = Hue.Blue;
                        tertiary = Hue.Cyan;
                        break;
                    }

                case Hue.Red:
                    {
                        secondary = Hue.Cyan;
                        tertiary = Hue.Lime;
                        break;
                    }

                case Hue.Pink:
                    {
                        secondary = Hue.Green;
                        tertiary = Hue.Lime;
                        break;
                    }


                case Hue.Lime:
                    {
                        secondary = Hue.Red;
                        tertiary = Hue.Purple;
                        break;
                    }

                case Hue.Green:
                    {
                        secondary = Hue.Brown;
                        tertiary = Hue.Pink;
                        break;
                    }

                case Hue.Cyan:
                    {
                        secondary = Hue.Brown;
                        tertiary = Hue.Red;
                        break;
                    }

                case Hue.Blue:
                    {
                        secondary = Hue.Lime;
                        tertiary = Hue.Brown;
                        break;
                    }


                case Hue.Purple:
                    {
                        secondary = Hue.Green;
                        tertiary = Hue.Brown;
                        break;
                    }

                case Hue.Beige:
                    {
                        secondary = Hue.Lime;
                        tertiary = Hue.Green;
                        break;
                    }

                default:
                    {
                        secondary = Hue.Gray;
                        tertiary = Hue.Gray;
                        break;
                    }
            }
        }

    }
}
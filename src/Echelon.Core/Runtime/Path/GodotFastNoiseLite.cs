// MIT License
//
// Copyright (c) 2023 Jordan Peck (jordan.me2@gmail.com)
// Copyright (c) 2023 Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// OpenSimplex2S + fBm port of Godot 4.7.2's thirdparty/misc/FastNoiseLite.h.

namespace Echelon.Core
{
    public sealed class GodotFastNoiseLite
    {
        private const int PrimeX = 501125321;
        private const int PrimeY = 1136930381;

        private static readonly float[] Gradients2D =
        {
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.130526192220052f, 0.99144486137381f, 0.38268343236509f, 0.923879532511287f, 0.608761429008721f, 0.793353340291235f, 0.793353340291235f, 0.608761429008721f,
            0.923879532511287f, 0.38268343236509f, 0.99144486137381f, 0.130526192220051f, 0.99144486137381f, -0.130526192220051f, 0.923879532511287f, -0.38268343236509f,
            0.793353340291235f, -0.60876142900872f, 0.608761429008721f, -0.793353340291235f, 0.38268343236509f, -0.923879532511287f, 0.130526192220052f, -0.99144486137381f,
            -0.130526192220052f, -0.99144486137381f, -0.38268343236509f, -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
            -0.923879532511287f, -0.38268343236509f, -0.99144486137381f, -0.130526192220052f, -0.99144486137381f, 0.130526192220051f, -0.923879532511287f, 0.38268343236509f,
            -0.793353340291235f, 0.608761429008721f, -0.608761429008721f, 0.793353340291235f, -0.38268343236509f, 0.923879532511287f, -0.130526192220052f, 0.99144486137381f,
            0.38268343236509f, 0.923879532511287f, 0.923879532511287f, 0.38268343236509f, 0.923879532511287f, -0.38268343236509f, 0.38268343236509f, -0.923879532511287f,
            -0.38268343236509f, -0.923879532511287f, -0.923879532511287f, -0.38268343236509f, -0.923879532511287f, 0.38268343236509f, -0.38268343236509f, 0.923879532511287f,
        };

        private readonly int _seed;
        private readonly float _frequency;
        private readonly int _octaves;
        private readonly float _lacunarity;
        private readonly float _gain;
        private readonly float _fractalBounding;

        public GodotFastNoiseLite(int seed, float frequency, int octaves, float lacunarity = 2.0f, float gain = 0.5f)
        {
            _seed = seed;
            _frequency = frequency;
            _octaves = octaves;
            _lacunarity = lacunarity;
            _gain = gain;

            float absGain = gain < 0.0f ? -gain : gain;
            float amp = absGain;
            float ampFractal = 1.0f;
            for (int i = 1; i < octaves; i++)
            {
                ampFractal += amp;
                amp *= absGain;
            }

            _fractalBounding = 1.0f / ampFractal;
        }

        public float GetNoise2D(float x, float y)
        {
            x *= _frequency;
            y *= _frequency;

            const float sqrt3 = 1.7320508075688772935274463415059f;
            float f2 = 0.5f * (sqrt3 - 1.0f);
            float t = (x + y) * f2;
            x += t;
            y += t;

            int seed = _seed;
            float sum = 0.0f;
            float amp = _fractalBounding;
            for (int i = 0; i < _octaves; i++)
            {
                float noise = SingleOpenSimplex2S(seed++, x, y);
                sum += noise * amp;
                float weighted = 1.0f + 0.0f * ((noise + 1.0f < 2.0f ? noise + 1.0f : 2.0f) * 0.5f - 1.0f);
                amp *= weighted;
                x *= _lacunarity;
                y *= _lacunarity;
                amp *= _gain;
            }

            return sum;
        }

        private static float SingleOpenSimplex2S(int seed, float x, float y)
        {
            const float sqrt3 = 1.7320508075688772935274463415059f;
            float g2 = (3.0f - sqrt3) / 6.0f;

            int i = FastFloor(x);
            int j = FastFloor(y);
            float xi = x - i;
            float yi = y - j;

            unchecked
            {
                i *= PrimeX;
                j *= PrimeY;
            }

            int i1 = unchecked(i + PrimeX);
            int j1 = unchecked(j + PrimeY);

            float t = (xi + yi) * g2;
            float x0 = xi - t;
            float y0 = yi - t;

            float a0 = 2.0f / 3.0f - x0 * x0 - y0 * y0;
            float value = a0 * a0 * (a0 * a0) * GradCoord(seed, i, j, x0, y0);

            float a1 = 2.0f * (1.0f - 2.0f * g2) * (1.0f / g2 - 2.0f) * t +
                (-2.0f * (1.0f - 2.0f * g2) * (1.0f - 2.0f * g2) + a0);
            float x1 = x0 - (1.0f - 2.0f * g2);
            float y1 = y0 - (1.0f - 2.0f * g2);
            value += a1 * a1 * (a1 * a1) * GradCoord(seed, i1, j1, x1, y1);

            float xmyi = xi - yi;
            if (t > g2)
            {
                if (xi + xmyi > 1.0f)
                {
                    float x2 = x0 + (3.0f * g2 - 2.0f);
                    float y2 = y0 + (3.0f * g2 - 1.0f);
                    float a2 = 2.0f / 3.0f - x2 * x2 - y2 * y2;
                    if (a2 > 0.0f)
                    {
                        value += a2 * a2 * (a2 * a2) * GradCoord(seed, i + (PrimeX << 1), j + PrimeY, x2, y2);
                    }
                }
                else
                {
                    float x2 = x0 + g2;
                    float y2 = y0 + (g2 - 1.0f);
                    float a2 = 2.0f / 3.0f - x2 * x2 - y2 * y2;
                    if (a2 > 0.0f)
                    {
                        value += a2 * a2 * (a2 * a2) * GradCoord(seed, i, j + PrimeY, x2, y2);
                    }
                }

                if (yi - xmyi > 1.0f)
                {
                    float x3 = x0 + (3.0f * g2 - 1.0f);
                    float y3 = y0 + (3.0f * g2 - 2.0f);
                    float a3 = 2.0f / 3.0f - x3 * x3 - y3 * y3;
                    if (a3 > 0.0f)
                    {
                        value += a3 * a3 * (a3 * a3) * GradCoord(seed, i + PrimeX, j + (PrimeY << 1), x3, y3);
                    }
                }
                else
                {
                    float x3 = x0 + (g2 - 1.0f);
                    float y3 = y0 + g2;
                    float a3 = 2.0f / 3.0f - x3 * x3 - y3 * y3;
                    if (a3 > 0.0f)
                    {
                        value += a3 * a3 * (a3 * a3) * GradCoord(seed, i + PrimeX, j, x3, y3);
                    }
                }
            }
            else
            {
                if (xi + xmyi < 0.0f)
                {
                    float x2 = x0 + (1.0f - g2);
                    float y2 = y0 - g2;
                    float a2 = 2.0f / 3.0f - x2 * x2 - y2 * y2;
                    if (a2 > 0.0f)
                    {
                        value += a2 * a2 * (a2 * a2) * GradCoord(seed, i - PrimeX, j, x2, y2);
                    }
                }
                else
                {
                    float x2 = x0 + (g2 - 1.0f);
                    float y2 = y0 + g2;
                    float a2 = 2.0f / 3.0f - x2 * x2 - y2 * y2;
                    if (a2 > 0.0f)
                    {
                        value += a2 * a2 * (a2 * a2) * GradCoord(seed, i + PrimeX, j, x2, y2);
                    }
                }

                if (yi < xmyi)
                {
                    float x2 = x0 - g2;
                    float y2 = y0 - (g2 - 1.0f);
                    float a2 = 2.0f / 3.0f - x2 * x2 - y2 * y2;
                    if (a2 > 0.0f)
                    {
                        value += a2 * a2 * (a2 * a2) * GradCoord(seed, i, j - PrimeY, x2, y2);
                    }
                }
                else
                {
                    float x2 = x0 + g2;
                    float y2 = y0 + (g2 - 1.0f);
                    float a2 = 2.0f / 3.0f - x2 * x2 - y2 * y2;
                    if (a2 > 0.0f)
                    {
                        value += a2 * a2 * (a2 * a2) * GradCoord(seed, i, j + PrimeY, x2, y2);
                    }
                }
            }

            return value * 18.24196194486065f;
        }

        private static int FastFloor(float value)
        {
            return value >= 0.0f ? (int)value : (int)value - 1;
        }

        private static float GradCoord(int seed, int xPrimed, int yPrimed, float xd, float yd)
        {
            int hash;
            unchecked
            {
                hash = seed ^ xPrimed ^ yPrimed;
                hash *= unchecked((int)0x27d4eb2d);
                hash ^= hash >> 15;
            }

            hash &= 127 << 1;
            float xg = Gradients2D[hash];
            float yg = Gradients2D[hash | 1];
            return xd * xg + yd * yg;
        }
    }
}

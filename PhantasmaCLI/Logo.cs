namespace Phantasma.CLI
{
    public static class Logo
    {
        public const int Width = 16;
        public const int Height = 10;

        private static int Ofs(int x, int y)
        {
            return x + Width * y;
        }

        public static byte[] GetPixels()
        {
            var pixels = new byte[Width * Height];

            byte blue = 1;
            byte cyan = 2;
            byte white = 3;

            for (int i = 2; i <= 13; i++)
                pixels[Ofs(i, 0)] = blue;

            for (int i = 1; i <= 14; i++)
                pixels[Ofs(i, 1)] = blue;

            for (int j = 2; j <= 8; j++)
                for (int i = 0; i <= 15; i++)
                    pixels[Ofs(i, j)] = blue;

            pixels[Ofs(1, 9)] = blue;
            pixels[Ofs(5, 9)] = blue;
            pixels[Ofs(6, 9)] = blue;
            pixels[Ofs(10, 9)] = blue;
            pixels[Ofs(11, 9)] = blue;
            pixels[Ofs(15, 9)] = blue;

            pixels[Ofs(1, 0)] = cyan;
            pixels[Ofs(14, 0)] = cyan;

            pixels[Ofs(0, 1)] = cyan;
            pixels[Ofs(15, 1)] = cyan;

            pixels[Ofs(3, 4)] = cyan;
            pixels[Ofs(4, 4)] = cyan;

            pixels[Ofs(11, 4)] = cyan;
            pixels[Ofs(12, 4)] = cyan;

            pixels[Ofs(3, 5)] = cyan;
            pixels[Ofs(4, 5)] = cyan;

            pixels[Ofs(11, 5)] = cyan;
            pixels[Ofs(12, 5)] = cyan;

            pixels[Ofs(0, 9)] = cyan;
            pixels[Ofs(4, 9)] = cyan;
            pixels[Ofs(9, 9)] = cyan;
            pixels[Ofs(14, 9)] = cyan;

            pixels[Ofs(3, 3)] = white;
            pixels[Ofs(4, 3)] = white;

            pixels[Ofs(11, 3)] = white;
            pixels[Ofs(12, 3)] = white;

            pixels[Ofs(3, 6)] = white;
            pixels[Ofs(4, 6)] = white;

            pixels[Ofs(11, 6)] = white;
            pixels[Ofs(12, 6)] = white;

            pixels[Ofs(2, 4)] = white;
            pixels[Ofs(5, 4)] = white;
            pixels[Ofs(10, 4)] = white;
            pixels[Ofs(13, 4)] = white;
            pixels[Ofs(2, 5)] = white;
            pixels[Ofs(5, 5)] = white;
            pixels[Ofs(10, 5)] = white;
            pixels[Ofs(13, 5)] = white;

            return pixels;
        }
    }
}

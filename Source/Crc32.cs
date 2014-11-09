/*
 * Copyright (C) Nicco, 2013
 * 
 * You may use, distribute or modify the following source code and all its content as long as the following tag stays.
 * 
 * Origin: http://mcvoltz.nprog.com/patch/
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MC_Custom_Updater
{
    public class Crc32
    {
        private static uint[] Table = new uint[256];
        private const uint Polynomial = 0xedb88320;
        private const uint Seed = 0xffffffff;

        static Crc32()
        {
            for (int i = 0; i < 256; ++i)
            {
                uint dw = (uint)i;
                for (int j = 0; j < 8; ++j)
                {
                    if ((dw & 1) != 0)
                        dw = (dw >> 1) ^ Polynomial;
                    else
                        dw = dw >> 1;
                }
                Table[i] = dw;
            }
        }

        public static uint ComputeFile(string filename, uint seed = Seed)
        {
            byte[] buffer = new byte[1048576];
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                while (fs.Position < fs.Length)
                {
                    int read = fs.Read(buffer, 0, (int)Math.Min(fs.Length - fs.Position, buffer.Length));
                    for (int i = 0; i < read; ++i)
                        seed = (seed >> 8) ^ Table[buffer[i] ^ seed & 0xff];
                }
            }

            return seed;
        }
    }
}
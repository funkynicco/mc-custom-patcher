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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MC_Custom_Updater
{
    public class Utilities
    {
        public static string GetProgress(long value, long max, ProgressType type)
        {
            if (max == 0)
                return "0 / 0" + (type == ProgressType.Size ? " B" : "") + " (0.00%)";

            double percent = (double)value / max * 100.0;
            SizeType valueType = value.GetSizeType();
            SizeType maxType = max.GetSizeType();
    
            return string.Format("{0} / {1} ({2:0.00}%)", value.GetSize(valueType != maxType), max.GetSize(), percent);
        }
    }

    public enum ProgressType
    {
        None,
        Size
    }

    public static class Extensions
    {
        public static SizeType GetSizeType(this long size)
        {
            double _size = (double)size;
            int i = 0;

            while (_size >= 1024.0)
            {
                _size /= 1024.0;
                ++i;
            }

            return (SizeType)i;
        }

        public static string GetSize(this long size, bool includeSizeType = true)
        {
            if (size < 1024)
                return size + (includeSizeType ? " B" : "");

            double _size = (double)size;
            int i = 0;

            while (_size >= 1024.0)
            {
                _size /= 1024.0;
                ++i;
            }

            string output = string.Format("{0:0.00}", _size);

            if (includeSizeType)
            {
                switch (i)
                {
                    case 1: output += " KB"; break;
                    case 2: output += " MB"; break;
                    case 3: output += " GB"; break;
                    case 4: output += " TB"; break;
                    case 5: output += " PB"; break;
                    case 6: output += " EB"; break;
                }
            }

            return output;
        }
    }

    public enum SizeType : int
    {
        Bytes,
        KiloBytes,
        MegaBytes,
        GigaBytes,
        TeraBytes,
        PetaBytes,
        ExaBytes
    }
}
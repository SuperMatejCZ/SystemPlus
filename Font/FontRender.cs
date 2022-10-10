﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using SystemPlus;
using SystemPlus.Utils;
using SystemPlus.Vectors;
using SystemPlus.Windows;

namespace SystemPlus.Font
{
    public static class FontRender
    {
        static object lockConvertToBitmap = new object();
        static Bitmap ConvertToBitmap(GlyphBitmap image)
        {
            var bmp = new Bitmap(image.Width, image.Height);
            for (int j = 0; j < image.Height; j++)
            {
                for (int i = 0; i < image.Width; i++)
                {
                    byte alpha = image.Pixels[i + j * image.Width];
                    lock (lockConvertToBitmap)
                    {
                        bmp.SetPixel(i, j, Color.FromArgb(alpha, alpha, alpha, alpha));
                    }
                }
            }

            return bmp;
        }

        static bool drw = false;
        static void Draw(DirectBitmap to, GlyphBitmap other, int x, int y, int color)
        {
            while (drw) { Thread.Sleep(0); }
            drw = true;
            for (int j = 0; j < other.Height; j++)
            {
                for (int i = 0; i < other.Width; i++)
                {
                    int srcOfs = i + j * other.Width;
                    int destOfs = (x + i) + (y + j) * to.Width;
                    if (other.Pixels[srcOfs] > 0)
                        to[destOfs] = color;
                }
            }
            drw = false;
        }

        static void Draw(GlyphBitmap to, GlyphBitmap other, int x, int y)
        {
            for (int j = 0; j < other.Height; j++)
            {
                for (int i = 0; i < other.Width; i++)
                {
                    var srcOfs = i + j * other.Width;
                    var destOfs = (x + i) + (y + j) * to.Width;
                    if (other.Pixels[srcOfs] > 0)
                        to.Pixels[destOfs] = other.Pixels[srcOfs];
                }
            }
        }
        static object gbgbDrawLock = new object();
        public static Size GetTextSize(Font font, string txt, int size = 32, int xOffset = 2)
        {
            // make this a number larger than 1 to enable SDF output
            int SDF_scale = 1;

            // here is the desired height in pixels of the output
            // the real value might not be exactly this depending on which characters are part of the input text
            int height = size * SDF_scale;
            var scale = font.ScaleInPixels(height);

            string[] lines = txt.Split('\n');

            int width = 0;
            int retHeight = 0;

            for (int u = 0; u < lines.Length; u++)
            {
                string text = lines[u];

                var glyphs = new ConcurrentDictionary<char, FontGlyph>();
                var bitmaps = new ConcurrentDictionary<char, Bitmap>();
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];

                    if (glyphs.ContainsKey(ch))
                    {
                        continue;
                    }
                    else
                    {
                        var glyph = font.RenderGlyph(ch, scale);
                        if (glyph == null)
                            glyph = font.RenderGlyph('?', scale);
                        glyph.xAdvance += xOffset;
                        glyphs[ch] = glyph;
                        bitmaps[ch] = ConvertToBitmap(glyph.Image);       
                    }
                }

                int ascent, descent, lineGap;
                font.GetFontVMetrics(out ascent, out descent, out lineGap);
                int baseLine = height - (int)(ascent * scale);


                int minX = int.MaxValue;
                int maxX = int.MinValue;
                int minY = int.MaxValue;
                int maxY = int.MinValue;

                var positions = new Point[text.Length];

                int loopX = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    var ch = text[i];
                    var glyph = glyphs[ch];

                    var next = i < text.Length - 1 ? text[i + 1] : '\0';

                    var kerning = font.GetKerning(ch, next, scale);

                    int y0 = height - baseLine + glyph.yOfs;
                    int y1 = y0 + glyph.Image.Height;

                    int x0 = loopX + glyph.xOfs - kerning;
                    int x1 = x0 + glyph.Image.Width;
                    loopX += glyph.xAdvance;

                    positions[i] = new Point(x0, y0);

                    x1 = Math.Max(loopX, x1);

                    minX = Math.Min(minX, x0);
                    maxX = Math.Max(maxX, x1);

                    minY = Math.Min(minY, y0);
                    maxY = Math.Max(maxY, y1);
                }

                int realWidth = (maxX - minX) + 1;
                int realHeight = (maxY - minY) + 1;

                for (int i = 0; i < text.Length; i++)
                {
                    positions[i].x -= minX;
                    positions[i].y -= minY;
                }

                var tempBmp = new GlyphBitmap(realWidth, realHeight);
                {
                    // draw the baseline height in blue color
                    var ly = height - (baseLine + minY);

                    // now draw each character
                    loopX = 0;

                    for (int i = 0; i < text.Length; i++)
                    {

                        var ch = text[i];
                        var glyph = glyphs[ch];
                        var bmp = bitmaps[ch];
                        var pos = positions[i];
                        lock (gbgbDrawLock)
                            tempBmp.Draw(glyph.Image, (int)pos.x, (int)pos.y);

                    }
                }

                if (SDF_scale > 1)
                {
                    tempBmp = DistanceFieldUtils.CreateDistanceField(tempBmp, SDF_scale, 32);
                }

                if (tempBmp.Width > width)
                    width = tempBmp.Width;

                retHeight = height * u + tempBmp.Height + 5;
            }

            return new Size(width, retHeight);
        }

        [STAThread]
        public static void Render(Font font, DirectBitmap dibm, string text, Vector2Int pos, int size = 32, int color = int.MaxValue)
            => Render(font, dibm, text, pos.x, pos.y, size, color, 2);
        [STAThread]
        public static void Render(Font font, DirectBitmap dibm, string text, Vector2Int pos, int size = 32, int color = int.MaxValue,
            int xOffset = 2, RenderType rt = RenderType.UpToDown)
            => Render(font, dibm, text, pos.x, pos.y, size, color, xOffset, rt);
        [STAThread]
        public static void Render(Font font, DirectBitmap dibm, string txt, int x, int y, int size = 32, int color = int.MaxValue,
            int xOffset = 2, RenderType rt = RenderType.UpToDown)
        {
            // make this a number larger than 1 to enable SDF output
            int SDF_scale = 1;

            // here is the desired height in pixels of the output
            // the real value might not be exactly this depending on which characters are part of the input text
            int height = size * SDF_scale;
            var scale = font.ScaleInPixels(height);

            string[] lines = txt.Split('\n');

            for (int u = 0; u < lines.Length; u++)
            {
                string text;
                if (rt == RenderType.UpToDown)
                    text = lines[u];
                else
                    text = lines[lines.Length - 1 - u];

                var glyphs = new ConcurrentDictionary<char, FontGlyph>();
                var bitmaps = new ConcurrentDictionary<char, Bitmap>();
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];

                    if (glyphs.ContainsKey(ch))
                    {
                        continue;
                    }
                    else
                    {
                        var glyph = font.RenderGlyph(ch, scale);
                        if (glyph == null)
                            return;

                        glyph.xAdvance += xOffset;
                        glyphs[ch] = glyph;
                        bitmaps[ch] = ConvertToBitmap(glyph.Image);     
                    }
                }

                int ascent, descent, lineGap;
                font.GetFontVMetrics(out ascent, out descent, out lineGap);
                int baseLine = height - (int)(ascent * scale);


                int minX = int.MaxValue;
                int maxX = int.MinValue;
                int minY = int.MaxValue;
                int maxY = int.MinValue;

                var positions = new Point[text.Length];

                int loopX = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    var ch = text[i];
                    var glyph = glyphs[ch];

                    var next = i < text.Length - 1 ? text[i + 1] : '\0';

                    var kerning = font.GetKerning(ch, next, scale);

                    int y0 = height - baseLine + glyph.yOfs;
                    int y1 = y0 + glyph.Image.Height;

                    int x0 = loopX + glyph.xOfs - kerning;
                    int x1 = x0 + glyph.Image.Width;
                    loopX += glyph.xAdvance;

                    positions[i] = new Point(x0, y0);

                    x1 = Math.Max(loopX, x1);

                    minX = Math.Min(minX, x0);
                    maxX = Math.Max(maxX, x1);

                    minY = Math.Min(minY, y0);
                    maxY = Math.Max(maxY, y1);
                }

                int realWidth = (maxX - minX) + 1;
                int realHeight = (maxY - minY) + 1;

                for (int i = 0; i < text.Length; i++)
                {
                    positions[i].x -= minX;
                    positions[i].y -= minY;
                }

                var tempBmp = new GlyphBitmap(realWidth, realHeight);
                {
                    // draw the baseline height in blue color
                    var ly = height - (baseLine + minY);

                    // now draw each character
                    loopX = 0;

                    for (int i = 0; i < text.Length; i++)
                    {
                        try
                        {
                            var ch = text[i];
                            var glyph = glyphs[ch];
                            var bmp = bitmaps[ch];
                            var pos = positions[i];
                            lock (gbgbDrawLock)
                                tempBmp.Draw(glyph.Image, (int)pos.x, (int)pos.y);
                        }
                        catch { return; }
                    }
                }

                if (SDF_scale > 1)
                {
                    tempBmp = DistanceFieldUtils.CreateDistanceField(tempBmp, SDF_scale, 32);
                }

                if (rt == RenderType.UpToDown)
                    lock (gbbDrawLock)
                        Draw(dibm, tempBmp, x, y + height * u, color);
                else if (rt == RenderType.DownToUp)
                    lock (gbbDrawLock)
                        Draw(dibm, tempBmp, x, y + height * -u, color);
            }
        }
        static object gbbDrawLock = new object();

        [STAThread]
        public static DirectBitmap GetRendered(ref Font font, string txt, int size = 32, int color = int.MaxValue,
            int xOffset = 2, RenderType rt = RenderType.UpToDown)
        {
            // make this a number larger than 1 to enable SDF output
            int SDF_scale = 1;

            // here is the desired height in pixels of the output
            // the real value might not be exactly this depending on which characters are part of the input text
            int height = size * SDF_scale;
            float scale = font.ScaleInPixels(height);

            string[] lines = txt.Split('\n');

            GlyphBitmap[] lineImages = new GlyphBitmap[lines.Length];

            int totalWidth = 0;
            int totalHeight = 0;

            for (int u = 0; u < lines.Length; u++)
            {
                string text;
                if (rt == RenderType.UpToDown)
                    text = lines[u];
                else
                    text = lines[lines.Length - 1 - u];

                ConcurrentDictionary<char, FontGlyph> glyphs = new ConcurrentDictionary<char, FontGlyph>();
                ConcurrentDictionary<char, Bitmap> bitmaps = new ConcurrentDictionary<char, Bitmap>();
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];

                    if (glyphs.ContainsKey(ch))
                    {
                        continue;
                    }
                    else
                    {
                        var glyph = font.RenderGlyph(ch, scale);
                        if (glyph == null)
                            return null;

                        glyph.xAdvance += xOffset;
                        glyphs[ch] = glyph;
                        bitmaps[ch] = ConvertToBitmap(glyph.Image);
                    }
                }

                int ascent, descent, lineGap;
                font.GetFontVMetrics(out ascent, out descent, out lineGap);
                int baseLine = height - (int)(ascent * scale);


                int minX = int.MaxValue;
                int maxX = int.MinValue;
                int minY = int.MaxValue;
                int maxY = int.MinValue;

                var positions = new Point[text.Length];

                int loopX = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];
                    FontGlyph glyph = glyphs[ch];

                    char next = i < text.Length - 1 ? text[i + 1] : '\0';

                    int kerning = font.GetKerning(ch, next, scale);

                    int y0 = height - baseLine + glyph.yOfs;
                    int y1 = y0 + glyph.Image.Height;

                    int x0 = loopX + glyph.xOfs - kerning;
                    int x1 = x0 + glyph.Image.Width;
                    loopX += glyph.xAdvance;

                    positions[i] = new Point(x0, y0);

                    x1 = Math.Max(loopX, x1);

                    minX = Math.Min(minX, x0);
                    maxX = Math.Max(maxX, x1);

                    minY = Math.Min(minY, y0);
                    maxY = Math.Max(maxY, y1);
                }

                int realWidth = (maxX - minX) + 1;
                int realHeight = (maxY - minY) + 1;

                for (int i = 0; i < text.Length; i++)
                {
                    positions[i].x -= minX;
                    positions[i].y -= minY;
                }

                GlyphBitmap tempBmp = new GlyphBitmap(realWidth, realHeight);
                {
                    // draw the baseline height in blue color
                    int ly = height - (baseLine + minY);

                    // now draw each character
                    loopX = 0;

                    for (int i = 0; i < text.Length; i++)
                    {
                        try
                        {
                            char ch = text[i];
                            FontGlyph glyph = glyphs[ch];
                            Bitmap bmp = bitmaps[ch];
                            Point pos = positions[i];
                            lock (gbgbDrawLock)
                                tempBmp.Draw(glyph.Image, (int)pos.x, (int)pos.y);
                        }
                        catch { return null; }
                    }
                }

                if (SDF_scale > 1)
                {
                    tempBmp = DistanceFieldUtils.CreateDistanceField(tempBmp, SDF_scale, 32);
                }

                lineImages[u] = tempBmp;

                if (tempBmp.Width > totalWidth)
                    totalWidth = tempBmp.Width;
                totalHeight += tempBmp.Height;
            }

            DirectBitmap textDb = new DirectBitmap(totalWidth, totalHeight);

            for (int u = 0; u < lineImages.Length; u++)
            {
                if (rt == RenderType.UpToDown)
                    lock (gbbDrawLock)
                        Draw(textDb, lineImages[u], 0, height * u, color);
                else if (rt == RenderType.DownToUp)
                    lock (gbbDrawLock)
                        Draw(textDb, lineImages[u], 0, height * -u, color);
            }

            return textDb;
        }

        [STAThread]
        public static void RenderConsole(Font font, DirectBitmap dibm, CPChar[] text, int[] colorBase, int x, int y, int size = 32,
            int xOffset = 2)
        {
            // make this a number larger than 1 to enable SDF output
            int SDF_scale = 1;

            // here is the desired height in pixels of the output
            // the real value might not be exactly this depending on which characters are part of the input text
            int height = size * SDF_scale;
            var scale = font.ScaleInPixels(height);

            List<CPChar> textL = new List<CPChar>();

            var glyphs = new ConcurrentDictionary<char, FontGlyph>();
            var bitmaps = new ConcurrentDictionary<char, Bitmap>();
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i].Char;
                if (ch == '\0')
                    ch = ' ';

                if (glyphs.ContainsKey(ch))
                {
                    textL.Add(text[i]);
                    continue;
                }
                else
                {
                    FontGlyph glyph = font.RenderGlyph(ch, scale);
                    if (glyph == null)
                    {
                        glyph = font.RenderGlyph(' ', scale);
                        textL.Add(new CPChar(' ', text[i].Color));
                    }
                    else
                        textL.Add(text[i]);

                    glyph.xAdvance += xOffset;
                    glyphs[ch] = glyph;
                    bitmaps[ch] = ConvertToBitmap(glyph.Image);        
                }
            }

            int ascent, descent, lineGap;
            font.GetFontVMetrics(out ascent, out descent, out lineGap);
            int baseLine = height - (int)(ascent * scale);


            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            var positions = new Point[text.Length];

            int loopX = 0;
            for (int i = 0; i < textL.Count; i++)
            {
                CPChar ch = textL[i];
                char chh = ch.Char;
                if (chh == '\0')
                    chh = ' ';
                FontGlyph glyph = glyphs[chh];

                char next = i < textL.Count - 1 ? (text[i + 1].Char == '\0' ? ' ' : text[i + 1].Char) : '\0';

                var kerning = font.GetKerning(ch.Char, next, scale);

                int y0 = height - baseLine + glyph.yOfs;
                int y1 = y0 + glyph.Image.Height;

                int x0 = loopX + glyph.xOfs - kerning;
                int x1 = x0 + glyph.Image.Width;
                loopX += glyph.xAdvance;

                positions[i] = new Point(x0, y0);

                x1 = Math.Max(loopX, x1);

                minX = Math.Min(minX, x0);
                maxX = Math.Max(maxX, x1);

                minY = Math.Min(minY, y0);
                maxY = Math.Max(maxY, y1);
            }

            int realWidth = (maxX - minX) + 1;
            int realHeight = (maxY - minY) + 1;

            for (int i = 0; i < textL.Count; i++)
            {
                positions[i].x -= minX;
                positions[i].y -= minY;
            }

            DirectBitmap tempBmp = new DirectBitmap(realWidth, realHeight);
            {
                // draw the baseline height in blue color
                var ly = height - (baseLine + minY);
                //g.DrawLine(new Pen(Color.Blue), 0, ly, realWidth - 1, ly);

                // now draw each character
                loopX = 0;

                for (int i = 0; i < textL.Count; i++)
                {
                    try
                    {
                        CPChar ch = textL[i];
                        char chh = ch.Char;
                        if (chh == '\0')
                            chh = ' ';
                        FontGlyph glyph = glyphs[chh];
                        Bitmap bmp = bitmaps[chh];
                        Point pos = positions[i];
                        lock (gbgbDrawLock)
                            tempBmp.Draw(glyph.Image, (int)pos.x, (int)pos.y, colorBase[textL[i].Color & 0b_0000_1111],
                                            colorBase[(textL[i].Color & 0b_1111_0000) >> 4]);
                    }
                    catch { return; }
                }
            }

            lock (gbbDrawLock)
                dibm.DrawPlus(tempBmp, x, y);//Draw(dibm, tempBmp, x, y, color);
        }
    
    }

    public enum RenderType
    {
        UpToDown,
        DownToUp,
    }
}
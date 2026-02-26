using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Fonts;

namespace AccommodationSystem.Services
{
    /// <summary>
    /// PdfSharp 1.5x がWindows日本語フォント（TTC形式）を
    /// 直接解析できない問題を解消するカスタムフォントリゾルバー。
    /// TTCファイルから対象フォントを抽出してPdfSharpに渡す。
    /// </summary>
    public class JapaneseFontResolver : IFontResolver
    {
        private static readonly string FontsFolder =
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        // フォントファミリー名 → (TTCファイル名, コレクション内インデックス)
        private static readonly Dictionary<string, (string File, int Index)> FontMap =
            new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
            {
                { "MS Gothic",    ("msgothic.ttc", 0) },
                { "MS PGothic",   ("msgothic.ttc", 1) },
                { "MS UI Gothic", ("msgothic.ttc", 2) },
            };

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            if (FontMap.ContainsKey(familyName))
                return new FontResolverInfo(familyName);
            return null; // その他フォントはPdfSharpデフォルト処理に委譲
        }

        public byte[] GetFont(string faceName)
        {
            if (!FontMap.TryGetValue(faceName, out var info)) return null;

            string path = Path.Combine(FontsFolder, info.File);
            if (!File.Exists(path)) return null;

            byte[] data = File.ReadAllBytes(path);
            return IsTtc(data) ? ExtractFromTtc(data, info.Index) : data;
        }

        // ─── TTCかどうか判定 ─────────────────────────────
        private static bool IsTtc(byte[] d) =>
            d.Length >= 4 &&
            d[0] == 0x74 && d[1] == 0x74 && d[2] == 0x63 && d[3] == 0x66; // "ttcf"

        // ─── TTCから単一フォント(TTF)を抽出 ──────────────
        private static byte[] ExtractFromTtc(byte[] src, int fontIndex)
        {
            uint numFonts = RU32(src, 8);
            if (fontIndex >= (int)numFonts) fontIndex = 0;

            uint sfntOff   = RU32(src, 12 + fontIndex * 4);
            uint numTables = RU16(src, sfntOff + 4);

            // テーブルレコードを読み取る
            var tables = new (string Tag, uint Checksum, uint Offset, uint Length)[numTables];
            for (uint i = 0; i < numTables; i++)
            {
                uint r = sfntOff + 12 + i * 16;
                tables[i] = (
                    Tag:      new string(new[] { (char)src[r], (char)src[r+1], (char)src[r+2], (char)src[r+3] }),
                    Checksum: RU32(src, r + 4),
                    Offset:   RU32(src, r + 8),
                    Length:   RU32(src, r + 12)
                );
            }

            // 出力バッファのオフセットを計算（4バイトアライン）
            uint writePos = (uint)(12 + numTables * 16);
            var  newOffsets = new uint[numTables];
            for (int i = 0; i < numTables; i++)
            {
                newOffsets[i] = writePos;
                writePos += tables[i].Length;
                if (writePos % 4 != 0) writePos += 4 - (writePos % 4);
            }

            var dst = new byte[writePos];

            // sfntヘッダー（12バイト）をそのままコピー
            Array.Copy(src, sfntOff, dst, 0, 12);

            // テーブルレコードを新オフセットで書き込む
            for (int i = 0; i < numTables; i++)
            {
                int p = 12 + i * 16;
                dst[p]   = (byte)tables[i].Tag[0]; dst[p+1] = (byte)tables[i].Tag[1];
                dst[p+2] = (byte)tables[i].Tag[2]; dst[p+3] = (byte)tables[i].Tag[3];
                WU32(dst, p +  4, tables[i].Checksum);
                WU32(dst, p +  8, newOffsets[i]);
                WU32(dst, p + 12, tables[i].Length);
            }

            // テーブルデータをコピー
            for (int i = 0; i < numTables; i++)
                Array.Copy(src, tables[i].Offset, dst, newOffsets[i], tables[i].Length);

            return dst;
        }

        // ─── バイト列読み書きヘルパー（ビッグエンディアン）──
        private static uint RU32(byte[] d, uint o) => RU32(d, (int)o);
        private static uint RU32(byte[] d, int  o) =>
            ((uint)d[o] << 24) | ((uint)d[o+1] << 16) | ((uint)d[o+2] << 8) | d[o+3];

        private static uint RU16(byte[] d, uint o) => RU16(d, (int)o);
        private static uint RU16(byte[] d, int  o) => ((uint)d[o] << 8) | d[o+1];

        private static void WU32(byte[] d, int o, uint v)
        {
            d[o]   = (byte)(v >> 24); d[o+1] = (byte)(v >> 16);
            d[o+2] = (byte)(v >>  8); d[o+3] = (byte) v;
        }
    }
}

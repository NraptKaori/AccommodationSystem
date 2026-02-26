using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using AccommodationSystem.Models;

namespace AccommodationSystem.Services
{
    public static class ExcelService
    {
        // 列番号 → 税率の対応
        // Col 2: 一般客 2万円未満/5万円未満  (税率 300)
        // Col 3: 一般客 2万円以上/10万円未満 (税率 400)
        // Col 4: 一般客 5万円以上/10万円未満 (税率 1,000)
        // Col 5: 一般客 10万円以上           (税率 2,500)
        // Col 6: 各種大会 2万円未満          (税率 100)
        // Col 7: 各種大会 2万円以上          (税率 200)
        // Col 8: 課税対象外 修学旅行等       (税率 500)
        private static readonly int[] ColRates = { 0, 0, 300, 400, 1000, 2500, 100, 200, 500 };

        private static int GetCategoryColumn(decimal taxRatePerPersonPerNight)
        {
            int rate = (int)taxRatePerPersonPerNight;
            for (int c = 2; c <= 8; c++)
                if (ColRates[c] == rate) return c;
            return 2; // デフォルト: 一般客 2万円未満
        }

        public static byte[] GenerateMonthlySummary(
            int year, int month,
            List<Reservation> reservations,
            SystemSettings settings)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add(year + "年" + month.ToString("D2") + "月集計");
                int daysInMonth = DateTime.DaysInMonth(year, month);
                int categoryCol = GetCategoryColumn(settings.TaxRatePerPersonPerNight);

                // --- 日別集計データを構築 ---
                // dailyPersons[day][col]: その日・その区分の人数（人泊）
                var dailyPersons = new int[daysInMonth + 1, 9];
                var dailyTax = new decimal[daysInMonth + 1];
                var dailyTotalPersons = new int[daysInMonth + 1];
                var colPersonTotals = new int[9]; // 区分ごとの人泊数合計

                foreach (var r in reservations)
                {
                    if (!r.IsPaid) continue;
                    // 1泊あたりの税額
                    decimal taxPerNight = r.NumNights > 0
                        ? r.AccommodationTax / r.NumNights
                        : 0;

                    // 各宿泊日ごとにデータを分散（チェックイン日〜最終泊）
                    for (int n = 0; n < r.NumNights; n++)
                    {
                        var nightDate = r.CheckinDate.AddDays(n);
                        // 当月・当年のみ対象
                        if (nightDate.Year != year || nightDate.Month != month) continue;

                        int d = nightDate.Day;
                        dailyPersons[d, categoryCol]  += r.NumPersons;
                        dailyTax[d]                   += taxPerNight;
                        dailyTotalPersons[d]          += r.NumPersons;
                        colPersonTotals[categoryCol]  += r.NumPersons;
                    }
                }

                // 合計はdaily配列から集計（月をまたぐ予約にも正確に対応）
                decimal grandTotalTax = 0;
                int grandTotalPersons = 0;
                for (int d = 1; d <= daysInMonth; d++)
                {
                    grandTotalTax     += dailyTax[d];
                    grandTotalPersons += dailyTotalPersons[d];
                }

                // =========================================
                // ヘッダー部（行 1〜5）
                // =========================================
                BuildHeader(ws, year, month, settings);

                // =========================================
                // 列ヘッダー部（行 7〜10）
                // =========================================
                int headerStart = 7;
                BuildColumnHeaders(ws, headerStart);

                // =========================================
                // 日別データ行（行 11〜41）
                // =========================================
                int dataStart = headerStart + 4; // 行 11
                for (int d = 1; d <= daysInMonth; d++)
                {
                    int row = dataStart + d - 1;
                    ws.Cell(row, 1).Value = d;
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    for (int c = 2; c <= 8; c++)
                    {
                        if (dailyPersons[d, c] > 0)
                        {
                            ws.Cell(row, c).Value = dailyPersons[d, c];
                            ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                    }

                    if (dailyTax[d] > 0)
                    {
                        ws.Cell(row, 9).Value = (double)dailyTax[d];
                        ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
                    }

                    if (dailyTotalPersons[d] > 0)
                    {
                        ws.Cell(row, 10).Value = dailyTotalPersons[d];
                        ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    // 行の高さ
                    ws.Row(row).Height = 16;
                }

                // =========================================
                // 合計行
                // =========================================
                int totalRow = dataStart + daysInMonth;
                ws.Cell(totalRow, 1).Value = "合計";
                ws.Cell(totalRow, 1).Style.Font.Bold = true;
                ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var totalBg = XLColor.FromHtml("#FFF3CD");
                for (int c = 2; c <= 8; c++)
                {
                    var cell = ws.Cell(totalRow, c);
                    if (colPersonTotals[c] > 0)
                    {
                        cell.Value = colPersonTotals[c];
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = totalBg;
                }

                ws.Cell(totalRow, 9).Value = (double)grandTotalTax;
                ws.Cell(totalRow, 9).Style.NumberFormat.Format = "#,##0";
                ws.Cell(totalRow, 9).Style.Font.Bold = true;
                ws.Cell(totalRow, 9).Style.Fill.BackgroundColor = totalBg;

                ws.Cell(totalRow, 10).Value = grandTotalPersons;
                ws.Cell(totalRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(totalRow, 10).Style.Font.Bold = true;
                ws.Cell(totalRow, 10).Style.Fill.BackgroundColor = totalBg;
                ws.Row(totalRow).Height = 18;

                // =========================================
                // 罫線・列幅
                // =========================================
                ApplyTableStyles(ws, headerStart, dataStart, daysInMonth, totalRow);

                ws.Column(1).Width = 8;
                for (int c = 2; c <= 8; c++) ws.Column(c).Width = 13;
                ws.Column(9).Width = 12;
                ws.Column(10).Width = 10;

                // 印刷設定
                ws.PageSetup.PaperSize = XLPaperSize.A3Paper;
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.FitToPages(1, 1);

                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        private static void BuildHeader(IXLWorksheet ws, int year, int month, SystemSettings settings)
        {
            // 行1: タイトル「【宿泊税月計表】」
            ws.Cell(1, 1).Value = "【宿泊税月計表】";
            ws.Range(1, 1, 1, 10).Merge();
            var title = ws.Cell(1, 1);
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 18;
            title.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A6632");
            title.Style.Font.FontColor = XLColor.White;
            title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(1).Height = 30;

            // 行2: 作成日時（右寄せ）
            ws.Cell(2, 7).Value = "作成日時：" + DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            ws.Range(2, 7, 2, 10).Merge();
            ws.Cell(2, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(2, 7).Style.Font.FontSize = 10;

            // 行3: 対象月
            ws.Cell(3, 1).Value = "対象月：" + year + "年" + month.ToString("D2") + "月";
            ws.Range(3, 1, 3, 10).Merge();
            var tgtMonth = ws.Cell(3, 1);
            tgtMonth.Style.Font.Bold = true;
            tgtMonth.Style.Font.FontSize = 14;
            tgtMonth.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(3).Height = 24;

            // 行4: 特別徴収義務者 / 指定番号
            ws.Cell(4, 1).Value = "特別徴収義務者：";
            ws.Cell(4, 1).Style.Font.Bold = true;
            ws.Cell(4, 2).Value = settings.BusinessInfo;
            ws.Range(4, 2, 4, 5).Merge();

            ws.Cell(4, 7).Value = "指定番号：";
            ws.Cell(4, 7).Style.Font.Bold = true;
            ws.Cell(4, 8).Value = settings.TaxNumber;
            ws.Range(4, 8, 4, 10).Merge();

            // 行5: 宿泊施設名
            ws.Cell(5, 1).Value = "宿泊施設名：";
            ws.Cell(5, 1).Style.Font.Bold = true;
            ws.Cell(5, 2).Value = settings.PropertyName;
            ws.Range(5, 2, 5, 10).Merge();

            // 行4・5に薄い背景
            var infoBg = XLColor.FromHtml("#F0F8F0");
            ws.Range(4, 1, 5, 10).Style.Fill.BackgroundColor = infoBg;
            ws.Range(4, 1, 5, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // 行6: 空行（区切り）
            ws.Row(6).Height = 6;
        }

        private static void BuildColumnHeaders(IXLWorksheet ws, int startRow)
        {
            var bg1 = XLColor.FromHtml("#C6EFCE"); // 濃い緑系
            var bg2 = XLColor.FromHtml("#E2EFDA"); // 薄い緑系

            // --- 行 startRow（1段目）---
            // "区分" → 4行分をマージ
            ws.Cell(startRow, 1).Value = "区分";
            ws.Range(startRow, 1, startRow + 3, 1).Merge();
            ApplyHeaderStyle(ws.Cell(startRow, 1), bg1, true, 11);
            ws.Cell(startRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // "課税対象" → 列2〜8をマージ
            ws.Cell(startRow, 2).Value = "課税対象";
            ws.Range(startRow, 2, startRow, 8).Merge();
            ApplyHeaderStyle(ws.Cell(startRow, 2), bg1, true, 11);

            // "合計" → 4行分をマージ
            ws.Cell(startRow, 9).Value = "合計";
            ws.Range(startRow, 9, startRow + 3, 9).Merge();
            ApplyHeaderStyle(ws.Cell(startRow, 9), bg1, true, 11);
            ws.Cell(startRow, 9).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // "合計人数" → 4行分をマージ
            ws.Cell(startRow, 10).Value = "合計\n人数";
            ws.Range(startRow, 10, startRow + 3, 10).Merge();
            ApplyHeaderStyle(ws.Cell(startRow, 10), bg1, true, 11);
            ws.Cell(startRow, 10).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(startRow, 10).Style.Alignment.WrapText = true;

            // --- 行 startRow+1（2段目）---
            // "一般客" → 列2〜5マージ
            ws.Cell(startRow + 1, 2).Value = "一般客";
            ws.Range(startRow + 1, 2, startRow + 1, 5).Merge();
            ApplyHeaderStyle(ws.Cell(startRow + 1, 2), bg2, true, 10);

            // "各種大会" → 列6〜7マージ
            ws.Cell(startRow + 1, 6).Value = "各種大会";
            ws.Range(startRow + 1, 6, startRow + 1, 7).Merge();
            ApplyHeaderStyle(ws.Cell(startRow + 1, 6), bg2, true, 10);

            // "課税対象外" → 列8
            ws.Cell(startRow + 1, 8).Value = "課税対象外";
            ApplyHeaderStyle(ws.Cell(startRow + 1, 8), bg2, true, 10);

            // --- 行 startRow+2（3段目）: 宿泊料金区分 ---
            var priceLabels = new[]
            {
                "2万円未満\n/5万円未満",
                "2万円以上\n/10万円未満",
                "5万円以上\n/10万円未満",
                "10万円以上",
                "2万円未満\n/5万円未満",
                "2万円以上\n/10万円未満",
                "修学旅行\n/その他\n/学校行事等",
            };
            for (int i = 0; i < priceLabels.Length; i++)
            {
                var cell = ws.Cell(startRow + 2, 2 + i);
                cell.Value = priceLabels[i];
                ApplyHeaderStyle(cell, bg2, false, 9);
                cell.Style.Alignment.WrapText = true;
            }

            // --- 行 startRow+3（4段目）: "日" ＋ 税率 ---
            ws.Cell(startRow + 3, 1).Value = "日";
            ApplyHeaderStyle(ws.Cell(startRow + 3, 1), bg1, true, 10);

            var rateLabels = new[]
            {
                "税率\n300",
                "税率\n400",
                "税率\n1,000",
                "税率\n2,500",
                "税率\n100",
                "税率\n200",
                "税率\n500",
            };
            for (int i = 0; i < rateLabels.Length; i++)
            {
                var cell = ws.Cell(startRow + 3, 2 + i);
                cell.Value = rateLabels[i];
                ApplyHeaderStyle(cell, bg1, true, 9);
                cell.Style.Alignment.WrapText = true;
            }

            // 行の高さ
            ws.Row(startRow).Height = 16;
            ws.Row(startRow + 1).Height = 16;
            ws.Row(startRow + 2).Height = 36;
            ws.Row(startRow + 3).Height = 28;
        }

        private static void ApplyHeaderStyle(IXLCell cell, XLColor bg, bool bold, int fontSize)
        {
            cell.Style.Fill.BackgroundColor = bg;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.FontSize = fontSize;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        private static void ApplyTableStyles(
            IXLWorksheet ws, int headerStart, int dataStart, int daysInMonth, int totalRow)
        {
            // 表全体に細い罫線
            var tableRange = ws.Range(headerStart, 1, totalRow, 10);
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // ヘッダー外枠を太く
            ws.Range(headerStart, 1, headerStart + 3, 10)
              .Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // 合計行外枠を太く
            ws.Range(totalRow, 1, totalRow, 10)
              .Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // 5日ごとに水平区切り線を少し太くする
            for (int d = 5; d <= daysInMonth; d += 5)
            {
                int row = dataStart + d - 1;
                ws.Range(row, 1, row, 10)
                  .Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            }

            // 隔行の薄い背景色（視認性向上）
            for (int d = 1; d <= daysInMonth; d += 2)
            {
                int row = dataStart + d - 1;
                ws.Range(row, 1, row, 10)
                  .Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FFF9");
            }
        }
    }
}

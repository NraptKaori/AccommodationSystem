using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using AccommodationSystem.Models;

namespace AccommodationSystem.Services
{
    public static class ExcelService
    {
        public static byte[] GenerateMonthlySummary(int year, int month, List<Reservation> reservations)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add(year + "年" + month + "月集計");

                ws.Cell(1, 1).Value = "宿泊税月次集計 - " + year + "年" + month.ToString("D2") + "月";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Range(1, 1, 1, 7).Merge();

                int totalBookings = reservations.Count;
                int totalPersons = 0, totalNights = 0, paidCount = 0, unpaidCount = 0;
                decimal totalTax = 0;

                foreach (var r in reservations)
                {
                    totalPersons += r.NumPersons;
                    totalNights += r.NumNights;
                    if (r.IsPaid) { totalTax += r.AccommodationTax; paidCount++; }
                    else unpaidCount++;
                }

                int row = 3;
                Action<string, string> addSummary = (label, value) =>
                {
                    ws.Cell(row, 1).Value = label;
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 2).Value = value;
                    row++;
                };

                addSummary("対象月", year + "年" + month.ToString("D2") + "月");
                addSummary("総宿泊件数", totalBookings + " 件");
                addSummary("総宿泊人数", totalPersons + " 人");
                addSummary("総宿泊泊数", totalNights + " 泊");
                addSummary("総宿泊税額（支払済）", "¥ " + totalTax.ToString("N0"));
                addSummary("決済済件数", paidCount + " 件");
                addSummary("未決済件数", unpaidCount + " 件");

                row += 2;
                var headers = new[] { "予約番号", "宿泊者名", "チェックイン", "チェックアウト", "人数", "泊数", "税額", "決済状態" };
                for (int c = 0; c < headers.Length; c++)
                {
                    ws.Cell(row, c + 1).Value = headers[c];
                    ws.Cell(row, c + 1).Style.Font.Bold = true;
                    ws.Cell(row, c + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
                }
                row++;

                foreach (var r in reservations)
                {
                    ws.Cell(row, 1).Value = r.ReservationNumber;
                    ws.Cell(row, 2).Value = r.GuestName;
                    ws.Cell(row, 3).Value = r.CheckinDate.ToString("yyyy/MM/dd");
                    ws.Cell(row, 4).Value = r.CheckoutDate.ToString("yyyy/MM/dd");
                    ws.Cell(row, 5).Value = r.NumPersons;
                    ws.Cell(row, 6).Value = r.NumNights;
                    ws.Cell(row, 7).Value = (double)r.AccommodationTax;
                    ws.Cell(row, 7).Style.NumberFormat.Format = "¥#,##0";
                    ws.Cell(row, 8).Value = r.PaymentStatusDisplay;
                    row++;
                }

                ws.Columns().AdjustToContents();

                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}

using System;
using System.IO;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using AccommodationSystem.Models;
using AccommodationSystem.Data;

namespace AccommodationSystem.Services
{
    public static class PdfService
    {
        public static byte[] GenerateReceipt(Reservation reservation, string receiptNumber)
        {
            var settings = DatabaseService.GetSettings();

            // TTCフォントをPdfSharpで使えるようカスタムリゾルバーを登録
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new JapaneseFontResolver();

            var document = new PdfDocument();
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            var gfx = XGraphics.FromPdfPage(page);

            var fontBold = new XFont("MS Gothic", 16, XFontStyle.Bold);
            var fontNormal = new XFont("MS Gothic", 10, XFontStyle.Regular);
            var fontSmall = new XFont("MS Gothic", 9, XFontStyle.Regular);
            var fontLarge = new XFont("MS Gothic", 20, XFontStyle.Bold);
            var fontMid = new XFont("MS Gothic", 18, XFontStyle.Bold);

            double y = 60;
            double left = 60;
            double right = page.Width - 60;

            gfx.DrawString("領　収　書", fontLarge, XBrushes.Black,
                new XRect(0, y, page.Width, 40), XStringFormats.Center);
            y += 60;

            gfx.DrawString("発行日: " + DateTime.Now.ToString("yyyy年MM月dd日"), fontNormal, XBrushes.Black,
                new XRect(0, y, right, 20), XStringFormats.TopRight);
            y += 20;
            gfx.DrawString("領収書番号: " + receiptNumber, fontNormal, XBrushes.Black,
                new XRect(0, y, right, 20), XStringFormats.TopRight);
            y += 40;

            gfx.DrawString(reservation.GuestName + "　様", fontBold, XBrushes.Black, left, y);
            y += 35;

            gfx.DrawRectangle(XPens.Black, XBrushes.LightGray, left, y, right - left, 50);
            gfx.DrawString("¥ " + reservation.AccommodationTax.ToString("N0") + " 　（税込）", fontMid,
                XBrushes.Black, new XRect(left, y, right - left, 50), XStringFormats.Center);
            y += 70;

            gfx.DrawString("但　宿泊税として　上記正に領収いたしました", fontNormal, XBrushes.Black, left, y);
            y += 40;

            gfx.DrawLine(XPens.Gray, left, y, right, y);
            y += 20;

            Action<string, string> drawRow = (label, value) =>
            {
                gfx.DrawString(label, fontNormal, XBrushes.DarkGray, left + 10, y);
                gfx.DrawString(value, fontNormal, XBrushes.Black, left + 180, y);
                y += 22;
            };

            gfx.DrawString("【宿泊明細】", fontNormal, XBrushes.Black, left, y); y += 25;
            drawRow("施設名", settings.PropertyName);
            drawRow("宿泊者名", reservation.GuestName);
            drawRow("チェックイン日", reservation.CheckinDate.ToString("yyyy年MM月dd日"));
            drawRow("チェックアウト日", reservation.CheckoutDate.ToString("yyyy年MM月dd日"));
            drawRow("宿泊人数", reservation.NumPersons + "名");
            drawRow("宿泊泊数", reservation.NumNights + "泊");
            drawRow("単価", "1人1泊 " + settings.TaxRatePerPersonPerNight.ToString("N0") + "円");
            drawRow("宿泊税合計", "¥ " + reservation.AccommodationTax.ToString("N0"));
            drawRow("決済方法", "クレジットカード");

            y += 20;
            gfx.DrawLine(XPens.Gray, left, y, right, y);
            y += 20;

            gfx.DrawString(settings.PropertyName, fontBold, XBrushes.Black, left, y); y += 22;
            gfx.DrawString(settings.PropertyAddress, fontNormal, XBrushes.Black, left, y); y += 22;
            if (!string.IsNullOrEmpty(settings.TaxNumber))
            {
                gfx.DrawString("登録番号: " + settings.TaxNumber, fontSmall, XBrushes.Black, left, y);
                y += 20;
            }
            if (!string.IsNullOrEmpty(settings.BusinessInfo))
                gfx.DrawString(settings.BusinessInfo, fontSmall, XBrushes.Black, left, y);

            using (var ms = new MemoryStream())
            {
                document.Save(ms, false);
                return ms.ToArray();
            }
        }
    }
}

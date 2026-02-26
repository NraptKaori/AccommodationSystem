using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AccommodationSystem.Models;
using AccommodationSystem.Data;

namespace AccommodationSystem.Services
{
    public static class CsvImportService
    {
        /// <summary>
        /// CSVファイルをインポートして件数を返す
        /// </summary>
        public static (int imported, int skipped, List<string> errors) Import(string filePath)
        {
            int imported = 0, skipped = 0;
            var errors = new List<string>();
            var settings = DatabaseService.GetSettings();

            
            var lines = File.ReadAllLines(filePath, DetectEncoding(filePath));

            if (lines.Length < 2)
            {
                errors.Add("データが存在しません。");
                return (0, 0, errors);
            }

            // ヘッダー解析
            var headers = ParseCsvLine(lines[0]);
            int idxResNum = FindHeader(headers, "Reservation number", "予約番号");
            int idxArrival = FindHeader(headers, "Arrival", "チェックイン日");
            int idxDeparture = FindHeader(headers, "Departure", "チェックアウト日");
            int idxGuest = FindHeader(headers, "Guest name", "宿泊者名");
            int idxPersons = FindHeader(headers, "Persons", "宿泊人数");
            int idxNights = FindHeader(headers, "Room nights", "宿泊泊数");

            if (idxResNum < 0 || idxArrival < 0 || idxDeparture < 0 || idxGuest < 0 || idxPersons < 0 || idxNights < 0)
            {
                errors.Add("必須列が見つかりません。CSVのヘッダーを確認してください。");
                return (0, 0, errors);
            }

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                try
                {
                    var cols = ParseCsvLine(lines[i]);
                    var r = new Reservation
                    {
                        ReservationNumber = cols[idxResNum].Trim(),
                        GuestName = cols[idxGuest].Trim(),
                        CheckinDate = DateTime.Parse(cols[idxArrival].Trim()),
                        CheckoutDate = DateTime.Parse(cols[idxDeparture].Trim()),
                        NumPersons = int.Parse(cols[idxPersons].Trim()),
                        NumNights = int.Parse(cols[idxNights].Trim()),
                    };
                    r.AccommodationTax = r.NumPersons * r.NumNights * settings.TaxRatePerPersonPerNight;

                    if (DatabaseService.ReservationExists(r.ReservationNumber))
                    {
                        skipped++;
                        continue;
                    }

                    DatabaseService.UpsertReservation(r);
                    imported++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"行{i + 1}: {ex.Message}");
                }
            }
            return (imported, skipped, errors);
        }

        /// <summary>
        /// BOMまたはUTF-8デコード可否でエンコーディングを判定し、
        /// 失敗時はCP932（Shift-JIS）を返す
        /// </summary>
        private static Encoding DetectEncoding(string filePath)
        {
            byte[] bom = new byte[4];
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                fs.Read(bom, 0, 4);

            // BOM付きUTF-8
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return new UTF8Encoding(true);
            // BOM付きUTF-16 LE
            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            // BOM付きUTF-16 BE
            if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            // BOMなし：UTF-8として読めるか検証
            try
            {
                var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
                utf8.GetString(File.ReadAllBytes(filePath));
                return utf8;
            }
            catch
            {
                // UTF-8で読めない場合はCP932（Shift-JIS）
                return Encoding.GetEncoding(932);
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            foreach (var ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; }
                else if (ch == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
                else current.Append(ch);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private static int FindHeader(string[] headers, params string[] candidates)
        {
            for (int i = 0; i < headers.Length; i++)
                foreach (var c in candidates)
                    if (headers[i].Trim().Equals(c, StringComparison.OrdinalIgnoreCase))
                        return i;
            return -1;
        }
    }
}

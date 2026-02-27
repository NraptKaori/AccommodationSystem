using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using AccommodationSystem.Models;
using AccommodationSystem.Services;

namespace AccommodationSystem.Data
{
    public static class DatabaseService
    {
        private static readonly string DbFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AccommodationSystem");
        public static readonly string DbPath = Path.Combine(DbFolder, "accommodation.db");
        private static string ConnectionString { get { return "Data Source=" + DbPath + ";Version=3;"; } }

        // 暗号化が必要な設定キー
        private static readonly HashSet<string> EncryptedKeys = new HashSet<string>
        {
            "stripe_api_key", "smtp_password"
        };

        private static string DictGet(Dictionary<string, string> d, string key, string def)
        {
            string val;
            return d.TryGetValue(key, out val) ? val : def;
        }

        public static void Initialize()
        {
            if (!Directory.Exists(DbFolder))
                Directory.CreateDirectory(DbFolder);

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS reservations (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        reservation_number TEXT NOT NULL UNIQUE,
                        guest_name TEXT NOT NULL,
                        checkin_date DATE NOT NULL,
                        checkout_date DATE NOT NULL,
                        num_persons INTEGER NOT NULL,
                        num_nights INTEGER NOT NULL,
                        accommodation_tax DECIMAL NOT NULL,
                        total_fee DECIMAL NOT NULL DEFAULT 0,
                        payment_status TEXT DEFAULT 'unpaid',
                        payment_date DATETIME NULL,
                        stripe_payment_id TEXT NULL,
                        created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE TABLE IF NOT EXISTS system_settings (
                        key TEXT PRIMARY KEY,
                        value TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS receipts (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        reservation_id INTEGER NOT NULL,
                        receipt_number TEXT NOT NULL,
                        issued_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        email_hash TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS audit_log (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        event_type TEXT NOT NULL,
                        description TEXT,
                        created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE TABLE IF NOT EXISTS login_attempts (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        attempt_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        success INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE TABLE IF NOT EXISTS tax_master (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        municipality TEXT NOT NULL,
                        from_amount INTEGER NOT NULL DEFAULT 0,
                        to_amount INTEGER,
                        tax_amount INTEGER NOT NULL DEFAULT 0,
                        sort_order INTEGER NOT NULL DEFAULT 0
                    );";
                cmd.ExecuteNonQuery();
                InsertDefaultSettings(conn);
                EnsureDefaultTaxRates(conn);
            }

            // スキーママイグレーション：既存DBに不足カラムを追加
            RunMigrations();
        }

        private static void RunMigrations()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                // receipts.email_hash が存在しない古いDBへの対応
                var checkReceipts = conn.CreateCommand();
                checkReceipts.CommandText = "PRAGMA table_info(receipts)";
                bool hasEmailHash = false;
                using (var reader = checkReceipts.ExecuteReader())
                    while (reader.Read())
                        if (reader.GetString(1) == "email_hash") { hasEmailHash = true; break; }

                if (!hasEmailHash)
                {
                    var alter = conn.CreateCommand();
                    alter.CommandText = "ALTER TABLE receipts ADD COLUMN email_hash TEXT NOT NULL DEFAULT ''";
                    alter.ExecuteNonQuery();
                }

                // reservations.total_fee が存在しない古いDBへの対応
                var checkRes = conn.CreateCommand();
                checkRes.CommandText = "PRAGMA table_info(reservations)";
                bool hasTotalFee = false;
                using (var reader = checkRes.ExecuteReader())
                    while (reader.Read())
                        if (reader.GetString(1) == "total_fee") { hasTotalFee = true; break; }

                if (!hasTotalFee)
                {
                    var alter = conn.CreateCommand();
                    alter.CommandText = "ALTER TABLE reservations ADD COLUMN total_fee DECIMAL NOT NULL DEFAULT 0";
                    alter.ExecuteNonQuery();
                }

                // receipts に旧 email カラム（NOT NULL）が残っている場合テーブルを再構築して除去
                var checkEmailCol = conn.CreateCommand();
                checkEmailCol.CommandText = "PRAGMA table_info(receipts)";
                bool hasOldEmailCol = false;
                using (var reader = checkEmailCol.ExecuteReader())
                    while (reader.Read())
                        if (reader.GetString(1) == "email") { hasOldEmailCol = true; break; }

                if (hasOldEmailCol)
                {
                    var c1 = conn.CreateCommand();
                    c1.CommandText = @"CREATE TABLE IF NOT EXISTS receipts_new (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        reservation_id INTEGER NOT NULL,
                        receipt_number TEXT NOT NULL,
                        issued_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                        email_hash TEXT NOT NULL DEFAULT ''
                    )";
                    c1.ExecuteNonQuery();

                    var c2 = conn.CreateCommand();
                    c2.CommandText = @"INSERT INTO receipts_new (id, reservation_id, receipt_number, issued_at, email_hash)
                        SELECT id, reservation_id, receipt_number, issued_at, COALESCE(email_hash, '') FROM receipts";
                    c2.ExecuteNonQuery();

                    var c3 = conn.CreateCommand();
                    c3.CommandText = "DROP TABLE receipts";
                    c3.ExecuteNonQuery();

                    var c4 = conn.CreateCommand();
                    c4.CommandText = "ALTER TABLE receipts_new RENAME TO receipts";
                    c4.ExecuteNonQuery();
                }
            }
        }

        private static void InsertDefaultSettings(SQLiteConnection conn)
        {
            var defaults = new Dictionary<string, string>
            {
                { "property_name", "宿泊施設名" },
                { "property_address", "住所を設定してください" },
                { "stripe_api_key", EncryptionService.Encrypt("sk_test_XXXXXXXXXXXXXXXX") },
                { "smtp_host", "smtp.gmail.com" },
                { "smtp_port", "587" },
                { "smtp_user", "your@email.com" },
                { "smtp_password", EncryptionService.Encrypt("password") },
                { "tax_rate_per_person_per_night", "300" },
                { "municipality", "札幌市" },
                { "default_room_rate_per_person", "10000" },
                { "admin_password_hash", BCrypt.Net.BCrypt.HashPassword("admin1234") },
                { "business_info", "" },
                { "tax_number", "" },
                { "login_lock_until", "" },
            };

            foreach (var kv in defaults)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO system_settings (key, value) VALUES (@k, @v)";
                cmd.Parameters.AddWithValue("@k", kv.Key);
                cmd.Parameters.AddWithValue("@v", kv.Value);
                cmd.ExecuteNonQuery();
            }
        }

        // ---- Tax Master ----

        // デフォルト税率テーブル (市区町村, 下限以上, 上限以下 null=上限なし, 宿泊税/人/泊)
        private static readonly (string Muni, int From, int? To, int Tax)[] DefaultTaxRates =
        {
            // 札幌市（道税100円 + 市税）
            ("札幌市",        0,  19999, 300),
            ("札幌市",    20000,  49999, 400),
            ("札幌市",    50000,   null, 1000),
            // 小樽市（道税100円 + 市税）
            ("小樽市",        0,  19999, 300),
            ("小樽市",    20000,  49999, 400),
            ("小樽市",    50000,   null, 700),
            // ニセコ町（道税100円 + 町税）
            ("ニセコ町",      0,   5000, 200),
            ("ニセコ町",   5001,  19999, 300),
            ("ニセコ町",  20000,  49999, 700),
            ("ニセコ町",  50000,  99999, 1500),
            ("ニセコ町", 100000,   null, 2500),
            // 留寿都村（道税100円 + 村税）
            ("留寿都村",      0,  19999, 200),
            ("留寿都村",  20000,  49999, 400),
            ("留寿都村",  50000,   null, 1000),
            // 赤井川村（道税100円 + 村税）
            ("赤井川村",      0,   7999, 100),
            ("赤井川村",   8000,  19999, 300),
            ("赤井川村",  20000,  49999, 700),
            ("赤井川村",  50000,   null, 1000),
            // 洞爺湖町（道税100円 + 町税）
            ("洞爺湖町",      0,  19999, 300),
            ("洞爺湖町",  20000,  49999, 700),
            ("洞爺湖町",  50000,   null, 1500),
            // 函館市（道税100円 + 市税）
            ("函館市",        0,  19999, 200),
            ("函館市",    20000,  49999, 400),
            ("函館市",    50000,  99999, 1000),
            ("函館市",   100000,   null, 2500),
        };

        private static void EnsureDefaultTaxRates(SQLiteConnection conn)
        {
            var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM tax_master";
            if (Convert.ToInt32(countCmd.ExecuteScalar()) > 0) return;
            InsertDefaultTaxRatesCore(conn);
        }

        private static void InsertDefaultTaxRatesCore(SQLiteConnection conn)
        {
            int sort = 1;
            foreach (var (muni, from, to, tax) in DefaultTaxRates)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO tax_master (municipality, from_amount, to_amount, tax_amount, sort_order)
                                    VALUES (@muni, @from, @to, @tax, @sort)";
                cmd.Parameters.AddWithValue("@muni", muni);
                cmd.Parameters.AddWithValue("@from", from);
                cmd.Parameters.AddWithValue("@to", to.HasValue ? (object)to.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@tax", tax);
                cmd.Parameters.AddWithValue("@sort", sort++);
                cmd.ExecuteNonQuery();
            }
        }

        public static List<TaxRate> GetTaxRates()
        {
            var list = new List<TaxRate>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id, municipality, from_amount, to_amount, tax_amount, sort_order FROM tax_master ORDER BY sort_order, id";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(new TaxRate
                        {
                            Id = r.GetInt32(0),
                            Municipality = r.GetString(1),
                            FromAmount = r.GetInt32(2),
                            ToAmount = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                            TaxAmount = r.GetInt32(4),
                            SortOrder = r.GetInt32(5),
                        });
                }
            }
            return list;
        }

        public static List<string> GetMunicipalities()
        {
            var list = new List<string>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT municipality FROM tax_master GROUP BY municipality ORDER BY MIN(id)";
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(r.GetString(0));
            }
            return list;
        }

        public static void SaveTaxRates(List<TaxRate> rates)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    var del = conn.CreateCommand();
                    del.CommandText = "DELETE FROM tax_master";
                    del.ExecuteNonQuery();

                    int sort = 1;
                    foreach (var r in rates)
                    {
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = @"INSERT INTO tax_master (municipality, from_amount, to_amount, tax_amount, sort_order)
                                            VALUES (@muni, @from, @to, @tax, @sort)";
                        cmd.Parameters.AddWithValue("@muni", r.Municipality);
                        cmd.Parameters.AddWithValue("@from", r.FromAmount);
                        cmd.Parameters.AddWithValue("@to", r.ToAmount.HasValue ? (object)r.ToAmount.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@tax", r.TaxAmount);
                        cmd.Parameters.AddWithValue("@sort", sort++);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
        }

        public static void ResetTaxRatesToDefaults()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    var del = conn.CreateCommand();
                    del.CommandText = "DELETE FROM tax_master";
                    del.ExecuteNonQuery();
                    InsertDefaultTaxRatesCore(conn);
                    tx.Commit();
                }
            }
        }

        // ---- Login / Lock ----

        public static bool IsLoginLocked()
        {
            var lockUntilStr = GetRawSetting("login_lock_until");
            if (string.IsNullOrEmpty(lockUntilStr)) return false;
            if (DateTime.TryParse(lockUntilStr, out var lockUntil))
                return DateTime.Now < lockUntil;
            return false;
        }

        public static DateTime GetLockUntil()
        {
            var lockUntilStr = GetRawSetting("login_lock_until");
            if (DateTime.TryParse(lockUntilStr, out var dt)) return dt;
            return DateTime.MinValue;
        }

        public static void RecordLoginAttempt(bool success)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO login_attempts (success) VALUES (@s)";
                cmd.Parameters.AddWithValue("@s", success ? 1 : 0);
                cmd.ExecuteNonQuery();

                if (!success)
                {
                    // 直近10分の失敗数をカウント
                    var countCmd = conn.CreateCommand();
                    countCmd.CommandText = @"SELECT COUNT(*) FROM login_attempts
                        WHERE success=0 AND attempt_at > datetime('now', '-10 minutes')";
                    var failCount = Convert.ToInt32(countCmd.ExecuteScalar());

                    if (failCount >= 5)
                    {
                        // 30分ロック
                        var lockUntil = DateTime.Now.AddMinutes(30);
                        var lockCmd = conn.CreateCommand();
                        lockCmd.CommandText = "INSERT OR REPLACE INTO system_settings (key, value) VALUES ('login_lock_until', @v)";
                        lockCmd.Parameters.AddWithValue("@v", lockUntil.ToString("yyyy-MM-dd HH:mm:ss"));
                        lockCmd.ExecuteNonQuery();
                        Log("security", "Admin login locked for 30 minutes due to 5 failed attempts");
                    }
                }
                else
                {
                    // 成功したらロック解除
                    SaveSetting("login_lock_until", "");
                }
            }
        }

        // ---- Reservations ----

        public static List<Reservation> GetReservations(string nameFilter = null, string statusFilter = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            var list = new List<Reservation>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM reservations WHERE 1=1";
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    cmd.CommandText += " AND LOWER(guest_name) LIKE @name";
                    cmd.Parameters.AddWithValue("@name", "%" + nameFilter.ToLower() + "%");
                }
                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
                {
                    cmd.CommandText += " AND payment_status = @status";
                    cmd.Parameters.AddWithValue("@status", statusFilter);
                }
                if (fromDate.HasValue)
                {
                    cmd.CommandText += " AND checkin_date >= @from";
                    cmd.Parameters.AddWithValue("@from", fromDate.Value.ToString("yyyy-MM-dd"));
                }
                if (toDate.HasValue)
                {
                    cmd.CommandText += " AND checkin_date <= @to";
                    cmd.Parameters.AddWithValue("@to", toDate.Value.ToString("yyyy-MM-dd"));
                }
                cmd.CommandText += " ORDER BY checkin_date DESC";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(MapReservation(reader));
                }
            }
            return list;
        }

        public static List<Reservation> SearchReservations(string query)
        {
            var list = new List<Reservation>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT * FROM reservations
                    WHERE LOWER(reservation_number) = @exact
                    OR LOWER(guest_name) LIKE @partial
                    ORDER BY checkin_date DESC";
                cmd.Parameters.AddWithValue("@exact", query.ToLower());
                cmd.Parameters.AddWithValue("@partial", "%" + query.ToLower() + "%");
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(MapReservation(reader));
                }
            }
            return list;
        }

        public static bool ReservationExists(string reservationNumber)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM reservations WHERE reservation_number = @rn";
                cmd.Parameters.AddWithValue("@rn", reservationNumber);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public static void UpsertReservation(Reservation r)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO reservations
                    (reservation_number, guest_name, checkin_date, checkout_date, num_persons, num_nights, accommodation_tax, total_fee, payment_status, payment_date, stripe_payment_id)
                    VALUES (@rn, @gn, @ci, @co, @np, @nn, @tax, @tf, @ps, @pd, @sp)";
                cmd.Parameters.AddWithValue("@rn", r.ReservationNumber);
                cmd.Parameters.AddWithValue("@gn", r.GuestName);
                cmd.Parameters.AddWithValue("@ci", r.CheckinDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@co", r.CheckoutDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@np", r.NumPersons);
                cmd.Parameters.AddWithValue("@nn", r.NumNights);
                cmd.Parameters.AddWithValue("@tax", r.AccommodationTax);
                cmd.Parameters.AddWithValue("@tf", r.TotalFee);
                cmd.Parameters.AddWithValue("@ps", r.PaymentStatus);
                cmd.Parameters.AddWithValue("@pd", r.PaymentDate.HasValue
                    ? (object)r.PaymentDate.Value.ToString("yyyy-MM-dd HH:mm:ss")
                    : DBNull.Value);
                cmd.Parameters.AddWithValue("@sp", r.StripePaymentId != null
                    ? (object)r.StripePaymentId
                    : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public static void UpdatePaymentStatus(int id, string stripePaymentId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE reservations SET payment_status='paid', payment_date=@pd, stripe_payment_id=@sp WHERE id=@id";
                cmd.Parameters.AddWithValue("@pd", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@sp", stripePaymentId);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            Log("payment", "Payment completed for reservation id=" + id);
        }

        // ---- Settings ----

        private static string GetRawSetting(string key)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM system_settings WHERE key=@k";
                cmd.Parameters.AddWithValue("@k", key);
                var result = cmd.ExecuteScalar();
                return result != null ? result.ToString() : "";
            }
        }

        public static SystemSettings GetSettings()
        {
            var dict = new Dictionary<string, string>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT key, value FROM system_settings";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var key = reader.GetString(0);
                        var value = reader.GetString(1);
                        // 暗号化キーは復号して返す
                        dict[key] = EncryptedKeys.Contains(key) ? EncryptionService.Decrypt(value) : value;
                    }
                }
            }

            return new SystemSettings
            {
                PropertyName = DictGet(dict, "property_name", ""),
                PropertyAddress = DictGet(dict, "property_address", ""),
                StripeApiKey = DictGet(dict, "stripe_api_key", ""),
                SmtpHost = DictGet(dict, "smtp_host", ""),
                SmtpPort = int.Parse(DictGet(dict, "smtp_port", "587")),
                SmtpUser = DictGet(dict, "smtp_user", ""),
                SmtpPassword = DictGet(dict, "smtp_password", ""),
                TaxRatePerPersonPerNight = decimal.Parse(DictGet(dict, "tax_rate_per_person_per_night", "300")),
                Municipality = DictGet(dict, "municipality", "札幌市"),
                AdminPasswordHash = DictGet(dict, "admin_password_hash", ""),
                BusinessInfo = DictGet(dict, "business_info", ""),
                TaxNumber = DictGet(dict, "tax_number", ""),
            };
        }

        public static void SaveSetting(string key, string value)
        {
            // 暗号化が必要なキーは暗号化してから保存
            var storeValue = EncryptedKeys.Contains(key) ? EncryptionService.Encrypt(value) : value;
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO system_settings (key, value) VALUES (@k, @v)";
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", storeValue);
                cmd.ExecuteNonQuery();
            }
        }

        // ---- Receipts ----

        public static string GetNextReceiptNumber(string reservationNumber)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM receipts WHERE reservation_id = (SELECT id FROM reservations WHERE reservation_number=@rn)";
                cmd.Parameters.AddWithValue("@rn", reservationNumber);
                var count = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                return reservationNumber + "-" + count.ToString("D3");
            }
        }

        public static void SaveReceipt(int reservationId, string receiptNumber, string email)
        {
            // メールアドレスはハッシュ化して保存（個人情報を含まないログ設計）
            var emailHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create().ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(email)));

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO receipts (reservation_id, receipt_number, email_hash) VALUES (@rid, @rn, @eh)";
                cmd.Parameters.AddWithValue("@rid", reservationId);
                cmd.Parameters.AddWithValue("@rn", receiptNumber);
                cmd.Parameters.AddWithValue("@eh", emailHash);
                cmd.ExecuteNonQuery();
            }
            Log("receipt", "Receipt " + receiptNumber + " issued");
        }

        public static int GetReceiptCount(int reservationId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM receipts WHERE reservation_id=@id";
                cmd.Parameters.AddWithValue("@id", reservationId);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ---- Data Clear ----

        /// <summary>予約・領収書・監査ログを全削除（設定・税率マスタは保持）</summary>
        public static void ClearAllReservations()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    foreach (var table in new[] { "receipts", "audit_log", "login_attempts", "reservations" })
                    {
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = $"DELETE FROM {table}";
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
        }

        // ---- Backup ----

        public static void BackupDatabase(string destPath)
        {
            File.Copy(DbPath, destPath, overwrite: true);
            Log("backup", "Database backed up to " + Path.GetFileName(destPath));
        }

        // ---- Monthly Summary ----

        public static List<Reservation> GetReservationsForMonth(int year, int month)
        {
            var list = new List<Reservation>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT * FROM reservations
                    WHERE strftime('%Y', checkin_date) = @y AND strftime('%m', checkin_date) = @m
                    ORDER BY checkin_date";
                cmd.Parameters.AddWithValue("@y", year.ToString());
                cmd.Parameters.AddWithValue("@m", month.ToString("D2"));
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(MapReservation(reader));
                }
            }
            return list;
        }

        // ---- Audit Log ----

        public static void Log(string eventType, string description)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO audit_log (event_type, description) VALUES (@et, @d)";
                    cmd.Parameters.AddWithValue("@et", eventType);
                    cmd.Parameters.AddWithValue("@d", description);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private static Reservation MapReservation(SQLiteDataReader r)
        {
            return new Reservation
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                ReservationNumber = r.GetString(r.GetOrdinal("reservation_number")),
                GuestName = r.GetString(r.GetOrdinal("guest_name")),
                CheckinDate = DateTime.Parse(r.GetString(r.GetOrdinal("checkin_date"))),
                CheckoutDate = DateTime.Parse(r.GetString(r.GetOrdinal("checkout_date"))),
                NumPersons = r.GetInt32(r.GetOrdinal("num_persons")),
                NumNights = r.GetInt32(r.GetOrdinal("num_nights")),
                AccommodationTax = r.GetDecimal(r.GetOrdinal("accommodation_tax")),
                TotalFee = r.IsDBNull(r.GetOrdinal("total_fee")) ? 0m : r.GetDecimal(r.GetOrdinal("total_fee")),
                PaymentStatus = r.GetString(r.GetOrdinal("payment_status")),
                PaymentDate = r.IsDBNull(r.GetOrdinal("payment_date"))
                    ? (DateTime?)null
                    : DateTime.Parse(r.GetString(r.GetOrdinal("payment_date"))),
                StripePaymentId = r.IsDBNull(r.GetOrdinal("stripe_payment_id"))
                    ? null
                    : r.GetString(r.GetOrdinal("stripe_payment_id")),
            };
        }
    }
}

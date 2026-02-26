using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AccommodationSystem.Data;
using AccommodationSystem.Models;
using AccommodationSystem.Services;

namespace AccommodationSystem.Views
{
    public partial class AdminWindow : Window
    {
        private string _backupFolder = "";

        public AdminWindow()
        {
            InitializeComponent();
            InitMunicipalityCombo();
            LoadSettings();
            LoadReservations();
            InitYearCombo();
        }

        private void InitMunicipalityCombo()
        {
            foreach (var m in TaxMasterService.Municipalities)
                MunicipalityCombo.Items.Add(new ComboBoxItem { Content = m });
        }

        private void InitYearCombo()
        {
            var now = DateTime.Now;
            for (int y = now.Year - 1; y <= now.Year + 1; y++)
                YearCombo.Items.Add(new ComboBoxItem { Content = y + "年", Tag = y });
            YearCombo.SelectedIndex = 1;
            MonthCombo.SelectedIndex = now.Month - 1;
        }

        // ---- 宿泊者一覧 ----

        private void LoadReservations()
        {
            var name = ListSearchBox.Text.Trim();
            var status = (StatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
            var list = DatabaseService.GetReservations(
                string.IsNullOrEmpty(name) ? null : name,
                status,
                FromDate.SelectedDate,
                ToDate.SelectedDate);

            ReservationGrid.ItemsSource = list;
            UpdateSummary(list);
        }

        private void UpdateSummary(List<Reservation> list)
        {
            int paid = 0, unpaid = 0;
            decimal totalTax = 0;
            foreach (var r in list)
            {
                if (r.IsPaid) { paid++; totalTax += r.AccommodationTax; }
                else unpaid++;
            }
            SummaryTotal.Text = list.Count + " 件";
            SummaryPaid.Text = paid + " 件";
            SummaryUnpaid.Text = unpaid + " 件";
            SummaryTax.Text = "¥" + totalTax.ToString("N0");
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e) => LoadReservations();

        private void ReservationGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 支払済みの行を選択したら領収書再発行を提案
            if (ReservationGrid.SelectedItem is Reservation r && r.IsPaid)
            {
                var result = MessageBox.Show(
                    r.GuestName + " 様の領収書を再発行しますか？",
                    "領収書再発行",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var dlg = new ReceiptEmailWindow(r) { Owner = this };
                    dlg.ShowDialog();
                }
                ReservationGrid.UnselectAll();
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
                Title = "予約CSVファイルを選択"
            };
            if (dlg.ShowDialog() != true) return;

            var (imported, skipped, errors) = CsvImportService.Import(dlg.FileName);
            var msg = "インポート完了\n\n取込成功: " + imported + " 件\nスキップ: " + skipped + " 件";
            if (errors.Count > 0)
            {
                msg += "\n\n--- エラー詳細 ---\n";
                msg += string.Join("\n", errors.GetRange(0, Math.Min(10, errors.Count)));
                if (errors.Count > 10)
                    msg += "\n... 他 " + (errors.Count - 10) + " 件";
            }

            MessageBox.Show(msg, "CSVインポート結果",
                MessageBoxButton.OK,
                errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            DatabaseService.Log("csv_import", "Imported " + imported + " records, skipped " + skipped);
            LoadReservations();
        }

        // ---- 月次集計 ----

        private (int year, int month) GetSelectedYearMonth()
        {
            var yearItem = YearCombo.SelectedItem as ComboBoxItem;
            var monthItem = MonthCombo.SelectedItem as ComboBoxItem;
            int year = yearItem != null
                ? int.Parse(yearItem.Content.ToString().Replace("年", ""))
                : DateTime.Now.Year;
            int month = monthItem != null
                ? int.Parse(monthItem.Content.ToString().Replace("月", ""))
                : DateTime.Now.Month;
            return (year, month);
        }

        private void PreviewMonthlyButton_Click(object sender, RoutedEventArgs e)
        {
            var (year, month) = GetSelectedYearMonth();
            var reservations = DatabaseService.GetReservationsForMonth(year, month);

            int totalPersons = 0, totalNights = 0, paidCount = 0, unpaidCount = 0;
            decimal totalTax = 0;
            foreach (var r in reservations)
            {
                totalPersons += r.NumPersons;
                totalNights += r.NumNights;
                if (r.IsPaid) { totalTax += r.AccommodationTax; paidCount++; }
                else unpaidCount++;
            }

            PreviewTitle.Text = year + "年" + month + "月 集計結果";
            PvTotalBookings.Text = reservations.Count + " 件";
            PvTotalPersons.Text = totalPersons + " 人";
            PvTotalNights.Text = totalNights + " 泊";
            PvTotalTax.Text = "¥" + totalTax.ToString("N0");
            PvPaidCount.Text = paidCount + " 件";
            PvUnpaidCount.Text = unpaidCount + " 件";
            PreviewGrid.Visibility = Visibility.Visible;
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            var (year, month) = GetSelectedYearMonth();
            var reservations = DatabaseService.GetReservationsForMonth(year, month);

            if (reservations.Count == 0)
            {
                MessageBox.Show("該当月のデータがありません。", "確認",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var settings = DatabaseService.GetSettings();
            var excelBytes = ExcelService.GenerateMonthlySummary(year, month, reservations, settings);

            var saveDlg = new SaveFileDialog
            {
                Filter = "Excelファイル (*.xlsx)|*.xlsx",
                FileName = "宿泊税集計_" + year + month.ToString("D2") + ".xlsx",
                Title = "保存先を選択"
            };
            if (saveDlg.ShowDialog() != true) return;

            File.WriteAllBytes(saveDlg.FileName, excelBytes);
            DatabaseService.Log("excel_export", "Monthly summary exported: " + year + "/" + month);

            MessageBox.Show("Excelファイルを保存しました。\n" + saveDlg.FileName, "出力完了",
                MessageBoxButton.OK, MessageBoxImage.Information);

            var open = MessageBox.Show("ファイルを開きますか？", "確認",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (open == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(saveDlg.FileName) { UseShellExecute = true });
        }

        // ---- 設定 ----

        private void LoadSettings()
        {
            var s = DatabaseService.GetSettings();
            PropertyNameBox.Text = s.PropertyName;
            PropertyAddressBox.Text = s.PropertyAddress;
            BusinessInfoBox.Text = s.BusinessInfo;
            TaxNumberBox.Text = s.TaxNumber;
            StripeKeyBox.Text = s.StripeApiKey;
            SmtpHostBox.Text = s.SmtpHost;
            SmtpPortBox.Text = s.SmtpPort.ToString();
            SmtpUserBox.Text = s.SmtpUser;
            SmtpPassBox.Password = s.SmtpPassword;

            // 市区町村コンボ選択
            for (int i = 0; i < MunicipalityCombo.Items.Count; i++)
            {
                if ((MunicipalityCombo.Items[i] as ComboBoxItem)?.Content?.ToString() == s.Municipality)
                {
                    MunicipalityCombo.SelectedIndex = i;
                    break;
                }
            }
            if (MunicipalityCombo.SelectedIndex < 0) MunicipalityCombo.SelectedIndex = 0;

            DefaultRoomRateBox.Text = s.DefaultRoomRatePerPerson.ToString("0");
            UpdateEffectiveTaxRate();
        }

        // 市区町村 or 宿泊料金が変わったら税率を自動更新
        private void MunicipalityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateEffectiveTaxRate();

        private void DefaultRoomRateBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => UpdateEffectiveTaxRate();

        private void UpdateEffectiveTaxRate()
        {
            var municipality = (MunicipalityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(municipality))
            {
                EffectiveTaxRateText.Text = "—";
                return;
            }
            if (!decimal.TryParse(DefaultRoomRateBox.Text, out var rate) || rate < 0)
            {
                EffectiveTaxRateText.Text = "—（金額を正しく入力してください）";
                return;
            }
            var tax = TaxMasterService.GetTaxPerPersonPerNight(municipality, rate);
            EffectiveTaxRateText.Text = tax.ToString("N0") + " 円 / 人 / 泊";
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // バリデーション
            if (string.IsNullOrWhiteSpace(PropertyNameBox.Text))
            {
                MessageBox.Show("施設名は必須です。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var municipality = (MunicipalityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(municipality))
            {
                MessageBox.Show("市区町村を選択してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!decimal.TryParse(DefaultRoomRateBox.Text, out var roomRate) || roomRate < 0)
            {
                MessageBox.Show("一人当たり宿泊料金は0以上の数値で入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!string.IsNullOrEmpty(NewPassBox.Password))
            {
                if (NewPassBox.Password.Length < 8)
                {
                    MessageBox.Show("パスワードは8文字以上で設定してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (NewPassBox.Password != ConfirmPassBox.Password)
                {
                    MessageBox.Show("パスワードが一致しません。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                // 市区町村・宿泊料金から有効税率を自動計算して保存
                var effectiveTax = TaxMasterService.GetTaxPerPersonPerNight(municipality, roomRate);

                DatabaseService.SaveSetting("property_name", PropertyNameBox.Text.Trim());
                DatabaseService.SaveSetting("property_address", PropertyAddressBox.Text.Trim());
                DatabaseService.SaveSetting("business_info", BusinessInfoBox.Text.Trim());
                DatabaseService.SaveSetting("tax_number", TaxNumberBox.Text.Trim());
                DatabaseService.SaveSetting("municipality", municipality);
                DatabaseService.SaveSetting("default_room_rate_per_person", roomRate.ToString("0"));
                DatabaseService.SaveSetting("tax_rate_per_person_per_night", effectiveTax.ToString("0"));
                DatabaseService.SaveSetting("stripe_api_key", StripeKeyBox.Text.Trim());
                DatabaseService.SaveSetting("smtp_host", SmtpHostBox.Text.Trim());
                DatabaseService.SaveSetting("smtp_port", SmtpPortBox.Text.Trim());
                DatabaseService.SaveSetting("smtp_user", SmtpUserBox.Text.Trim());
                if (!string.IsNullOrEmpty(SmtpPassBox.Password))
                    DatabaseService.SaveSetting("smtp_password", SmtpPassBox.Password);

                if (!string.IsNullOrEmpty(NewPassBox.Password))
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(NewPassBox.Password);
                    DatabaseService.SaveSetting("admin_password_hash", hash);
                    NewPassBox.Clear();
                    ConfirmPassBox.Clear();
                    MessageBox.Show("パスワードを変更しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DatabaseService.Log("settings_saved",
                    $"Admin settings updated: municipality={municipality}, roomRate={roomRate}, taxRate={effectiveTax}");
                MessageBox.Show(
                    $"設定を保存しました。\n宿泊税単価: {effectiveTax:N0} 円 / 人 / 泊",
                    "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存エラー: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- バックアップ ----

        private void BrowseBackupButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "バックアップ先フォルダを選択してください"
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _backupFolder = dlg.SelectedPath;
                BackupPathBox.Text = _backupFolder;
            }
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_backupFolder))
            {
                MessageBox.Show("バックアップ先フォルダを選択してください。", "確認",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var fileName = "accommodation_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db";
                var destPath = Path.Combine(_backupFolder, fileName);
                DatabaseService.BackupDatabase(destPath);
                BackupStatusText.Text = "✓ バックアップ完了\n" + destPath + "\n" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                MessageBox.Show("バックアップエラー: " + ex.Message, "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

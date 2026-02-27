using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            LoadTaxMaster();
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

        // ---- 宿泊税マスタ ----

        /// <summary>DataGrid バインディング用ビューモデル行</summary>
        public class TaxRateRow
        {
            public int Id { get; set; }
            public string Municipality { get; set; } = "";
            public int FromAmount { get; set; }
            public string ToAmountStr { get; set; } = "";  // 空欄 = 上限なし
            public int TaxAmount { get; set; }
        }

        private void LoadTaxMaster()
        {
            var rates = DatabaseService.GetTaxRates();
            var rows = new ObservableCollection<TaxRateRow>();
            foreach (var r in rates)
                rows.Add(new TaxRateRow
                {
                    Id = r.Id,
                    Municipality = r.Municipality,
                    FromAmount = r.FromAmount,
                    ToAmountStr = r.ToAmount.HasValue ? r.ToAmount.Value.ToString() : "",
                    TaxAmount = r.TaxAmount,
                });
            TaxMasterGrid.ItemsSource = rows;
        }

        private void AddTaxRateRow_Click(object sender, RoutedEventArgs e)
        {
            if (TaxMasterGrid.ItemsSource is ObservableCollection<TaxRateRow> rows)
                rows.Add(new TaxRateRow { Municipality = "", FromAmount = 0, ToAmountStr = "", TaxAmount = 0 });
        }

        private void DeleteTaxRateRow_Click(object sender, RoutedEventArgs e)
        {
            if (!(TaxMasterGrid.ItemsSource is ObservableCollection<TaxRateRow> rows) ||
                !(TaxMasterGrid.SelectedItem is TaxRateRow selected))
            {
                MessageBox.Show("削除する行を選択してください。", "確認",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            rows.Remove(selected);
        }

        private void ResetTaxRates_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "宿泊税マスタをデフォルト値に戻します。現在の設定は上書きされます。よろしいですか？",
                "デフォルトに戻す",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            DatabaseService.ResetTaxRatesToDefaults();
            LoadTaxMaster();
            RefreshMunicipalityCombo();
            MessageBox.Show("宿泊税マスタをデフォルト値に戻しました。", "完了",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveTaxRatesButton_Click(object sender, RoutedEventArgs e)
        {
            // DataGrid の編集中セルをコミット
            TaxMasterGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            if (!(TaxMasterGrid.ItemsSource is ObservableCollection<TaxRateRow> rows)) return;

            var rates = new List<TaxRate>();
            var errors = new List<string>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (string.IsNullOrWhiteSpace(row.Municipality))
                {
                    errors.Add($"行{i + 1}: 市区町村が空です");
                    continue;
                }

                int? toAmount = null;
                if (!string.IsNullOrWhiteSpace(row.ToAmountStr))
                {
                    if (!int.TryParse(row.ToAmountStr.Replace(",", ""), out var ta) || ta < 0)
                    {
                        errors.Add($"行{i + 1} ({row.Municipality}): 上限金額が正しくありません");
                        continue;
                    }
                    toAmount = ta;
                }

                rates.Add(new TaxRate
                {
                    Municipality = row.Municipality.Trim(),
                    FromAmount = row.FromAmount,
                    ToAmount = toAmount,
                    TaxAmount = row.TaxAmount,
                });
            }

            if (errors.Count > 0)
            {
                MessageBox.Show("以下のエラーを修正してください：\n\n" + string.Join("\n", errors),
                    "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DatabaseService.SaveTaxRates(rates);
            LoadTaxMaster();
            RefreshMunicipalityCombo();
            DatabaseService.Log("tax_master_updated", $"Tax master updated: {rates.Count} entries");
            MessageBox.Show("宿泊税マスタを保存しました。", "完了",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>市区町村コンボを再構築し、以前の選択を復元する</summary>
        private void RefreshMunicipalityCombo()
        {
            var current = (MunicipalityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
                          ?? DatabaseService.GetSettings().Municipality;
            MunicipalityCombo.Items.Clear();
            InitMunicipalityCombo();

            for (int i = 0; i < MunicipalityCombo.Items.Count; i++)
            {
                if ((MunicipalityCombo.Items[i] as ComboBoxItem)?.Content?.ToString() == current)
                {
                    MunicipalityCombo.SelectedIndex = i;
                    return;
                }
            }
            if (MunicipalityCombo.Items.Count > 0)
                MunicipalityCombo.SelectedIndex = 0;
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
                DatabaseService.SaveSetting("property_name", PropertyNameBox.Text.Trim());
                DatabaseService.SaveSetting("property_address", PropertyAddressBox.Text.Trim());
                DatabaseService.SaveSetting("business_info", BusinessInfoBox.Text.Trim());
                DatabaseService.SaveSetting("tax_number", TaxNumberBox.Text.Trim());
                DatabaseService.SaveSetting("municipality", municipality);
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
                    $"Admin settings updated: municipality={municipality}");
                MessageBox.Show("設定を保存しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存エラー: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- データ初期化 ----

        private void ClearAllDataButton_Click(object sender, RoutedEventArgs e)
        {
            var res1 = MessageBox.Show(
                "全ての予約データ・領収書・ログを削除します。\n施設設定と宿泊税マスタは保持されます。\n\nよろしいですか？",
                "予約データの初期化",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (res1 != MessageBoxResult.Yes) return;

            var res2 = MessageBox.Show(
                "削除したデータは復元できません。\n本当に削除しますか？",
                "最終確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (res2 != MessageBoxResult.Yes) return;

            DatabaseService.ClearAllReservations();
            LoadReservations();
            MessageBox.Show("全予約データを削除しました。", "完了",
                MessageBoxButton.OK, MessageBoxImage.Information);
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

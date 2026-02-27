using System;
using System.Windows;
using AccommodationSystem.Data;
using AccommodationSystem.Models;
using AccommodationSystem.Services;

namespace AccommodationSystem.Views
{
    public partial class ReceiptEmailWindow : Window
    {
        private readonly Reservation _reservation;
        private readonly bool        _isReissue;
        private readonly int         _receiptCount;

        public ReceiptEmailWindow(Reservation reservation, bool isReissue = false)
        {
            InitializeComponent();
            _reservation  = reservation;
            _isReissue    = isReissue;
            _receiptCount = DatabaseService.GetReceiptCount(reservation.Id);

            // Subscribe before RefreshUI so language change while window is open also updates
            LanguageService.LanguageChanged += RefreshUI;
            Unloaded += (s, e) => LanguageService.LanguageChanged -= RefreshUI;

            RefreshUI();
        }

        /// <summary>
        /// Updates all text that cannot be handled by XAML LocalizationProxy bindings alone
        /// (title varies by reissue flag; summary includes runtime data).
        /// </summary>
        private void RefreshUI()
        {
            TitleText.Text = _isReissue
                ? LanguageService.T("receipt_title_reissue")
                : LanguageService.T("receipt_title");

            ReservationSummary.Text =
                _reservation.GuestName + LanguageService.T("guest_suffix") + " ／ " +
                _reservation.CheckinDate.ToString("yyyy/MM/dd") +
                LanguageService.T("item_dash") +
                _reservation.CheckoutDate.ToString("yyyy/MM/dd") + " ／ " +
                LanguageService.T("lbl_tax_total") + ": ¥" +
                _reservation.AccommodationTax.ToString("N0");

            if (_receiptCount > 0)
                StatusText.Text =
                    LanguageService.T("receipt_issued_count_prefix") +
                    _receiptCount +
                    LanguageService.T("receipt_issued_count_suffix");

            SendButton.Content = LanguageService.T("btn_send");
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailBox.Text.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                MessageBox.Show(
                    LanguageService.T("val_email"),
                    LanguageService.T("err_input_title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SendButton.IsEnabled = false;
            StatusText.Text      = LanguageService.T("processing_pdf");

            try
            {
                var receiptNumber = DatabaseService.GetNextReceiptNumber(_reservation.ReservationNumber);
                var pdfBytes      = PdfService.GenerateReceipt(_reservation, receiptNumber);

                StatusText.Text   = LanguageService.T("processing_email");
                var settings      = DatabaseService.GetSettings();
                var subject       = "【" + settings.PropertyName + "】宿泊税領収書 - " + receiptNumber;
                var body          = _reservation.GuestName + " 様\n\n" +
                                    ((_isReissue ? "【再発行】" : "") + "宿泊税領収書をお送りします。\n\n") +
                                    "領収書番号: " + receiptNumber + "\n" +
                                    "宿泊税額: ¥" + _reservation.AccommodationTax.ToString("N0") + "\n\n" +
                                    settings.PropertyName;

                await MailService.SendReceiptAsync(email, subject, body, pdfBytes,
                    "receipt_" + receiptNumber + ".pdf");

                DatabaseService.SaveReceipt(_reservation.Id, receiptNumber, email);

                MessageBox.Show(
                    LanguageService.T("msg_send_ok_prefix") + email,
                    LanguageService.T("msg_send_ok_title"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    LanguageService.T("msg_send_fail_prefix") + ex.Message,
                    LanguageService.T("err_title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SendButton.IsEnabled = true;
                SendButton.Content   = LanguageService.T("btn_send");
                StatusText.Text      = "";
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}

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
        private readonly bool _isReissue;

        public ReceiptEmailWindow(Reservation reservation, bool isReissue = false)
        {
            InitializeComponent();
            _reservation = reservation;
            _isReissue = isReissue;

            if (isReissue)
                TitleText.Text = "領収書の再発行";

            // 宿泊情報サマリー表示
            ReservationSummary.Text =
                reservation.GuestName + " 様 ／ " +
                reservation.CheckinDate.ToString("yyyy/MM/dd") + " 〜 " +
                reservation.CheckoutDate.ToString("yyyy/MM/dd") + " ／ " +
                "宿泊税: ¥" + reservation.AccommodationTax.ToString("N0");

            // 過去の発行回数を表示
            var count = DatabaseService.GetReceiptCount(reservation.Id);
            if (count > 0)
                StatusText.Text = "※ この予約ではすでに " + count + " 回発行済みです";
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailBox.Text.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                MessageBox.Show("有効なメールアドレスを入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SendButton.IsEnabled = false;
            StatusText.Text = "PDF生成中...";

            try
            {
                var receiptNumber = DatabaseService.GetNextReceiptNumber(_reservation.ReservationNumber);
                var pdfBytes = PdfService.GenerateReceipt(_reservation, receiptNumber);

                StatusText.Text = "メール送信中...";
                var settings = DatabaseService.GetSettings();
                var subject = "【" + settings.PropertyName + "】宿泊税領収書 - " + receiptNumber;
                var body = _reservation.GuestName + " 様\n\n" +
                    ((_isReissue ? "【再発行】" : "") + "宿泊税領収書をお送りします。\n\n") +
                    "領収書番号: " + receiptNumber + "\n" +
                    "宿泊税額: ¥" + _reservation.AccommodationTax.ToString("N0") + "\n\n" +
                    settings.PropertyName;

                await MailService.SendReceiptAsync(email, subject, body, pdfBytes,
                    "receipt_" + receiptNumber + ".pdf");

                DatabaseService.SaveReceipt(_reservation.Id, receiptNumber, email);

                MessageBox.Show(
                    "領収書を送信しました。\n送付先: " + email,
                    "送信完了", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("送信エラー: " + ex.Message, "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SendButton.IsEnabled = true;
                StatusText.Text = "";
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}

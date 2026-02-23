using System;
using System.Threading.Tasks;
using System.Windows;
using AccommodationSystem.Data;
using AccommodationSystem.Models;
using AccommodationSystem.Services;
using Stripe;

namespace AccommodationSystem.Views
{
    public partial class PaymentWindow : Window
    {
        private readonly Reservation _reservation;

        public PaymentWindow(Reservation reservation)
        {
            InitializeComponent();
            _reservation = reservation;
            LoadReservation();
        }

        private void LoadReservation()
        {
            GuestNameText.Text = _reservation.GuestName;
            ResNumText.Text = _reservation.ReservationNumber;
            CheckinText.Text = _reservation.CheckinDate.ToString("yyyy/MM/dd");
            CheckoutText.Text = _reservation.CheckoutDate.ToString("yyyy/MM/dd");
            PersonsText.Text = $"{_reservation.NumPersons} 名";
            NightsText.Text = $"{_reservation.NumNights} 泊";
            TaxAmountText.Text = $"¥ {_reservation.AccommodationTax:N0}";

            if (_reservation.IsPaid)
            {
                AlreadyPaidBorder.Visibility = Visibility.Visible;
                PaymentPanel.Visibility = Visibility.Collapsed;
                PayButton.IsEnabled = false;
                PayButton.Content = "支払い済み";
            }
        }

        private async void PayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            PayButton.IsEnabled = false;
            PayButton.Content = "処理中...";

            try
            {
                // Stripe PaymentMethod作成
                StripeService.Configure();
                var expParts = ExpBox.Text.Split('/');
                var pmOptions = new PaymentMethodCreateOptions
                {
                    Type = "card",
                    Card = new PaymentMethodCardOptions
                    {
                        Number = CardNumberBox.Text.Replace(" ", ""),
                        ExpMonth = long.Parse(expParts[0]),
                        ExpYear = long.Parse("20" + expParts[1]),
                        Cvc = CvcBox.Password,
                    },
                };
                var pmService = new PaymentMethodService();
                var pm = await pmService.CreateAsync(pmOptions);

                // PaymentIntent作成・確認
                var (_, intentId) = await StripeService.CreatePaymentIntent(_reservation.AccommodationTax);
                var status = await StripeService.ConfirmPayment(intentId, pm.Id);

                if (status == "succeeded")
                {
                    DatabaseService.UpdatePaymentStatus(_reservation.Id, intentId);
                    DatabaseService.Log("payment", $"Payment succeeded for reservation {_reservation.ReservationNumber}");

                    MessageBox.Show("決済が完了しました！", "決済完了",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // 領収書発行確認
                    var receiptResult = MessageBox.Show(
                        "領収書をメールで受け取りますか？",
                        "領収書発行",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (receiptResult == MessageBoxResult.Yes)
                    {
                        // 決済済みの予約情報を更新
                        _reservation.PaymentStatus = "paid";
                        _reservation.StripePaymentId = intentId;
                        var receiptDlg = new ReceiptEmailWindow(_reservation) { Owner = this };
                        receiptDlg.ShowDialog();
                    }

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show($"決済が完了しませんでした。ステータス: {status}", "エラー",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    PayButton.IsEnabled = true;
                    PayButton.Content = "💳  カードで支払う";
                }
            }
            catch (StripeException ex)
            {
                MessageBox.Show($"決済エラー: {ex.StripeError?.Message ?? ex.Message}", "決済エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                PayButton.IsEnabled = true;
                PayButton.Content = "💳  カードで支払う";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                PayButton.IsEnabled = true;
                PayButton.Content = "💳  カードで支払う";
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(CardNumberBox.Text) || CardNumberBox.Text.Replace(" ", "").Length < 13)
            {
                MessageBox.Show("有効なカード番号を入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(ExpBox.Text) || !ExpBox.Text.Contains("/"))
            {
                MessageBox.Show("有効期限を MM/YY 形式で入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(CvcBox.Password) || CvcBox.Password.Length < 3)
            {
                MessageBox.Show("セキュリティコードを入力してください。", "入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

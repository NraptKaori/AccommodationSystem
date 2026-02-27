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

            // Refresh dynamic labels whenever the language is toggled
            LanguageService.LanguageChanged += LoadReservation;
            Unloaded += (s, e) => LanguageService.LanguageChanged -= LoadReservation;

            LoadReservation();
        }

        private void LoadReservation()
        {
            GuestNameText.Text  = _reservation.GuestName;
            ResNumText.Text     = _reservation.ReservationNumber;
            CheckinText.Text    = _reservation.CheckinDate.ToString("yyyy/MM/dd");
            CheckoutText.Text   = _reservation.CheckoutDate.ToString("yyyy/MM/dd");
            PersonsText.Text    = $"{_reservation.NumPersons}{LanguageService.T("suffix_persons")}";
            NightsText.Text     = $"{_reservation.NumNights}{LanguageService.T("suffix_nights")}";
            RoomRateText.Text   = _reservation.RoomRatePerPersonPerNight > 0
                                    ? $"¥ {_reservation.RoomRatePerPersonPerNight:N0}"
                                    : "—";

            var taxPerPerson = (_reservation.NumPersons > 0 && _reservation.NumNights > 0)
                ? _reservation.AccommodationTax / _reservation.NumPersons / _reservation.NumNights
                : 0m;
            TaxPerPersonText.Text = taxPerPerson > 0 ? $"¥ {taxPerPerson:N0}" : "—";
            TaxAmountText.Text    = $"¥ {_reservation.AccommodationTax:N0}";

            if (_reservation.IsPaid)
            {
                AlreadyPaidBorder.Visibility = Visibility.Visible;
                PaymentPanel.Visibility      = Visibility.Collapsed;
                PayButton.IsEnabled          = false;
                PayButton.Content            = LanguageService.T("btn_paid");
            }
            else
            {
                // Only reset content when not currently processing
                if (PayButton.IsEnabled)
                    PayButton.Content = LanguageService.T("btn_pay");
            }
        }

        private async void PayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            PayButton.IsEnabled = false;
            PayButton.Content   = LanguageService.T("btn_processing");

            try
            {
                StripeService.Configure();

                // PaymentMethod ID を取得
                // テストモード（sk_test_）ではカード番号からStripeテストトークンにマッピング
                // 本番モードでは生カードデータ送信の特別権限が必要
                string paymentMethodId;
                if (StripeService.IsTestMode())
                {
                    paymentMethodId = GetTestPaymentMethodId(CardNumberBox.Text.Replace(" ", ""));
                }
                else
                {
                    var expParts  = ExpBox.Text.Split('/');
                    var pmOptions = new PaymentMethodCreateOptions
                    {
                        Type = "card",
                        Card = new PaymentMethodCardOptions
                        {
                            Number   = CardNumberBox.Text.Replace(" ", ""),
                            ExpMonth = long.Parse(expParts[0]),
                            ExpYear  = long.Parse("20" + expParts[1]),
                            Cvc      = CvcBox.Password,
                        },
                    };
                    var pmService   = new PaymentMethodService();
                    var pm          = await pmService.CreateAsync(pmOptions);
                    paymentMethodId = pm.Id;
                }

                // PaymentIntent作成・確認
                var (_, intentId) = await StripeService.CreatePaymentIntent(_reservation.AccommodationTax);
                var status        = await StripeService.ConfirmPayment(intentId, paymentMethodId);

                if (status == "succeeded")
                {
                    DatabaseService.UpdatePaymentStatus(_reservation.Id, intentId);
                    DatabaseService.Log("payment",
                        $"Payment succeeded for reservation {_reservation.ReservationNumber}");

                    MessageBox.Show(
                        LanguageService.T("msg_pay_ok"),
                        LanguageService.T("msg_pay_ok_title"),
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // 領収書発行確認
                    var receiptResult = MessageBox.Show(
                        LanguageService.T("msg_receipt_q"),
                        LanguageService.T("msg_receipt_title"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (receiptResult == MessageBoxResult.Yes)
                    {
                        _reservation.PaymentStatus    = "paid";
                        _reservation.StripePaymentId  = intentId;
                        var receiptDlg = new ReceiptEmailWindow(_reservation) { Owner = this };
                        receiptDlg.ShowDialog();
                    }

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        LanguageService.T("msg_pay_fail_prefix") + status,
                        LanguageService.T("err_title"),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    PayButton.IsEnabled = true;
                    PayButton.Content   = LanguageService.T("btn_pay");
                }
            }
            catch (StripeException ex)
            {
                MessageBox.Show(
                    LanguageService.T("stripe_pay_err_prefix") + (ex.StripeError?.Message ?? ex.Message),
                    LanguageService.T("err_title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                PayButton.IsEnabled = true;
                PayButton.Content   = LanguageService.T("btn_pay");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    LanguageService.T("err_title") + ": " + ex.Message,
                    LanguageService.T("err_title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                PayButton.IsEnabled = true;
                PayButton.Content   = LanguageService.T("btn_pay");
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(CardNumberBox.Text) ||
                CardNumberBox.Text.Replace(" ", "").Length < 13)
            {
                MessageBox.Show(
                    LanguageService.T("val_card_num"),
                    LanguageService.T("val_err_title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(ExpBox.Text) || !ExpBox.Text.Contains("/"))
            {
                MessageBox.Show(
                    LanguageService.T("val_expiry"),
                    LanguageService.T("val_err_title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(CvcBox.Password) || CvcBox.Password.Length < 3)
            {
                MessageBox.Show(
                    LanguageService.T("val_cvc"),
                    LanguageService.T("val_err_title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// テストカード番号をStripeのテスト用PaymentMethod IDにマッピングする
        /// 参考: https://docs.stripe.com/testing#cards
        /// </summary>
        private static string GetTestPaymentMethodId(string cardNumber)
        {
            if (cardNumber.StartsWith("4000000000000002")) return "pm_card_chargeDeclined";
            if (cardNumber.StartsWith("4000000000009995")) return "pm_card_chargeDeclinedInsufficientFunds";
            if (cardNumber.StartsWith("378282246310005"))  return "pm_card_amex";
            if (cardNumber.StartsWith("6011111111111117")) return "pm_card_discover";
            if (cardNumber.StartsWith("5555555555554444")) return "pm_card_mastercard";
            if (cardNumber.StartsWith("4"))                return "pm_card_visa";
            return "pm_card_visa";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

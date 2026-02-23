using System;
using System.Threading.Tasks;
using Stripe;
using AccommodationSystem.Data;

namespace AccommodationSystem.Services
{
    public static class StripeService
    {
        public static void Configure()
        {
            var settings = DatabaseService.GetSettings();
            StripeConfiguration.ApiKey = settings.StripeApiKey;
        }

        /// <summary>
        /// PaymentIntentを作成してclient_secretを返す
        /// </summary>
        public static async Task<(string clientSecret, string paymentIntentId)> CreatePaymentIntent(decimal amount, string currency = "jpy")
        {
            Configure();
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)amount,
                Currency = currency,
                PaymentMethodTypes = new System.Collections.Generic.List<string> { "card" },
            };
            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(options);
            return (intent.ClientSecret, intent.Id);
        }

        /// <summary>
        /// PaymentMethodを使って即時支払い（テスト用）
        /// </summary>
        public static async Task<string> ConfirmPayment(string paymentIntentId, string paymentMethodId)
        {
            Configure();
            var service = new PaymentIntentService();
            var options = new PaymentIntentConfirmOptions
            {
                PaymentMethod = paymentMethodId,
            };
            var intent = await service.ConfirmAsync(paymentIntentId, options);
            return intent.Status; // "succeeded" など
        }
    }
}

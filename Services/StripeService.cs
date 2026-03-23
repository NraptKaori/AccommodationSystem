using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
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

        public static bool IsTestMode()
        {
            var settings = DatabaseService.GetSettings();
            return settings.StripeApiKey?.StartsWith("sk_test_") == true;
        }

        public static string GetPublishableKey()
        {
            return DatabaseService.GetSettings().StripePublishableKey;
        }

        /// <summary>
        /// Publishable Key を使い Stripe /v1/tokens へ直接POST してカードトークンを取得する。
        /// 生カードデータはStripeサーバーにのみ送信される。
        /// </summary>
        public static async Task<string> CreateCardTokenAsync(
            string number, string expMonth, string expYear, string cvc, string name)
        {
            var pk = GetPublishableKey();
            if (string.IsNullOrWhiteSpace(pk))
                throw new InvalidOperationException("Stripe Publishable Key が設定されていません。管理画面で pk_live_ / pk_test_ キーを設定してください。");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", pk);

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("card[number]",    number),
                    new KeyValuePair<string, string>("card[exp_month]", expMonth),
                    new KeyValuePair<string, string>("card[exp_year]",  expYear),
                    new KeyValuePair<string, string>("card[cvc]",       cvc),
                    new KeyValuePair<string, string>("card[name]",      name),
                });

                var response = await client.PostAsync("https://api.stripe.com/v1/tokens", form);
                var json     = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var m = Regex.Match(json, "\"message\":\\s*\"([^\"]+)\"");
                    throw new Exception(m.Success ? m.Groups[1].Value : "Stripe tokenization failed");
                }

                var idMatch = Regex.Match(json, "\"id\":\\s*\"(tok_[^\"]+)\"");
                if (!idMatch.Success)
                    throw new Exception("Stripe レスポンスからトークンIDを取得できませんでした");

                return idMatch.Groups[1].Value;
            }
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

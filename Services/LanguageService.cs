using System.Collections.Generic;

namespace AccommodationSystem.Services
{
    public enum AppLanguage { JA, EN }

    public static class LanguageService
    {
        public static AppLanguage Current { get; private set; } = AppLanguage.JA;
        public static event System.Action LanguageChanged;

        public static void Toggle()
        {
            Current = Current == AppLanguage.JA ? AppLanguage.EN : AppLanguage.JA;
            LanguageChanged?.Invoke();
        }

        public static string T(string key)
        {
            if (_strings.TryGetValue(key, out var arr))
                return arr[(int)Current];
            return key;
        }

        // [0]=JA  [1]=EN
        private static readonly Dictionary<string, string[]> _strings =
            new Dictionary<string, string[]>
        {
            // --- General ---
            ["app_title"]                   = new[] { "🏨  宿泊税徴収管理・領収書発行システム",  "🏨  Accommodation Tax & Receipt System" },
            ["err_title"]                   = new[] { "エラー",                              "Error" },
            ["err_input_title"]             = new[] { "入力エラー",                          "Input Error" },

            // --- CheckinPage ---
            ["search_title"]                = new[] { "予約検索",                            "Reservation Search" },
            ["search_label"]                = new[] { "予約番号または宿泊者名（英字）を入力してください", "Enter reservation number or guest name" },
            ["search_placeholder"]          = new[] { "例: ABC12345 または Yamada",          "e.g. ABC12345 or Yamada" },
            ["search_btn"]                  = new[] { "検 索",                              "Search" },
            ["search_err_empty"]            = new[] { "検索キーワードを入力してください。",    "Please enter a search keyword." },
            ["welcome_title"]               = new[] { "ようこそ",                            "Welcome" },
            ["welcome_desc1"]               = new[] { "上の検索欄に予約番号または宿泊者名を入力して", "Enter a reservation number or guest name above" },
            ["welcome_desc2"]               = new[] { "「検索」ボタンを押してください",        "and press the Search button" },
            ["no_result"]                   = new[] { "予約が見つかりませんでした",            "No reservations found" },

            // DataTemplate labels
            ["item_res_num"]                = new[] { "予約番号: ",                          "Res#: " },
            ["item_checkin"]                = new[] { "チェックイン: ",                      "Check-in: " },
            ["item_dash"]                   = new[] { " 〜 ",                               " – " },
            ["item_tax_lbl"]                = new[] { "宿泊税",                              "Accommodation Tax" },
            ["suffix_persons"]              = new[] { " 名",                                " guest(s)" },
            ["suffix_nights"]               = new[] { " 泊",                                " night(s)" },
            ["status_paid"]                 = new[] { "支払済み",                            "Paid" },
            ["status_unpaid"]               = new[] { "未払い",                              "Unpaid" },

            // --- PaymentWindow ---
            ["pay_title"]                   = new[] { "宿泊税のお支払い",                    "Accommodation Tax Payment" },
            ["lbl_guest_name"]              = new[] { "宿泊者名",                            "Guest Name" },
            ["lbl_res_number"]              = new[] { "予約番号",                            "Reservation #" },
            ["lbl_checkin"]                 = new[] { "チェックイン",                        "Check-in" },
            ["lbl_checkout"]                = new[] { "チェックアウト",                      "Check-out" },
            ["lbl_persons"]                 = new[] { "宿泊人数",                            "Guests" },
            ["lbl_nights"]                  = new[] { "宿泊泊数",                            "Nights" },
            ["lbl_room_rate"]               = new[] { "1人1泊あたりの宿泊料金",               "Room Rate / Person / Night" },
            ["lbl_tax_per_person"]          = new[] { "1人1泊あたりの宿泊税",                 "Tax / Person / Night" },
            ["lbl_tax_total"]               = new[] { "宿泊税合計",                          "Tax Total" },
            ["already_paid"]                = new[] { "✓ 宿泊税は支払い済みです",             "✓ Accommodation tax already paid" },
            ["card_title"]                  = new[] { "クレジットカード情報",                  "Credit Card Information" },
            ["lbl_card_num"]                = new[] { "カード番号",                          "Card Number" },
            ["lbl_expiry"]                  = new[] { "有効期限 (MM/YY)",                    "Expiry (MM/YY)" },
            ["lbl_cvc"]                     = new[] { "セキュリティコード",                    "Security Code" },
            ["lbl_card_name"]               = new[] { "カード名義（半角英字）",                "Cardholder Name" },
            ["stripe_note"]                 = new[] { "※ 決済はStripeによる安全な処理で行われます", "※ Payments are processed securely via Stripe" },
            ["btn_cancel"]                  = new[] { "キャンセル",                          "Cancel" },
            ["btn_pay"]                     = new[] { "💳  カードで支払う",                   "💳  Pay by Card" },
            ["btn_paid"]                    = new[] { "支払い済み",                          "Already Paid" },
            ["btn_processing"]              = new[] { "処理中...",                           "Processing..." },
            ["msg_pay_ok"]                  = new[] { "決済が完了しました！",                  "Payment completed!" },
            ["msg_pay_ok_title"]            = new[] { "決済完了",                            "Payment Complete" },
            ["msg_receipt_q"]               = new[] { "領収書をメールで受け取りますか？",       "Would you like to receive a receipt by email?" },
            ["msg_receipt_title"]           = new[] { "領収書発行",                          "Receipt" },
            ["msg_pay_fail_prefix"]         = new[] { "決済が完了しませんでした。ステータス: ", "Payment incomplete. Status: " },
            ["val_card_num"]                = new[] { "有効なカード番号を入力してください。",    "Please enter a valid card number." },
            ["val_expiry"]                  = new[] { "有効期限を MM/YY 形式で入力してください。", "Please enter the expiry date in MM/YY format." },
            ["val_cvc"]                     = new[] { "セキュリティコードを入力してください。",  "Please enter the security code." },
            ["val_err_title"]               = new[] { "入力エラー",                          "Input Error" },
            ["stripe_pay_err_prefix"]       = new[] { "決済エラー: ",                        "Payment error: " },

            // --- ReceiptEmailWindow ---
            ["receipt_title"]               = new[] { "領収書をメールで送付します",            "Send Receipt by Email" },
            ["receipt_title_reissue"]       = new[] { "領収書の再発行",                      "Re-issue Receipt" },
            ["guest_suffix"]                = new[] { " 様",                                "" },
            ["lbl_email"]                   = new[] { "送付先メールアドレス",                  "Email Address" },
            ["btn_skip"]                    = new[] { "スキップ",                            "Skip" },
            ["btn_send"]                    = new[] { "📧  送信する",                        "📧  Send" },
            ["processing_pdf"]              = new[] { "PDF生成中...",                        "Generating PDF..." },
            ["processing_email"]            = new[] { "メール送信中...",                      "Sending email..." },
            ["msg_send_ok_prefix"]          = new[] { "領収書を送信しました。\n送付先: ",       "Receipt sent.\nTo: " },
            ["msg_send_ok_title"]           = new[] { "送信完了",                            "Sent" },
            ["msg_send_fail_prefix"]        = new[] { "送信エラー: ",                        "Send error: " },
            ["val_email"]                   = new[] { "有効なメールアドレスを入力してください。", "Please enter a valid email address." },
            ["receipt_issued_count_prefix"] = new[] { "※ この予約ではすでに ",               "※ This reservation has already been issued " },
            ["receipt_issued_count_suffix"] = new[] { " 回発行済みです",                     " time(s)." },
        };
    }
}

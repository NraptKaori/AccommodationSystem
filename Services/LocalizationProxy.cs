using System.ComponentModel;

namespace AccommodationSystem.Services
{
    /// <summary>
    /// INotifyPropertyChanged singleton used as XAML binding source.
    /// When LanguageService.LanguageChanged fires, PropertyChanged("") is raised
    /// so every bound control refreshes automatically.
    /// Usage in XAML:
    ///   xmlns:svc="clr-namespace:AccommodationSystem.Services"
    ///   Text="{Binding Source={x:Static svc:LocalizationProxy.Instance}, Path=SearchTitle}"
    /// </summary>
    public class LocalizationProxy : INotifyPropertyChanged
    {
        public static readonly LocalizationProxy Instance = new LocalizationProxy();

        private LocalizationProxy()
        {
            LanguageService.LanguageChanged += Refresh;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Refresh()
        {
            // Empty-string key notifies all bound properties at once
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

        // --- General ---
        public string AppTitle              => LanguageService.T("app_title");

        // --- CheckinPage ---
        public string SearchTitle           => LanguageService.T("search_title");
        public string SearchLabel           => LanguageService.T("search_label");
        public string SearchPlaceholder     => LanguageService.T("search_placeholder");
        public string SearchBtn             => LanguageService.T("search_btn");
        public string WelcomeTitle          => LanguageService.T("welcome_title");
        public string WelcomeDesc1          => LanguageService.T("welcome_desc1");
        public string WelcomeDesc2          => LanguageService.T("welcome_desc2");
        public string NoResult              => LanguageService.T("no_result");
        public string ItemResNum            => LanguageService.T("item_res_num");
        public string ItemCheckin           => LanguageService.T("item_checkin");
        public string ItemDash              => LanguageService.T("item_dash");
        public string ItemTaxLbl            => LanguageService.T("item_tax_lbl");
        public string SuffixPersons         => LanguageService.T("suffix_persons");
        public string SuffixNights          => LanguageService.T("suffix_nights");
        public string StatusPaid            => LanguageService.T("status_paid");
        public string StatusUnpaid          => LanguageService.T("status_unpaid");

        // --- PaymentWindow ---
        public string PayTitle              => LanguageService.T("pay_title");
        public string LblGuestName          => LanguageService.T("lbl_guest_name");
        public string LblResNumber          => LanguageService.T("lbl_res_number");
        public string LblCheckin            => LanguageService.T("lbl_checkin");
        public string LblCheckout           => LanguageService.T("lbl_checkout");
        public string LblPersons            => LanguageService.T("lbl_persons");
        public string LblNights             => LanguageService.T("lbl_nights");
        public string LblRoomRate           => LanguageService.T("lbl_room_rate");
        public string LblTaxPerPerson       => LanguageService.T("lbl_tax_per_person");
        public string LblTaxTotal           => LanguageService.T("lbl_tax_total");
        public string AlreadyPaid           => LanguageService.T("already_paid");
        public string CardTitle             => LanguageService.T("card_title");
        public string LblCardNum            => LanguageService.T("lbl_card_num");
        public string LblExpiry             => LanguageService.T("lbl_expiry");
        public string LblCvc                => LanguageService.T("lbl_cvc");
        public string LblCardName           => LanguageService.T("lbl_card_name");
        public string StripeNote            => LanguageService.T("stripe_note");
        public string BtnCancel             => LanguageService.T("btn_cancel");
        public string BtnPay                => LanguageService.T("btn_pay");

        // --- ReceiptEmailWindow ---
        public string LblEmail              => LanguageService.T("lbl_email");
        public string BtnSkip               => LanguageService.T("btn_skip");
        public string BtnSend               => LanguageService.T("btn_send");
    }
}

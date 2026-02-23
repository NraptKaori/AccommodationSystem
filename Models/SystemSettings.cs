namespace AccommodationSystem.Models
{
    public class SystemSettings
    {
        public string PropertyName { get; set; } = "";
        public string PropertyAddress { get; set; } = "";
        public string StripeApiKey { get; set; } = "";
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public decimal TaxRatePerPersonPerNight { get; set; } = 200m;
        public string AdminPasswordHash { get; set; } = "";
        public string BusinessInfo { get; set; } = "";
        public string TaxNumber { get; set; } = "";
    }
}

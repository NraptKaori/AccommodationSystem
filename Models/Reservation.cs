using System;

namespace AccommodationSystem.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        public string ReservationNumber { get; set; } = "";
        public string GuestName { get; set; } = "";
        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }
        public int NumPersons { get; set; }
        public int NumNights { get; set; }
        public decimal AccommodationTax { get; set; }
        public string PaymentStatus { get; set; } = "unpaid";
        public DateTime? PaymentDate { get; set; }
        public string StripePaymentId { get; set; }   // nullable disable のため ? 不要
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsPaid => PaymentStatus == "paid";
        public string PaymentStatusDisplay => IsPaid ? "支払済み" : "未払い";
    }
}

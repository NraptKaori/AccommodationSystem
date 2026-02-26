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
        public decimal TotalFee { get; set; }           // CSV 取込時の宿泊料金合計（税抜・清掃費等除く）
        public string PaymentStatus { get; set; } = "unpaid";
        public DateTime? PaymentDate { get; set; }
        public string StripePaymentId { get; set; }   // nullable disable のため ? 不要
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsPaid => PaymentStatus == "paid";
        public string PaymentStatusDisplay => IsPaid ? "支払済み" : "未払い";

        /// <summary>1人1泊あたりの宿泊料金（TotalFee ÷ 人数 ÷ 泊数）</summary>
        public decimal RoomRatePerPersonPerNight =>
            (NumPersons > 0 && NumNights > 0) ? Math.Round(TotalFee / NumPersons / NumNights, 0) : 0m;
    }
}

namespace AccommodationSystem.Models
{
    /// <summary>
    /// 宿泊税マスタのタックスレート（1人1泊あたり）
    /// </summary>
    public class TaxRate
    {
        public int Id { get; set; }
        public string Municipality { get; set; } = "";

        /// <summary>適用下限（この金額以上、円）</summary>
        public int FromAmount { get; set; }

        /// <summary>適用上限（この金額以下、円）。null = 上限なし</summary>
        public int? ToAmount { get; set; }

        /// <summary>1人1泊あたりの宿泊税（道税＋市区町村税の合計、円）</summary>
        public int TaxAmount { get; set; }

        public int SortOrder { get; set; }
    }
}

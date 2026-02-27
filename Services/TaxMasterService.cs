using System.Collections.Generic;
using AccommodationSystem.Data;

namespace AccommodationSystem.Services
{
    /// <summary>
    /// 宿泊税マスタ（DB読み込み）
    /// </summary>
    public static class TaxMasterService
    {
        /// <summary>
        /// 選択可能な市区町村一覧（tax_masterテーブルから取得）
        /// </summary>
        public static string[] Municipalities
        {
            get
            {
                var list = DatabaseService.GetMunicipalities();
                return list.Count > 0 ? list.ToArray() : new[] { "札幌市" };
            }
        }

        /// <summary>
        /// 市区町村と宿泊料金（1人1泊）から道＋市区町村合計の宿泊税額を返す。
        /// 該当なしの場合は 0 を返す。
        /// </summary>
        public static decimal GetTaxPerPersonPerNight(string municipality, decimal roomRatePerPerson)
        {
            var rates = DatabaseService.GetTaxRates();
            var roomRate = (int)roomRatePerPerson;
            foreach (var r in rates)
            {
                if (r.Municipality == municipality
                    && roomRate >= r.FromAmount
                    && (!r.ToAmount.HasValue || roomRate <= r.ToAmount.Value))
                    return r.TaxAmount;
            }
            return 0m;
        }
    }
}

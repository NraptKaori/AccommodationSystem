namespace AccommodationSystem.Services
{
    /// <summary>
    /// 北海道の市区町村別宿泊税マスタ（道税＋市町村税の合計、1人1泊）
    /// 出典: 北海道宿泊税制度
    /// </summary>
    public static class TaxMasterService
    {
        // (市区町村名, 宿泊料金の下限, 宿泊料金の上限, 合計税率/人/泊)
        // 上限が decimal.MaxValue の場合は「以上」を意味する
        private static readonly (string Name, decimal From, decimal To, decimal Tax)[] TaxTable =
        {
            // 札幌市（道税100円 + 市税）
            ("札幌市",        0m,      19999.99m,   300m),  // 2万円未満
            ("札幌市",    20000m,      49999.99m,   400m),  // 2万円以上5万円未満
            ("札幌市",    50000m, decimal.MaxValue, 1000m), // 5万円以上

            // 小樽市（道税100円 + 市税）
            ("小樽市",        0m,      19999.99m,   300m),  // 2万円未満
            ("小樽市",    20000m,      49999.99m,   400m),  // 2万円以上5万円未満
            ("小樽市",    50000m, decimal.MaxValue,  700m), // 5万円以上

            // ニセコ町（道税100円 + 町税）
            ("ニセコ町",      0m,       5000m,       200m), // 5,000円以下
            ("ニセコ町",   5001m,      19999.99m,   300m),  // 5,001円〜2万円未満
            ("ニセコ町",  20000m,      49999.99m,   700m),  // 2万円以上5万円未満
            ("ニセコ町",  50000m,      99999.99m,  1500m),  // 5万円以上10万円未満
            ("ニセコ町", 100000m, decimal.MaxValue, 2500m), // 10万円以上

            // 留寿都村（道税100円 + 村税）
            ("留寿都村",      0m,      19999.99m,   200m),  // 2万円未満
            ("留寿都村",  20000m,      49999.99m,   400m),  // 2万円以上5万円未満
            ("留寿都村",  50000m, decimal.MaxValue, 1000m), // 5万円以上

            // 赤井川村（道税100円 + 村税）
            ("赤井川村",      0m,       7999.99m,   100m),  // 8,000円未満（道税のみ）
            ("赤井川村",   8000m,      19999.99m,   300m),  // 8,000円以上2万円未満
            ("赤井川村",  20000m,      49999.99m,   700m),  // 2万円以上5万円未満
            ("赤井川村",  50000m, decimal.MaxValue, 1000m), // 5万円以上

            // 洞爺湖町（道税100円 + 町税、導入予定）
            ("洞爺湖町",      0m,      19999.99m,   300m),  // 2万円未満
            ("洞爺湖町",  20000m,      49999.99m,   700m),  // 2万円以上5万円未満
            ("洞爺湖町",  50000m, decimal.MaxValue, 1500m), // 5万円以上

            // 函館市（道税100円 + 市税）
            ("函館市",        0m,      19999.99m,   200m),  // 2万円未満
            ("函館市",    20000m,      49999.99m,   400m),  // 2万円以上5万円未満
            ("函館市",    50000m,      99999.99m,  1000m),  // 5万円以上10万円未満
            ("函館市",   100000m, decimal.MaxValue, 2500m), // 10万円以上
        };

        /// <summary>
        /// 選択可能な市区町村一覧
        /// </summary>
        public static readonly string[] Municipalities =
        {
            "札幌市", "小樽市", "ニセコ町", "留寿都村", "赤井川村", "洞爺湖町", "函館市"
        };

        /// <summary>
        /// 市区町村と宿泊料金（1人1泊）から道+市町村合計の宿泊税率を返す。
        /// 該当なしの場合は 0 を返す。
        /// </summary>
        public static decimal GetTaxPerPersonPerNight(string municipality, decimal roomRatePerPerson)
        {
            foreach (var (name, from, to, tax) in TaxTable)
                if (name == municipality && roomRatePerPerson >= from && roomRatePerPerson <= to)
                    return tax;
            return 0m;
        }
    }
}

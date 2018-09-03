using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.ExchangeRate.FcbktwtpExchange
{
    public class FcbktwtpRateObject
    {
        //匯率國家(中文)
        public string Currency { get; set; }

        //匯率代碼
        public string CurrencyCode { get; set; }

        //現今買入價
        public decimal? CashBuying { get; set; }

        //現金賣出價
        public decimal? CashSelling { get; set; }

        //及時買入價
        public decimal? SpotBuying { get; set; }

        //及時賣出價
        public decimal? SpotSelling { get; set; }
    }
}

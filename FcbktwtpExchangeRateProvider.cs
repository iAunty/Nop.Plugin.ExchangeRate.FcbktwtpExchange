using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Xml;
using HtmlAgilityPack;
using Nop.Core;
using Nop.Core.Plugins;
using Nop.Plugin.ExchangeRate.FcbktwtpExchange;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;

namespace Nop.Plugin.ExchangeRate.EcbExchange
{
    public class FcbktwtpExchangeRateProvider : BasePlugin, IExchangeRateProvider
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public FcbktwtpExchangeRateProvider(ILocalizationService localizationService,
            ILogger logger)
        {
            this._localizationService = localizationService;
            this._logger = logger;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets currency live rates
        /// Rate Url is : https://ibank.firstbank.com.tw/NetBank/7/0201.html?sh=none
        /// </summary>
        /// <param name="exchangeRateCurrencyCode">Exchange rate currency code</param>
        /// <returns>Exchange rates</returns>
        public IList<Core.Domain.Directory.ExchangeRate> GetCurrencyLiveRates(string exchangeRateCurrencyCode)
        {
            if (exchangeRateCurrencyCode == null)
                throw new ArgumentNullException(nameof(exchangeRateCurrencyCode));

            //add euro with rate 1
            var ratesToTwd = new List<Core.Domain.Directory.ExchangeRate>
            {
                new Core.Domain.Directory.ExchangeRate
                {
                    CurrencyCode = "TWD",
                    Rate = 1,
                    UpdatedOn = DateTime.UtcNow
                }
            };

            var currencyList = new HtmlWeb().Load("https://ibank.firstbank.com.tw/NetBank/7/0201.html?sh=none")
                .DocumentNode.SelectNodes("//table[@id='table1']//tr");
            var rateResult = new List<FcbktwtpRateObject>();
            //第一銀行的第1個tr是title,略過不做。
            for (int i = 1; i < currencyList.Count; i++)
            {
                string cashBuying = "-";
                string cashSelling = "-";
                string spotBuying = "-";
                string spotSelling = "-";

                //小發現，這裡的編排是即期先，再來才是現金。
                //這是匯率一定會有即期，現金則不一定。
                string currency = currencyList[i].SelectSingleNode("td[1]").InnerText.Trim().Replace("&nbsp;", String.Empty);
                string nextCurrency;
                if (i + 1 == currencyList.Count)
                {
                    nextCurrency = null;
                }
                else
                {
                    nextCurrency = currencyList[i + 1].SelectSingleNode("td[1]").InnerText.Trim().Replace("&nbsp;", String.Empty); //下一個tr的幣種
                }
                
                //如果下一行也是該幣種，代表有現金和即期匯率。
                //沒有的話只有即期匯率。
                if (currency == nextCurrency)
                {
                    spotBuying = currencyList[i].SelectSingleNode("td[3]").InnerText.Trim();
                    spotSelling = currencyList[i].SelectSingleNode("td[4]").InnerText.Trim();
                    cashBuying = currencyList[i+1].SelectSingleNode("td[3]").InnerText.Trim();
                    cashSelling = currencyList[i+1].SelectSingleNode("td[4]").InnerText.Trim();
                    i++;
                }
                else
                {
                    spotBuying = currencyList[i].SelectSingleNode("td[3]").InnerText.Trim();
                    spotSelling = currencyList[i].SelectSingleNode("td[4]").InnerText.Trim();
                }

                string[] splitCurrencyString = null;
                splitCurrencyString = currency.Split("(");
                currency = splitCurrencyString[0];
                string currencyCode;
                if (splitCurrencyString.Length == 1) currencyCode = currency;
                else currencyCode= splitCurrencyString[1].Replace(")", String.Empty);

                rateResult.Add(new FcbktwtpRateObject
                {
                    Currency = currency,
                    CurrencyCode = currencyCode,
                    CashBuying = (!cashBuying.Contains("-")) ? Convert.ToDecimal(cashBuying) : new decimal?(),
                    CashSelling = (!cashSelling.Contains("-")) ? Convert.ToDecimal(cashSelling) : new decimal?(),
                    SpotBuying = (!spotBuying.Contains("-")) ? Convert.ToDecimal(spotBuying) : new decimal?(),
                    SpotSelling = (!spotSelling.Contains("-")) ? Convert.ToDecimal(spotSelling) : new decimal?()
                });
            }

            //converr rate to nop currency rate object
            foreach(var rate in rateResult)
            { 
                var averageRate = rate.SpotBuying!=null
                    ? (1 / (( rate.SpotBuying.Value + rate.SpotSelling.Value ) / 2))
                    : (1 / (( rate.CashBuying.Value + rate.CashBuying.Value ) / 2));

                ratesToTwd.Add(new Core.Domain.Directory.ExchangeRate
                { 
                    CurrencyCode = rate.CurrencyCode,
                    Rate = averageRate,
                    UpdatedOn = DateTime.UtcNow,
                });
            }

            //return result for the euro
            if (exchangeRateCurrencyCode.Equals("twd", StringComparison.InvariantCultureIgnoreCase))
                return ratesToTwd;

            //use only currencies that are supported by FCBKTWTP
            var exchangeRateCurrency = ratesToTwd.FirstOrDefault(rate => rate.CurrencyCode.Equals(exchangeRateCurrencyCode, StringComparison.InvariantCultureIgnoreCase));
            if (exchangeRateCurrency == null)
                throw new NopException(_localizationService.GetResource("Plugins.ExchangeRate.FcbktwtpExchange.Error"));

            //return result for the selected (not euro) currency
            return ratesToTwd.Select(rate => new Core.Domain.Directory.ExchangeRate
            {
                CurrencyCode = rate.CurrencyCode,
                Rate = Math.Round(rate.Rate / exchangeRateCurrency.Rate, 4),
                UpdatedOn = rate.UpdatedOn
            }).ToList();
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.ExchangeRate.FcbktwtpExchange.Error", "You can use FCBKTWTP (European central bank) exchange rate provider only when the primary exchange rate currency is supported by FCBKTWTP");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.ExchangeRate.FcbktwtpExchange.Error");

            base.Uninstall();
        }

        #endregion

    }
}
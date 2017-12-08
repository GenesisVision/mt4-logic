using System;
using System.Linq;
using DataModel;
using StatisticService.Entities;
using StatisticService.Entities.Enums;

namespace StatisticService.Logic.Models
{
	public class SymbolDayStatistic
	{
		public DateTime AddingDate { get; set; }
		public string Symbol { get; set; }
		public BarType BarType { get; set; }
		public DateTime DateTime { get; set; }
		public double PercentSymbolToTotalTrades { get; set; }
		public double PercentBuyToSellVolume { get; set; }
		public double PercentSymbolToTotalVolume { get; set; }
		public double PercentSymbolToTotalDeferredTrades { get; set; }
		public double PercentBuyToBuySellVolume { get; set; }
		public int CountTrades { get; set; }
		public int BuyVolume { get; set; }
		public int SellVolume { get; set; }
		public int CountDeferredTrades { get; set; }
		public double AverageBuyPrice { get; set; }
		public double AverageSellPrice { get; set; }
		public double AverageBuyLimitPrice { get; set; }
		public double AverageBuyStopPrice { get; set; }
		public double AverageSellLimitPrice { get; set; }
		public double AverageSellStopPrice { get; set; }
		public double AveragePrice { get; set; }
	}
}

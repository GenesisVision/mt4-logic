using System;
using StatisticService.Entities;
using StatisticService.Entities.Enums;

namespace StatisticService.Logic.Models
{
	public class SymbolsTotalDayStatistic
	{
		public BarType BarType { get; set; }
		public DateTime DateTime { get; set; }
		public int TotalCountTrades { get; set; }
		public int TotalVolume { get; set; }
		public int TotalCountDeferredTrades { get; set; }
	}
}

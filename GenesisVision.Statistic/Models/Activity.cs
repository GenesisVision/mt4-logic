using System;

namespace StatisticService.Logic.Models
{
	public class Activity
	{
		public long TradingAccountId { get; set; }
		public string Nickname { get; set; }
		public OrderAction OrderAction { get; set; }
		public OrderType OrderType { get; set; }
		public string Symbol { get; set; }
		public double Price { get; set; }
		public double Profit { get; set; }
		public DateTime Time { get; set; }
		public string Country { get; set; }
		public string Avatar { get; set; }
	}
}

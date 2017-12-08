using System;

namespace TournamentService.Logic.Interfaces
{
	public class TournamentActivity
	{
		public long TradingAccountId { get; set; }
		public string Nickname { get; set; }
		public string OrderType { get; set; }
		public string Symbol { get; set; }
		public double Price { get; set; }
		public double Profit { get; set; }
		public DateTime Time { get; set; }
		public string Country { get; set; }
	}
}

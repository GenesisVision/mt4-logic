using System.Collections.Generic;

namespace StatisticService.Logic.Models
{
	public class Trader
	{
		public long AccountId { get; set; }

		public int Login { get; set; }

		public string Country { get; set; }

		public string AccountNickname { get; set; }

		public float Profit { get; set; }

		public float ProfitLastDay { get; set; }

		public int Age { get; set; }

		public int ProfitOrders { get; set; }

		public int LoseOrders { get; set; }

		public float Volatility { get; set; }

		public float MaxDailyDrowdown { get; set; }

		public string Avatar { get; set; }

		public float? Rating { get; set; }

		public string ClientNickname { get; set; }

		public string Currency { get; set; }

		public int AccountsNumber { get; set; }

		public List<double> Chart { get; set; }
	}
}


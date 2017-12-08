namespace StatisticService.Logic.Models
{
	public class BasicStatistic
	{
		public long AccountId { get; set; }

		public decimal ProfitPoints { get; set; }

		public decimal Profit { get; set; }

		public int ProfitOrders { get; set; }

		public int LoseOrders { get; set; }

		public decimal Volatility { get; set; }

		public float MaxDailyDrowdown { get; set; }
	}
}
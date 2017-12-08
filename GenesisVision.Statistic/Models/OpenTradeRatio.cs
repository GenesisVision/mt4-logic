namespace StatisticService.Logic.Models
{
	public class OpenTradeRatio
	{
		public string Symbol { get; set; }
		public int SellRatio { get; set; }
		public int BuyRatio { get; set; }
		public int Volume { get; set; }
	}
}

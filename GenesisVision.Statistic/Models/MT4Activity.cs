using DataModel;

namespace StatisticService.Logic.Models
{
	public class MT4Activity
	{
		public mt4_trades Mt4Trades { get; set; }
		public OrderAction OrderAction { get; set; }
	}
}

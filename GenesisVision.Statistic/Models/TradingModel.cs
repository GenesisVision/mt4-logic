using DataModel;

namespace StatisticService.Logic.Models
{
	public class TradingModel
	{
		public mt4_accounts Account { get; set; }

		public trading_servers Server { get; set; }
	}
}

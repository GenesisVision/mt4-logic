using System.Collections.Generic;
using SignalService.AccountService;

namespace SignalService.Models
{
	public class SubscriberOrders
	{
		public long MasterId { get; set; }

		public string MasterNickname { get; set; }

		public string MasterAvatar { get; set; }

		public decimal TotalProfit { get; set; }

		public List<Trade> OpenedOrders { get; set; }
	}
}

using DataModel;

namespace SignalService.Models.Repository
{
	public class SubscriptionStateModel
	{
		public bool IsSubscribed { get; set; }
		
		public signal_providers Provider { get; set; }
	}
}
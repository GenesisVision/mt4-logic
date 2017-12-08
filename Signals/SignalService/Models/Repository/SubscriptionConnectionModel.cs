using DataModel;

namespace SignalService.Models.Repository
{
	public class SubscriptionConnectionModel
	{
		public signal_providers Provider { get; set; }
 		public signal_subscribers Subscriber { get; set; }
	}
}
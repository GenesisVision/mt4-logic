using DataModel;

namespace SignalService.Models.Repository
{
	public class SubscriberWithSettingsModel
	{
		public signal_subscribers Subscriber { get; set; }

		public signal_subscriptions Subscription { get; set; }
	}
}

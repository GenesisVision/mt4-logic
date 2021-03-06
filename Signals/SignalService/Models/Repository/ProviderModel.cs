﻿using DataModel;

namespace SignalService.Models.Repository
{
	public class ProviderModel
	{
		public signal_subscribers Subscriber { get; set; }

		public signal_subscriptions Subscription { get; set; }

		public signal_providers Provider { get; set; }
	}
}
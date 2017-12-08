using System;
using System.Runtime.Serialization;
using DataModel;

namespace SignalService.Models
{
	[DataContract, KnownType(typeof(ISubscribeSettings))]
	public class SubscriptionSettings : ISubscribeSettings
	{
		public SubscriptionSettings(signal_subscriptions sub)
		{
			Status = sub.status;
			SignalType = sub.subscription_type;
			Multiplier = sub.multiplier;
			Reverse = sub.reverse;
			Risk = sub.risk;
			MaxOrderCount = sub.max_order_count;
			MaxVolume = sub.max_volume;
			OrdersType = sub.order_type;
		}

		[DataMember]
		public int Status { get; set; }
		[DataMember]
		public int SignalType { get; set; }
		[DataMember]
		public double Multiplier { get; set; }
	    [DataMember]
		public Boolean Reverse { get; set; }
		[DataMember]
		public double Risk { get; set; }
		[DataMember]
		public int MaxOrderCount { get; set; }
		[DataMember]
		public int MaxVolume { get; set; }
		[DataMember]
		public int OrdersType { get; set; }
		[DataMember]
		public int Volume { get; set; }
	}

	public interface ISubscribeSettings
	{
		int Status { get; set; }
		int SignalType { get; set; }
		double Multiplier { get; set; }
		bool Reverse { get; set; }
		double Risk { get; set; }
		int MaxOrderCount { get; set; }
		int MaxVolume { get; set; }
		int OrdersType { get; set; }
	}

}

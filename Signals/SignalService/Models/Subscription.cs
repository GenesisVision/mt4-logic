using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SignalService.Models
{
	public class Subscription
	{
		[DataMember]
		public long AccountId { get; set; }

		[DataMember]
		public int SubscribersCount { get; set; }

		[DataMember]
		public decimal Profit { get; set; }
		
		[DataMember]
		public List<AccountConnection> Providers { get; set; }
	}
}
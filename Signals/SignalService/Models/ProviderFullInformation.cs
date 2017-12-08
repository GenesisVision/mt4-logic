using System.Collections.Generic;
using System.Runtime.Serialization;
using SignalService.AccountService;

namespace SignalService.Models
{
	[DataContract]
	public class ProviderFullInformation
	{
		public ProviderFullInformation()
		{
			Subscribers = new List<SubscriberData>();
		}
		[DataMember]
		public long AccountId { get; set; }

		[DataMember]
		public int Login { get; set; }

		[DataMember]
		public string Avatar { get; set; }

		[DataMember]
		public AccountType AccountType { get; set; }

		[DataMember]
		public decimal Balance { get; set; }

		[DataMember]
		public decimal Equity { get; set; }

		[DataMember]
		public int Leverage { get; set; }

		[DataMember]
		public decimal Profit { get; set; }

		[DataMember]
		public string Currency { get; set; }

		[DataMember]
		public int WorkingDays { get; set; }

		[DataMember]
		public string Nickname { get; set; }

		[DataMember]
		public long ClientId { get; set; }

		[DataMember]
		public bool IsVisible { get; set; }

		[DataMember]
		public float RatingValue { get; set; }

		[DataMember]
		public List<SubscriberData> Subscribers { get; set; }
	}
}

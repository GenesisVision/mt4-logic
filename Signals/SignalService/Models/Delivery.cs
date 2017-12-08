using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SignalService.Models
{
	[DataContract]
	public class Delivery
	{
		[DataMember]
		public long AccountId { get; set; }

		[DataMember]
		public int Login { get; set; }

		[DataMember]
		public string Avatar { get; set; }

		[DataMember]
		public string AccountType { get; set; }

		[DataMember]
		public decimal Balance { get; set; }

		[DataMember]
		public decimal Equity { get; set; }

		[DataMember]
		public int SubscribersCount { get; set; }

		[DataMember]
		public decimal Commission { get; set; }

		[DataMember]
		public int Leverage { get; set; }

		[DataMember]
		public string Currency { get; set; }

		[DataMember]
		public decimal Profit { get; set; }

		[DataMember]
		public int WorkingDays { get; set; }

		[DataMember]
		public string Nickname { get; set; }

		[DataMember]
		public decimal CashFounds { get; set; }

		[DataMember]
		public decimal Procent { get; set; }

		[DataMember]
		public bool IsVisible { get; set; }

		[DataMember]
		public List<AccountConnection> Subscribers { get; set; }

		[DataMember]
		public float? RatingValue { get; set; }

		[DataMember]
		public int? RatingCount { get; set; }
	}
}

using System.Runtime.Serialization;

namespace SignalService.Models
{
	public class SubscriptionData
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
		public int Leverage { get; set; }

		[DataMember]
		public decimal Profit { get; set; }

		[DataMember]
		public string Currency { get; set; }

		[DataMember]
		public int WorkingDays { get; set; }

		[DataMember]
		public decimal Procent { get; set; }

		[DataMember]
		public string Nickname { get; set; }

		[DataMember]
		public long ClientId { get; set; }

		[DataMember]
		public string ProviderNickname { get; set; }
	}
}
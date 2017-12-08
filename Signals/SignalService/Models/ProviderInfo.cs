using System.Runtime.Serialization;

namespace SignalService.Models
{
	[DataContract]
	public class ProviderInfo
	{
		[DataMember]
		public long AccountId { get; set; }

		[DataMember]
		public string Nickname { get; set; }

		[DataMember]
		public string Avatar { get; set; }

		[DataMember]
		public decimal Profit { get; set; }
	}
}

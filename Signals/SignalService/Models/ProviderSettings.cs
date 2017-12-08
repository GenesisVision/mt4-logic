using System.Runtime.Serialization;

namespace SignalService.Models
{
	public class ProviderSettings
	{
		[DataMember]
		public int Login { get; set; }

		[DataMember]
		public string ServerName { get; set; }

		[DataMember]
		public string Nickname { get; set; }

		[DataMember]
		public bool CloseOnly { get; set; }

		[DataMember]
		public bool Reverse { get; set; }
		
		[DataMember]
		public short Status { get; set; }

		[DataMember]
		public SubscriptionSettings Settings { get; set; }
	}
}

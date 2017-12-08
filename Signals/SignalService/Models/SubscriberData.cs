using System.Runtime.Serialization;

namespace SignalService.Models
{
	[DataContract]
	public class SubscriberData
	{
		[DataMember]
		public long ClientId { get; set; }
		
		[DataMember]
		public string Avatar { get; set; }
	
		[DataMember]
		public string Nickname { get; set; }

		[DataMember]
		public long AccountId { get; set; }
	}
}

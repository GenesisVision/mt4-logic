using System;
using System.Runtime.Serialization;
using SignalService.AccountService;

namespace SignalService.Models
{
	[DataContract]
	public class Provider
	{
		[DataMember]
		public Int64 Id { get; set; }

		[DataMember]
		public int Login { get; set; }

		[DataMember]
		public String Nickname { get; set; }

		[DataMember]
		public Boolean IsSubscribe { get; set; }

		[DataMember]
		public long AccountId { get; set; }

		[DataMember]
		public AccountType AccountType { get; set; }
	}
}

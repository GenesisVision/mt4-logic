using System.Runtime.Serialization;

namespace TournamentService.Logic.Models
{
	[DataContract]
	public class TournamentAccount
	{
		[DataMember]
		public long Id { get; set; }
		[DataMember]
		public long AccountType { get; set; }
		[DataMember]
		public string Avatar { get; set; }
		[DataMember]
		public long ClientAccountId { get; set; }
		[DataMember]
		public string Nickname { get; set; }
		[DataMember]
		public int Login { get; set; }
	}
}

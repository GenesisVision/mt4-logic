using System.Runtime.Serialization;

namespace TournamentService.Logic.Models
{
	[DataContract]
	public class ParticipateAccount
	{
		[DataMember]
		public string ServerName { get; set; }
		
		[DataMember]
		public int Login { get; set; }
		
		[DataMember]
		public string Password { get; set; }

	}
}

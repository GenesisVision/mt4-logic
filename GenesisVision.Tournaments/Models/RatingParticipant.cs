using System.Runtime.Serialization;

namespace TournamentService.Logic.Models
{
	[DataContract]
	public class RatingParticipant
	{
		[DataMember]
		public long RoundParticipantId { get; set; }

		[DataMember]
		public long ParticipantId { get; set; }

		[DataMember]
		public long Login { get; set; }
		
		[DataMember]
		public int Position { get; set; }
		
		[DataMember]
		public decimal Profit { get; set; }

		[DataMember]
		public decimal Volume { get; set; }

		[DataMember]
		public int Pips { get; set; }

		[DataMember]
		public string Avatar { get; set; }
		
		[DataMember]
		public string Nickname { get; set; }

		[DataMember]
		public decimal Prize { get; set; }

		[DataMember]
		public long ClientId { get; set; }

		[DataMember]
		public bool IsBanned { get; set; }
	}
}

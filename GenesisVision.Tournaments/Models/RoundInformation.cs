using System.Collections.Generic;

namespace TournamentService.Logic.Models
{
	public class RoundInformation
	{
		public RoundInformation()
		{
			Participants = new List<RatingParticipant>();
		}
		public RoundData Round { get; set; }
		public List<RatingParticipant> Participants { get; set; }
	}
}
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TournamentService.Logic.Models
{
	[DataContract]
	public class TournamentFullInformation
	{
		public TournamentFullInformation()
		{
			Rounds = new List<RoundInformation>();
		}

		[DataMember]
		public TournamentInformation TournamentInfo { get; set; }

		[DataMember]
		public List<RoundInformation> Rounds { get; set; }
	}
}

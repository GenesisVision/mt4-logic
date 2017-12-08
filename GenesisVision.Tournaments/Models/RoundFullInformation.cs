using System.Runtime.Serialization;

namespace TournamentService.Logic.Models
{
	[DataContract]
	public class RoundFullInformation
	{
		[DataMember]
		public TournamentInformation TournamentInfo { get; set; }

		[DataMember]
		public RoundInformation RoundInformation { get; set; }
	}
}
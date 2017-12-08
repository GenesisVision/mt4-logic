using System.Runtime.Serialization;

namespace TournamentService.Logic.Models
{
	[DataContract]
	public class RatingPrice
	{
		[DataMember]
		public long Id { get; set; }

		[DataMember]
		public short Rank { get; set; }

		[DataMember]
		public decimal Award { get; set; }
	}
}
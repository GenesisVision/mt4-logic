namespace TournamentService.Logic.Models
{
	public class RatingUpdate
	{
		public long TounamentParticipantId { get; set; }
		public decimal Profit { get; set; }
		public int Position { get; set; }
		public int Points { get; set; }
		public decimal Volume { get; set; }
	}
}

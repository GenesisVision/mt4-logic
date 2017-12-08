
namespace TournamentService.Logic.Models
{
	public class Pass
	{
		public long Id { get; set; }

		public string TournamentId { get; set; }

		public string TournamentName { get; set; }

		public long? RoundNumber { get; set; }
	}
}

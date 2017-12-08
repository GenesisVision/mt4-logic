using DataModel;

namespace TournamentService.Logic.Models.Repository
{
	public class TournamentRoundModel
	{
		public tournament_rounds Round { get; set; }

		public tournament Tournament { get; set; }

		public long AccountType { get; set; }
	}
}
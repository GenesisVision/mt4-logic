using DataModel;

namespace TournamentService.Logic.Models.Repository
{
	public class TournamentLightModel
	{
		public tournament Tournament { get; set; }

		public tournament_types Type { get; set; }
	}
}
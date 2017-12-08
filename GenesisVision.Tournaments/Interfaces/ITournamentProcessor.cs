using DataModel;

namespace TournamentService.Logic.Interfaces
{
	public interface ITournamentProcessor
	{
		void StartRound(tournament_rounds round);
		void FinishRound(tournament_rounds round);
		void GenerateRounds(string tournamentId);
	}
}
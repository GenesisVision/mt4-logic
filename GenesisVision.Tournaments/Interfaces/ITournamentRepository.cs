using System;
using System.Collections.Generic;
using DataModel;
using TournamentService.Logic.Models;
using TournamentService.Logic.Models.Repository;

namespace TournamentService.Logic.Interfaces
{
	public interface ITournamentRepository
	{
		void AddParticipant(ParticipantModel participant);

		/// <summary>
		/// Get rounds to start. Time is shifted by 5 seconds
		/// </summary>
		tournament_rounds[] GetRoundsToStart(DateTime beginDate);

		void AddRoundParticipation(long participantId, long roundId, long walletId);

		void DeleteAccountFromRound(long participantId, long roundId);

		TournamentRoundModel GetRound(long roundId);

		/// <summary>
		/// Return collection of all tournaments
		/// </summary>
		/// <returns></returns>
		Tournament[] GetAllTournaments();

		RatingParticipant[] GetRaitingParticipants(long roundId);

		MyTournament[] GetClientMyTournaments(long clientId);
		/// <summary>
		/// Get rounds to start. Time is shifted by 5 seconds
		/// </summary>
		tournament_rounds[] GetRoundsToStop(DateTime endDate);


		/// <summary>
		/// Get participants for round
		/// </summary>
		/// <param name="roundId">Round Id</param>
		/// <param name="clientId">Client Id</param>
		/// <returns></returns>
		bool CheckRoundParticipation(long roundId, long clientId);

		bool CheckSpecialTournamentParticipation(long clientId, string tournamentId);

		tournament_round_participants[] GetRoundParticipants(long roundId);

		long[] GetRoundParticipantIds(long roundId);

		/// <summary>
		/// Get tournament participants for round
		/// </summary>
		/// <param name="roundId"></param>
		/// <returns></returns>
		tournament_participants[] GetTournamentParticipants(long roundId);

		/// <summary>
		/// Set field isStarted to TRUE
		/// </summary>
		/// <param name="round"></param>
		void StartNormalRound(long round);
        
		/// <summary>
		/// Add round
		/// </summary>
		/// <param name="round"></param>
		void AddRound(tournament_rounds round);

		/// <summary>
		/// Get tournament by id
		/// </summary>
		/// <param name="tournamentId"></param>
		tournament GetTournament(string tournamentId);

		/// <summary>
		/// Get count of planned tournaments
		/// </summary>
		/// <param name="tournamentId"></param>
		/// <returns></returns>
		tournament_rounds[] GetPlannedRounds(string tournamentId);

		/// <summary>
		/// Get last round for tournament
		/// </summary>
		/// <param name="tournamentId"></param>
		/// <returns></returns>
		tournament_rounds GetLastRound(string tournamentId);

		/// <summary>
		/// Update participants
		/// </summary>
		/// <param name="ratingUpdates"></param>
		void UpdateRating(RatingUpdate[] ratingUpdates, long roundId);

		/// <summary>
		/// Get awards for participants
		/// </summary>
		/// <param name="id"></param>
		tournament_rating_price[] GetAwards(long roundId);

		/// <summary>
		/// Ban round participant
		/// </summary>
		/// <param name="id">Id</param>
		void BanRoundParticipant(long id);

		void ChangeAccountVisibility(long accountId, bool value);

		void DeleteAccount(long accountId);

		void DeleteAccounts(long[] accountIds);

		tournament_participants GetParticipant(long accountId);
		
		WinnersData[] GetWinners(int number);

		void UpdateRealPrize(long roundParticipantId, decimal prize);

		void ChangeAvatar(long accountId, string newAvatar);

		void ChangeDescription(long accountId, string newDescription);

		TournamentRound[] GetTournamentTypeRoundList(int tournamentTypeId, int index, int count, long? lastRoundId);

		TournamentRound[] GetTournamentRoundList(string tournamentId, int index, int count, long? lastRoundId);

		TournamentType[] GetTournamentTypes();

		/// <summary>
		/// Get tournaments list for administration 
		/// </summary>
		/// <returns></returns>
		TournamentLightModel[] GetTournamentList();

		TournamentLightModel GetTournamentCommonInformation(string tournamentId);

		InformationModel[] GetTournamentInformation(string tournamentId);

		void AddTournament(TournamentInformation tournamentAddData);

		int AddTournamentType(string name);

		void DeleteTournament(string tournamentId);

		void DeleteTournamentType(long typeId);

		TournamentFullInformation GetTournamentFullInformation(string tournamentName);

		void EditTournament(TournamentInformation tournamentEditData);

		void EditTournamentType(long typeId, string name);

		/// <summary>
		/// Get round full information
		/// </summary>
		/// <param name="roundId">Round id</param>
		/// <returns></returns>
		RoundFullInformation GetRoundInformation(long roundId);

		/// <summary>
		/// Get active rounds
		/// </summary>
		/// <returns></returns>
		tournament_rounds[] GetActiveRounds();

		/// <summary>
		/// Get account for joining in round
		/// </summary>
		List<TournamentAccount> GetAccountsForRound(long clientId, long roundId);

		/// <summary>
		/// Finish round
		/// </summary>
		/// <param name="roundId"></param>
		void FinishRound(int roundId);

		tournament_passes[] GetClientTournamentPasses(long clientId);

		void UseTournamentClientPass(long passId);

		long AddTournamentClientPass(long clientId, long passId, DateTime? expiryDate);
		
		tournament_rounds[] GetRoundsToWarn(DateTime beginDate, int interval);

		tournament_client_passes[] GetPassesForRound(string tournamentId, long number);

		tournament_rounds[] GetRoundsForAccount(long accountId);

		TournamentTabInfo[] GetAccountActiveRounds(long accountId);

		TournamentTabInfo[] GetAccountFinishedRounds(long accountId, int index, int count);

		TournamentRoundModel GetNearestRound(string tournamentId);

		/// <summary>
		/// Get participant's ids of tournaments with visible orders
		/// </summary>
		/// <returns></returns>
		long[] GetVisibleTournamentsAccountIds();

		/// <summary>
		/// Check if cleint has prize in given tournament
		/// </summary>
		/// <param name="clientId"></param>
		/// <param name="tournamentId"></param>
		/// <returns></returns>
		bool CheckClientTournamentPrizes(long clientId, string tournamentId);

		/// <summary>
		/// Remove all client's registarions on future rounds of given tournament
		/// </summary>
		/// <param name="clientId"></param>
		/// <param name="tournamentId"></param>
		void BanClientInFutureTournament(long clientId, string tournamentId);

		/// <summary>
		/// Remove all client's registarions on future rounds of given tournament type
		/// </summary>
		/// <param name="clientId"></param>
		/// <param name="tournamentTypeId"></param>
		void BanClientInFutureTournamentType(long clientId, short tournamentTypeId);

		/// <summary>
		/// Check if client was banned
		/// </summary>
		/// <param name="clientId"></param>
		/// <param name="tournamentTypeId"></param>
		/// <returns></returns>
		bool CheckClientBans(long clientId, int tournamentTypeId);

		MyTournament[] GetClientsTournamentsAdmin(long[] clientsId, DateTime? fromDate, DateTime? toDate);

		long[] GetAllActiveParticipants();

		long[] GetActiveAccounts();

		long GetParticipantAccountId(long roundParticipantId);

		Pass[] GetAllTournamentsPasses();

		tournament_passes GetTournamentPass(long id);

		ClientPass[] GetClientPasses(long clientId);
	}
}

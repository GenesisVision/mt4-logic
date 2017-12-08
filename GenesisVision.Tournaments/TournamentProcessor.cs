using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataModel;
using FluentScheduler;
using Helpers;
using TournamentService.Entities.Enums;
using TournamentService.Logic.AccountService;
using TournamentService.Logic.ContentServiceReference;
using TournamentService.Logic.Helpers;
using TournamentService.Logic.Interfaces;
using TournamentService.Logic.Models;
using TournamentService.Logic.StatisticServiceReference;

namespace TournamentService.Logic
{
	public class TournamentProcessor : ITournamentProcessor
	{
		#region Fields

		private readonly ITournamentRepository repository;
		private readonly IAccountService accountService;
		private readonly IClientService clientService;
		private readonly IStatisticService statisticService;
		private readonly IContentService contentService;
		private readonly IMailingService mailingService;

		private const int plannedRoundCount = 3;
        
		private readonly DateTime maxDate = new DateTime(2100, 01, 01, 0, 0, 0);

		private const int sheduleRoundsStarterInterval = 60;	// Interval in seconds

		#endregion

		#region Construction

		public TournamentProcessor(ITournamentRepository repository, IAccountService accountService, IClientService clientService,
			IStatisticService statisticService, IContentService contentService, IMailingService mailingService, bool withTournamentHandlers = true)
		{
			this.repository = repository;
			this.accountService = accountService;
			this.clientService = clientService;
			this.statisticService = statisticService;
			this.contentService = contentService;
			this.mailingService = mailingService;
			this.commissionService = commissionService;

			GenerateRoundsForAllTournaments();

			if (withTournamentHandlers)
			{
				TaskManager.AddTask(TournamentsHandler, x => x.WithName("TournamentsHandler").ToRunEvery(sheduleRoundsStarterInterval).Seconds());
				TaskManager.AddTask(DailyDisableAccounts, x => x.WithName("DailyDisableAccounts").ToRunEvery(0).Days().At(22, 00));
				TaskManager.AddTask(DailyEnableAccounts, x => x.WithName("DailyEnableAccounts").ToRunEvery(0).Days().At(09, 00));
			}

			TournamentService.Logger.Trace("Tournament processor init");
		}

		#endregion

		#region Public methods

		public void StartRound(tournament_rounds round)
		{
			try
			{
				TournamentService.Logger.Info("Starting {0} (round {1}, id {2})...", round.tournament_id, round.number, round.id);

				var tournament = repository.GetTournament(round.tournament_id);
				if (tournament == null)
				{
					TournamentService.Logger.Error("Starting {0} (round {1}, id {2}). Tournament does not exist", round.tournament_id, round.number, round.id);
					return;
				}

				var participants = repository.GetRoundParticipants(round.id);
				TournamentService.Logger.Info("Starting {2} (round {3}, id {4}). Received {0} participants: {1}", participants.Count(),
					string.Join(", ", participants.Select(participant => participant.participant_id)), tournament.name, round.number, round.id);

				if (!CheckRoundForMinimalParticipants(round, participants, tournament))
					return;

				var activeParticipants = new ConcurrentBag<long>();
				var deletedParticipants = new ConcurrentBag<long>();
				Parallel.ForEach(participants, tournamentRoundParticipant =>
				{
					if (!CheckAccountExists(round, tournamentRoundParticipant, activeParticipants, tournament, deletedParticipants))
						return;

					if (!CheckAccountForOpenOrder(round, tournamentRoundParticipant, tournament, deletedParticipants))
						return;

					Mt4ActoinsAtStartRound(round, tournament, tournamentRoundParticipant, deletedParticipants);
				});

				if (!DeleteDeletedAccountsFromRound(round, deletedParticipants, activeParticipants, tournament))
					return;

				if (!AccrueDepositAtStartRound(round, activeParticipants, tournament))
					return;

                repository.StartNormalRound(round.id);

				GenerateRounds(round.tournament_id);

				TournamentService.Logger.Info("Starting {0} (round {1}, id {2}): done!", round.tournament_id, round.number, round.id);
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Fatal("Error at starting {0} (round {1}, id {2}): {3}",
					round.tournament_id, round.number, round.id, ex);
			}
		}

		public void FinishRound(tournament_rounds round)
		{
			try
			{
				TournamentService.Logger.Info("Finishing {0} (round {1}, id {2})...", round.tournament_id, round.number, round.id);

				repository.FinishRound(round.id);

				var tournament = repository.GetTournament(round.tournament_id);
				var participantIds = repository.GetRoundParticipantIds(round.id);

				if (participantIds.Any())
				{
					TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Received {3} participants",
						tournament.name, round.number, round.id, participantIds.Length);
					Mt4ActoinsAtFinishRound(participantIds, tournament, round);

					TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Construct rating...", tournament.name, round.number, round.id);
					ConstructRoundRating(round);

					TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Get awards...", tournament.name, round.number, round.id);
					var participants = repository.GetRoundParticipants(round.id);
					if (tournament.id_name == "")
						FinishSpecialRound(round, participants, tournament);
					else
						FinishNormalRound(round, tournament, participants);

					TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Withdraw money...", tournament.name, round.number, round.id);
					WithdrawMoneyAtFinishRound(round, tournament, participantIds);
				}

				TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}): done!", round.tournament_id, round.number, round.id);
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Fatal("Error at finishing {0} (round {1}, id {2}): {3}",
					round.tournament_id, round.number, round.id, ex);
			}
		}

		public void GenerateRounds(string tournamentId)
		{
			try
			{
				TournamentService.Logger.Trace("Generating rounds for tournament {0}...", tournamentId);

				var tournament = repository.GetTournament(tournamentId);
				if (tournament == null)
					throw new Exception(string.Format("Tournament id '{0}' does not exist", tournamentId));

				if (tournament.id_name == "")
					return;

				if (tournament.id_name == "")
					GenerateOnePerMonthRounds(tournament);
				else
					GenerateNormalRounds(tournament);

				TournamentService.Logger.Trace("Generating rounds for tournament {0} done!", tournamentId);
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Error at generating rounds for tournament {0}: {1}", tournamentId, ex);
			}
		}

		public void ConstructRoundRating(tournament_rounds round)
		{
			try
			{
				TournamentService.Logger.Trace("Construct rating for {0} (round {1}, id {2})...",
					round.tournament_id, round.number, round.id);

				var roundParticipants = repository.GetRoundParticipants(round.id);
				var accountIds = roundParticipants.Where(x => !x.isbanned).Select(x => x.participant_id).ToArray();
				var tournament = repository.GetTournament(round.tournament_id);

				if (tournament.id_name == "")
				{
					ConstructSpecialRatingProfit(round, accountIds);
				}
				else
				{
					switch ((TournamentCalculationType)tournament.calculation_type_id)
					{
						case TournamentCalculationType.Profit:
							ConstructRatingProfit(round, accountIds);
							break;
						case TournamentCalculationType.Pips:
							ConstructRatingPoints(round, accountIds);
							break;
						case TournamentCalculationType.Volume:
							ConstructRatingVolume(round, accountIds);
							break;
					}
				}

				TournamentService.Logger.Trace("Construct rating for {0} (round {1}, id {2}) done!",
					round.tournament_id, round.number, round.id);
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Error at construct rating for {0} (round {1}, id {2}): {3}",
					round.tournament_id, round.number, round.id, ex);
			}
		}

		public void WarnRound(tournament_rounds round)
		{
			try
			{
				TournamentService.Logger.Debug("Warn users about round starting in 1 day...");

				var passes = repository.GetPassesForRound(round.tournament_id, round.number);
				if (passes.Any())
				{
					var tournament = repository.GetTournament(round.tournament_id);
					var clients = clientService.GetClients(passes.Select(x => x.client_id).ToArray());
					if (!clients.IsSuccess)
						throw new Exception(clients.Error);

					foreach (var client in clients.Result)
					{
						//ToDo: get mail template from content service

						/*var clientName = string.Format("{0} {1}", client.FirstName, client.LastName);
						var language = LangHelper.GetLang(client.Country);

						var template = contentService.GetMessageTemplate(MessageTemplateType.TournamentWarning, language);
						template.Result = template.Result ?? new MessageTemplate();

						var body = MessageTemplateHelper.ReplaceKeys(
							template.Result.Template,
							template.Result.Template,
							new[] {KeyName, KeyTournament},
							new[] {clientName, tournament.name});*/

						var body = "Tournament, on which you have free pass is starting in 1 day";

						if (!string.IsNullOrEmpty(body))
						{
							var email = client.Email;
							Task.Factory.StartNew(() => mailingService.SendMail(email, "Tournament notification", body, body));
						}
					}
				}

				TournamentService.Logger.Debug("Warn users about round starting in 1 day done");
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Error at warn users about round starting in 1 day: {0}", ex.ToString());
			}
		}

		#endregion

		#region Tournaments handlers

		private void TournamentsHandler()
		{
			try
			{
				TournamentService.Logger.Trace("Tournaments processing started");
				var beginDate = DateTime.Now.Round(TimeSpan.FromMinutes(1));
				var endDate = DateTime.Now.AddSeconds(sheduleRoundsStarterInterval).Round(TimeSpan.FromMinutes(1));

				var roundsToStart = repository.GetRoundsToStart(beginDate);
				TournamentService.Logger.Trace("Received {0} rounds to start. {1}", roundsToStart.Length,
					string.Join(", ", roundsToStart.Select(x => string.Format("{0} (round {1})", x.tournament_id, x.id))));
				roundsToStart.AsParallel().ForAll(StartRound);

				var roundsToEnd = repository.GetRoundsToStop(endDate);
				TournamentService.Logger.Trace("Received {0} rounds to finish. {1}", roundsToEnd.Length,
					string.Join(", ", roundsToEnd.Select(x => string.Format("{0} (round {1})", x.tournament_id, x.id))));
				roundsToEnd.AsParallel().ForAll(FinishRound);

				//var roundsToWarn = repository.GetRoundsToWarn(beginDate.AddDays(1), sheduleRoundsStarterInterval);
				//TournamentService.Logger.Trace("Received {0} rounds to warn. {1}", roundsToWarn.Length,
				//	string.Join(", ", roundsToWarn.Select(x => string.Format("{0} (round {1})", x.tournament_id, x.id))));
				//roundsToWarn.AsParallel().ForAll(WarnRound);

				UpdateActiveTournamentsRating();
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Tournament handler error: {0}", ex.ToString());
			}
		}

		#endregion

		#region Rating

		private void UpdateActiveTournamentsRating()
		{
			try
			{
				TournamentService.Logger.Trace("Update active tournaments ratings...");

				foreach (var tournamentRound in repository.GetActiveRounds())
				{
					ConstructRoundRating(tournamentRound);
				}

				TournamentService.Logger.Trace("Update active tournaments ratings done!");
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Error at update active tournaments rating: {0}", ex.ToString());
			}
		}

		private void ConstructRatingProfit(tournament_rounds round, long[] accountIds)
		{
			var reqRes = accountService.GetAccountsInfo(accountIds);
			if (!reqRes.IsSuccess)
				throw new Exception(reqRes.Error);
			var stat = statisticService.GetTradesVolume(reqRes.Result.First().ServerName,
				reqRes.Result.Select(x => x.Login).ToArray(), round.date_start, round.date_end);

			var updates = new RatingUpdate[stat.Result.Length];
			for (var i = 0; i < updates.Length; i++)
			{
				var accInfo = reqRes.Result.First(x => x.Login == stat.Result[i].m_Item1);
				updates[i] = new RatingUpdate
							{
								TounamentParticipantId = accInfo.AccountId,
								Profit = (decimal)accInfo.Equity - round.base_deposit,
							};
			}

			Array.Sort(updates, (item1, item2) => item2.Profit.CompareTo(item1.Profit));

			for (var i = 0; i < updates.Length; i++)
			{
				updates[i].Position = i + 1;
			}

			for (var i = 1; i < updates.Length; i++)
			{
				if (updates[i].Profit == updates[i - 1].Profit)
					updates[i].Position = updates[i - 1].Position;
			}

			repository.UpdateRating(updates, round.id);
		}

		private void ConstructSpecialRatingProfit(tournament_rounds round, long[] accountIds)
		{
			var reqRes = accountService.GetAccountsInfo(accountIds);
			if (!reqRes.IsSuccess)
				throw new Exception(reqRes.Error);
			var stat = statisticService.GetTradesVolume(reqRes.Result.First().ServerName,
				reqRes.Result.Select(x => x.Login).ToArray(), round.date_start, round.date_end);

			var updates = new RatingUpdate[stat.Result.Length];
			for (var i = 0; i < updates.Length; i++)
			{
				var accInfo = reqRes.Result.First(x => x.Login == stat.Result[i].m_Item1);

				updates[i] = new RatingUpdate
							{
								TounamentParticipantId = accInfo.AccountId,
								Profit = (decimal)accInfo.Balance,
								Volume = stat.Result[i].m_Item2
							};
			}

			Array.Sort(updates, (item1, item2) => item2.Profit.CompareTo(item1.Profit));

			for (var i = 0; i < updates.Length; i++)
			{
				updates[i].Position = i + 1;
			}

			for (var i = 1; i < updates.Length; i++)
			{
				if (updates[i].Profit == updates[i - 1].Profit)
					updates[i].Position = updates[i - 1].Position;
			}

			repository.UpdateRating(updates, round.id);
		}

		private void ConstructRatingPoints(tournament_rounds round, long[] accountIds)
		{
			var accounts = accountService.GetAccountsMt4Location(accountIds);
			var stat = statisticService.GetTradesProfitInPoints(accounts.Result.First().ServerName,
				accounts.Result.Select(x => x.Login).ToArray(), round.date_start, round.date_end);

			var updates = new RatingUpdate[stat.Result.Length];
			for (var i = 0; i < updates.Length; i++)
			{
				updates[i] = new RatingUpdate
							{
								TounamentParticipantId = accounts.Result.First(x => x.Login == stat.Result[i].m_Item1).AccountId,
								Points = stat.Result[i].m_Item2,
							};
			}

			Array.Sort(updates, (item1, item2) => item2.Points.CompareTo(item1.Points));

			for (var i = 0; i < updates.Length; i++)
			{
				updates[i].Position = i + 1;
			}

			for (var i = 1; i < updates.Length; i++)
			{
				if (updates[i].Points == updates[i - 1].Points)
					updates[i].Position = updates[i - 1].Position;
			}

			repository.UpdateRating(updates, round.id);
		}

		private void ConstructRatingVolume(tournament_rounds round, long[] accountIds)
		{
			var accounts = accountService.GetAccountsMt4Location(accountIds);
			var stat = statisticService.GetTradesVolume(accounts.Result.First().ServerName,
				accounts.Result.Select(x => x.Login).ToArray(), round.date_start, round.date_end);

			var updates = new RatingUpdate[stat.Result.Length];
			for (var i = 0; i < updates.Length; i++)
			{
				updates[i] = new RatingUpdate
							{
								TounamentParticipantId = accounts.Result.First(x => x.Login == stat.Result[i].m_Item1).AccountId,
								Volume = stat.Result[i].m_Item2,
							};
			}

			Array.Sort(updates, (item1, item2) => item2.Volume.CompareTo(item1.Volume));

			for (var i = 0; i < updates.Length; i++)
			{
				updates[i].Position = i + 1;
			}

			for (var i = 1; i < updates.Length; i++)
			{
				if (updates[i].Volume == updates[i - 1].Volume)
					updates[i].Position = updates[i - 1].Position;
			}

			repository.UpdateRating(updates, round.id);
		}

		#endregion

		#region Generating rounds

		private void GenerateRoundsForAllTournaments()
		{
			try
			{
				TournamentService.Logger.Trace("Generate rounds for all tournaments...");

				foreach (var tournament in repository.GetAllTournaments())
				{
					GenerateRounds(tournament.TournamentId);
				}

				TournamentService.Logger.Trace("Generate rounds for all tournaments done!");
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Error at generate rounds for all tournaments: {0}", ex.ToString());
			}
		}

		private void GenerateNormalRounds(tournament tournament)
		{
			var plannedRounds = repository.GetPlannedRounds(tournament.id_name);
			if (plannedRounds.Length == plannedRoundCount)
			{
				TournamentService.Logger.Trace("Planned rounds {1} enought for {0}", tournament.id_name, plannedRoundCount);
				return;
			}

			for (var i = 0; i < plannedRoundCount - plannedRounds.Length; i++)
			{
				var lastRound = repository.GetLastRound(tournament.id_name);
				if (lastRound == null)
					TournamentService.Logger.Debug("Not found rounds for {0}. Generate first round", tournament.name);

				var newRound = lastRound == null
					? new tournament_rounds
						{
							base_deposit = tournament.base_deposit,
							number = 1,
							date_start = tournament.date_start,
							date_end = tournament.date_start.AddHours(tournament.time_duration),
							is_started = false,
							entry_fee = tournament.entry_fee,
							tournament_id = tournament.id_name,
							show_orders = tournament.orders_visible
						}
					: new tournament_rounds
						{
							base_deposit = lastRound.base_deposit,
							entry_fee = lastRound.entry_fee,
							is_started = false,
							show_orders = lastRound.show_orders,
							tournament_id = lastRound.tournament_id,
							date_start = lastRound.date_start.AddDays(tournament.day_interval),
							date_end = lastRound.date_end.AddDays(tournament.day_interval),
							number = lastRound.number + 1
						};
				repository.AddRound(newRound);
			}
			if (plannedRoundCount - plannedRounds.Length > 0)
				TournamentService.Logger.Info("Generate {0} new rounds for tournament {1}", plannedRoundCount - plannedRounds.Length, tournament.id_name);
		}

		private void GenerateOnePerMonthRounds(tournament tournament)
		{
			var plannedRounds = repository.GetPlannedRounds(tournament.id_name);
			if (plannedRounds.Length == plannedRoundCount)
			{
				TournamentService.Logger.Trace("Planned rounds {1} enought for {0}", tournament.id_name, plannedRoundCount);
				return;
			}

			for (var i = 0; i < plannedRoundCount - plannedRounds.Length; i++)
			{
				var lastRound = repository.GetLastRound(tournament.id_name);
				if (lastRound == null)
					TournamentService.Logger.Debug("Not found rounds for {0}. Generate first round", tournament.name);

				tournament_rounds newRound;
				if (lastRound == null)
				{
					var firstMonday = CalculateFirstMondayDate(tournament.date_start);
					if (firstMonday < tournament.date_start)
						firstMonday = CalculateFirstMondayDate(tournament.date_start.AddMonths(1));
					newRound = new tournament_rounds
								{
									base_deposit = tournament.base_deposit,
									number = 1,
									date_start = firstMonday,
									date_end = firstMonday.AddHours(tournament.time_duration),
									is_started = false,
									entry_fee = tournament.entry_fee,
									tournament_id = tournament.id_name,
									show_orders = tournament.orders_visible
								};
				}
				else
				{
					var firstMonday = CalculateFirstMondayDate(lastRound.date_start.AddMonths(1));
					if (firstMonday <= lastRound.date_start)
						firstMonday = CalculateFirstMondayDate(lastRound.date_start.AddMonths(2));
					newRound = new tournament_rounds
								{
									base_deposit = lastRound.base_deposit,
									entry_fee = lastRound.entry_fee,
									is_started = false,
									show_orders = lastRound.show_orders,
									tournament_id = lastRound.tournament_id,
									date_start = firstMonday,
									date_end = firstMonday.AddDays(tournament.time_duration),
									number = lastRound.number + 1
								};
				}
				repository.AddRound(newRound);
			}
			if (plannedRoundCount - plannedRounds.Length > 0)
				TournamentService.Logger.Info("Generate {0} new rounds for tournament {1}", plannedRoundCount - plannedRounds.Length, tournament.id_name);
		}

		private DateTime CalculateEndDateExt(int duration)
		{
			var endDate = DateTime.Now.AddHours(duration);

			switch (DateTime.Now.DayOfWeek)
			{
				case DayOfWeek.Saturday:
					endDate = DateTime.Now.AddDays(2);
					return new DateTime(endDate.Year, endDate.Month, endDate.Day, duration, 0, 0);
				case DayOfWeek.Sunday:
					endDate = DateTime.Now.AddDays(1);
					return new DateTime(endDate.Year, endDate.Month, endDate.Day, duration, 0, 0);
			}

			switch (endDate.DayOfWeek)
			{
				case DayOfWeek.Saturday:
					return endDate.AddDays(2);
				case DayOfWeek.Sunday:
					return endDate.AddDays(1);
			}

			return endDate;
		}

		private DateTime CalculateFirstMondayDate(DateTime date)
		{
			var firstMonday = new DateTime(date.Year, date.Month, 1);
			while (firstMonday.DayOfWeek != DayOfWeek.Monday)
			{
				firstMonday = firstMonday.AddDays(1);
			}
			return firstMonday;
		}

		#endregion

		#region Daily actions

		private void DailyDisableAccounts()
		{
			if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
				return;

			try
			{
				TournamentService.Logger.Info("Daily  disabling started...");

				var accounts = repository.GetActiveAccounts();
				TournamentService.Logger.Info("Daily  disabling. Load {0} accounts {1}", accounts.Count(), string.Join(", ", accounts));

				var closingRes = accountService.CloseAllOrdersForce(accounts, "Out of session");
				if (closingRes.IsSuccess)
					TournamentService.Logger.Info("Daily  disabling. Close orders");
				else
					TournamentService.Logger.Error("Daily  disabling. Error at close orders");

				var readOnlyRes = accountService.ChangeReadOnlyFlags(accounts, true);
				if (readOnlyRes.IsSuccess)
					TournamentService.Logger.Info("Daily  disabling. Disable accounts");
				else
					TournamentService.Logger.Error("Daily  disabling. Error at disable accounts");

				TournamentService.Logger.Info("Daily  disabling done!");
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Error at daily  disabling: {0}", ex.ToString());
			}
		}

		private void DailyEnableAccounts()
		{

			if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
				return;

			try
			{
				TournamentService.Logger.Info("Daily enabling started...");

				var accounts = repository.GetActiveAccounts();
				TournamentService.Logger.Info("Daily  enabling. Load {0} accounts {1}", accounts.Count(), string.Join(", ", accounts));

				var readOnlyRes = accountService.ChangeReadOnlyFlags(accounts, false);
				if (readOnlyRes.IsSuccess)
					TournamentService.Logger.Info("Daily  disabling. Enable accounts");
				else
					TournamentService.Logger.Error("Daily  disabling. Error at enable accounts");

				TournamentService.Logger.Info("Daily  enabling done!");
			}
			catch (Exception ex)
			{
				TournamentService.Logger.Error("Error at daily  enabling: {0}", ex.ToString());
			}
		}

		#endregion

		#region Start round methods

		private bool CheckRoundForMinimalParticipants(tournament_rounds round, tournament_round_participants[] participants, tournament tournament)
		{
			if (participants.Count() >= tournament.participants_minimal)
				return true;
            
			TournamentService.Logger.Error("Starting {0} (round {1}, id {2}). Not enough participants. Round finished",
				tournament.name, round.number, round.id);

			if (!tournament.isdemo)
			{
				Parallel.ForEach(participants, roundParticipant =>
				{
					var participant = repository.GetParticipant(roundParticipant.participant_id);
					var wallets = GetUserWallets(participant.client_account_id);
					var finRes = ChangeWalletBalance(
						wallets.Result.First(x => string.Equals(x.Currency, "USD", StringComparison.InvariantCultureIgnoreCase)).WalletId,
						tournament.base_deposit + tournament.entry_fee, string.Format("Cancel {0} #{1}", tournament.name, round.number), "USD");
					if (!finRes.IsSuccess)
						TournamentService.Logger.Error("Error at return tournament entry fee. Client {0}, {1} (round {2}, id {3})",
							participant.client_account_id, tournament.name, round.number, round.id);
				});
			}
			repository.FinishRound(round.id);
			return false;
		}

		private void Mt4ActoinsAtStartRound(tournament_rounds round, tournament tournament, tournament_round_participants tournamentRoundParticipant, ConcurrentBag<long> deletedParticipants)
		{
			if (!string.IsNullOrEmpty(tournament.group_name))
			{
				var resMove = accountService.ChangeAccountGroup(tournamentRoundParticipant.participant_id, tournament.group_name);
				if (!resMove.IsSuccess)
				{
					TournamentService.Logger.Error("Starting {0} (round {1}, id {2}). Error at move account {3} to {4}. Account deleted",
						tournament.name, round.number, round.id, tournamentRoundParticipant.participant_id, tournament.group_name);
					deletedParticipants.Add(tournamentRoundParticipant.participant_id);
				}
			}

			if (tournament.leverage != null)
			{
				var resLev = accountService.ChangeAccountLeverage(tournamentRoundParticipant.participant_id, (int)tournament.leverage);
				if (!resLev.IsSuccess)
					TournamentService.Logger.Error("Starting {0} (round {1}, id {2}). Error at change account {3} leverage to {4}",
						tournament.name, round.number, round.id, tournamentRoundParticipant.participant_id, tournament.leverage);
			}

			var resEnable = accountService.ChangeReadOnlyFlag(tournamentRoundParticipant.participant_id, false);
			if (!resEnable.IsSuccess)
				TournamentService.Logger.Error("Starting {0} (round {1}, id {2}). Error at enable account {3}",
					tournament.name, round.number, round.id, tournamentRoundParticipant.participant_id);
		}

		private bool CheckAccountForOpenOrder(tournament_rounds round, tournament_round_participants tournamentRoundParticipant, tournament tournament, ConcurrentBag<long> deletedParticipants)
		{
			var openOrders = accountService.GetOpenOrders(tournamentRoundParticipant.participant_id);
			if (!openOrders.IsSuccess)
			{
				TournamentService.Logger.Error("Starting {1} (round {2}, id {3}). Error at get open trades for account {0}. Account deleted",
					tournamentRoundParticipant.participant_id, tournament.name, round.number, round.id);
				deletedParticipants.Add(tournamentRoundParticipant.participant_id);
				return false;
			}
			if (openOrders.Result.Any())
			{
				accountService.CloseAllOrdersForce(new[] { tournamentRoundParticipant.participant_id }, "Closed due to start of tournament");
				Thread.Sleep(1000);
				openOrders = accountService.GetOpenOrders(tournamentRoundParticipant.participant_id);
				if (!openOrders.IsSuccess)
				{
					TournamentService.Logger.Error("Starting {1} (round {2}, id {3}). Error at repeat get open trades for account {0}. Account deleted",
						tournamentRoundParticipant.participant_id, tournament.name, round.number, round.id);
					deletedParticipants.Add(tournamentRoundParticipant.participant_id);
					return false;
				}
				if (openOrders.Result.Any())
				{
					repository.BanRoundParticipant(tournamentRoundParticipant.id);
					TournamentService.Logger.Error("Starting {0} (round {1}, id {2}). Account {3} has opened orders. Account banned",
						tournament.name, round.number, round.id, tournamentRoundParticipant.participant_id);
				}
			}
			return true;
		}

		private bool CheckAccountExists(tournament_rounds round, tournament_round_participants tournamentRoundParticipant,
			ConcurrentBag<long> activeParticipants, tournament tournament, ConcurrentBag<long> deletedParticipants)
		{
			var transferInfoData = accountService.GetTransferInfo(tournamentRoundParticipant.participant_id);
			if (transferInfoData.IsSuccess)
			{
				activeParticipants.Add(tournamentRoundParticipant.participant_id);
				return true;
			}

			TournamentService.Logger.Error("Starting {1} (round {2}, id {3}). Error get transfer info for account {0}. Account deleted",
				tournamentRoundParticipant.participant_id, tournament.name, round.number, round.id);
			deletedParticipants.Add(tournamentRoundParticipant.participant_id);
			return false;
		}

		private bool DeleteDeletedAccountsFromRound(tournament_rounds round, ConcurrentBag<long> deletedParticipants, ConcurrentBag<long> activeParticipants, tournament tournament)
		{
			// if mt4 respond and some accounts does not exist
			if (deletedParticipants.Any() && (activeParticipants.Any() || tournament.participants_number <= 5))
			{
				foreach (var participant in deletedParticipants)
				{
					repository.DeleteAccountFromRound(participant, round.id);
					TournamentService.Logger.Debug("Starting {0} (round {1}, id {2}). Delete account {3} from round",
						tournament.name, round.number, round.id, participant);
				}
			}

			if (!activeParticipants.Any())
			{
				TournamentService.Logger.Debug("Starting {0} (round {1}, id {2}). MT4 not responding. Round skipped",
					tournament.name, round.number, round.id);
				return false;
			}
			return true;
		}

		private bool AccrueDepositAtStartRound(tournament_rounds round, ConcurrentBag<long> activeParticipants, tournament tournament)
		{
			var participantsIds = activeParticipants.ToArray();
			var depRes = accountService.SetAccountsBalance(participantsIds, round.base_deposit,
				participantsIds
					.Select(id => string.Format(MT4CommentHelper.GetMt4CommentTournament(MT4CommentHelper.MT4CommentType.Deposit, tournament.id_name, round.id, id)))
					.ToArray());

			if (depRes.IsSuccess)
			{
				TournamentService.Logger.Info("Starting {0} (round {1}, id {2}). Change balance for {3} to {4} USD",
					round.tournament_id, round.number, round.id, string.Join(", ", participantsIds), Math.Round(round.base_deposit, 2));
				return true;
			}

			TournamentService.Logger.Error("Starting {0} (round {1}, id {2}). Error at change balance for accounts. Round skipped",
				round.tournament_id, round.number, round.id);
			return false;
		}

		#endregion

		#region Finish round methods

		private void Mt4ActoinsAtFinishRound(long[] participantIds, tournament tournament, tournament_rounds round)
		{
			var closingRes = accountService.CloseAllOrdersForce(participantIds, "Closed due to end of tournament");
			if (closingRes.IsSuccess)
				TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Orders closed", tournament.name, round.number, round.id);
			else
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at close orders", tournament.name, round.number, round.id);

			var readOnlyRes = accountService.ChangeReadOnlyFlags(participantIds, true);
			if (readOnlyRes.IsSuccess)
				TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Accounts disabled", tournament.name, round.number, round.id);
			else
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Errors at disable accounts", tournament.name, round.number, round.id);
		}

		private void FinishSpecialRound(tournament_rounds round, tournament_round_participants[] participants, tournament tournament)
		{
			Parallel.ForEach(participants, participant =>
			{
				var participantAccount = repository.GetParticipant(participant.participant_id);
				var clientData = clientService.GetClient(participantAccount.client_account_id);
				var language = LangHelper.GetLang(clientData.Result != null ? clientData.Result.Country : "");

				if (participant.rating_volume < 400 || participant.rating_profit < 2300)
				{
					var template = contentService.GetMessageTemplate(MessageTemplateType.TournamentFail, language);
					if (template.Result != null && !string.IsNullOrEmpty(template.Result.Template))
						Task.Factory.StartNew(() => mailingService.SendMail(clientData.Result.Email, " trading",
							template.Result.Template, template.Result.Template));
					return;
				}

				var res = accountService.AddTournamentAccount(participantAccount.id, round.number,
					(double)participant.rating_profit, (double)participant.rating_volume / 100.0);
				if (res.IsSuccess)
					TournamentService.Logger.Info("Finishing {1} (round {2}, id {3}). Add  account for client {0}",
						participantAccount.client_account_id, tournament.name, round.number, round.id);
				else
					TournamentService.Logger.Error("Finishing {1} (round {2}, id {3}). Error at add  account for client {0}",
						participantAccount.client_account_id, tournament.name, round.number, round.id);
			});
		}

		private void FinishNormalRound(tournament_rounds round, tournament tournament, tournament_round_participants[] participants)
		{
			var awards = repository.GetAwards(round.id);
			var roundAwards = awards.Where(x => x.round_number == round.number).ToArray();
			if (roundAwards.Count() != 0)
				awards = roundAwards;

			Parallel.ForEach(awards, award =>
			{
				var accounts = participants
									.Where(x => x.rating_position == award.rank)
									.ToArray();

				if (accounts.Length == 1)
				{
					FinishNormalRoundForOneParticipant(round, tournament, accounts.First(), award.award, award.award_pass_id);
				}
				else if (accounts.Length > 1)
				{
					TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Accounts {3} on same position",
						tournament.name, round.number, round.id, string.Join(", ", accounts.Select(x => x.id)));

					var summPrize = award.award;
					for (var i = 1; i < accounts.Length; i++)
					{
						var summ = awards.FirstOrDefault(tuple => tuple.rank == award.rank + i);
						if (summ != null)
							summPrize += summ.award;
					}
					var prize = MathHelper.UnfairRound(summPrize / accounts.Length);

					foreach (var account in accounts)
					{
						FinishNormalRoundForOneParticipant(round, tournament, account, prize, award.award_pass_id);
					}
				}
			});
		}

		private void FinishNormalRoundForOneParticipant(tournament_rounds round, tournament tournament, tournament_round_participants account, decimal prize, long? passId)
		{
			var participant = repository.GetParticipant(account.participant_id);

			AccruePrize(participant, round, tournament, prize, account);

			repository.UpdateRealPrize(account.id, prize);

			AccruePass(participant, passId, tournament, round);

			RemoveClientFromFutureRounds(tournament, participant, round);
		}

		private void AccruePrize(tournament_participants participant, tournament_rounds round, tournament tournament, decimal prize, tournament_round_participants account)
		{
			TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Account {3}, position {4}, prize {5} USD",
				tournament.name, round.number, round.id, account.participant_id, account.rating_position, Math.Round(prize, 2));

			var wallets = GetUserWallets(participant.client_account_id);
			if (!wallets.IsSuccess)
			{
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at get wallets for client {3} (account {4})",
					tournament.name, round.number, round.id, participant.client_account_id, account.participant_id);
				return;
			}

			var finRes = ChangeWalletBalance(wallets.Result.First(x => string.Equals(x.Currency, "USD",
				StringComparison.InvariantCultureIgnoreCase)).WalletId, prize, string.Format("Award. {0} #{1}", tournament.name, round.number), "USD");
			if (!finRes.IsSuccess)
			{
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at accrue award for client {3} (account {4})",
					tournament.name, round.number, round.id, participant.client_account_id, account.participant_id);
				return;
			}

			var finAllRes = AddAmountPaymentSystemAll(participant.client_account_id, prize, "USD");
			if (!finAllRes.IsSuccess)
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at add PS all for client {3} (account {4})",
					tournament.name, round.number, round.id, participant.client_account_id, account.participant_id);
		}

		private void RemoveClientFromFutureRounds(tournament tournament, tournament_participants participant, tournament_rounds round)
		{
			if (tournament.type_id != (int)TournamentsType.FreeRoll)
				return;

			repository.BanClientInFutureTournament(participant.client_account_id, tournament.id_name);

			SendEmailToExcludeFromTournament(participant, tournament, round);
		}

		private void SendEmailToExcludeFromTournament(tournament_participants participant, tournament tournament, tournament_rounds round)
		{
			var clientData = clientService.GetClient(participant.client_account_id);
			if (!clientData.IsSuccess)
			{
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at get client {3} (account {4})",
					tournament.name, round.number, round.id, participant.client_account_id, participant.id);
				return;
			}

			var clientName = string.Format("{0} {1}", clientData.Result.FirstName, clientData.Result.LastName);
			var language = LangHelper.GetLang(clientData.Result.Country);

			var template = contentService.GetMessageTemplate(MessageTemplateType.TournamentExclude, language);
			template.Result = template.Result ?? new MessageTemplate { Template = "" };

			var body = MessageTemplateHelper.ReplaceKeys(template.Result.Template, template.Result.Template,
				new[] { TournamentService.KeyName, TournamentService.KeyTournament },
				new[] { clientName, tournament.name });

			if (!string.IsNullOrEmpty(body))
				Task.Factory.StartNew(() => mailingService.SendMail(clientData.Result.Email, "Exclude from tournament", body, body));
		}

		private void AccruePass(tournament_participants participant, long? passId, tournament tournament, tournament_rounds round)
		{
			if (!passId.HasValue)
				return;

			var newPassId = repository.AddTournamentClientPass(participant.client_account_id, passId.Value, null);
			var resPrize = clientService.AddPrize(participant.client_account_id, newPassId, (short)PrizeType.Tournament, null, null);
			if (!resPrize.IsSuccess)
			{
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at add pass for client {3} (account {4})",
					tournament.name, round.number, round.id, participant.client_account_id, participant.id);
				return;
			}

			var clientData = clientService.GetClient(participant.client_account_id);
			if (!clientData.IsSuccess)
			{
				TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at get client {3} (account {4})",
					tournament.name, round.number, round.id, participant.client_account_id, participant.id);
				return;
			}

			MessageTemplateType type = 0;

			var template = contentService.GetMessageTemplate(type, LangHelper.GetLang(clientData.Result.Country));
			template.Result = template.Result ?? new MessageTemplate { Template = "" };

			var body = MessageTemplateHelper.ReplaceKeys(template.Result.Template, template.Result.Template,
				new[] { TournamentService.KeyName, TournamentService.KeyTournament },
				new[] { string.Format("{0} {1}", clientData.Result.FirstName, clientData.Result.LastName), tournament.name });

			if (!string.IsNullOrEmpty(body))
				Task.Factory.StartNew(() => mailingService.SendMail(clientData.Result.Email, "Tournament free pass", body, body));
		}

		private void WithdrawMoneyAtFinishRound(tournament_rounds round, tournament tournament, long[] participantIds)
		{
			if (tournament.isdemo)
			{
				var resBalance = accountService.SetAccountsBalance(participantIds, 0,
					participantIds
						.Select(id => string.Format(MT4CommentHelper.GetMt4CommentTournament(MT4CommentHelper.MT4CommentType.Withdrawal, tournament.id_name, round.id, id)))
						.ToArray());
				if (!resBalance.IsSuccess)
					TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at set accounts balance to 0",
						tournament.name, round.number, round.id);
				else
					TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Set accounts balance to 0",
						tournament.name, round.number, round.id);
			}
			else
			{
				var accountInfos = accountService.GetAccountsInfo(participantIds);
				if (!accountInfos.IsSuccess)
				{
					TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at get accounts info",
						tournament.name, round.number, round.id);
					return;
				}

				Parallel.ForEach(accountInfos.Result, account =>
				{
					if (account.Balance <= 0.01)
						return;

					var wallets = GetUserWallets(account.ClientId);
					if (!wallets.IsSuccess)
					{
						TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at get wallets for client {3} (account {4})",
							tournament.name, round.number, round.id, account.ClientId, account.AccountId);
						return;
					}

					var finRes = AccountToWallet(account.AccountId, wallets.Result.First(x => string.Equals(x.Currency, "USD",
						StringComparison.InvariantCultureIgnoreCase)).WalletId, (decimal)account.Balance, null);
					if (finRes.IsSuccess)
						TournamentService.Logger.Info("Finishing {0} (round {1}, id {2}). Transfer account to wallet {5} USD for client {3} (account {4})",
							tournament.name, round.number, round.id, account.ClientId, account.AccountId, Math.Round(account.Balance, 2));
					else
						TournamentService.Logger.Error("Finishing {0} (round {1}, id {2}). Error at transfer account to wallet for client {3} (account {4})",
							tournament.name, round.number, round.id, account.ClientId, account.AccountId);
				});
			}
		}

		#endregion
	}
}
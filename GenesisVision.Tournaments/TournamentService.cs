using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataModel;
using FluentScheduler;
using Helpers;
using Helpers.ResultCodes;
using TournamentService.Entities.Enums;
using TournamentService.Logic.Interfaces;
using TournamentService.Logic.Models;
using TournamentService.Logic.Models.Repository;
using NLog;
using AccountType = TournamentService.Logic.Models.AccountType;

namespace TournamentService.Logic
{
	public class TournamentService 
	{
		#region Fields

		public static Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly ITournamentRepository tournamentRepository;
		private readonly TournamentProcessor tournamentProcessor;

		public const string KeyTournament = "{TOURNAMENT}";
		public const string KeyRound = "{ROUND}";
		public const string KeyTime = "{TIME}";
		public const string KeyName = "{NAME}";

		#endregion

		#region Construction

		public TournamentService(
			ITournamentRepository tournamentRepository)
		{
			Logger.Trace("Tournament service construction...");

			this.tournamentRepository = tournamentRepository;

			tournamentProcessor = new TournamentProcessor(tournamentRepository);

			TaskManager.AddTask(CheckTournamentParticipantsExits, x => x.WithName("CheckTournamentParticipantsExits").ToRunEvery(0).Days().At(02, 00));
		}

		#endregion

		#region Public Methods

		public void ParticipateInRoundWithExistingAccount(long clientId, long roundId, long walletId, long accountId, bool joinNearest, string language, bool? usePass)
		{
			Logger.Trace("Participate in round {0}. Client {4}, account {2}, wallet {1}, {3}use pass",
				roundId, walletId, accountId, !usePass.HasValue || !usePass.Value ? "don't " : "", clientId);

			return InvokeOperations.InvokeOperation(() =>
			{
				try
				{
					var clientData = clientService.GetClient(clientId);
					if (!clientData.IsSuccess) throw new OperationException(clientData.Error, clientData.Code);

					if (tournamentRepository.CheckRoundParticipation(roundId, clientId))
						throw new OperationException("Already participant", ResultCode.TournamentAlreadyParticipate);
					var round = tournamentRepository.GetRound(roundId);
					var account = tournamentRepository.GetParticipant(accountId);
					if (account.client_account_id != clientId)
						throw new Exception("Wrong client id");
					if (round.AccountType != account.account_type_id)
						throw new Exception(String.Format("Wrong account type. was {0} expected {1}", account.account_type_id, round.AccountType));

					var tournament = tournamentRepository.GetTournament(round.Round.tournament_id);
					if (tournament.entrance_only_by_pass && (!usePass.HasValue || !usePass.Value))
						throw new OperationException("Entrance only by pass", ResultCode.TournamentWelcomeNotAllowed);
					if (tournament.authorized_only)
					{
						var clientStatusesData = clientService.GetClientStatuses(clientId);
						if (!clientStatusesData.IsSuccess)
							throw new OperationException(clientStatusesData.Error, clientStatusesData.Code);

						if (!clientStatusesData.Result.Has(ClientStatuses.IsApproved))
							throw new OperationException("Client is not approved", ResultCode.TournamentAuthorizedOnly);
					}
					//Check freeroll prizes and bans
					if (tournament.type_id == (int)TournamentsType.FreeRoll)
					{
						if (clientData.Result.ClientStatuses.Has(ClientStatuses.IsPromotionBlocked))
							throw new OperationException("Participation in round is not available", ResultCode.SiteOperationNotAvailable);

						if (tournamentRepository.CheckClientTournamentPrizes(clientId, tournament.id_name)) throw new OperationException("Already has prize", ResultCode.TournamentAlreadyWon);
						if (tournamentRepository.CheckClientBans(clientId, tournament.type_id)) throw new OperationException("Banned", ResultCode.TournamentBanned);
					}
					else if (tournament.type_id == (int)TournamentsType.Special)
					{
						if (tournamentRepository.CheckSpecialTournamentParticipation(clientId, tournament.id_name))
							throw new OperationException("Already participant", ResultCode.TournamentChallengeAlreadyParticipate);
					}

					if (joinNearest)
						round = tournamentRepository.GetNearestRound(tournament.id_name);
					else if (round.Round.is_started && !tournament.entry_after_start)
						throw new OperationException("Round is already started", ResultCode.TournamentRoundAlreadyStarted);

					var participants = tournamentRepository.GetRoundParticipants(roundId).Count();
					if (tournament.participants_number != 0)
					{
						if (participants >= tournament.participants_number)
							throw new OperationException("Round is full", ResultCode.TournamentRoundIsFull);
					}
					if (CheckRoundsIntersection(tournamentRepository.GetAccountActiveRounds(accountId), round.Round.date_start, round.Round.date_end))
						throw new OperationException("Rounds intersection", ResultCode.TournamentRoundsIntersection);

					PayForTournament(tournament, round, walletId, clientId, language, clientData.Result.Email, usePass);
					accountService.ChangeAccountBalance(accountId, 0, "");

					tournamentRepository.AddRoundParticipation(accountId, roundId, walletId);
					if (round.Round.is_started)
					{
						var resMove = accountService.ChangeAccountGroup(accountId, tournament.group_name);
						if (!resMove.IsSuccess)
							throw new OperationException(resMove.Error, (ResultCode)resMove.Code);
						if (tournament.leverage != null)
						{
							var leverageRes = accountService.ChangeAccountLeverage(accountId, (int)tournament.leverage);
							if (!leverageRes.IsSuccess) throw new OperationException(leverageRes.Error, (ResultCode)leverageRes.Code);
						}
						var flagResult = accountService.ChangeReadOnlyFlag(accountId, false);
						if (!flagResult.IsSuccess)
							throw new OperationException(flagResult.Error, (ResultCode)flagResult.Code);
						var setBalanceResult = accountService.SetAccountsBalance(new[] { accountId }, tournament.base_deposit,
							new[] { string.Format(MT4CommentHelper.GetMt4CommentTournament(MT4CommentHelper.MT4CommentType.Deposit, tournament.id_name, round.Round.id, accountId)) });
						if (!setBalanceResult.IsSuccess)
							throw new OperationException(setBalanceResult.Error, (ResultCode)setBalanceResult.Code);
                        
					}
					tournamentProcessor.ConstructRoundRating(round.Round);

					participants = tournamentRepository.GetRoundParticipants(roundId).Count();
					if (!round.Round.is_started && participants == tournament.participants_number)
						tournamentProcessor.StartRound(round.Round);
				}
				catch (Exception ex)
				{
					Logger.Error("Error at participate in round {0}. Client {4}, account {2}, wallet {1}, {3}use pass. {5}",
						roundId, walletId, accountId, !usePass.HasValue || !usePass.Value ? "don't " : "", clientId, ex.ToString());
					throw;
				}
			});
		}

		public ParticipateAccount ParticipateInRoundWithNewAccount(long clientId, long roundId, long walletId, string nickname, string language, bool joinNearest, bool? usePass)
		{
			Logger.Trace("Participate in round {1}. Client {0}, nickname {3}, wallet {2}, {4}use pass",
				clientId, roundId, walletId, nickname, !usePass.HasValue || !usePass.Value ? "don't " : "");

			return InvokeOperations.InvokeOperation(() =>
			{
				try
				{
					var clientData = clientService.GetClient(clientId);
					if (!clientData.IsSuccess) throw new OperationException(clientData.Error, clientData.Code);

					if (tournamentRepository.CheckRoundParticipation(roundId, clientId))
						throw new OperationException("Already participant", ResultCode.TournamentAlreadyParticipate);
					var round = tournamentRepository.GetRound(roundId);

					var tournament = tournamentRepository.GetTournament(round.Round.tournament_id);
					if (tournament.entrance_only_by_pass && (!usePass.HasValue || !usePass.Value))
						throw new OperationException("Entrance only by pass", ResultCode.TournamentWelcomeNotAllowed);
					if (tournament.authorized_only)
					{
						var clientStatusesData = clientService.GetClientStatuses(clientId);
						if (!clientStatusesData.IsSuccess)
							throw new OperationException(clientStatusesData.Error, clientStatusesData.Code);

						if (!clientStatusesData.Result.Has(ClientStatuses.IsApproved))
							throw new OperationException("Client is not approved", ResultCode.TournamentAuthorizedOnly);
					}
					//Check freeroll prizes
					if (tournament.type_id == (int)TournamentsType.FreeRoll)
					{
						if (clientData.Result.ClientStatuses.Has(ClientStatuses.IsPromotionBlocked))
							throw new OperationException("Participation in round is not available", ResultCode.SiteOperationNotAvailable);

						if (tournamentRepository.CheckClientTournamentPrizes(clientId, tournament.id_name)) throw new OperationException("Already has prize", ResultCode.TournamentAlreadyWon);
						if (tournamentRepository.CheckClientBans(clientId, tournament.type_id)) throw new OperationException("Banned", ResultCode.TournamentBanned);
					}
					else if (tournament.type_id == (int)TournamentsType.Special)
					{
						if (tournamentRepository.CheckSpecialTournamentParticipation(clientId, tournament.id_name))
							throw new OperationException("Already participant", ResultCode.TournamentChallengeAlreadyParticipate);
					}

					if (joinNearest)
						round = tournamentRepository.GetNearestRound(tournament.id_name);
					else if (round.Round.is_started && !tournament.entry_after_start)
						throw new OperationException("Round is already started", ResultCode.TournamentRoundAlreadyStarted);

					var participants = tournamentRepository.GetRoundParticipants(roundId).Count();
					if (tournament.participants_number != 0)
					{
						if (participants >= tournament.participants_number)
							throw new OperationException("Round is full", ResultCode.TournamentRoundIsFull);
					}

					var payment = tournament.isdemo ? 0 : round.Round.base_deposit + walletId == -1 ? 0 : round.Round.entry_fee;
					
					var res = accountService.CreateTradingAccount(clientId, "USD", (AccountService.AccountType)round.AccountType, nickname, language);
					if (!res.IsSuccess)
						throw new OperationException(String.Format("Create trading account error: {0}", res.Error), res.Code);
					var resRole = accountService.ChangeAccountRole(res.Result.AccountId, AccountRole.Tournament);
					if (!resRole.IsSuccess)
						throw new OperationException(String.Format("Change account role error: {0}", res.Error), res.Code);
					var resAcc = accountService.GetMt4AccountInfo(res.Result.AccountId);
					if (!resAcc.IsSuccess)
						throw new OperationException(String.Format("Get account error: {0}", res.Error), res.Code);
					var acc = resAcc.Result;

					PayForTournament(tournament, round, walletId, clientId, language, clientData.Result.Email, usePass);

					var client = clientService.GetClient(clientId);
					var isBot = client.IsSuccess && client.Result.IsUnreal;

					var account = new ParticipantModel
								{
									AccountTypeId = (long)acc.AccountTypeId,
									Avatar = acc.Avatar,
									Nickname = nickname,
									ClientAccountId = acc.ClientId,
									Description = acc.Description,
									Id = acc.AccountId,
									Login = acc.Login,
									IsBot = isBot
								};
					tournamentRepository.AddParticipant(account);
					tournamentRepository.AddRoundParticipation(account.Id, round.Round.id, walletId);
					if (round.Round.is_started)
					{
						var resMove = accountService.ChangeAccountGroup(res.Result.AccountId, tournament.group_name);
						if (!resMove.IsSuccess) throw new OperationException(resMove.Error, resMove.Code);
						if (tournament.leverage != null)
						{
							var leverageRes = accountService.ChangeAccountLeverage(account.Id, (int)tournament.leverage);
							if (!leverageRes.IsSuccess)
								throw new OperationException(leverageRes.Error, leverageRes.Code);
						}
						var flagResult = accountService.ChangeReadOnlyFlag(account.Id, false);
						if (!flagResult.IsSuccess)
							throw new OperationException(flagResult.Error, flagResult.Code);
						var setBalanceResult = accountService.SetAccountsBalance(new[] { account.Id }, tournament.base_deposit,
							new[] { string.Format(MT4CommentHelper.GetMt4CommentTournament(MT4CommentHelper.MT4CommentType.Deposit, tournament.id_name, round.Round.id, account.Id)) });
						if (!setBalanceResult.IsSuccess)
							throw new OperationException(setBalanceResult.Error, setBalanceResult.Code);
                        
					}
					tournamentProcessor.ConstructRoundRating(round.Round);

					participants = tournamentRepository.GetRoundParticipants(roundId).Count();
					if (!round.Round.is_started && participants == tournament.participants_number)
						tournamentProcessor.StartRound(round.Round);

					return new ParticipateAccount
							{
								Login = (int)res.Result.Login,
								Password = res.Result.Password,
								ServerName = res.Result.ServerName
							};
				}
				catch (Exception ex)
				{
					Logger.Trace("Error at participate in round {1}. Client {0}, nickname {3}, wallet {2}, {4}use pass. {5}",
						clientId, roundId, walletId, nickname, !usePass.HasValue || !usePass.Value ? "don't" : "", ex.ToString());
					throw;
				}
			});
		}

		public RoundData GetTournamentRound(long roundId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get tournament round, round id - {0}", roundId);
				var round = tournamentRepository.GetRound(roundId);

				return new RoundData
				{
					TournamentId = round.Round.tournament_id,
					TournamentName = round.Tournament.name,
					RoundId = roundId,
					EntryFee = round.Round.entry_fee,
					Start = round.Round.date_start,
					End = round.Round.date_end,
					DefaultAccountType = round.AccountType,
					Currency = "USD",
					BaseDeposit = round.Round.base_deposit,
					Number = round.Round.number,
					AreOrdersVisible = round.Tournament.orders_visible,
					IsDemo = round.Tournament.isdemo,
				};
			});
		}

		public TournamentRound[] GetTournamentTypeRounds(int tournamentTypeId, int index, int count, long? lastRoundId)
		{
			Logger.Trace("Get tournament rounds, tournament type - {0}", tournamentTypeId);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.GetTournamentTypeRoundList(tournamentTypeId, index, count, lastRoundId));
		}

		public TournamentRound[] GetTournamentRounds(string tournamentId, int index, int count, long? lastRoundId)
		{
			Logger.Trace("Get tournament rounds, tournament id - {0}", tournamentId);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.GetTournamentRoundList(tournamentId, index, count, lastRoundId));
		}

		public TournamentType[] GetTournamentTypes()
		{
			Logger.Trace("Get tournament types");
			return InvokeOperations.InvokeOperation(() => tournamentRepository.GetTournamentTypes());
		}

		public TournamentLight[] GetTournamentList()
		{
			Logger.Trace("Get tournament list");
			return InvokeOperations.InvokeOperation(() =>
			{
				var tournaments = tournamentRepository.GetTournamentList();

				var dateTime = DateTime.Now;

				return tournaments.Select(x => new TournamentLight
				{
					Id = x.Tournament.id_name,
					IsStarted = dateTime > x.Tournament.date_start,
					Name = x.Tournament.name,
					SmallLogo = x.Tournament.small_logo,
					TypeId = x.Type.id,
					TypeName = x.Type.name
				}).ToArray();
			});
		}

		public TournamentInformation GetTournamentInformation(string tournamentId)
		{
			Logger.Trace("Get tournament information {0}", tournamentId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountTypes = accountService.GetAccountTypes();
				if (!accountTypes.IsSuccess)
					throw new OperationException(accountTypes.Error, accountTypes.Code);

				var tournamentInformation = tournamentRepository.GetTournamentInformation(tournamentId);
				var tournament = tournamentRepository.GetTournament(tournamentId);

				var information = new TournamentInformation
				{
					Name = tournament.name,
					FullDescription = tournament.full_description,
					ShortDescription = tournament.short_description,
					BigLogo = tournament.big_logo,
					SmallLogo = tournament.small_logo,
					EntryFee = tournament.entry_fee,
					DateStart = tournament.date_start,
					DayInterval = tournament.day_interval,
					TimeDuration = tournament.time_duration,
					IsDemo = tournament.isdemo,
					TypeId = tournament.type_id,
					BaseDeposit = tournament.base_deposit,
					IdName = tournament.id_name,
					AccountTypes = new List<AccountType>(),
					Ratings = new List<RatingPrice>()
				};

				foreach (var accountInformation in tournamentInformation.Where(x => x.AccountType != null))
				{
					if (information.AccountTypes.Any(x => x.Id == accountInformation.AccountType.account_type_id)) continue;

					information.AccountTypes.Add(new AccountType
					{
						Id = accountInformation.AccountType.account_type_id,
						Name = accountTypes.Result[accountInformation.AccountType.account_type_id]
					});
				}

				foreach (var ratingInformation in tournamentInformation.Where(x => x.RatingPrice != null))
				{
					if (information.Ratings.Any(x => x.Id == ratingInformation.RatingPrice.id)) continue;

					information.Ratings.Add(new RatingPrice
					{
						Id = ratingInformation.RatingPrice.id,
						Award = ratingInformation.RatingPrice.award,
						Rank = ratingInformation.RatingPrice.rank,
					});
				}

				return information;
			});
		}

		public MyTournament[] GetClientsTournamentsAdmin(long[] clientsId, DateTime? fromDate, DateTime? toDate)
		{
			Logger.Trace("Get clients tournaments for admin");
			return InvokeOperations.InvokeOperation(() => tournamentRepository.GetClientsTournamentsAdmin(clientsId, fromDate, toDate));
		}

		public TournamentLight AddTournament(TournamentInformation tournamentAddData)
		{
			Logger.Trace("Add new tournament");
			return InvokeOperations.InvokeOperation(() =>
			{
				tournamentRepository.AddTournament(tournamentAddData);
				var tournament = tournamentRepository.GetTournamentCommonInformation(tournamentAddData.IdName);
				Task.Factory.StartNew(() => tournamentProcessor.GenerateRounds(tournamentAddData.IdName));
				return new TournamentLight
				{
					Id = tournament.Tournament.id_name,
					IsStarted = tournament.Tournament.date_start >= DateTime.Now,
					Name = tournament.Tournament.name,
					SmallLogo = tournament.Tournament.small_logo,
					TypeId = tournament.Type.id,
					TypeName = tournament.Type.name
				};
			});
		}

		public TournamentLight EditTournament(TournamentInformation tournamentEditData)
		{
			Logger.Trace("Add new tournament");
			return InvokeOperations.InvokeOperation(() =>
			{
				tournamentRepository.EditTournament(tournamentEditData);

				var tournament = tournamentRepository.GetTournamentCommonInformation(tournamentEditData.IdName);

				return new TournamentLight
				{
					Id = tournament.Tournament.id_name,
					IsStarted = tournament.Tournament.date_start >= DateTime.Now,
					Name = tournament.Tournament.name,
					SmallLogo = tournament.Tournament.small_logo,
					TypeId = tournament.Type.id,
					TypeName = tournament.Type.name
				};
			});
		}

		public int AddTournamentType(string name)
		{
			Logger.Trace("Add tournament type {0}", name);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.AddTournamentType(name));
		}

		public void EditTournamentType(long typeId, string name)
		{
			Logger.Trace("Add tournament type {0}", name);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.EditTournamentType(typeId, name));
		}

		public OperationResult DeleteTournament(string tournamentId)
		{
			Logger.Trace("Delete tournament {0}", tournamentId);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.DeleteTournament(tournamentId));
		}

		public OperationResult DeleteTournamentType(long typeId)
		{
			Logger.Trace("Delete tournament type {0}", typeId);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.DeleteTournamentType(typeId));
		}

		public OperationResult<TournamentFullInformation> GetTournamentFullInformation(string tournamentName)
		{
			Logger.Trace("Get tournament full information: {0}", tournamentName);
			return InvokeOperations.InvokeOperation(() =>
				tournamentRepository.GetTournamentFullInformation(tournamentName));
		}

		public OperationResult<List<TournamentAccount>> GetAccountsForRound(long clientId, long roundId)
		{
			Logger.Trace("Get client {0} accounts for round id {1}", clientId, roundId);
			return InvokeOperations.InvokeOperation(() =>
			{
				bool isDemo = tournamentRepository.GetRound(roundId).Tournament.isdemo;

				var accountsData = accountService.GetAccountsInfoByRole(clientId, AccountRole.Tournament);
				if (!accountsData.IsSuccess)
					throw new Exception(accountsData.Error);

				return accountsData.Result
					.Where(x => isDemo 
						? x.AccountType.ToLower().Contains("demo") 
						: !x.AccountType.ToLower().Contains("demo"))
					.Select(x => new TournamentAccount
					{
						Id = x.AccountId,
						ClientAccountId = x.ClientId,
						Login = x.Login,
						Avatar = x.Avatar,
						Nickname = x.Nickname
					})
					.ToList();
			});
		}

		public OperationResult<TournamentTabInfo[]> GetAccountTournamentsActive(long accountId)
		{
			Logger.Trace("Get active tournaments for account {0}", accountId);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.GetAccountActiveRounds(accountId));
		}
		public OperationResult<TournamentTabInfo[]> GetAccountTournamentsFinished(long accountId, int index, int count)
		{
			Logger.Trace("Get finished tournaments for account {0}", accountId);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.GetAccountFinishedRounds(accountId, index, count));
		}

		public OperationResult<bool> IsPassAvailable(long clientId, string tournamentId, long roundNumber)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Check if client {0} have pass for tournament {1} round {2}", clientId, tournamentId, roundNumber);
				var passes = tournamentRepository.GetClientTournamentPasses(clientId);
				return passes.Any(x => x.tournament_id == tournamentId && (x.round_number == roundNumber || x.round_number == null));
			});
		}

		public OperationResult<TournamentActivity[]> GetTournamentActivities(int count)
		{
			Logger.Trace("Get last {0} activities", count);
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountIds = tournamentRepository.GetVisibleTournamentsAccountIds();

				var activities = statisticService.GetAccountsActivities(count, accountIds);
				if (!activities.IsSuccess) throw new OperationException(activities.Error, activities.Code);

				return activities.Result.Select(x => new TournamentActivity
				{
					Country = x.Country,
					Nickname = x.Nickname,
					OrderType = x.OrderType.ToString(),
					Price = x.Price,
					Profit = x.Profit,
					Symbol = x.Symbol,
					Time = x.Time,
					TradingAccountId = x.TradingAccountId
				}).ToArray();
			});
		}

		public OperationResult BanRoundParticipant(long clientId, long roundParticipantId)
		{
			Logger.Trace("Ban round participant {0}", roundParticipantId);
			return InvokeOperations.InvokeOperation(() =>
			{
				tournamentRepository.BanRoundParticipant(roundParticipantId);
				tournamentRepository.BanClientInFutureTournamentType(clientId, (short)TournamentsType.FreeRoll);

				long accountId = tournamentRepository.GetParticipantAccountId(roundParticipantId);
				BanAccountInTournament(clientId, accountId);
			});
		}

		public OperationResult BanAccountInTournament(long clientId, long accountId)
		{
			Logger.Trace("Ban tournament account. Client = {0}, Account = {1}", clientId, accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				accountService.SetAccountsBalance(new[] { accountId }, 0, new[] { "Banned account" });

				clientService.SetStatusIsSuspiciousByFraud(clientId, true);
				clientService.AppendClientComment(clientId, "Tournament fraudster", false);
			});
		}

		public OperationResult<Tournament[]> GetAllTournaments()
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get all tournaments");
				var result = tournamentRepository.GetAllTournaments();
				return result;
			});
		}

		public OperationResult<MyTournament[]> GetClientMyTournaments(long clientId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get client MyTournaments, client id - {0}", clientId);
				var tournaments = tournamentRepository.GetClientMyTournaments(clientId);

				/*
				 * Uncomment if need Balance and Equity data from AccountService
				 * 
				var tradingInfos = accountService.GetAccountsInfo(tournaments.Select(t => t.TradingAccountId).ToArray());

				foreach (var tournament in tournaments)
				{
					var tradingInfo = tradingInfos.Result.First(ti => ti.AccountType == tournament.TradingAccountId);
					tournament.Balance = Convert.ToDecimal(tradingInfo.Balance);
					tournament.Equity = Convert.ToDecimal(tradingInfo.Equity);
				}
				*/

				return tournaments;
			});
		}

		public OperationResult<RatingParticipant[]> GetRating(long roundId)
		{
			Logger.Trace("Get rating, round id - {0}", roundId);
			return InvokeOperations.InvokeOperation(() => tournamentRepository.GetRaitingParticipants(roundId));
		}

		public OperationResult<RatingParticipant[]> GetParticipantRating(long participantId, long roundId, int upShift = 1, int downShift = 1)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get participant rating, participant id - {0}, round id - {1}", participantId, roundId);
				var result = tournamentRepository.GetRaitingParticipants(roundId);
				var list = new List<RatingParticipant>();

				var client = result.Single(c => c.ParticipantId == participantId);
				var clientIndex = Array.IndexOf(result, client);

				if (clientIndex != 0)
				{
					var pos = (clientIndex - upShift < 0) ? 0 : clientIndex - upShift;

					while (pos < clientIndex)
						list.Add(result.ElementAt(pos++));
				}

				list.Add(client);

				if (clientIndex != result.Count() - 1)
				{
					var pos = clientIndex + 1;

					while (pos <= clientIndex + downShift && pos < result.Count())
						list.Add(result.ElementAt(pos++));
				}

				return list.ToArray();
			});
		}

		public OperationResult CheckRoundParticipation(long roundId, long clientId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Check round participation, round id -{0}, client id - {1}", roundId, clientId);
				var result = tournamentRepository.CheckRoundParticipation(roundId, clientId);
				if (!result) throw new Exception("Participant does not exist!");
			});
		}

		public OperationResult<RoundFullInformation> GetRoundInformation(long roundId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get round information {0}", roundId);
				var result = tournamentRepository.GetRoundInformation(roundId);
				return result;
			});

		}

		public OperationResult ChangeAccountVisibility(long accountId, bool value)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Master visibility for {0} set to {1}", accountId, value);

				tournamentRepository.ChangeAccountVisibility(accountId, value);
			});
		}

		public OperationResult DeleteAccount(long accountId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Closing account {0}", accountId);

				var res = accountService.DeleteAccount(accountId);
				if (!res.IsSuccess)
					throw new OperationException(res.Error, (ResultCode)res.Code);

				tournamentRepository.DeleteAccount(accountId);
			});
		}

		public OperationResult<WinnersData[]> GetWinners(int number)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Getting random tournament winners");

				return tournamentRepository.GetWinners(number);
			});
		}

		public OperationResult ChangeParticipantAvatar(long accountId, string newAvatar)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Change avatar for {0}", accountId);

				tournamentRepository.ChangeAvatar(accountId, newAvatar);
			});
		}

		public OperationResult ChangeParticipatnDescription(long accountId, string newDescription)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Change description for {0}", accountId);

				tournamentRepository.ChangeDescription(accountId, newDescription);
			});
		}

		public OperationResult<Pass[]> GetAllTournamentsPasses()
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get all tournament passes");
				return tournamentRepository.GetAllTournamentsPasses();
			});
		}
        
		public OperationResult<ClientPass[]> GetClientPasses(long clientId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get client {0} passes", clientId);
				return tournamentRepository.GetClientPasses(clientId);
			});
		}

		public OperationResult AddPrize(long passId, long clientId, bool sendMail, DateTime? expiryDate, long? managerId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Add prize (pass id {1}) for client {0}", clientId, passId);

				var newPassId = tournamentRepository.AddTournamentClientPass(clientId, passId, expiryDate);
				var resPrize = clientService.AddPrize(clientId, newPassId, (short)PrizeType.Tournament, expiryDate, managerId);
				if (!resPrize.IsSuccess)
					throw new OperationException(resPrize.Error, resPrize.Code);

				

				if (sendMail)
				{
					var client = clientService.GetClient(clientId);
					if (!client.IsSuccess) return;

					var pass = tournamentRepository.GetTournamentPass(passId);
					if (pass == null) return;
					var tournament = tournamentRepository.GetTournament(pass.tournament_id);
					if (tournament == null) return;
                    
					var template = contentService.GetMessageTemplate(type, LangHelper.GetLang(client.Result.Country));
					template.Result = template.Result ?? new MessageTemplate { Template = "" };

					var body = MessageTemplateHelper.ReplaceKeys(template.Result.Template, template.Result.Template,
						new[] { KeyName, KeyTournament },
						new[] { string.Format("{0} {1}", client.Result.FirstName, client.Result.LastName), tournament.name });

					if (!string.IsNullOrEmpty(body))
						Task.Factory.StartNew(() => mailingService.SendMail(client.Result.Email, "Tournament free pass", body, body));
				}
			});
		}

		#endregion

		#region Private

		/// <summary>
		/// Checks if round time intersection occurs on new participation
		/// </summary>
		/// <param name="clientRounds">Rounds in which client is already participating</param>
		/// <param name="newRoundBeginDate">New round start time</param>
		/// <param name="newRoundEndDate">New round end time</param>
		/// <returns>true - instersection occurs, false - no intersections</returns>
		private static bool CheckRoundsIntersection(TournamentTabInfo[] clientRounds, DateTime newRoundBeginDate, DateTime newRoundEndDate)
		{
			if (clientRounds.Count() != 0 && newRoundBeginDate == DateTime.MaxValue) return true;

			foreach (var round in clientRounds)
			{
				if (round.BeginDate <= newRoundBeginDate)
				{
					if (round.EndDate >= newRoundEndDate || (round.EndDate >= newRoundBeginDate && round.EndDate <= newRoundEndDate)) return true;
				}
				else if (round.EndDate <= newRoundEndDate || (round.BeginDate <= newRoundEndDate && round.EndDate >= newRoundEndDate)) return true;
			}
			return false;
		}

		private void PayForTournament(tournament tournament, TournamentRoundModel round, long walletId, long clientId, string language, string email, bool? usePass)
		{
			var payment = tournament.isdemo ? 0 : round.Round.base_deposit;
			if (!usePass.HasValue || !usePass.Value)
			{
				payment += round.Round.entry_fee;
			}
			else
			{
				var passes = tournamentRepository.GetClientPasses(clientId);
				var passId = passes.FirstOrDefault(x => x.IsRelevant && x.Pass.TournamentId == round.Round.tournament_id && (x.Pass.RoundNumber == round.Round.number)) ?? passes.FirstOrDefault(x => x.IsRelevant && x.Pass.TournamentId == round.Round.tournament_id && x.Pass.RoundNumber == null);
				var prizeResult = clientService.UsePrize(passId.Id, (short)PrizeType.Tournament);
				if (!prizeResult.IsSuccess) throw new OperationException(prizeResult.Error, prizeResult.Code);
				tournamentRepository.UseTournamentClientPass(passId.Id);
				Logger.Debug("Client {1} use pass id {0} for tournament {2}, round {3}", passId.Id, clientId, tournament.name, round.Round.id);
			}

			var template = contentService.GetMessageTemplate(MessageTemplateType.TournamentRegistration, language);
			template.Result = template.Result ?? new MessageTemplate();

			var body = MessageTemplateHelper.ReplaceKeys(template.Result.Template, template.Result.Template, new[] { KeyTournament, KeyRound, KeyTime }, new[] { tournament.name, round.Round.number.ToString(), round.Round.date_start.ToString() });

			var bodyPlain = MessageTemplateHelper.ReplaceKeys(template.Result.Template, template.Result.Template, new[] { KeyTournament, KeyRound, KeyTime }, new[] { tournament.name, round.Round.number.ToString(), round.Round.date_start.ToString() });

			Task.Factory.StartNew(() => mailingService.SendMail(email, "Tournament registration", body, bodyPlain));
		}

		private void CheckTournamentParticipantsExits()
		{
			try
			{
				Logger.Trace("Check tournament participants exits");

				var activeParticipants = tournamentRepository.GetAllActiveParticipants();
				var deletedAccounts = accountService.GetDeletedAccountsFromList(activeParticipants);
				if (deletedAccounts.IsSuccess && deletedAccounts.Result.Any())
				{
					Logger.Info("Delete not exits participants: {0}", string.Join(", ", deletedAccounts.Result));
					tournamentRepository.DeleteAccounts(deletedAccounts.Result);
				}
			}
			catch (Exception ex)
			{
				Logger.Error("Error at check tournament participants exits. {0}", ex.ToString());
			}
		}

		#endregion
	}
}

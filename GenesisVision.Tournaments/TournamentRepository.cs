using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using DataModel;
using LinqToDB;
using Helpers;
using Helpers.ResultCodes;
using Helpers.Security;
using TournamentService.Entities.Enums;
using TournamentService.Logic.Interfaces;
using TournamentService.Logic.Models;
using TournamentService.Logic.Models.Repository;

namespace TournamentService.Logic
{
	public class TournamentRepository : ITournamentRepository
	{
		#region Fields

		private readonly Cache<string, tournament> tournamentsCache = new Cache<string, tournament>();

		#endregion

		#region Constructor

		public TournamentRepository()
		{
			InitCache();
		}

		#endregion

		#region Public Methods

		public void AddParticipant(ParticipantModel participantModel)
		{
			using (var db = GetConnection())
			{
				var pm = participantModel;
				var participant = new tournament_participants
				{
					account_type_id = pm.AccountTypeId,
					avatar = pm.Avatar,
					client_account_id = pm.ClientAccountId,
					date_description_updated = pm.DateDescriptionUpdated,
					description = pm.Description,
					isdeleted = false,
					isenabled = true,
					isvisible = true,
					login = pm.Login,
					nickname = pm.Nickname,
					rating_count = null,
					rating_value = null,
					id = pm.Id,
					is_bot = pm.IsBot
				};
				db.Insert(participant);
			}
		}

		public tournament_rounds[] GetRoundsToStart(DateTime beginDate)
		{
			using (var db = GetConnection())
			{
				var res = db.tournament_rounds
					.Where(x => x.date_start.AddSeconds(-5) < beginDate && !x.is_started && x.date_end > DateTime.Now)
					.Select(x => x)
					.ToArray();
				return res;
			}
		}

		public tournament_rounds[] GetRoundsToStop(DateTime endDate)
		{
			using (var db = GetConnection())
			{
				var res = db.tournament_rounds
					.Where(round => round.date_end.AddSeconds(-5) < endDate && round.is_started)
					.Select(round => round)
					.ToArray();
				return res;
			}
		}

		public tournament_round_participants[] GetRoundParticipants(long roundId)
		{
			using (var db = GetConnection())
			{
				var res = db.tournament_round_participants
					.Where(participant => participant.tournament_round_id == roundId)
					.ToArray();
				return res;
			}
		}

		public long[] GetRoundParticipantIds(long roundId)
		{
			using (var db = GetConnection())
			{
				var res = db.tournament_round_participants
					.Where(participant => participant.tournament_round_id == roundId)
					.Select(x => x.participant_id).ToArray();
				return res;
			}
		}

		public tournament_participants[] GetTournamentParticipants(long roundId)
		{
			using (var db = GetConnection())
			{
				var query = from roundPart in db.tournament_round_participants
							where roundPart.id == roundId
							join tournamentRoundParticipant in db.tournament_round_participants on roundPart.id equals
								tournamentRoundParticipant.tournament_round_id
							join tournamentParticipant in db.tournament_participants on tournamentRoundParticipant.participant_id equals
								tournamentParticipant.id
							where !tournamentParticipant.isdeleted
							select tournamentParticipant;

				return query.ToArray();
			}
		}

		public void StartNormalRound(long roundId)
		{
			using (var db = GetConnection())
			{
				db.tournament_rounds
					.Where(tournamentRound => tournamentRound.id == roundId)
					.Set(tournamentRound => tournamentRound.is_started, true)
					.Update();
			}
		}

		public void AddRound(tournament_rounds round)
		{
			using (var db = GetConnection())
			{
				db.Insert(round);
			}
		}

		public tournament GetTournament(string tournamentId)
		{
			return tournamentsCache.Get(tournamentId);
		}

		public tournament_rounds[] GetPlannedRounds(string tournamentId)
		{
			using (var db = GetConnection())
			{
				return
					db.tournament_rounds
						.Where(round => round.tournament_id == tournamentId && round.date_start > DateTime.Now)
						.OrderBy(round => round.date_start)
						.Select(round => round)
						.ToArray();
			}
		}

		public tournament_rounds GetLastRound(string tournamentId)
		{
			using (var db = GetConnection())
			{
				return
					db.tournament_rounds
						.Where(round => round.tournament_id == tournamentId)
						.OrderByDescending(round => round.date_start)
						.FirstOrDefault();
			}
		}

		public void AddRoundParticipation(long participantId, long roundId, long walletId)
		{
			using (var dataBase = GetConnection())
			{
				var round = dataBase.tournament_rounds.FirstOrDefault(x => x.id == roundId);

				if (round == null)
				{
					throw new NullReferenceException(string.Format("There is no round with Id - {0}!", roundId));
				}

				dataBase.Insert(new tournament_round_participants
				{
					tournament_round_id = roundId,
					participant_id = participantId,
					wallet_id = walletId,
					rating_profit = 0,
					rating_points = 0,
					rating_volume = 0,
					rating_position = 10000
				});
			}
		}

		public TournamentRoundModel GetRound(long roundId)
		{
			using (var dataBase = GetConnection())
			{
				var rounds = from round in dataBase.tournament_rounds
							 join tournament in dataBase.tournaments on round.tournament_id equals tournament.id_name
							 join accountType in dataBase.tournament_account_types on tournament.id_name equals accountType.tournament_id
							 where round.id == roundId
							 select new TournamentRoundModel
									 {
										 Round = round,
										 Tournament = tournament,
										 AccountType = accountType.account_type_id,
									 };
				return rounds.FirstOrDefault();
			}
		}

		public void UpdateRating(RatingUpdate[] ratingUpdates, long roundId)
		{
			using (var db = GetConnection())
			{
				try
				{
					db.BeginTransaction();
					foreach (var ratingUpdate in ratingUpdates)
					{
						var update = ratingUpdate;
						db.tournament_round_participants
							.Where(x => x.participant_id == update.TounamentParticipantId && x.tournament_round_id == roundId)
							.Set(x => x.rating_profit, update.Profit)
							.Set(x => x.rating_position, update.Position)
							.Set(x => x.rating_volume, update.Volume)
							.Set(x => x.rating_points, update.Points)
							.Update();
					}
					db.CommitTransaction();
				}
				catch (Exception ex)
				{
					TournamentService.Logger.Error("Error at update rating in database: {0}", ex.ToString());
					db.RollbackTransaction();
				}
			}
		}

		public tournament_rating_price[] GetAwards(long id)
		{
			using (var db = GetConnection())
			{
				var query = from awards in db.tournament_rating_price
							join tournament in db.tournaments on awards.tournament_id equals tournament.id_name
							join tournamentRound in db.tournament_rounds on tournament.id_name equals tournamentRound.tournament_id
							where tournamentRound.id == id
							select awards;
				return query.ToArray();
			}
		}

		public void BanRoundParticipant(long id)
		{
			using (var db = GetConnection())
			{
				db.tournament_round_participants
					.Where(participant => participant.id == id)
					.Set(participant => participant.isbanned, true)
					.Set(participant => participant.rating_points, 0)
					.Set(participant => participant.rating_position, 10000)
					.Set(participant => participant.rating_volume, 0)
					.Update();
			}
		}

		public long GetParticipantAccountId(long roundParticipantId)
		{
			using (var db = GetConnection())
			{
				return db.tournament_round_participants
					.First(x => x.id == roundParticipantId)
					.participant_id;
			}
		}

		public void ChangeAccountVisibility(long accountId, bool value)
		{
			using (var db = GetConnection())
			{
				db.tournament_participants
					.Where(acc => acc.id == accountId)
					.Set(acc => acc.isvisible, value)
					.Update();
			}
		}

		public void DeleteAccount(long accountId)
		{
			using (var db = GetConnection())
			{
				db.tournament_participants
					.Where(acc => acc.id == accountId)
					.Set(acc => acc.isdeleted, true)
					.Update();
			}
		}

		public void DeleteAccounts(long[] accountIds)
		{
			using (var db = GetConnection())
			{
				db.tournament_participants
					.Where(acc => accountIds.Contains(acc.id))
					.Set(acc => acc.isdeleted, true)
					.Update();
			}
		}

		public void DeleteAccountFromRound(long participantId, long roundId)
		{
			using (var db = GetConnection())
			{
				db.tournament_round_participants.Delete(r => r.tournament_round_id == roundId && r.participant_id == participantId);
			}
		}

		public tournament_participants GetParticipant(long accountId)
		{
			using (var db = GetConnection())
			{
				return db.tournament_participants.FirstOrDefault(acc => acc.id == accountId);
			}
		}

		public WinnersData[] GetWinners(int number)
		{
			using (var db = GetConnection())
			{
				var query = from p in db.tournament_participants
							join rp in db.tournament_round_participants on p.id equals rp.participant_id
							join r in db.tournament_rounds on rp.tournament_round_id equals r.id
							join t in db.tournaments on r.tournament_id equals t.id_name
							where r.date_end < DateTime.Now
								  && rp.rating_position == 1
								  && rp.real_prize > 0
							select new WinnersData
							{
								Avatar = p.avatar,
								Award = (rp.real_prize ?? 0),
								DateFinished = r.date_end,
								Nickname = p.nickname,
								TournamentName = t.name,
								AccountId = p.id
							};
				var res = query.OrderByDescending(x => x.DateFinished).Take(number).ToArray();
				return res;
			}
		}

		public void UpdateRealPrize(long roundParticipantId, decimal prize)
		{
			using (var db = GetConnection())
			{
				db.tournament_round_participants
					.Where(p => p.id == roundParticipantId)
					.Set(x => x.real_prize, prize)
					.Update();
			}
		}

		public void ChangeAvatar(long accountId, string newAvatar)
		{
			using (var db = GetConnection())
			{
				db.tournament_participants
					.Where(acc => acc.id == accountId)
					.Set(acc => acc.avatar, newAvatar)
					.Update();
			}
		}

		public void ChangeDescription(long accountId, string newDescription)
		{
			using (var db = GetConnection())
			{
				db.tournament_participants
					.Where(acc => acc.id == accountId)
					.Set(acc => acc.description, newDescription)
					.Update();
			}
		}

		public Tournament[] GetAllTournaments()
		{
			using (var db = GetConnection())
			{
				var query = from t in db.tournaments
							where !t.isdeleted
							select new Tournament
							{
								DateStart = t.date_start,
								EntryFee = t.entry_fee,
								FullDescription = t.full_description,
								ShortDescription = t.short_description,
								BigLogo = t.big_logo,
								SmallLogo = t.small_logo,
								Name = t.name,
								TournamentId = t.id_name,
								PrizeFund = (int)db.tournament_rating_price.Where(source => source.tournament_id == t.id_name).Sum(price => price.award), // Very bad?
								TypeId = t.type_id
							};
				var tournaments = query.ToArray();
				foreach (var tournament in tournaments)
				{
					var nearestRound = db.tournament_rounds
						.Where(round => round.tournament_id == tournament.TournamentId && !round.is_started && round.date_start > DateTime.Now)
						.OrderBy(round => round.date_start)
						.FirstOrDefault();
					if (nearestRound != null)
					{
						tournament.DateEnd = nearestRound.date_end;
						tournament.RoundId = nearestRound.id;
					}
				}
				return tournaments;
			}
		}

		public MyTournament[] GetClientMyTournaments(long clientId)
		{
			using (var db = GetConnection())
			{
				var result = from tp in db.tournament_participants
							 join trp in db.tournament_round_participants on tp.id equals trp.participant_id into participantRoundParticipantJoin
							 from participantRoundParticipant in participantRoundParticipantJoin.DefaultIfEmpty()
							 join tr in db.tournament_rounds on participantRoundParticipant.tournament_round_id equals tr.id into roundRoundParticipantJoin
							 from roundRoundParticipant in roundRoundParticipantJoin.DefaultIfEmpty()
							 join t in db.tournaments on roundRoundParticipant.tournament_id equals t.id_name into tournamentRoundJoin
							 from tournamentRound in tournamentRoundJoin.DefaultIfEmpty()
							 where tp.client_account_id == clientId && !tp.isdeleted
							 select new MyTournament
							 {
								 Id = tournamentRound.id_name,
								 TournamentType = (TournamentsType)tournamentRound.type_id,
								 RoundNumber = roundRoundParticipant.number,
								 RoundId = roundRoundParticipant.id,
								 RoundParticipantId = participantRoundParticipant.id,
								 Name = tournamentRound.name,
								 BeginDate = roundRoundParticipant.date_start,
								 EndDate = roundRoundParticipant.date_end,
								 Duration = tournamentRound.time_duration,
								 ParticipantsNumber = db.tournament_round_participants.Count(x => x.tournament_round_id == roundRoundParticipant.id),
								 ParticipantsNeed = tournamentRound.participants_number,
								 TradingAccountId = tp.id,
								 Login = tp.login,
								 Avatar = tp.avatar,
								 Rating = tp.rating_value ?? 0,
								 BaseDeposit = tournamentRound.base_deposit,
								 EntryFee = tournamentRound.entry_fee,
								 CalculationType = (TournamentCalculationType)tournamentRound.calculation_type_id,
								 Profit = participantRoundParticipant.rating_profit,
								 Volume = participantRoundParticipant.rating_volume / 100,
								 Pips = participantRoundParticipant.rating_points,
								 Place = participantRoundParticipant.rating_position,
								 IsRunning = roundRoundParticipant.is_started,
								 IsDemo = tournamentRound.isdemo,
								 Nickname = tp.nickname,
								 Logo = tournamentRound.small_logo,
								 PrizeFund = db.tournament_rating_price.Where(source => source.tournament_id == tournamentRound.id_name)
										   .Sum(prize => prize.award),
								 ClientId = clientId,
								 Prize = participantRoundParticipant.real_prize
							 };
				var res = result.ToArray();
				return res;
			}
		}

		public MyTournament[] GetClientsTournamentsAdmin(long[] clientsId, DateTime? fromDate, DateTime? toDate)
		{
			using (var db = GetConnection())
			{
				var result = from tp in db.tournament_participants
							 join trp in db.tournament_round_participants on tp.id equals trp.participant_id into participantRoundParticipantJoin
							 from participantRoundParticipant in participantRoundParticipantJoin.DefaultIfEmpty()
							 join tr in db.tournament_rounds on participantRoundParticipant.tournament_round_id equals tr.id into roundRoundParticipantJoin
							 from roundRoundParticipant in roundRoundParticipantJoin.DefaultIfEmpty()
							 where (fromDate == null || roundRoundParticipant.date_start >= fromDate) && (toDate == null || roundRoundParticipant.date_start <= toDate)
							 join t in db.tournaments on roundRoundParticipant.tournament_id equals t.id_name into tournamentRoundJoin
							 from tournamentRound in tournamentRoundJoin.DefaultIfEmpty()
							 where clientsId.Contains(tp.client_account_id) && !tp.isdeleted
							 select new MyTournament
							 {
								 Id = tournamentRound.id_name,
								 TournamentType = (TournamentsType)tournamentRound.type_id,
								 RoundNumber = roundRoundParticipant.number,
								 RoundId = roundRoundParticipant.id,
								 RoundParticipantId = participantRoundParticipant.id,
								 Name = tournamentRound.name,
								 BeginDate = roundRoundParticipant.date_start,
								 EndDate = roundRoundParticipant.date_end,
								 Duration = tournamentRound.time_duration,
								 ParticipantsNumber = db.tournament_round_participants.Count(x => x.tournament_round_id == roundRoundParticipant.id),
								 ParticipantsNeed = tournamentRound.participants_number,
								 TradingAccountId = tp.id,
								 Login = tp.login,
								 Avatar = tp.avatar,
								 Rating = tp.rating_value ?? 0,
								 BaseDeposit = tournamentRound.base_deposit,
								 EntryFee = tournamentRound.entry_fee,
								 CalculationType = (TournamentCalculationType)tournamentRound.calculation_type_id,
								 Profit = participantRoundParticipant.rating_profit,
								 Volume = participantRoundParticipant.rating_volume / 100,
								 Pips = participantRoundParticipant.rating_points,
								 Place = participantRoundParticipant.rating_position,
								 IsRunning = roundRoundParticipant.is_started,
								 IsDemo = tournamentRound.isdemo,
								 Nickname = tp.nickname,
								 Logo = tournamentRound.small_logo,
								 PrizeFund = db.tournament_rating_price.Where(source => source.tournament_id == tournamentRound.id_name)
										   .Sum(prize => prize.award),
								 ClientId = tp.client_account_id,
								 Prize = participantRoundParticipant.real_prize
							 };
				var res = result.ToArray();
				return res;
			}
		}

		public long[] GetAllActiveParticipants()
		{
			using (var db = GetConnection())
			{
				return db.tournament_participants
						.Where(x => !x.isdeleted)
						.Select(x => x.id)
						.ToArray();
			}
		}

		public RatingParticipant[] GetRaitingParticipants(long roundId)
		{
			using (var db = GetConnection())
			{
				var result = from trp in db.tournament_round_participants
							 join tp in db.tournament_participants on trp.participant_id equals tp.id
							 where trp.tournament_round_id == roundId
							 orderby trp.rating_position ascending
							 select new RatingParticipant
							 {
								 ParticipantId = tp.id,
								 Login = tp.login,
								 Position = trp.rating_position,
								 Profit = trp.rating_profit,
								 Volume = trp.rating_volume / 100,
								 Pips = trp.rating_points,
								 ClientId = tp.client_account_id,
								 Avatar = tp.avatar,
								 Nickname = tp.nickname,
								 IsBanned = trp.isbanned
							 };

				return result.ToArray();
			}
		}


		public bool CheckRoundParticipation(long roundId, long clientId)
		{
			using (var dataBase = GetConnection())
			{
				var query = from roundParticipant in dataBase.tournament_round_participants
							join participant in dataBase.tournament_participants
								on roundParticipant.participant_id equals participant.id

							where participant.client_account_id == clientId &&
								  roundParticipant.tournament_round_id == roundId &&
								  !participant.isdeleted

							select new
							{
								roundParticipant,
								participant
							};
				return query.Any();
			}
		}

		public bool CheckSpecialTournamentParticipation(long clientId, string tournamentId)
		{
			using (var dataBase = GetConnection())
			{
				var query = from roundParticipant in dataBase.tournament_round_participants
							join participant in dataBase.tournament_participants on roundParticipant.participant_id equals participant.id
							join rounds in dataBase.tournament_rounds on roundParticipant.tournament_round_id equals rounds.id
							where participant.client_account_id == clientId && !participant.isdeleted && rounds.tournament_id == tournamentId
							select new
								{
									roundParticipant,
									participant
								};
				return query.Any();
			}
		}

		public TournamentRound[] GetTournamentTypeRoundList(int tournamentTypeId, int index, int count, long? lastRoundId)
		{
			using (var db = GetConnection())
			{
				//HYPER OPTIMIZATION HERE!!!11
				var startedAndAvailable = (from round in db.tournament_rounds
										   join tour in db.tournaments on round.tournament_id equals tour.id_name
										   join accType in db.tournament_account_types on tour.id_name equals accType.tournament_id
										   where tour.type_id == tournamentTypeId
										   && round.is_started && tour.entry_after_start
										   select new TournamentRound
										   {
											   IsDemo = tour.isdemo,
											   EntryAfterStart = tour.entry_after_start,
											   Status = round.is_started ? RoundStatus.IsStarted : round.date_start > DateTime.Now ? RoundStatus.OpenedForRegistration : RoundStatus.IsFinished,
											   TournamentId = round.tournament_id,
											   TournamentRoundId = round.id,
											   EntryFee = round.entry_fee,
											   Name = tour.name,
											   Start = round.date_start,
											   End = round.date_end,
											   TournamentTypeId = tour.type_id,
											   TournamentTypeName = ((TournamentsType)tour.type_id).ToString(),
											   AccountTypeId = (int)accType.account_type_id,
											   BaseDeposit = round.base_deposit,
											   PrizeFound = (int)
												   db.tournament_rating_price.Where(source => source.tournament_id == round.tournament_id)
													   .Sum(price => price.award),
											   ParticipantsCount = db.tournament_round_participants
												   .Count(participant => participant.tournament_round_id == round.id),
											   ParticipantsNeed = tour.participants_number,
											   SmallLogo = tour.small_logo,
											   BigLogo = tour.big_logo,
											   RoundNumber = round.number
										   })
										  .ToArray();

				var other = (from round in db.tournament_rounds
							 join tour in db.tournaments on round.tournament_id equals tour.id_name
							 join accType in db.tournament_account_types on tour.id_name equals accType.tournament_id
							 where tour.type_id == tournamentTypeId
							 && !(round.is_started && tour.entry_after_start)
							 select new TournamentRound
							 {
								 IsDemo = tour.isdemo,
								 EntryAfterStart = tour.entry_after_start,
								 Status = round.is_started ? RoundStatus.IsStarted : round.date_start > DateTime.Now ? RoundStatus.OpenedForRegistration : RoundStatus.IsFinished,
								 TournamentId = round.tournament_id,
								 TournamentRoundId = round.id,
								 EntryFee = round.entry_fee,
								 Name = tour.name,
								 Start = round.date_start,
								 End = round.date_end,
								 TournamentTypeId = tour.type_id,
								 TournamentTypeName = ((TournamentsType)tour.type_id).ToString(),
								 AccountTypeId = (int)accType.account_type_id,
								 BaseDeposit = round.base_deposit,
								 PrizeFound = (int)
									 db.tournament_rating_price.Where(source => source.tournament_id == round.tournament_id)
										 .Sum(price => price.award),
								 ParticipantsCount = db.tournament_round_participants
									 .Count(participant => participant.tournament_round_id == round.id),
								 ParticipantsNeed = tour.participants_number,
								 SmallLogo = tour.small_logo,
								 BigLogo = tour.big_logo,
								 RoundNumber = round.number
							 })
							.ToArray();

				var result = startedAndAvailable.OrderByDescending(x => x.Start).ToList();
				result.AddRange(other.Where(x => x.Status == RoundStatus.OpenedForRegistration).OrderBy(x => x.Start));
				result.AddRange(other.Where(x => x.Status == RoundStatus.IsFinished).OrderByDescending(x => x.Start));

				return lastRoundId != null
					? result.Skip(result.IndexOf(result.First(x => x.TournamentRoundId == lastRoundId)) + 1).Take(count).ToArray()
					: result.Skip(index).Take(count).ToArray();
			}
		}

		public TournamentRound[] GetTournamentRoundList(string tournamentId, int index, int count, long? lastRoundId)
		{
			using (var db = GetConnection())
			{
				//HYPER OPTIMIZATION HERE!!!11
				var startedAndAvailable = (from round in db.tournament_rounds
										   join tour in db.tournaments on round.tournament_id equals tour.id_name
										   join accType in db.tournament_account_types on tour.id_name equals accType.tournament_id
										   where tour.id_name == tournamentId
												 && round.is_started && tour.entry_after_start
										   select new TournamentRound
										   {
											   IsDemo = tour.isdemo,
											   EntryAfterStart = tour.entry_after_start,
											   Status =
												   round.is_started
													   ? RoundStatus.IsStarted
													   : round.date_start > DateTime.Now ? RoundStatus.OpenedForRegistration : RoundStatus.IsFinished,
											   TournamentId = round.tournament_id,
											   TournamentRoundId = round.id,
											   EntryFee = round.entry_fee,
											   Name = tour.name,
											   Start = round.date_start,
											   End = round.date_end,
											   TournamentTypeId = tour.type_id,
											   AccountTypeId = (int)accType.account_type_id,
											   BaseDeposit = round.base_deposit,
											   PrizeFound = (int)
												   db.tournament_rating_price.Where(source => source.tournament_id == round.tournament_id)
													   .Sum(price => price.award),
											   ParticipantsCount = db.tournament_round_participants
												   .Count(participant => participant.tournament_round_id == round.id),
											   ParticipantsNeed = tour.participants_number,
											   SmallLogo = tour.small_logo,
											   BigLogo = tour.big_logo,
											   RoundNumber = round.number
										   })
										  .ToArray();

				var other = (from round in db.tournament_rounds
							 join tour in db.tournaments on round.tournament_id equals tour.id_name
							 join accType in db.tournament_account_types on tour.id_name equals accType.tournament_id
							 where tour.id_name == tournamentId
								   && !(round.is_started && tour.entry_after_start)
							 select new TournamentRound
							 {
								 IsDemo = tour.isdemo,
								 EntryAfterStart = tour.entry_after_start,
								 Status =
									 round.is_started
										 ? RoundStatus.IsStarted
										 : round.date_start > DateTime.Now ? RoundStatus.OpenedForRegistration : RoundStatus.IsFinished,
								 TournamentId = round.tournament_id,
								 TournamentRoundId = round.id,
								 EntryFee = round.entry_fee,
								 Name = tour.name,
								 Start = round.date_start,
								 End = round.date_end,
								 TournamentTypeId = tour.type_id,
								 AccountTypeId = (int)accType.account_type_id,
								 BaseDeposit = round.base_deposit,
								 PrizeFound = (int)
									 db.tournament_rating_price.Where(source => source.tournament_id == round.tournament_id)
										 .Sum(price => price.award),
								 ParticipantsCount = db.tournament_round_participants
									 .Count(participant => participant.tournament_round_id == round.id),
								 ParticipantsNeed = tour.participants_number,
								 SmallLogo = tour.small_logo,
								 BigLogo = tour.big_logo,
								 RoundNumber = round.number
							 })
							.ToArray();

				var result = startedAndAvailable.OrderByDescending(x => x.Start).ToList();
				result.AddRange(other.Where(x => x.Status == RoundStatus.OpenedForRegistration).OrderBy(x => x.Start));
				result.AddRange(other.Where(x => x.Status == RoundStatus.IsFinished).OrderByDescending(x => x.Start));

				return lastRoundId != null
					? result.Skip(result.IndexOf(result.First(x => x.TournamentRoundId == lastRoundId)) + 1).Take(count).ToArray()
					: result.Skip(index).Take(count).ToArray();
			}
		}

		public TournamentType[] GetTournamentTypes()
		{
			using (var dataBase = GetConnection())
			{
				return dataBase.tournament_types.Select(x =>
					new TournamentType
					{
						Id = x.id,
						Name = x.name
					}).OrderByDescending(x => x.Name).ToArray();
			}
		}

		public TournamentLightModel[] GetTournamentList()
		{
			using (var dataBase = GetConnection())
			{
				return (from tournament in dataBase.tournaments

						join tournamentType in dataBase.tournament_types
							on tournament.type_id equals tournamentType.id

						where !tournament.isdeleted

						select new TournamentLightModel
						{
							Tournament = tournament,
							Type = tournamentType
						}).ToArray();
			}
		}

		public TournamentLightModel GetTournamentCommonInformation(string tournamentId)
		{
			using (var dataBase = GetConnection())
			{
				return (from tournament in dataBase.tournaments

						join tournamentType in dataBase.tournament_types
							on tournament.type_id equals tournamentType.id

						where !tournament.isdeleted && tournament.id_name == tournamentId

						select new TournamentLightModel
						{
							Tournament = tournament,
							Type = tournamentType
						}).FirstOrDefault();
			}
		}

		public InformationModel[] GetTournamentInformation(string tournamentId)
		{
			using (var dataBase = GetConnection())
			{
				return (from tournament in dataBase.tournaments

						join rating in dataBase.tournament_rating_price
							on tournament.id_name equals rating.tournament_id
							into ratingNull
						from rating in ratingNull.DefaultIfEmpty()

						join accountType in dataBase.tournament_account_types
							on tournament.id_name equals accountType.tournament_id
							into accountTypeNull
						from accountType in accountTypeNull.DefaultIfEmpty()

						where tournament.id_name == tournamentId

						select new InformationModel
						{
							RatingPrice = rating,
							AccountType = accountType,
						}).ToArray();
			}
		}

		public void AddTournament(TournamentInformation tournamentAddData)
		{
			var dataBase = GetConnection();

			try
			{
				dataBase.BeginTransaction();

				dataBase.Insert(new tournament
				{
					id_name = tournamentAddData.IdName,
					big_logo = tournamentAddData.BigLogo,
					small_logo = tournamentAddData.SmallLogo,
					date_start = tournamentAddData.DateStart,
					day_interval = tournamentAddData.DayInterval,
					entry_fee = tournamentAddData.EntryFee,
					full_description = tournamentAddData.FullDescription,
					isdeleted = false,
					isdemo = tournamentAddData.IsDemo,
					name = tournamentAddData.Name,
					short_description = tournamentAddData.ShortDescription,
					type_id = tournamentAddData.TypeId,
					time_duration = tournamentAddData.TimeDuration,
					base_deposit = tournamentAddData.BaseDeposit
				});

				foreach (var accountType in tournamentAddData.AccountTypes)
				{
					dataBase.Insert(new tournament_account_types
					{
						tournament_id = tournamentAddData.IdName,
						account_type_id = accountType.Id
					});
				}

				foreach (var rating in tournamentAddData.Ratings)
				{
					dataBase.Insert(new tournament_rating_price
					{
						tournament_id = tournamentAddData.IdName,
						award = rating.Award,
						rank = rating.Rank
					});
				}

				dataBase.CommitTransaction();
			}
			catch
			{
				dataBase.RollbackTransaction();
				throw;
			}
			finally
			{
				dataBase.Dispose();
			}
		}

		public int AddTournamentType(string name)
		{
			using (var dataBase = GetConnection())
			{
				return ((int)(long)dataBase.InsertWithIdentity(new tournament_types { name = name }));
			}
		}

		public void DeleteTournament(string tournamentId)
		{
			using (var dataBase = GetConnection())
			{
				if (!dataBase.tournament_rounds.Any(x => x.tournament_id == tournamentId))
				{
					// delete if there was no round
					dataBase.tournament_rating_price
						.Where(x => x.tournament_id == tournamentId)
						.Delete();

					dataBase.tournament_account_types
						.Where(x => x.tournament_id == tournamentId)
						.Delete();

					dataBase.tournaments
						.Where(x => x.id_name == tournamentId)
						.Delete();
				}
				else
				{
					// mark as deleted if there is any rounds
					dataBase.tournaments
						.Where(x => x.id_name == tournamentId)
						.Set(x => x.isdeleted, true)
						.Update();
				}
			}
		}

		public void DeleteTournamentType(long typeId)
		{
			using (var dataBase = GetConnection())
			{
				dataBase.tournament_types
						.Where(x => x.id == typeId)
						.Delete();
			}
		}

		public void EditTournament(TournamentInformation tournamentEditData)
		{
			var dataBase = GetConnection();

			try
			{
				dataBase.BeginTransaction();

				dataBase.tournaments
					.Where(x => x.id_name == tournamentEditData.IdName)
					.Set(x => x.big_logo, tournamentEditData.BigLogo)
					.Set(x => x.small_logo, tournamentEditData.SmallLogo)
					.Set(x => x.date_start, tournamentEditData.DateStart)
					.Set(x => x.day_interval, tournamentEditData.DayInterval)
					.Set(x => x.entry_fee, tournamentEditData.EntryFee)
					.Set(x => x.full_description, tournamentEditData.FullDescription)
					.Set(x => x.isdemo, tournamentEditData.IsDemo)
					.Set(x => x.name, tournamentEditData.Name)
					.Set(x => x.short_description, tournamentEditData.ShortDescription)
					.Set(x => x.type_id, tournamentEditData.TypeId)
					.Set(x => x.time_duration, tournamentEditData.TimeDuration)
					.Set(x => x.base_deposit, tournamentEditData.BaseDeposit)
					.Update();

				// RATING
				var ratings = dataBase.tournament_rating_price.Where(x => x.tournament_id == tournamentEditData.IdName).ToList();
				// Delete ratings awards that was removed
				foreach (var tournamentRatingPrice in ratings.Where(x => tournamentEditData.Ratings.All(y => x.id != y.Id)).ToArray())
				{
					dataBase.Delete(tournamentRatingPrice);
					ratings.Remove(tournamentRatingPrice);
				}
				// add new rating awards
				foreach (var source in tournamentEditData.Ratings.Where(x => ratings.All(y => x.Id != y.id)).ToArray())
				{
					dataBase.Insert(new tournament_rating_price
					{
						tournament_id = tournamentEditData.IdName,
						award = source.Award,
						rank = source.Rank
					});
				}
				// edit old
				foreach (var source in tournamentEditData.Ratings.Where(x => ratings.Any(y => y.id == x.Id)))
				{
					dataBase.tournament_rating_price
						.Where(x => x.id == source.Id)
						.Set(x => x.rank, source.Rank)
						.Set(x => x.award, source.Award)
						.Update();
				}

				// ACCOUNT
				var accountTypes = dataBase.tournament_account_types.Where(x => x.tournament_id == tournamentEditData.IdName).ToList();
				// Delete account types that was removed
				foreach (var tournamentAccountType in accountTypes.Where(x => tournamentEditData.AccountTypes.All(y => x.account_type_id != y.Id)).ToArray())
				{
					dataBase.Delete(tournamentAccountType);
					accountTypes.Remove(tournamentAccountType);
				}
				// add new account types
				foreach (var source in tournamentEditData.AccountTypes.Where(x => accountTypes.All(y => x.Id != y.account_type_id)).ToArray())
				{
					dataBase.Insert(new tournament_account_types
					{
						tournament_id = tournamentEditData.IdName,
						account_type_id = source.Id
					});
				}

				dataBase.CommitTransaction();
			}
			catch
			{
				dataBase.RollbackTransaction();
				throw;
			}
			finally
			{
				dataBase.Dispose();
			}
		}

		public void EditTournamentType(long typeId, string name)
		{
			using (var dataBase = GetConnection())
			{
				dataBase.tournament_types
					.Where(x => x.id == typeId)
					.Set(x => x.name, name)
					.Update();
			}
		}

		public RoundFullInformation GetRoundInformation(long roundId)
		{
			using (var db = GetConnection())
			{
				var round = db.tournament_rounds.First(tournamentRound => tournamentRound.id == roundId);
				var tournament = db.tournaments.First(tournament1 => tournament1.id_name == round.tournament_id);

				var roundInformation = new RoundInformation
				{
					Round = new RoundData
					{
						TournamentId = round.tournament_id,
						BaseDeposit = round.base_deposit,
						Currency = "USD",
						End = round.date_end,
						EntryFee = round.entry_fee,
						RoundId = round.id,
						Start = round.date_start,
						RoundStatus = round.is_started
							? RoundStatus.IsStarted
							: round.date_end < DateTime.Now
								? RoundStatus.IsFinished
								: RoundStatus.OpenedForRegistration,
						Number = round.number,
					},
				};

				var participantsQuery = from roundParticipants in db.tournament_round_participants
										join tournamentParticipant in db.tournament_participants on roundParticipants.participant_id equals
											tournamentParticipant.id
										where roundParticipants.tournament_round_id == round.id
										orderby roundParticipants.rating_position
										select new RatingParticipant
										{
											RoundParticipantId = roundParticipants.id,
											Avatar = tournamentParticipant.avatar,
											Profit = roundParticipants.rating_profit,
											Volume = roundParticipants.rating_volume / 100,
											Pips = roundParticipants.rating_points,
											Nickname = tournamentParticipant.nickname,
											ParticipantId = tournamentParticipant.id,
											Login = tournamentParticipant.login,
											Position = roundParticipants.rating_position,
											ClientId = tournamentParticipant.client_account_id,
											Prize = roundParticipants.real_prize ?? 0,
											IsBanned = roundParticipants.isbanned
										};
				var participants = participantsQuery.ToArray();
				//if (participants.Length != 0)
				//{
				//	var awards = db.tournament_rating_price.Where(price => price.tournament_id == round.tournament_id)
				//		.Select(price => price).ToDictionary(price => price.rank, price => price.award);

				//	foreach (var award in awards)
				//	{
				//		var accounts =
				//			participantsQuery.Select(participant => participant)
				//				.Where(participant => participant.Position == award.Key)
				//				.ToArray();
				//		if (accounts.Length == 0)
				//			continue;
				//		if (accounts.Length == 1)
				//		{
				//			accounts.First().Prize = award.Value;
				//		}
				//		else
				//		{
				//			var count = accounts.Length;
				//			var summPrize = award.Value;

				//			for (int i = 1; i < count; i++)
				//			{
				//				if (awards.ContainsKey((short)(award.Key + i)))
				//				{
				//					var summ = awards.FirstOrDefault(tuple => tuple.Key == award.Key + i);
				//					summPrize += summ.Value;
				//				}
				//			}

				//			var prize = MathHelper.UnfairRound(summPrize / count);

				//			foreach (var tournamentRoundParticipant in accounts)
				//			{
				//				tournamentRoundParticipant.Prize = prize;
				//			}
				//		}
				//	}
				//}

				roundInformation.Participants.AddRange(participants);

				var roundFullInformation = new RoundFullInformation
				{
					TournamentInfo = new TournamentInformation
					{
						BigLogo = tournament.big_logo,
						DateStart = tournament.date_start,
						DayInterval = tournament.day_interval,
						EntryFee = tournament.entry_fee,
						FullDescription = tournament.full_description,
						Name = tournament.name,
						ShortDescription = tournament.short_description,
						SmallLogo = tournament.small_logo,
						TimeDuration = tournament.time_duration,
						TypeId = tournament.type_id,
						IsDemo = tournament.isdemo,
						PrizeFund =
							(int)
								db.tournament_rating_price.Where(source => source.tournament_id == round.tournament_id)
									.Sum(price => price.award),
						CalculationType = (TournamentCalculationType)tournament.calculation_type_id
					},
					RoundInformation = roundInformation
				};

				return roundFullInformation;
			}
		}

		public tournament_rounds[] GetActiveRounds()
		{
			using (var db = GetConnection())
			{
				var rounds = db.tournament_rounds.Where(round => round.is_started).ToArray();
				return rounds;
			}
		}

		public List<TournamentAccount> GetAccountsForRound(long clientId, long roundId)
		{
			using (var db = GetConnection())
			{
				var round = db.tournament_rounds.First(tournamentRound => tournamentRound.id == roundId);
				var qa = (from account in db.tournament_participants
						  join tournamentRoundParticipant in db.tournament_round_participants on account.id equals
							  tournamentRoundParticipant.participant_id into tournamentRoundParticipantJoin
						  from tournamentRoundParticipant in tournamentRoundParticipantJoin.DefaultIfEmpty()
						  join tournamentRound in db.tournament_rounds on tournamentRoundParticipant.tournament_round_id equals
							  tournamentRound.id into tournamentRoundJoin
						  from tournamentRound in tournamentRoundJoin.DefaultIfEmpty()
						  join tournamentRoundAccount in db.tournament_account_types on tournamentRound.tournament_id equals
							  tournamentRoundAccount.tournament_id into tournamentRoundAccountJoin
						  from tournamentRoundAccount in tournamentRoundAccountJoin.DefaultIfEmpty()
						  where !account.isdeleted && account.isenabled
							  && account.client_account_id == clientId
							  && account.account_type_id == tournamentRoundAccount.account_type_id
						  select account).ToArray();

				var q = from arr in qa
						group arr by arr.id
							into g
							select g.First();

				var accounts = q.ToArray();
				var res = new List<TournamentAccount>();
				foreach (var account in accounts)
				{
					var checkQuery = from r in db.tournament_rounds
									 join tournamentRoundParticipant in db.tournament_round_participants on r.id equals
										 tournamentRoundParticipant.tournament_round_id
									 where tournamentRoundParticipant.participant_id == account.id
										 && (
										 (round.date_start <= r.date_end && round.date_start >= r.date_start) ||
										 (round.date_end >= r.date_start && round.date_end <= r.date_end) ||
										 (round.date_start <= r.date_start && round.date_end >= r.date_end) ||
										 r.date_start == DateTime.MaxValue ||
										(round.date_start == DateTime.MaxValue && (r.date_start > DateTime.Now || r.is_started))
										 )
									 select r;
					if (checkQuery.Any())
						continue;
					res.Add(new TournamentAccount
					{
						Id = account.id,
						AccountType = account.account_type_id,
						Avatar = account.avatar,
						ClientAccountId = account.client_account_id,
						Nickname = account.nickname,
						Login = account.login
					});
				}

				return res;
			}
		}

		public void FinishRound(int roundId)
		{
			using (var db = GetConnection())
			{
				db.tournament_rounds.Where(round => round.id == roundId)
					.Set(round => round.date_end, DateTime.Now)
					.Set(round => round.is_started, false)
					.Update();
			}
		}

		public tournament_passes[] GetClientTournamentPasses(long clientId)
		{
			using (var db = GetConnection())
			{
				var query = from pass in db.tournament_passes
							join clientPass in db.tournament_client_passes on pass.id equals clientPass.pass_id
							where clientPass.is_relevant && clientPass.client_id == clientId && (clientPass.expiry_date == null || clientPass.expiry_date > DateTime.Now)
							select pass;
				return query.ToArray();
			}
		}

		public void UseTournamentClientPass(long passId)
		{
			using (var db = GetConnection())
			{
				db.tournament_client_passes.Where(x => x.id == passId)
					.Set(x => x.is_relevant, false)
					.Set(x => x.use_date, DateTime.Now)
					.Update();
			}
		}

		public long AddTournamentClientPass(long clientId, long passId, DateTime? expiryDate)
		{
			using (var db = GetConnection())
			{
				return (long)db.InsertWithIdentity(new tournament_client_passes
				{
					client_id = clientId,
					pass_id = passId,
					is_relevant = true,
					expiry_date = expiryDate
				});
			}
		}

		public tournament_rounds[] GetRoundsToWarn(DateTime beginDate, int interval)
		{
			using (var db = GetConnection())
			{
				var res = db.tournament_rounds
					.Where(round => round.date_start <= beginDate && round.date_start > beginDate.AddSeconds(-interval)
						&& !round.is_started
						&& round.date_end > DateTime.Now)
					.Select(round => round)
					.ToArray();
				return res;
			}
		}

		public tournament_client_passes[] GetPassesForRound(string tournamentId, long number)
		{
			using (var db = GetConnection())
			{
				var query = from clientPass in db.tournament_client_passes
							join pass in db.tournament_passes on clientPass.pass_id equals pass.id
							where pass.tournament_id == tournamentId &&
								(pass.round_number == number || pass.round_number == null) &&
								clientPass.is_relevant &&
								(clientPass.expiry_date == null || clientPass.expiry_date > DateTime.Now)
							select clientPass;
				return query.ToArray();
			}
		}

		public tournament_rounds[] GetRoundsForAccount(long accountId)
		{
			using (var db = GetConnection())
			{
				var query = from participant in db.tournament_round_participants
							join round in db.tournament_rounds on participant.tournament_round_id equals round.id
							where participant.id == accountId && round.date_end > DateTime.Now
							select round;
				return query.ToArray();
			}
		}

		public TournamentTabInfo[] GetAccountActiveRounds(long accountId)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.tournaments
							join tr in db.tournament_rounds on t.id_name equals tr.tournament_id
							join trp in db.tournament_round_participants on tr.id equals trp.tournament_round_id
							join tp in db.tournament_participants on trp.participant_id equals tp.id
							where tp.id == accountId && !tp.isdeleted && tr.date_end > DateTime.Now
							select new TournamentTabInfo
							{
								TournamentId = t.id_name,
								Logo = t.small_logo,
								Name = t.name,
								TournamentType = ((TournamentsType)t.type_id).ToString(),
								RoundId = tr.id,
								RoundNumber = tr.number,
								IsStarted = tr.is_started,
								AreOrdersVisible = t.orders_visible,
								ParticipantsNumber = (!tr.is_started)
									? db.tournament_round_participants.Count(x => x.tournament_round_id == tr.id)
									: 0,
								ParticipantsNeed = t.participants_number,
								BeginDate = tr.date_start,
								EndDate = tr.date_end,
								Place = trp.rating_position,
								IsBanned = trp.isbanned
							};
				return query.OrderByDescending(x => x.EndDate).ToArray();
			}
		}

		public TournamentTabInfo[] GetAccountFinishedRounds(long accountId, int index, int count)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.tournaments
							join tr in db.tournament_rounds on t.id_name equals tr.tournament_id
							join trp in db.tournament_round_participants on tr.id equals trp.tournament_round_id
							join tp in db.tournament_participants on trp.participant_id equals tp.id
							where tp.id == accountId && !tp.isdeleted && tr.date_end < DateTime.Now
							select new TournamentTabInfo
							{
								TournamentId = t.id_name,
								Logo = t.small_logo,
								Name = t.name,
								AreOrdersVisible = t.orders_visible,
								TournamentType = ((TournamentsType)t.type_id).ToString(),
								RoundId = tr.id,
								RoundNumber = tr.number,
								BeginDate = tr.date_start,
								EndDate = tr.date_end,
								Place = trp.rating_position,
								Prize = trp.real_prize ?? 0,
								IsBanned = trp.isbanned
							};
				return query.OrderByDescending(x => x.EndDate)
					.Skip(index)
					.Take(count)
					.ToArray();
			}
		}

		public TournamentRoundModel GetNearestRound(string tournamentId)
		{
			using (var dataBase = GetConnection())
			{
				var rounds = from round in dataBase.tournament_rounds
							 join tournament in dataBase.tournaments
								 on round.tournament_id equals tournament.id_name
							 join accountType in dataBase.tournament_account_types
								 on tournament.id_name equals accountType.tournament_id
							 where round.tournament_id == tournamentId && round.date_start > DateTime.Now && !round.is_started
							 select new TournamentRoundModel
							 {
								 Round = round,
								 Tournament = tournament,
								 AccountType = accountType.account_type_id,
							 };

				return rounds.OrderBy(x => x.Round.date_start).FirstOrDefault();
			}
		}

		public long[] GetVisibleTournamentsAccountIds()
		{
			using (var db = GetConnection())
			{
				var query = from t in db.tournaments
							join tr in db.tournament_rounds on t.id_name equals tr.tournament_id
							join trp in db.tournament_round_participants on tr.id equals trp.tournament_round_id
							join tp in db.tournament_participants on trp.participant_id equals tp.id
							where !tp.isdeleted && t.orders_visible
							select tp.id;

				return query.Distinct().ToArray();
			}
		}

		public bool CheckClientTournamentPrizes(long clientId, string tournamentId)
		{
			using (var db = GetConnection())
			{
				var query = from tr in db.tournament_rounds
							join trp in db.tournament_round_participants on tr.id equals trp.tournament_round_id
							join tp in db.tournament_participants on trp.participant_id equals tp.id
							where tr.tournament_id == tournamentId && tp.client_account_id == clientId && trp.real_prize != null
							select trp.id;

				return query.Any();
			}
		}

		public void BanClientInFutureTournament(long clientId, string tournamentId)
		{
			using (var db = GetConnection())
			{
				var query = from tr in db.tournament_rounds
							join trp in db.tournament_round_participants on tr.id equals trp.tournament_round_id
							join tp in db.tournament_participants on trp.participant_id equals tp.id
							where tp.client_account_id == clientId && tr.tournament_id == tournamentId && tr.date_start > DateTime.Now
							select trp.id;

				db.tournament_round_participants.Where(x => query.Contains(x.id))
					.Set(x => x.isbanned, true)
					.Update();
			}
		}

		public void BanClientInFutureTournamentType(long clientId, short tournamentTypeId)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.tournaments
							join tr in db.tournament_rounds on t.id_name equals tr.tournament_id
							join trp in db.tournament_round_participants on tr.id equals trp.tournament_round_id
							join tp in db.tournament_participants on trp.participant_id equals tp.id
							where tp.client_account_id == clientId && t.type_id == tournamentTypeId && tr.date_start > DateTime.Now
							select trp.id;

				db.tournament_round_participants.Where(x => query.Contains(x.id))
					.Set(x => x.isbanned, true)
					.Update();
			}
		}

		public bool CheckClientBans(long clientId, int tournamentTypeId)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.tournaments
							join tr in db.tournament_rounds on t.id_name equals tr.tournament_id
							join trp in db.tournament_round_participants on tr.id equals trp.tournament_round_id
							join tp in db.tournament_participants on trp.participant_id equals tp.id
							where t.type_id == tournamentTypeId && tp.client_account_id == clientId && trp.isbanned
							select trp.id;

				return query.Any();
			}
		}

		public TournamentFullInformation GetTournamentFullInformation(string tournamentName) // TODO make it query normal
		{
			using (var db = GetConnection())
			{
				var t = new TournamentFullInformation();
				var tournament = db.tournaments.First(tournament1 => tournament1.id_name == tournamentName);
				var rounds = new List<tournament_rounds>();

				if (tournament.entry_after_start)
				{
					rounds = db.tournament_rounds.Where(round => round.tournament_id == tournamentName && round.is_started)
						.OrderBy(round => round.date_start).ToList();
					rounds.AddRange(db.tournament_rounds.Where(round => round.tournament_id == tournamentName && !round.is_started && round.date_start > DateTime.Now)
						.OrderBy(round => round.date_start).Take(30).ToList());
					rounds.AddRange(db.tournament_rounds.Where(round => round.tournament_id == tournamentName && !round.is_started && round.date_start <= DateTime.Now)
						.OrderByDescending(round => round.date_start).Take(30).ToList());
				}
				else
				{
					rounds = db.tournament_rounds.Where(round => round.tournament_id == tournamentName && round.date_start > DateTime.Now)
						.OrderBy(round => round.date_start).Take(30).ToList();
					rounds.AddRange(db.tournament_rounds.Where(round => round.tournament_id == tournamentName && round.date_start <= DateTime.Now)
						.OrderByDescending(round => round.date_start).Take(30).ToList());
				}

				var awards = db.tournament_rating_price.Where(price => price.tournament_id == tournamentName)
					.Select(price => price).ToDictionary(price => price.rank, price => price.award);
				t.TournamentInfo = new TournamentInformation
				{
					BigLogo = tournament.big_logo,
					BaseDeposit = tournament.base_deposit,
					DateStart = tournament.date_start,
					DayInterval = tournament.day_interval,
					EntryFee = tournament.entry_fee,
					FullDescription = tournament.full_description,
					Name = tournament.name,
					ShortDescription = tournament.short_description,
					SmallLogo = tournament.small_logo,
					TimeDuration = tournament.time_duration,
					TypeId = tournament.type_id,
					PrizeFund = (int)awards.Values.Sum(),
					PrizePlaces = awards.Count,
					EntryAfterStart = tournament.entry_after_start,
					IdName = tournament.id_name,
					ParticipantsNeed = tournament.participants_number
				};
				foreach (var tournamentRound in rounds)
				{
					var roundInformation = new RoundInformation
					{
						Round = new RoundData
						{
							TournamentId = tournamentRound.tournament_id,
							BaseDeposit = tournamentRound.base_deposit,
							Currency = "USD",
							End = tournamentRound.date_end,
							EntryFee = tournamentRound.entry_fee,
							RoundId = tournamentRound.id,
							Start = tournamentRound.date_start,
							RoundStatus = tournamentRound.is_started
								? RoundStatus.IsStarted
								: tournamentRound.date_end < DateTime.Now
									? RoundStatus.IsFinished
									: RoundStatus.OpenedForRegistration,
							Number = tournamentRound.number
						},
					};

					var participantsQuery = (from roundParticipants in db.tournament_round_participants
											 join tournamentParticipant in db.tournament_participants on roundParticipants.participant_id equals
												 tournamentParticipant.id
											 join round in db.tournament_rounds on roundParticipants.tournament_round_id equals round.id
											 where roundParticipants.tournament_round_id == tournamentRound.id
											 orderby roundParticipants.rating_position
											 select new RatingParticipant
											 {
												 Avatar = tournamentParticipant.avatar,
												 Profit = roundParticipants.rating_profit,
												 Volume = roundParticipants.rating_volume / 100,
												 Pips = roundParticipants.rating_points,
												 Nickname = tournamentParticipant.nickname,
												 ParticipantId = tournamentParticipant.id,
												 Login = tournamentParticipant.login,
												 Position = roundParticipants.rating_position,
												 ClientId = tournamentParticipant.client_account_id,
												 IsBanned = roundParticipants.isbanned
											 }).ToArray();
					if (participantsQuery.Length != 0)
					{
						foreach (var award in awards)
						{
							var accounts =
								participantsQuery.Select(participant => participant)
									.Where(participant => participant.Position == award.Key)
									.ToArray();
							if (accounts.Length == 0)
								continue;
							if (accounts.Length == 1)
							{
								accounts.First().Prize = award.Value;
							}
							else
							{
								var count = accounts.Length;
								var summPrize = award.Value;

								for (int i = 1; i < count; i++)
								{
									if (awards.ContainsKey((short)(award.Key + i)))
									{
										var summ = awards.FirstOrDefault(tuple => tuple.Key == award.Key + i);
										summPrize += summ.Value;
									}
								}

								var prize = MathHelper.UnfairRound(summPrize / count);

								foreach (var tournamentRoundParticipant in accounts)
								{
									tournamentRoundParticipant.Prize = prize;
								}
							}
						}
					}

					var participants = participantsQuery.ToArray();
					roundInformation.Participants.AddRange(participants);

					t.Rounds.Add(roundInformation);
				}
				return t;
			}
		}

		public long[] GetActiveAccounts()
		{
			using (var db = GetConnection())
			{
				var res = from tournament in db.tournaments
						  join round in db.tournament_rounds on tournament.id_name equals round.tournament_id
						  join roundParticipant in db.tournament_round_participants on round.id equals roundParticipant.tournament_round_id
						  join participant in db.tournament_participants on roundParticipant.participant_id equals participant.id
						  where round.date_start <= DateTime.Now && round.date_end >= DateTime.Now
						  select participant.id;
				return res.ToArray();
			}
		}

		public Pass[] GetAllTournamentsPasses()
		{
			using (var db = GetConnection())
			{
				return db.tournament_passes
						.Select(x => new Pass
									{
										Id = x.id,
										TournamentId = x.tournament_id,
										TournamentName = tournamentsCache.Get(x.tournament_id).name,
										RoundNumber = x.round_number
									})
						.ToArray()
						.OrderBy(x => x.TournamentName)
						.ToArray();
			}
		}

		public tournament_passes GetTournamentPass(long id)
		{
			using (var db = GetConnection())
			{
				return db.tournament_passes.FirstOrDefault(x => x.id == id);
			}
		}

		public ClientPass[] GetClientPasses(long clientId)
		{
			using (var db = GetConnection())
			{
				var req = from clientPasses in db.tournament_client_passes
						  where clientPasses.client_id == clientId
						  join passes in db.tournament_passes on clientPasses.pass_id equals passes.id
						  select new ClientPass
								  {
									  Id = clientPasses.id,
									  ClientId = clientPasses.client_id,
									  IsRelevant = clientPasses.is_relevant && (!clientPasses.expiry_date.HasValue || clientPasses.expiry_date.Value > DateTime.Now),
									  ExpireDate = clientPasses.expiry_date,
									  Pass = new Pass
											  {
												  Id = passes.id,
												  TournamentId = passes.tournament_id,
												  TournamentName = tournamentsCache.Get(passes.tournament_id).name,
												  RoundNumber = passes.round_number
											  }
								  };
				return req.ToArray()
						.OrderBy(x => x.IsRelevant)
						.ToArray();
			}
		}

		#endregion

		#region Private Methods

		public void InitCache()
		{
			TournamentService.Logger.Trace("Repository cache initializing...");
			using (var db = GetConnection())
			{
				var tournaments = db.tournaments
								.Select(x => x)
								.ToDictionary(x => x.id_name, x => x);

				tournamentsCache.Init(tournaments);
			}
			TournamentService.Logger.Trace("Repository cache initialized");
		}

		private static DB GetConnection()
		{
			var hashedConStr = ConfigurationManager.ConnectionStrings[""].ToString();
			var connectionString = Encrypter.DecryptConnectionString(hashedConStr);
			return new DB(connectionString, true);
		}

		#endregion
	}
}

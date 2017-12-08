using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using DataModel;
using GenesisVision.TrustManagement;
using GenesisVision.TrustManagement.Models;
using LinqToDB;
using Helpers.Security;
using Service.Entities.Enums;
using Service.Logic.AccountServiceReference;
using Service.Logic.Interfaces;
using Service.Logic.Models;
using Service.Logic.Models.Manager;
using AccountStatus = Service.Entities.Enums.AccountStatus;

namespace Service.Logic
{
	public class Repository : IRepository
	{
		#region Public Methods

		public _master_accounts GetMaster(long masterId)
		{
			using (var db = GetDBConnect())
			{
				return db._master_accounts.First(account => account.trading_account_id == masterId);
			}
		}

		public Tuple<_investor_accounts, _master_accounts> GetInvestSettings(long investId)
		{
			using (var db = GetDBConnect())
			{
				return db._investor_accounts
					.Join(db._master_accounts, account => account.trading_master_id, account => account.trading_account_id
						, (account, masterAccount) => new { account, masterAccount })
						.Where(x => x.account.id == investId)
					.Select(arg => new Tuple<_investor_accounts, _master_accounts>(arg.account, arg.masterAccount))
					.First();
			}
		}

		public _investor_accounts GetInvestorById(long investorId)
		{
			using (var db = GetDBConnect())
			{
				return db._investor_accounts.FirstOrDefault(inv => inv.id == investorId);
			}
		}

		public _investor_accounts GetInvestor(long clientId, long masterId)
		{
			using (var db = GetDBConnect())
			{
				return
					db._investor_accounts.FirstOrDefault(
						inv => inv.client_account_id == clientId && inv.trading_master_id == masterId);
			}
		}

		public int GetInvestorsCount(long masterId)
		{
			using (var db = GetDBConnect())
			{
				return db._investor_accounts.Count(inv => inv.trading_master_id == masterId);
			}
		}

		public MasterData[] GetMasters(long clientId)
		{
			using (var db = GetDBConnect())
			{
				var query = db._master_accounts.Where(x => x.client_account_id == clientId).Select(m => new MasterData
				{
					ClientId = m.client_account_id,
					AccountId = m.trading_account_id,
					AccountType = m.account_type_id,
					Investments = m.amount_own,
					InvestorsCount = 0,
					DateNextProcessing = m.date_next_processing,
					Login = m.login,
					IsVisible = m.isvisible,
					Nickname = m.nickname,
					Avatar = m.avatar,
					Status = (AccountStatus)m.status,
					RatingValue = m.rating_value,
					RatingCount = m.rating_count
				});

				return query.ToArray();
			}
		}

		public MasterData[] GetClientsMastersAdmin(long[] clientsId)
		{
			using (var db = GetDBConnect())
			{
				var query = db._master_accounts.Where(x => clientsId.Contains(x.client_account_id)).Select(m => new MasterData
				{
					ClientId = m.client_account_id,
					AccountId = m.trading_account_id,
					AccountType = m.account_type_id,
					Investments = m.amount_own,
					InvestorsCount = 0,
					DateNextProcessing = m.date_next_processing,
					Login = m.login,
					IsVisible = m.isvisible,
					Nickname = m.nickname,
					Avatar = m.avatar,
					Status = (AccountStatus)m.status,
					RatingValue = m.rating_value,
					RatingCount = m.rating_count
				});

				return query.ToArray();
			}
		}

		public InvestorInformation[] GetInvestorsInfo(long masterAccountId)
		{
			using (var db = GetDBConnect())
			{
				var query = from investor in db._investor_accounts
					join master in db._master_accounts on investor.trading_master_id equals master.trading_account_id
					where investor.trading_master_id == masterAccountId && investor.status != (short)AccountStatus.Out
					select new InvestorInformation
					{
						InvestorId = investor.id,
						ClientId = investor.client_account_id,
						Profit = db._investor_statistics.Where(x => x.investor_id == investor.id).Sum(s => s.profit),
						TradingDays = investor.date_connect_accepted > DateTime.MinValue ? (DateTime.Now - investor.date_connect_accepted).Days : 0,
						AmountInvested = investor.amount,
						AmountInvestedPending =
							db._investor_invest_operations.Where(
								x => x.account_id == investor.id && x.date > master.date_previous_processing).Sum(x => x.amount)
					};

				return query.ToArray();
			}
		}

		public void CreateMasterAccount(MasterAccountSettings settings)
		{
			using (var db = GetDBConnect())
			{
				var account = new _master_accounts
				{
					trading_account_id = settings.TradingAccountId,
					account_type_id = settings.AccountTypeId,
					currency = settings.Currency,
					period = settings.Period,
					date_next_processing = settings.DateNextProcessing,
					date_previous_processing = DateTime.MinValue,
					amount_own = settings.AmountOwn,
					amount_min = settings.AmountMin,
					fee_management = settings.FeeMenagement,
					fee_success = settings.FeeSuccess,
					confirmation_required = settings.ConfirmationRequired,
					wallet_id = settings.WalletId,
					nickname = settings.NickName,
					description = settings.Description,
					date_open = settings.DateOpen,
					date_close = settings.DateClose,
					client_account_id = settings.ClientAccountId,
					login = settings.Login,
					isdeleted = false,
					isenabled = true,
					status = (short)AccountStatus.PendingIn,
					avatar = settings.Avatar
				};

				db.Insert(account);
			}
		}

		public long CreateInvestorAccount(long clientId, long masterId, int walletId, decimal amount, short reinvestPercent)
		{
			using (var db = GetDBConnect())
			{
				try
				{
					db.BeginTransaction();

					var investor = new _investor_accounts
					{
						client_account_id = clientId,
						wallet_id = walletId,
						amount = 0,
						date_connect_requested = DateTime.Now,
						status = (short)AccountStatus.PendingIn,
						trading_master_id = masterId,
						isdeleted = false,
						isvisible = true,
						avatar = ConfigurationManager.AppSettings["DefaultAvatar"],
						reinvest_percent = reinvestPercent
					};

					var id = db.InsertWithIdentity(investor);

					var operation = new _investor_invest_operations
					{
						account_id = (long)id,
						amount = amount,
						date = DateTime.Now
					};

					db.Insert(operation);
					db.CommitTransaction();

					return (long)id;
				}
				catch
				{
					db.RollbackTransaction();
					throw;
				}
			}
		}

		public MasterWithInvestors[] GetMasterWithInvestors(long[] masterIds)
		{
			using (var db = GetDBConnect())
			{
				var query = from master in db._master_accounts
							join investor in db._investor_accounts on master.trading_account_id equals investor.trading_master_id into investorJoin
							from investor in investorJoin.DefaultIfEmpty()
							where masterIds.Contains(master.trading_account_id) && master.status != (short)AccountStatus.Out
							select new
							{
								master.trading_account_id,
								master.amount_own,
								master.percent,
								master.fee_success,
								master.fee_management,
								master.status,
								master.wallet_id,
								master.date_next_processing,
								master.period,
								master.nickname,
								investor
							};

				var res = query.ToList();

				var query2 = from r in res
							 group r.investor by new
							 {
								 r.trading_account_id,
								 r.amount_own,
								 r.percent,
								 r.fee_success,
								 r.fee_management,
								 r.wallet_id,
								 r.status,
								 r.period,
								 r.nickname,
								 r.date_next_processing
							 }
								 into g
								 select new MasterWithInvestors
								 {
									 TradingAccountId = g.Key.trading_account_id,
									 WalletId = g.Key.wallet_id,
									 AmountOwn = g.Key.amount_own,
									 Percent = g.Key.percent,
									 FeeSuccess = g.Key.fee_success,
									 FeeManagement = g.Key.fee_management,
									 DateNextProcessing = g.Key.date_next_processing,
									 Period = g.Key.period,
									 Nickname = g.Key.nickname,
									 AccountStatus = (AccountStatus)g.Key.status,
									 Investors = g.Where(p => p != null && p.status != (short)AccountStatus.Out).ToList()
								 };

				return query2.ToArray();
			}
		}

		public decimal GetTotalInvestorProfit(long investorId, long masterId)
		{
			using (var db = GetDBConnect())
			{
				return db._investor_statistics
					.Where(stat => stat.investor_id == investorId && stat.master_id == masterId)
					.Sum(stat => stat.profit);
			}
		}

		public InvestorData[] GetInvestors(long clientId)
		{
			using (var db = GetDBConnect())
			{
				var currentDate = DateTime.Now;
				return (from investor in db._investor_accounts
						join master in db._master_accounts
							on investor.trading_master_id equals master.trading_account_id

						where investor.client_account_id == clientId && !investor.isdeleted
						select new InvestorData
						{
							MasterId = investor.trading_master_id,
							MasterAvatar = master.avatar,
							MasterClientId = master.client_account_id,
							MasterNickname = master.nickname,
							MasterRatingValue = master.rating_value,
							MasterRatingCount = master.rating_count,
							InvestorId = investor.id,
							ProfitProportion = investor.percent,
							ProfitTotal = db._investor_statistics.Where(x => x.investor_id == investor.id).Sum(x => x.profit),
							DateNextPeriod = master.date_next_processing,
							Investments = investor.amount,
							InvestmentsPending = db._investor_invest_operations.Where(x => x.account_id == investor.id && x.date > master.date_previous_processing).Sum(x => x.amount),
							WorkingDays = (int)(currentDate - investor.date_connect_requested).TotalDays,
							AccountType = ((AccountType)master.account_type_id).ToString(),
							IsVisible = investor.isvisible,
							Status = (AccountStatus)investor.status
						}).ToArray();
			}
		}

		public void InvestRequest(long id, decimal amount, bool isMaster)
		{
			using (var db = GetDBConnect())
			{
				if (isMaster)
					db.Insert(new _master_invest_operations
					{
						amount = amount,
						date = DateTime.Now,
						account_id = id
					});
				else
					db.Insert(new _investor_invest_operations
					{
						amount = amount,
						date = DateTime.Now,
						account_id = id,
					});
			}
		}

		public void CloseMasterRequest(long accountId)
		{
			using (var db = GetDBConnect())
			{
				try
				{
					db.BeginTransaction();

					db.Insert(new _requests
					{
						request_type = (short)RequestType.MasterClose,
						request_status = (short)RequestStatus.Pending,
						master_id = accountId,
						date_created = DateTime.Now
					});

					db._master_accounts
						.Where(master => master.trading_account_id == accountId)
						.Set(master => master.status, (short)AccountStatus.PendingOut)
						.Update();

					db.CommitTransaction();
				}
				catch
				{
					db.RollbackTransaction();
					throw;
				}
			}
		}

		public void CloseInvestorRequest(long accountId)
		{
			using (var db = GetDBConnect())
			{
				try
				{
					db.BeginTransaction();

					db.Insert(new _requests
					{
						request_type = (short)RequestType.InvestorClose,
						request_status = (short)RequestStatus.Pending,
						investor_id = accountId,
						date_created = DateTime.Now
					});

					var status = db._investor_accounts.First(inv => inv.id == accountId).status;

					db._investor_accounts
						.Where(inv => inv.id == accountId)
						.Set(inv => inv.status, status == (short)AccountStatus.PendingIn ? (short)AccountStatus.Out : (short)AccountStatus.PendingOut)
						.Set(inv => inv.reinvest_percent, (short?)0)
						.Set(inv => inv.date_disconnect_requested, DateTime.Now)
						.Update();

					db.CommitTransaction();
				}
				catch
				{
					db.RollbackTransaction();
					throw;
				}
			}
		}

		public void WithdrawRequest(long id, decimal amount, bool isMaster)
		{
			using (var db = GetDBConnect())
			{
				if (isMaster)
					db.Insert(new _master_invest_operations
					{
						amount = amount,
						date = DateTime.Now,
						account_id = id
					});
				else
					db.Insert(new _investor_invest_operations
					{
						amount = amount,
						date = DateTime.Now,
						account_id = id,
					});
			}
		}

		public decimal GetInvestSumByInvestor(long investorId)
		{
			using (var db = GetDBConnect())
			{
				var query = from operation in db._investor_invest_operations
							join investor in db._investor_accounts on operation.account_id equals investor.id
							join master in db._master_accounts on investor.trading_master_id equals master.trading_account_id
							where investor.id == investorId && operation.date > master.date_previous_processing
							select operation.amount;

				return query.Sum();
			}
		}

		public decimal GetInvestSumByMaster(long masterId)
		{
			using (var db = GetDBConnect())
			{
				var query = from operation in db._master_invest_operations
					join master in db._master_accounts on operation.account_id equals master.trading_account_id
							where master.trading_account_id == masterId && operation.date > master.date_previous_processing
					select operation.amount;
				
				return query.Sum();
			}
		}

		public decimal GetInvestSumByMasterInvestors(long masterId)
		{
			using (var db = GetDBConnect())
			{
				var query = from operation in db._investor_invest_operations
							join investor in db._investor_accounts on operation.account_id equals investor.id
							join master in db._master_accounts on investor.trading_master_id equals master.trading_account_id
							where master.trading_account_id == masterId && operation.date > master.date_previous_processing
							select operation.amount;

				return query.Sum();
			}
		}

		public _investor_invest_operations[] GetInvestorInvestOperations(long investorId)
		{
			using (var dataBase = GetDBConnect())
			{
				var investments = from investment in dataBase._investor_invest_operations

								  join investor in dataBase._investor_accounts
									on investment.account_id equals investor.id

								  join master in dataBase._master_accounts
									on investor.trading_master_id equals master.trading_account_id

								  where investment.account_id == investorId
										&& master.date_previous_processing <= investment.date
										&& master.date_next_processing >= investment.date

								  select investment;

				return investments.ToArray();
			}
		}

		public _master_invest_operations[] GetMasterInvestOperations(long masterid)
		{
			using (var dataBase = GetDBConnect())
			{
				var investments = from investment in dataBase._master_invest_operations

								  join master in dataBase._master_accounts
									on investment.account_id equals master.trading_account_id

								  where investment.account_id == masterid
										&& master.date_previous_processing <= investment.date
										&& master.date_next_processing >= investment.date

								  select investment;

				return investments.ToArray();
			}
		}

		public _investor_accounts[] GetMasterInvestors(long id)
		{
			using (var db = GetDBConnect())
			{
				return db._investor_accounts.Where(x => x.trading_master_id == id && x.status != (short)AccountStatus.Out).ToArray();
			}
		}

		public bool InverstorIsExist(long clientId, long masterId)
		{
			using (var db = GetDBConnect())
			{
				return
					db._investor_accounts.Any(
						account => account.client_account_id == clientId && account.trading_master_id == masterId);
				// TODO Select(1)
			}
		}

		public void ChangeParameters(ParametersChangeModel masterParameters, List<ParametersChangeModel> investorsAmount, DateTime datePreviousProcessing, DateTime dateNextProcessing)
		{
			using (var db = GetDBConnect())
			{
				db.BeginTransaction();

				db._master_accounts
					.Where(master => master.trading_account_id == masterParameters.Id)
					.Set(master => master.amount_own, masterParameters.AmountOwn)
					.Set(master => master.status, (short)masterParameters.NewAccountStatus)
					.Set(master => master.percent, masterParameters.Percent)
					.Set(master => master.date_next_processing, dateNextProcessing)
					.Set(master => master.date_previous_processing, datePreviousProcessing)
					.Set(master => master.date_close, masterParameters.NewAccountStatus == AccountStatus.Out ? DateTime.Now : DateTime.MinValue)
					.Update();

				foreach (var inv in investorsAmount)
				{
					var isdeleting = inv.NewAccountStatus == AccountStatus.Out;

					db._investor_accounts
						.Where(investor => investor.id == inv.Id)
						.Set(investor => investor.amount, inv.AmountOwn)
						.Set(investor => investor.status, (short)inv.NewAccountStatus)
						.Set(investor => investor.percent, inv.Percent)
						.Set(investor => investor.isdeleted, isdeleting)
						.Set(investor => investor.date_disconnect_accepted, isdeleting ? DateTime.Now : DateTime.MinValue)
						.Set(investor => investor.date_connect_accepted, inv.PrevAccountStatus == AccountStatus.PendingIn ? DateTime.Now : DateTime.MinValue)
						.Update();
				}

				db.CommitTransaction();
			}
		}

		public void CloseMasterWithInvestors(long masterId, long[] investorIds)
		{
			using (var db = GetDBConnect())
			{
				db.BeginTransaction();

				db._master_accounts
					.Where(master => master.trading_account_id == masterId)
					.Set(master => master.status, (short)AccountStatus.Out)
					.Set(master => master.isdeleted, true)
					.Update();

				foreach (var inv in investorIds)
				{
					db._investor_accounts
						.Where(investor => investor.id == inv)
						.Set(investor => investor.status, (short)AccountStatus.Out)
						.Set(investor => investor.isdeleted, true)
						.Update();
				}

				db.CommitTransaction();
			}
		}

		public long[] GetMasterIdsForProcessing(DateTime date)
		{
			using (var db = GetDBConnect())
			{
				var query = db._master_accounts
					.Where(account => account.isenabled && !account.isdeleted && account.date_next_processing < date.AddSeconds(5))
					.Select(account => account.trading_account_id);

				return query.ToArray();
			}
		}

		public void UpdateMasterDate(long tradingAccountId, DateTime nextProcessing)
		{
			using (var db = GetDBConnect())
			{
				db._master_accounts
					.Where(account => account.trading_account_id == tradingAccountId)
					.Set(account => account.date_next_processing, nextProcessing)
					.Update();
			}
		}

		public void WriteStatistics(long investorId, long masterId, decimal profit, decimal totalProfit, DateTime beginDate, DateTime endDate)
		{
			using (var db = GetDBConnect())
			{
				db.Insert(new _investor_statistics
				{
					investor_id = investorId,
					master_id = masterId,
					date_begin = beginDate,
					date_end = endDate,
					total_profit = totalProfit,
					profit = profit
				});
			}
		}

		public MasterSettings GetMasterSettings(long masterId)
		{

			using (var db = GetDBConnect())
			{
				return db._master_accounts.Where(x => x.trading_account_id == masterId).Select(x => new MasterSettings
				{
					MasterId = x.trading_account_id,
					ManagementFee = x.fee_management,
					MasterAmount = x.amount_own,
					MinimalAmount = x.amount_min,
					NextProcessing = x.date_next_processing,
					DateStart = (DateTime) x.date_open,
					Nickname = x.nickname,
					Period = x.period,
					SuccessFee = x.fee_success,
					TotalAmount = x.amount_own + db._investor_accounts.Where(y => y.trading_master_id == x.trading_account_id).Sum(y => y.amount)
				}).First();
			}
		}

		public void ChangeMasterVisibility(long accountId, bool value)
		{
			using (var db = GetDBConnect())
			{
				db._master_accounts
					.Where(acc => acc.trading_account_id == accountId)
					.Set(acc => acc.isvisible, value)
					.Update();
			}
		}

		public void ChangeSlaveVisibility(long accountId, bool value)
		{
			using (var db = GetDBConnect())
			{
				db._investor_accounts
					.Where(acc => acc.id == accountId)
					.Set(acc => acc.isvisible, value)
					.Update();
			}
		}

		public void ChangeMasterAvatar(long accountId, string newAvatar)
		{
			using (var db = GetDBConnect())
			{
				db._master_accounts
					.Where(acc => acc.trading_account_id == accountId)
					.Set(acc => acc.avatar, newAvatar)
					.Update();
			}
		}

		public void ChangeSlaveAvatar(long accountId, string newAvatar)
		{
			using (var db = GetDBConnect())
			{
				db._investor_accounts
					.Where(acc => acc.id == accountId)
					.Set(acc => acc.avatar, newAvatar)
					.Update();
			}
		}

		public void ChangeMasterDescription(long accountId, string newDescription)
		{
			using (var db = GetDBConnect())
			{
				db._master_accounts
					.Where(acc => acc.trading_account_id == accountId)
					.Set(acc => acc.description, newDescription)
					.Update();
			}
		}

		public void UpdateRating(long accountId, float? rating, int count)
		{
			using (var db = GetDBConnect())
			{
				db._master_accounts
					.Where(acc => acc.trading_account_id == accountId)
					.Set(acc => acc.rating_value, rating)
					.Set(acc => acc.rating_count, count)
					.Update();
			}
		}

		public bool CheckInvestorBelonging(long investorId, long clientId)
		{
			using (var db = GetDBConnect())
			{
				return db._investor_accounts.Any(x => x.id == investorId && x.client_account_id == clientId && !x.isdeleted);
			}
		}

		public InvestorStatistic[] GetInvestorsStatisticForPeriod(DateTime @from, DateTime to)
		{
			using (var db = GetDBConnect())
			{
				var x = from statistic in db._investor_statistics
						join investor in db._investor_accounts on statistic.investor_id equals investor.id
						join master in db._master_accounts on statistic.master_id equals master.trading_account_id
						where statistic.date_end >= @from && statistic.date_end < to
						select new
								{
									MasterClientId = master.client_account_id,
									MasterAccountId = master.trading_account_id,
									InvestorClientId = investor.client_account_id,
									PeriodBegin = statistic.date_begin,
									PeriodEnd = statistic.date_end,
									InvestorPercent = statistic.total_profit / statistic.profit
								};

				var result = new List<InvestorStatistic>();

				foreach (var statistic in x)
				{
					var add = false;
					foreach (var t in result)
					{
						if (t.MasterAccountId == statistic.MasterAccountId && t.MasterClientId == statistic.MasterClientId &&
							Math.Abs((t.PeriodBegin - statistic.PeriodBegin).TotalMinutes) < 1 &&
							Math.Abs((t.PeriodEnd - statistic.PeriodEnd).TotalMinutes) < 1)
						{
							t.InvestorsClientId.Add(statistic.InvestorClientId, statistic.InvestorPercent);
							add = true;
							break;
						}
					}
					if (!add)
					{
						result.Add(new InvestorStatistic
									{
										MasterAccountId = statistic.MasterAccountId,
										MasterClientId = statistic.MasterClientId,
										PeriodBegin = statistic.PeriodBegin,
										PeriodEnd = statistic.PeriodEnd,
										InvestorsClientId = new Dictionary<long, decimal> {{statistic.InvestorClientId, statistic.InvestorPercent}}
									});
					}
				}

				return result.ToArray();
			}
		}

		#endregion

		private DB GetDBConnect()
		{
			var hashedConStr = ConfigurationManager.ConnectionStrings[""].ToString();
			var connectionString = Encrypter.DecryptConnectionString(hashedConStr);
			return new DB(connectionString, true);
		}
	}
}

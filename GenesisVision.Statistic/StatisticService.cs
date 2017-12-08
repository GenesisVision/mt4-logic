using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using DataModel;
using Helpers;
using StatisticService.Logic.AccountServiceReference;
using StatisticService.Logic.ClientServiceReference;
using StatisticService.Logic.Enums;
using StatisticService.Logic.Helpers;
using StatisticService.Logic.Infrastructure;
using StatisticService.Logic.Interfaces;
using StatisticService.Logic.MailingServiceReference;
using StatisticService.Logic.Models;
using StatisticService.Logic.ServiceReference;
using StatisticService.Logic.SignalServiceReference;
using MoreLinq;
using NLog;
using AccountType = StatisticService.Logic.AccountServiceReference.AccountType;

namespace StatisticService.Logic
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
	public class StatisticService : IStatisticService
	{
		#region Fields

		public static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly StatisticManager statisticManager;
		private readonly IStatisticRepository statisticRepository;
		private readonly Dictionary<string, IMt4MySqlRepository> mt4Repositories;
		private readonly ICacheService cacheService;

		private Dictionary<long, Trader> topTraders = new Dictionary<long, Trader>();
		private object topAccountsLock = new object();

		private static Activity[] latestActivities = { };
		private static DateTime latestActivitiesTimestamp = DateTime.MinValue;

		private static OpenTradeRatio[] openTradesRatio = { };
		private static DateTime openTradesRatioTimestamp = DateTime.MinValue;

		private string ratesServer;


		#endregion

		#region Construction

		public StatisticService(
			IStatisticRepository statisticRepository, IAccountService accountService, StatisticManager statisticManager,
			Dictionary<string, IMt4MySqlRepository> repositories, IClientService clientServise, IService Service,
			IMailingService mailingService, ISignalService signalService, StatisticCalculator calculator, ICacheService cacheService
			)
		{
			try
			{
				ratesServer = ConfigurationManager.AppSettings["RatesServer"];
				this.statisticRepository = statisticRepository;
				this.accountService = accountService;
				this.clientService = clientServise;
				this.statisticManager = statisticManager;
				this.mt4Repositories = repositories;
				this.Service = Service;
				this.mailingService = mailingService;
				this.cacheService = cacheService;
				this.signalService = signalService;
				statisticManager.StatisticUpdated += StatisticUpdated;

				FirstUpdatingStatistics();

#if !DEBUG
				UpdateOpenTradesRatio();
				UpdateActivities(Constants.ActivitiesCachingAmount);
#endif
			}
			catch (Exception ex)
			{
				Logger.Fatal("Fatal error at init: {0}", ex.ToString());
				throw;
			}
		}

		#endregion

		#region Public methods

		public OperationResult CalculateStatistic(long clientId)
		{
			Logger.Trace("Recalculate statisctic for client: {0}", clientId);
			Task.Factory.StartNew(() =>
			{
				try
				{
					var clientAccounts = accountService.GetClientAccounts(clientId);
					if (!clientAccounts.IsSuccess)
					{
						Logger.Error("Error at calculate statistic for client {0}: {1}", clientId, clientAccounts.Error);
						return;
					}

					var accounts = new List<MT4AccountInfo>();

					foreach (var account in clientAccounts.Result)
					{
						var accInfo = accountService.GetMt4AccountInfo(account.TradingAccountId);
						if (!accInfo.IsSuccess)
						{
							Logger.Error("Error at calculate statistic for client {0}: {1}", clientId, accInfo.Error);
							continue;
						}

						statisticRepository.ClearStatistic(accInfo.Result.AccountId);

						var accountExist = false;
						for (var i = 0; i < 5; i++)
						{
							if (mt4Repositories[accInfo.Result.ServerName].GetAccount(accInfo.Result.Login) != null)
							{
								accountExist = true;
								break;
							}
							Thread.Sleep(2000);
						}
						if (!accountExist)
							continue;

						accounts.Add(accInfo.Result);
					}

					statisticManager.CalculateStatisticForAccouts(new Dictionary<long, stat_statistics>(), accounts);
				}
				catch (Exception ex)
				{
					Logger.Error("Error at calcultate statistic for client {0}: {1}", clientId, ex.ToString());
				}
			});
			return OperationResult.Ok();
		}

		public OperationResult CalculateStatisticAccount(long accountId)
		{
			Logger.Trace("Recalculate statistic for account: {0}", accountId);
			Task.Factory.StartNew(() =>
			{
				try
				{
					var accInfo = accountService.GetMt4AccountInfo(accountId);
					if (!accInfo.IsSuccess)
					{
						Logger.Error("Error at calculate statistic for account {0}: {1}", accountId, accInfo.Error);
						return;
					}

					statisticRepository.ClearStatistic(accInfo.Result.AccountId);

					var accountExist = false;
					for (var i = 0; i < 5; i++)
					{
						if (mt4Repositories[accInfo.Result.ServerName].GetAccount(accInfo.Result.Login) != null)
						{
							accountExist = true;
							break;
						}
						Thread.Sleep(2000);
					}
					if (!accountExist)
						return;

					statisticManager.CalculateStatisticForAccouts(new Dictionary<long, stat_statistics>(), new[] { accInfo.Result });

					UpdateStatisticAccount(accountId);
				}
				catch (Exception ex)
				{
					Logger.Error("Error at calcultate statistic for account {0}: {1}", accountId, ex.ToString());
				}
			});
			return OperationResult.Ok();
		}

		public OperationResult<Tuple<int, int>[]> GetTradesVolume(string serverName, int[] logins, DateTime dateFrom, DateTime dateTo)
		{
			Logger.Trace("Get trades volume for {2} accounts {0} on {1}", TrimString(string.Join(",", logins)), serverName, logins.Length);
			return InvokeOperations.InvokeOperation(() => mt4Repositories[serverName].GetAccountsVolume(logins, dateFrom, dateTo));
		}

		public OperationResult<Tuple<int, int>[]> GetTradesProfitInPoints(string serverName, int[] logins, DateTime dateFrom, DateTime dateTo)
		{
			Logger.Trace("Get trades volume for {2} accounts {0} on {1}", TrimString(string.Join(",", logins)), serverName, logins.Length);
			return InvokeOperations.InvokeOperation(() =>
			{
				var trades = mt4Repositories[serverName].GetAccountPeriodTrades(logins, dateFrom, dateTo);
				var activeLogins = trades.Select(x => x.LOGIN).Distinct();
				return activeLogins.Select(login => new Tuple<int, int>(login, trades.Where(x => x.LOGIN == login)
					.Sum(trade => trade.CMD == (int)OrderType.BUY
						? (int)((trade.CLOSE_PRICE - trade.OPEN_PRICE) / cacheService.GetSymbolCoefficient(trade.SYMBOL, trade.DIGITS))
						: (int)((trade.OPEN_PRICE - trade.CLOSE_PRICE) / cacheService.GetSymbolCoefficient(trade.SYMBOL, trade.DIGITS)))))
						.ToArray();
			});
		}

		public OperationResult<AccountDayStatistic> GetDayStatistic(long accountId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get day statistic for account {0}", accountId);
				var res = statisticRepository.GetLastStatistic(accountId);
				return res != null ? new AccountDayStatistic(res) : null;
			});
		}

		public OperationResult<Tuple<DateTime, decimal>[]> GetProfitStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (profit total) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);

				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, decimal>(s.date, s.closed_profit_total))
						.ToArray()
					: new[] { new Tuple<DateTime, decimal>(DateTime.Now.Date, 0m) };
			});
		}

		public OperationResult<StatisticsAccount[]> GetProfitStatisticsAccounts(long[] accountsId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistics (profit total) for {1} accounts: {0}", TrimString(string.Join(", ", accountsId)), accountsId.Length);

			return InvokeOperations.InvokeOperation(() =>
			{
				var statisticsAccounts = statisticRepository.GetStatisticsAccounts(accountsId, beginDate, endDate);

				return statisticsAccounts
					.GroupBy(x => x.account_id)
					.Select(x => new StatisticsAccount
								{
									AccountId = x.Key,
									Statistics = x
										.Select(s => new Tuple<DateTime, decimal>(s.date, s.closed_profit_total))
										.ToArray()
								})
					.Union(accountsId
						.Where(x => statisticsAccounts.All(s => s.account_id != x))
						.Select(x => new StatisticsAccount
									{
										AccountId = x,
										Statistics = new[] { new Tuple<DateTime, decimal>(DateTime.Now.Date, 0m) }
									}))
					.ToArray();
			});
		}

		public OperationResult<Tuple<DateTime, int>[]> GetProfitInPointsStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (profit in points per day) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, int>(s.date, (int)s.closed_profit_in_points_per_day))
						.ToArray()
					: new[] { new Tuple<DateTime, int>(DateTime.Now.Date, 0) };
			});
		}

		public OperationResult<Tuple<DateTime, int>[]> GetProfitInPointsTotalStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (profit in points total) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, int>(s.date, (int)s.closed_profit_in_points_total))
						.ToArray()
					: new[] { new Tuple<DateTime, int>(DateTime.Now.Date, 0) };
			});
		}

		public OperationResult<Tuple<DateTime, decimal>[]> GetProfitInPercentsTotalStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (profit in percents total) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, decimal>(s.date, s.closed_profit_in_percents_total))
						.ToArray()
					: new[] { new Tuple<DateTime, decimal>(DateTime.Now.Date, 0m) };
			});
		}

		public OperationResult<Tuple<DateTime, decimal>[]> GetProfitInPercentsStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (profit in percents per day) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, decimal>(s.date, s.closed_profit_in_percents_per_d))
						.ToArray()
					: new[] { new Tuple<DateTime, decimal>(DateTime.Now.Date, 0m) };
			});
		}

		public OperationResult<Tuple<DateTime, float>[]> GetProfitPerDayStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (profit per day) for account: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, float>(s.date, (float)s.closed_profit_per_day))
						.ToArray()
					: new[] { new Tuple<DateTime, float>(DateTime.Now.Date, 0) };
			});
		}

		public OperationResult<Tuple<DateTime, float>[]> GetProfitTotalStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (profit total) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, float>(s.date, (float)s.closed_profit_total))
						.ToArray()
					: new[] { new Tuple<DateTime, float>(DateTime.Now.Date, 0) };
			});
		}

		public OperationResult<Tuple<DateTime, float>[]> GetVolatilityStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (volatility per day) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, float>(s.date, (float)s.volatility_per_day))
						.ToArray()
					: new[] { new Tuple<DateTime, float>(DateTime.Now.Date, 0) };
			});
		}

		public OperationResult<Tuple<DateTime, float>[]> GetVolumeEfficiencyDayStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (volume efficiency per day) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, float>(s.date, (float)s.volume_efficiency_per_day))
						.ToArray()
					: new[] { new Tuple<DateTime, float>(DateTime.Now.Date, 0) };
			});
		}

		public OperationResult<Tuple<DateTime, int>[]> GetNewTradesStatistic(long accountId, DateTime beginDate, DateTime endDate)
		{
			Logger.Trace("Get statistic (trades opened per day) for: {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var stats = statisticRepository.GetStatistics(accountId, beginDate, endDate);
				return stats.Any()
					? stats
						.Select(s => new Tuple<DateTime, int>(s.date, s.trades_opened_per_day))
						.ToArray()
					: new[] { new Tuple<DateTime, int>(DateTime.Now.Date, 0) };
			});
		}

		public OperationResult<OpenedAccounts> GetOpenedAccountsStatistic()
		{
			Logger.Trace("Get opened accounts statistic");
			return InvokeOperations.InvokeOperation(() =>
			{
				var accounts = accountService.GetAllAccounts();
				if (!accounts.IsSuccess) throw new OperationException(accounts.Error, accounts.Code);

				var initialValues = statisticRepository.GetOpenedAccounts();

				var activeAccounts = accounts.Result.Where(mt4Account => !mt4Account.IsDeleted
						 && mt4Account.Action != AccountAction.Archivation)
						.ToList();
				var inactiveAccounts = accounts.Result.Except(activeAccounts)
						.ToList();

				var startTime = new DateTime(2015, 9, 22, 12, 0, 0);
				var endTime = startTime.AddDays(21);
				var nowTime = DateTime.Now;

				double coefficent;
				if (endTime > nowTime)
				{
					var totalHours = (int) Math.Round((endTime - startTime).TotalHours);
					var currentHours = (int) Math.Round((nowTime - startTime).TotalHours);
					coefficent = (double)currentHours/totalHours; 
				}
				else
				{
					coefficent = 1d;
				}
				var countInactiveTake = (int)Math.Round(inactiveAccounts.Count() * coefficent);
				activeAccounts.AddRange(inactiveAccounts.Take(countInactiveTake));
                
				return null;
			});
		}

		public OperationResult<BasicStatistic[]> GetBasicStatisticsCurrent(long[] accountsIds)
		{
			Logger.Trace("Get basic statistics for {0} accounts: {1}", accountsIds.Length, TrimString(string.Join(", ", accountsIds)));
			return InvokeOperations.InvokeOperation(() =>
			{
				var statistics = statisticRepository.GetCurrentAccountsStatistic(accountsIds);

				return statistics.Select(x => new BasicStatistic
				{
					// ToDo:
					AccountId = x.account_id,
					LoseOrders = x.closed_lose_trades_total,
					MaxDailyDrowdown = x.max_daily_drowdown,
					Profit = x.closed_profit_in_percents_total,
					ProfitOrders = x.closed_profit_trades_total,
					ProfitPoints = x.closed_profit_in_points_total,
					Volatility = x.volatility_per_day
				}).ToArray();
			});
		}

		public OperationResult<Activity[]> GetLastActivities(int count)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get last {0} activities", count);

				lock (latestActivities)
				{
					if ((DateTime.Now - latestActivitiesTimestamp) > Constants.Interval)
					{
						UpdateActivities(count);
					}

					return latestActivities;
				}
			});
		}

		public OperationResult<Activity[]> GetAccountsActivities(int count, long[] accountIds)
		{
			Logger.Trace("Get last {0} activities for {1} accounts: {2}", count, accountIds.Length, TrimString(string.Join(", ", accountIds)));
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountInfos = accountService.GetMt4AccountsInfo(accountIds);
				if (!accountInfos.IsSuccess) throw new OperationException(accountInfos.Error, accountInfos.Code);

				var clientInfos = clientService.GetClients(accountInfos.Result.Select(x => x.ClientId).Distinct().ToArray());
				if (!clientInfos.IsSuccess) throw new OperationException(clientInfos.Error, clientInfos.Code);

				var servers = accountInfos.Result.Select(x => x.ServerName).Distinct().ToArray();
				var res = new List<Activity>();

				foreach (var server in servers)
				{
					var trades = mt4Repositories[server]
						.GetAccountsClosedOrders(count, accountInfos.Result.Where(x => x.ServerName == server).Select(x => x.Login).ToArray());

					res.AddRange((from trade in trades
								  let tradingAccount = accountInfos.Result.FirstOrDefault(x => x.Login == trade.LOGIN && x.ServerName == server)
								  where tradingAccount != null
								  select new Activity
								  {
									  TradingAccountId = tradingAccount.AccountId,
									  Nickname = tradingAccount.Nickname,
									  OrderType = (OrderType)trade.CMD,
									  Price = Math.Round(trade.OPEN_PRICE, trade.DIGITS),
									  Profit = trade.CMD == (int)OrderType.BUY
										? (int)((trade.CLOSE_PRICE - trade.OPEN_PRICE) / cacheService.GetSymbolCoefficient(trade.SYMBOL, trade.DIGITS))
										: (int)((trade.OPEN_PRICE - trade.CLOSE_PRICE) / cacheService.GetSymbolCoefficient(trade.SYMBOL, trade.DIGITS)),
									  Symbol = trade.SYMBOL,
									  Time = trade.CLOSE_TIME,
									  Country = clientInfos.Result.First(x => x.ClientId == tradingAccount.ClientId).Country,
									  Avatar = tradingAccount.Avatar
								  }).ToArray());
				}

				return res.OrderByDescending(x => x.Time).Take(count).ToArray();
			});
		}

		public OperationResult<Dictionary<long, decimal>> GetBonusesTradeAmount(BonusTradeAmount[] bonuses)
		{
			Logger.Trace("Get trade amount for bonuses");
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountInfos = accountService.GetMt4AccountsInfo(bonuses.Select(x => x.AccountId).ToArray());
				if (!accountInfos.IsSuccess) throw new OperationException(accountInfos.Error, accountInfos.Code);

				var servers = accountInfos.Result.Select(x => x.ServerName).Distinct().ToList();
				var res = new Dictionary<long, decimal>();

				foreach (var server in servers)
				{
					var trades = mt4Repositories[server].GetAccountsAllClosedOrders(
						accountInfos.Result.Where(x => x.ServerName == server).Select(x => x.Login).ToArray());

					foreach (var bonus in bonuses)
					{
						var accountInfo = accountInfos.Result.FirstOrDefault(x => x.AccountId == bonus.AccountId);
						if (accountInfo != null)
						{
							var login = accountInfo.Login;
							var amount = trades.Where(x => x.LOGIN == login && x.OPEN_TIME >= bonus.DateBonus).Sum(x => x.VOLUME);
							res.Add(bonus.BonusId, (decimal)amount);
						}
						else
						{
							res.Add(bonus.BonusId, 0);
						}
					}
				}

				return res;
			});
		}

		public OperationResult<Trader[]> GetTradersRating()
		{
			Logger.Trace("Get traders rating");
			return InvokeOperations.InvokeOperation(() =>
			{
				lock (topAccountsLock)
				{
					return topTraders.Values.ToArray();
				}
			});
		}

		public OperationResult<SignalProvider[]> GetSignalProvidersRating()
		{
			Logger.Trace("Get signal providers rating");
			return InvokeOperations.InvokeOperation(() =>
			{
				lock (topAccountsLock)
				{
					return topSignalProviders.Values.ToArray();
				}
			});
		}

		public OperationResult<Manager[]> GetManagersRating()
		{
			Logger.Trace("Get  managers rating");
			return InvokeOperations.InvokeOperation(() =>
			{
				lock (topAccountsLock)
				{
					return topManagers.Values.ToArray();
				}
			});
		}

		public OperationResult<OrderData[]> GetHistory(long accountId, DateTime beginDate)
		{
			Logger.Trace("Get history for: {0} , begin date: {1}", accountId, beginDate);
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountInfo = accountService.GetMt4AccountInfo(accountId);
				var res = accountInfo.Result;
				if (!accountInfo.IsSuccess)
				{
					Logger.Error("Get trading info error: {0}", accountInfo.Error);
					throw new OperationException(accountInfo.Error, accountInfo.Code);
				}
				return mt4Repositories[res.ServerName]
							.GetOrders(res.Login, beginDate)
							.Select(o => new OrderData
										{
											ClosePrice = Math.Round(o.CLOSE_PRICE, o.DIGITS),
											Login = o.LOGIN,
											CloseTime = o.CLOSE_TIME,
											Cmd = (OrderType)o.CMD,
											Commission = o.COMMISSION,
											Digits = o.DIGITS,
											OpenPrice = Math.Round(o.OPEN_PRICE, o.DIGITS),
											OpenTime = o.OPEN_TIME,
											OrderId = o.TICKET,
											Profit = Math.Round(o.PROFIT, 2),
											Swap = o.SWAPS,
											Symbol = o.SYMBOL,
											Volume = Convert.ToInt32(o.VOLUME),
											RealVolume = o.VOLUME
										})
							.ToArray();
			});
		}

		public OperationResult<OrderData[]> GetHistory(long accountId, DateTime beginDate, int skip, int take)
		{
			Logger.Trace("Get history for: {0} , begin date: {1}, skip: {2}, take: {3}", accountId, beginDate, skip, take);
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountInfo = accountService.GetMt4AccountInfo(accountId);
				var res = accountInfo.Result;
				if (!accountInfo.IsSuccess)
				{
					Logger.Error("Get trading info error: {0}", accountInfo.Error);
					throw new OperationException(accountInfo.Error, accountInfo.Code);
				}
				return mt4Repositories[res.ServerName]
							.GetOrders(res.Login, beginDate, skip, take)
							.Select(o => new OrderData
										{
											ClosePrice = Math.Round(o.CLOSE_PRICE, o.DIGITS),
											Login = o.LOGIN,
											CloseTime = o.CLOSE_TIME,
											Cmd = (OrderType)o.CMD,
											Commission = o.COMMISSION,
											Digits = o.DIGITS,
											OpenPrice = Math.Round(o.OPEN_PRICE, o.DIGITS),
											OpenTime = o.OPEN_TIME,
											OrderId = o.TICKET,
											Profit = Math.Round(o.PROFIT, 2),
											Swap = o.SWAPS,
											Symbol = o.SYMBOL,
											Volume = Convert.ToInt32(o.VOLUME),
											RealVolume = o.VOLUME
										})
							.ToArray();
			});
		}

		public OperationResult<OrderData[]> GetPeriodHistory(long accountId, DateTime beginDate, DateTime endDate, int skip, int take)
		{
			Logger.Trace("Get history for: {0} , begin date: {1} end date: {2}", accountId, beginDate, endDate);
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountInfo = accountService.GetMt4AccountInfo(accountId);
				var res = accountInfo.Result;
				if (!accountInfo.IsSuccess)
				{
					Logger.Error("Get trading info error: {0}", accountInfo.Error);
					throw new OperationException(accountInfo.Error, accountInfo.Code);
				}
				return mt4Repositories[res.ServerName]
							.GetAccountPeriodTrades(new[] { res.Login }, beginDate, endDate, skip, take)
							.Select(o => new OrderData
										{
											ClosePrice = Math.Round(o.CLOSE_PRICE, o.DIGITS),
											Login = o.LOGIN,
											CloseTime = o.CLOSE_TIME,
											Cmd = (OrderType)o.CMD,
											Commission = o.COMMISSION,
											Digits = o.DIGITS,
											OpenPrice = Math.Round(o.OPEN_PRICE, o.DIGITS),
											OpenTime = o.OPEN_TIME,
											OrderId = o.TICKET,
											Profit = Math.Round(o.PROFIT, 2),
											Swap = o.SWAPS,
											Symbol = o.SYMBOL,
											Volume = Convert.ToInt32(o.VOLUME),
											RealVolume = o.VOLUME
										})
							.ToArray();
			});
		}

		public OperationResult<int> GetHistoryLength(long accountId, DateTime beginDate)
		{
			Logger.Trace("Get history for: {0} , begin date: {1}", accountId, beginDate);
			return InvokeOperations.InvokeOperation(() =>
			{
				var accountInfo = accountService.GetMt4AccountInfo(accountId);
				var res = accountInfo.Result;
				if (!accountInfo.IsSuccess)
				{
					Logger.Error("Get trading info error: {0}", accountInfo.Error);
					throw new OperationException(accountInfo.Error, accountInfo.Code);
				}
				var orders = mt4Repositories[res.ServerName].GetOrders(res.Login, beginDate);
				return orders.Length;
			});
		}

		public OperationResult<OpenTradeRatio[]> GetOpenTradesRatio(int count)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get open trades ratio");

				lock (openTradesRatio)
				{
					if ((DateTime.Now - openTradesRatioTimestamp) > Constants.Interval)
					{
						UpdateOpenTradesRatio();
					}

					return openTradesRatio.Take(count).ToArray();
				}
			});
		}


		#endregion

		#region Private methods

		private void FirstUpdatingStatistics()
		{
			Task.Factory.StartNew(() =>
			{
				while (true)
				{
					try
					{
						Service.GetMasters(1);
						break;
					}
					catch (Exception)
					{
						Thread.Sleep(1000);
					}
				}

				StatisticUpdated();
			});
		}

		private void StatisticUpdated()
		{
			try
			{
				Logger.Trace("Starting task update accounts..");

				var sortedAccounts = accountService.SortAccounts();
				if (!sortedAccounts.IsSuccess)
					throw new Exception(sortedAccounts.Error);

				var currentStatistic = statisticRepository.GetCurrentStatistics();

				var allAccounts = sortedAccounts.Result
									.TradingAccounts.Select(x => new Tuple<AccountRole, RatingAccount>(AccountRole.Trading, x))
									.Union(sortedAccounts.Result.SignalProviders.Select(x => new Tuple<AccountRole, RatingAccount>(AccountRole.SignalProvider, x)))
									.Union(sortedAccounts.Result.Managers.Select(x => new Tuple<AccountRole, RatingAccount>(AccountRole.Master, x)))
									.ToDictionary(x => x.Item2.AccountId, x => x);

				var allAccountsStatistic = currentStatistic.Values
															.Where(x => allAccounts.ContainsKey(x.account_id) &&
																(allAccounts[x.account_id].Item1 == AccountRole.Trading || allAccounts[x.account_id].Item1 == AccountRole.SignalProvider))
															.OrderByDescending(x => x.closed_profit_in_points_total)
															.Union(currentStatistic.Values
																					.Where(x => allAccounts.ContainsKey(x.account_id) && allAccounts[x.account_id].Item1 == AccountRole.Master)
																					.OrderByDescending(x => x.closed_profit_in_percents_total))
															.ToDictionary(x => x.account_id, x => x);

				var traders = new List<Trader>();
				var providers = new List<SignalProvider>();
				var s = new List<Manager>();

				foreach (var account in allAccounts)
				{
					stat_statistics s;
					var lastStatistic = allAccountsStatistic.TryGetValue(account.Key, out s) ? new AccountDayStatistic(s) : new AccountDayStatistic();

					AddStatisticAccount(account.Value.Item2, lastStatistic, account.Value.Item1, traders, providers, s);
				}

				traders = traders.DistinctBy(x => x.AccountId).ToList();
				providers = providers.DistinctBy(x => x.AccountId).ToList();
				s = s.DistinctBy(x => x.AccountId).ToList();

				lock (topAccountsLock)
				{
					topTraders = traders.ToDictionary(x => x.AccountId, x => x);
					topSignalProviders = providers.ToDictionary(x => x.AccountId, x => x);
					topManagers = s.ToDictionary(x => x.AccountId, x => x);
				}

				Logger.Trace("Updating accounts done");
			}
			catch (Exception ex)
			{
				Logger.Error("Error at updating statistic: {0}", ex.ToString());
			}
		}

		private void AddStatisticAccount(RatingAccount ratingAccount, AccountDayStatistic statistic, AccountRole accType, List<Trader> traders, List<SignalProvider> providers, List<Manager> s)
		{
			if (statistic.MaxOpenOrders <= 0)
				return;

			switch (accType)
			{
				case AccountRole.Trading:
					traders.Add(new Trader
								{
									AccountId = ratingAccount.AccountId,
									Age = ratingAccount.Age,
									Country = ratingAccount.Country,
									AccountNickname = ratingAccount.Nickname,
									Login = ratingAccount.Login,
									LoseOrders = statistic.ClosedLoseTradesTotal,
									MaxDailyDrowdown = (float)statistic.MaxDailyDrowdown,
									Profit = (float)statistic.ClosedProfitInPointsTotal,
									ProfitLastDay = (float)statistic.ClosedProfitInPointsPerDay,
									ProfitOrders = statistic.ClosedProfitTradesTotal,
									Volatility = (float)statistic.VolatilityPerDay,
									Avatar = ratingAccount.Avatar,
									Rating = ratingAccount.Rating,
									ClientNickname = ratingAccount.ClientNickname,
									Currency = ratingAccount.Currency,
									Chart = statisticRepository.GetDecimatedChart(ratingAccount.AccountId, Constants.PointsCountInSmallChart,
										StatisticRepository.StatisticType.ProfitInPointsTotal),
									AccountsNumber = ratingAccount.AccountsNumber,
								});
					break;
				case AccountRole.SignalProvider:
					var subscribers = signalService.GetProviderSubscribers(ratingAccount.AccountId);
					if (!subscribers.IsSuccess) throw new Exception(subscribers.Error);
					providers.Add(new SignalProvider
								{
									AccountId = ratingAccount.AccountId,
									Age = ratingAccount.Age,
									Country = ratingAccount.Country,
									AccountNickname = ratingAccount.Nickname,
									Login = ratingAccount.Login,
									Profit = (float)statistic.ClosedProfitInPointsTotal,
									ProfitLastDay = (float)statistic.ClosedProfitInPointsPerDay,
									Subscribers = subscribers.Result.Length,
									LoseOrders = statistic.ClosedLoseTradesTotal,
									MaxDailyDrowdown = (float)statistic.MaxDailyDrowdown,
									ProfitOrders = statistic.ClosedProfitTradesTotal,
									Volatility = (float)statistic.VolatilityPerDay,
									Avatar = ratingAccount.Avatar,
									Rating = ratingAccount.Rating,
									ClientNickname = ratingAccount.ClientNickname,
									Currency = ratingAccount.Currency,
									Chart = statisticRepository.GetDecimatedChart(ratingAccount.AccountId, Constants.PointsCountInSmallChart,
										StatisticRepository.StatisticType.ProfitInPointsTotal),
									AccountsNumber = ratingAccount.AccountsNumber,
								});
					break;
				case AccountRole.Master:
					var investorsCount = Service.GetIvestorsCount(ratingAccount.AccountId);
					if (!investorsCount.IsSuccess) throw new Exception(investorsCount.Error);
					s.Add(new Manager
							{
								AccountId = ratingAccount.AccountId,
								Age = ratingAccount.Age,
								Country = ratingAccount.Country,
								AccountNickname = ratingAccount.Nickname,
								Login = ratingAccount.Login,
								Profit = (float)statistic.ClosedProfitInPercentsTotal,
								ProfitLastDay = (float)statistic.ClosedProfitInPercentsPerDay,
								Investors = investorsCount.Result,
								LoseOrders = statistic.ClosedLoseTradesTotal,
								MaxDailyDrowdown = (float)statistic.MaxDailyDrowdown,
								ProfitOrders = statistic.ClosedProfitTradesTotal,
								Volatility = (float)statistic.VolatilityPerDay,
								Avatar = ratingAccount.Avatar,
								Rating = ratingAccount.Rating,
								ClientNickname = ratingAccount.ClientNickname,
								Currency = ratingAccount.Currency,
								Chart = statisticRepository.GetDecimatedChart(ratingAccount.AccountId, Constants.PointsCountInSmallChart,
									StatisticRepository.StatisticType.ProfitInPercentTotal),
								AccountsNumber = ratingAccount.AccountsNumber,
							});
					break;
			}
		}

		private void UpdateStatisticAccount(long accountId)
		{
			var mt4AccountData = accountService.GetMt4AccountInfo(accountId);
			if (!mt4AccountData.IsSuccess)
				throw new Exception(mt4AccountData.Error);

			var statistic = statisticRepository.GetLastStatistic(accountId);

			if (statistic == null && mt4AccountData.Result.Role == AccountRole.Trading)
				return;

			var clientData = clientService.GetClient(mt4AccountData.Result.ClientId);
			if (!clientData.IsSuccess)
				throw new Exception(clientData.Error);

			var traders = new List<Trader>();
			var providers = new List<SignalProvider>();
			var s = new List<Manager>();

			var clientAccountsNumberData = accountService.GetClientAccountsNumber(clientData.Result.Id, false);
			if (!clientAccountsNumberData.IsSuccess)
				throw new Exception(clientAccountsNumberData.Error);

			var ratingAccount = new RatingAccount
								{
									AccountId = mt4AccountData.Result.AccountId,
									Age = mt4AccountData.Result.Age,
									Avatar = mt4AccountData.Result.Avatar,
									Country = clientData.Result.Country,
									Currency = mt4AccountData.Result.Currency,
									Login = mt4AccountData.Result.Login,
									Name = string.IsNullOrEmpty(mt4AccountData.Result.Nickname)
										? mt4AccountData.Result.Login.ToString()
										: mt4AccountData.Result.Nickname,
									Nickname = mt4AccountData.Result.Nickname,
									Rating = mt4AccountData.Result.RatingValue,
									AccountsNumber = clientAccountsNumberData.Result,
									ClientNickname = clientData.Result.Nickname
								};

			AddStatisticAccount(ratingAccount, new AccountDayStatistic(statistic), mt4AccountData.Result.Role, traders, providers, s);
			lock (topAccountsLock)
			{
				switch (mt4AccountData.Result.Role)
				{
					case AccountRole.Trading:
						if (traders.Any())
							topTraders[traders.First().AccountId] = traders.First();
						break;
					case AccountRole.SignalProvider:
						if (providers.Any())
							topSignalProviders[providers.First().AccountId] = providers.First();
						break;
					case AccountRole.Master:
						if (s.Any())
							topManagers[s.First().AccountId] = s.First();
						break;
				}
			}
		}

		private string TrimString(string text)
		{
			return text.Length > 100
				? string.Concat(text.Substring(0, 100), "...")
				: text;
		}

		private decimal ConvertToUsd(Dictionary<string, decimal> exchangeRates, decimal amount, string currency)
		{
			if (String.Equals(currency, "USD", StringComparison.CurrentCultureIgnoreCase))
				return amount;

			decimal ratio;
			return exchangeRates.TryGetValue(string.Concat(currency, "USD"), out ratio)
				? amount * ratio
				: 0;
		}

		private void UpdateOpenTradesRatio()
		{
			Task.Factory.StartNew(() =>
			{
				try
				{
					Logger.Trace("Starting task update trades ratio..");

					var res = mt4Repositories[ratesServer].GetOpenTradesRatio(Constants.OpenTradesRatioCachingAmount);

					lock (openTradesRatio)
					{
						openTradesRatio = res;
						openTradesRatioTimestamp = DateTime.Now;
					}

					Logger.Trace("Update trades ratio done");
				}
				catch (Exception ex)
				{
					Logger.Error("Task update trades ratio error: {0}", ex.ToString());
				}
			});
		}

		private void UpdateActivities(int count)
		{
			Task.Factory.StartNew(() =>
			{
				try
				{
					Logger.Trace("Starting task update activities..");

					var serverNames = accountService.GetServersInformations().Result.Select(s => s.ServerName).ToArray();
					var res = new List<Activity>();

					foreach (var serverName in serverNames)
					{
						var trades = mt4Repositories[serverName].GetOrders(count);
						if (!trades.Any())
							continue;
						var accountInfos = accountService.GetAccountsInfoByLogin(serverName, trades.Select(x => x.Mt4Trades.LOGIN).Distinct().ToArray());
						if (!accountInfos.IsSuccess)
							throw new OperationException(accountInfos.Error, accountInfos.Code);
						var clients = accountInfos.Result.Select(x => x.ClientId).Distinct().ToArray();
						if (!clients.Any())
							continue;
						var clientInfos = clientService.GetClients(clients);
						if (!clientInfos.IsSuccess)
							throw new OperationException(clientInfos.Error, clientInfos.Code);

						res.AddRange(from trade in trades
									 let tradingAccount = accountInfos.Result.FirstOrDefault(x => x.Login == trade.Mt4Trades.LOGIN)
									 where tradingAccount != null
									 select new Activity
									 {
										 TradingAccountId = tradingAccount.AccountId,
										 Nickname = tradingAccount.Nickname,
										 OrderAction = trade.OrderAction,
										 OrderType = (OrderType)trade.Mt4Trades.CMD,
										 Price = Math.Round(trade.Mt4Trades.OPEN_PRICE, trade.Mt4Trades.DIGITS),
										 Profit = trade.Mt4Trades.CMD == (int)OrderType.BUY
													? (int)((trade.Mt4Trades.CLOSE_PRICE - trade.Mt4Trades.OPEN_PRICE) / cacheService.GetSymbolCoefficient(trade.Mt4Trades.SYMBOL, trade.Mt4Trades.DIGITS))
													: (int)((trade.Mt4Trades.OPEN_PRICE - trade.Mt4Trades.CLOSE_PRICE) / cacheService.GetSymbolCoefficient(trade.Mt4Trades.SYMBOL, trade.Mt4Trades.DIGITS)),
										 Symbol = trade.Mt4Trades.SYMBOL,
										 Time = trade.OrderAction == OrderAction.Open ? trade.Mt4Trades.OPEN_TIME : trade.Mt4Trades.CLOSE_TIME,
										 Country = clientInfos.Result.First(x => x.ClientId == tradingAccount.ClientId).Country,
										 Avatar = tradingAccount.Avatar
									 });
					}

					lock (latestActivities)
					{
						latestActivities = res.OrderByDescending(a => a.Time).ToArray();
						latestActivitiesTimestamp = DateTime.Now;
					}

					Logger.Trace("Updating activities done");
				}
				catch (Exception ex)
				{
					Logger.Error("Task update activity error: {0}", ex.ToString());
				}
			});
		}

		#endregion
	}
}
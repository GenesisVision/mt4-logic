using System;
using System.Collections.Generic;
using System.Linq;
using DataModel;
using FluentScheduler;
using StatisticService.Logic.AccountServiceReference;
using StatisticService.Logic.Helpers;
using StatisticService.Logic.Interfaces;
using StatisticService.Logic.Models;
using NLog;

namespace StatisticService.Logic
{
	public class StatisticManager
	{
		#region Fields

		private readonly Logger logger = LogManager.GetCurrentClassLogger();
		private readonly IStatisticRepository statisticRepository;
		private readonly Dictionary<string, IMt4MySqlRepository> mt4Repositories;
		private readonly StatisticCalculator calculator;
		private readonly object calculationLock;

		#endregion

		#region Construction

		public StatisticManager(IStatisticRepository statisticRepository, Dictionary<string, IMt4MySqlRepository> mt4Repositories, StatisticCalculator calculator)
		{
			this.statisticRepository = statisticRepository;
			this.mt4Repositories = mt4Repositories;
			this.calculator = calculator;

			calculationLock = new object();

			TaskManager.Initialize(new Registry());
			TaskManager.AddTask(UpdateStatistic, x => x.ToRunNow().DelayFor(5).Minutes());
			TaskManager.AddTask(UpdateStatistic, x => x.ToRunEvery(1).Days().At(1, 0));
		}

		#endregion

		#region Events

		public virtual event Action StatisticUpdated;

		private void OnStatisticUpdated()
		{
			var handler = StatisticUpdated;
			if (handler != null) handler();
		}

		#endregion

		#region Private methods

		private void UpdateStatistic()
		{
			try
			{
				lock (calculationLock)
				{
					logger.Info("Calculation daily statistic starting...");

					var accountsInService = accountService.GetAllAccounts();
					if (!accountsInService.IsSuccess)
						throw new Exception(accountsInService.Error);

					var accounts = accountsInService
										.Result
										.Where(x => x.Role != AccountRole.Tournament && x.AccountTypeId != AccountType.BO && x.AccountTypeId != AccountType.BODEMO)
										.ToArray();
					logger.Info("Received {0} accounts for calculate daily statistic", accounts.Length);

					statisticRepository.ClearStatisticOfDeletedAccounts(accounts.Select(x => x.AccountId).ToArray());

					var lastStatistics = statisticRepository
											.GetLastStatistic()
											.ToDictionary(x => x.account_id, x => x);

					CalculateStatisticForAccouts(lastStatistics, accounts);

					logger.Info("Calculation daily statistic done!");

					OnStatisticUpdated();
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error at calculate daily statistic: {0}", ex.ToString());
			}
		}

		public void CalculateStatisticForAccouts(IReadOnlyDictionary<long, stat_statistics> lastStatistics, IEnumerable<MT4AccountInfo> allAccounts)
		{
			foreach (var accountInfo in allAccounts)
			{
				var result = new List<AccountDayStatistic>();
				try
				{
					stat_statistics s;
					var lastStatistic = lastStatistics.TryGetValue(accountInfo.AccountId, out s) ? s : null;
					var lastStatisticModel = lastStatistic == null
						? new AccountDayStatistic()
						: new AccountDayStatistic(lastStatistic);
					var lastDate = lastStatistic == null ? Constants.BeginEpoch : lastStatistic.date.AddDays(1).Date;
					var nowDate = DateTime.Now.Date;

					var orders = mt4Repositories[accountInfo.ServerName].GetOrdersForDailyStatistic(accountInfo.Login, lastDate);
					if (!orders.Any() && lastDate == Constants.BeginEpoch)
						continue;

					if (lastDate == Constants.BeginEpoch)
						lastDate = orders.Min(x => x.OPEN_TIME).Date;

					for (var date = lastDate; date < nowDate; date = date.AddDays(1))
					{
						logger.Trace("Calculate account {0} for {1}", accountInfo.AccountId, date);
						var statistic = calculator.CalculateStatistic(accountInfo, date, orders, lastStatisticModel);

						lastStatisticModel = (AccountDayStatistic)statistic.Clone();
						result.Add(statistic);
					}

					//var log = string.Format("Date	Balance	Equity	Profit percent{0}{1}", Environment.NewLine,
					//	string.Join(Environment.NewLine, result.Select(x => string.Format("{3}	{0}	{1}	{2}",
					//		Math.Round(x.CurrentBalance, 2), Math.Round(x.CurrentEquity, 2), Math.Round(x.ProfitPercent, 2), x.Date))));
				}
				catch (Exception ex)
				{
					logger.Error("Error at calculate day statistic for account {0} ({1} {2}): {3}",
						accountInfo.AccountId, accountInfo.Login, accountInfo.ServerName, ex.ToString());
				}
				finally
				{
					try
					{
						if (result.Any())
							statisticRepository.InsertAccountDayStatisticNew(result.Select(x => x.ToDbModel()).ToArray());
					}
					catch (Exception ex)
					{
						logger.Error("Error at save to db day statistic for account {0} ({1} {2}): {3}",
							accountInfo.AccountId, accountInfo.Login, accountInfo.ServerName, ex.ToString());
					}
				}
			}
		}

		//private void CalculateSimbols(DateTime date)
		//{
		//	StatisticService.Logger.Trace("Calculating day {0} for bars", date);
		//	var nextDate = date.AddDays(1);

		//	try
		//	{
		//		var symbolDayStatisticsData = new List<SymbolDayStatistic[]>();
		//		var syncSymbolDayStatistics = new object();

		//		var serversName = mt4Repositories.Select(repository => repository.Key);
		//		serversName.AsParallel().ForAll(serverName =>
		//		{
		//			var unrealGroups =
		//				serversInfo.First(serverInfo => serverInfo.Name.ToLower().Equals(serverName.ToLower()))
		//					.UnrealGroups
		//					.ToArray();

		//			var symbolsData = mt4Repositories[serverName].GetSymbolsData(unrealGroups, symbols, date, nextDate);

		//			var calculatedSymbolDayStatistics = StatisticCalculator.CalculateSymbolDayStatistics(symbolsData);

		//			lock (syncSymbolDayStatistics)
		//			{
		//				symbolDayStatisticsData.Add(calculatedSymbolDayStatistics);
		//			}
		//		});

		//		var symbolDayStatistics = new List<SymbolDayStatistic>();
		//		foreach (var statistics in symbolDayStatisticsData)
		//		{
		//			foreach (var statistic in statistics)
		//			{
		//				var symbolDayStatistic =
		//					symbolDayStatistics.FirstOrDefault(
		//						stat =>
		//							stat.Symbol == statistic.Symbol && stat.DateTime == statistic.DateTime &&
		//							stat.BarType == statistic.BarType);

		//				if (symbolDayStatistic == null)
		//				{
		//					symbolDayStatistic = new SymbolDayStatistic
		//					{
		//						AddingDate = DateTime.Now,
		//						Symbol = statistic.Symbol,
		//						DateTime = statistic.DateTime,
		//						BarType = statistic.BarType
		//					};

		//					symbolDayStatistics.Add(symbolDayStatistic);
		//				}

		//				symbolDayStatistic.PercentSymbolToTotalTrades += statistic.PercentSymbolToTotalTrades;
		//				symbolDayStatistic.PercentBuyToSellVolume += statistic.PercentBuyToSellVolume;
		//				symbolDayStatistic.PercentSymbolToTotalVolume += statistic.PercentSymbolToTotalVolume;
		//				symbolDayStatistic.PercentSymbolToTotalDeferredTrades +=
		//					statistic.PercentSymbolToTotalDeferredTrades;
		//				symbolDayStatistic.PercentBuyToBuySellVolume += statistic.PercentBuyToBuySellVolume;
		//				symbolDayStatistic.CountTrades += statistic.CountTrades;
		//				symbolDayStatistic.BuyVolume += statistic.BuyVolume;
		//				symbolDayStatistic.SellVolume += statistic.SellVolume;
		//				symbolDayStatistic.CountDeferredTrades += statistic.CountDeferredTrades;
		//				symbolDayStatistic.AverageBuyPrice += statistic.AverageBuyPrice;
		//				symbolDayStatistic.AverageSellPrice += statistic.AverageSellPrice;
		//				symbolDayStatistic.AverageBuyLimitPrice += statistic.AverageBuyLimitPrice;
		//				symbolDayStatistic.AverageBuyStopPrice += statistic.AverageBuyStopPrice;
		//				symbolDayStatistic.AverageSellLimitPrice += statistic.AverageSellLimitPrice;
		//				symbolDayStatistic.AverageSellStopPrice += statistic.AverageSellStopPrice;
		//				symbolDayStatistic.AveragePrice += statistic.AveragePrice;
		//			}
		//		}

		//		var statSymbols = symbolDayStatistics.ToArray().ToDbModel();
		//		statisticRepository.InsertSymbolDayStatistic(statSymbols);
		//	}
		//	catch (Exception ex)
		//	{
		//		StatisticService.Logger.Error("Error: {0}", ex.Message);
		//	}
		//}

		#endregion
	}
}
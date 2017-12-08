using System;
using System.Collections.Generic;
using System.Linq;
using DataModel;
using StatisticService.Logic.AccountServiceReference;
using StatisticService.Logic.Helpers;
using StatisticService.Logic.Infrastructure;
using StatisticService.Logic.Interfaces;
using StatisticService.Logic.Models;
using NLog;

namespace StatisticService.Logic
{
	public class StatisticCalculator
	{
		private readonly Logger logger = LogManager.GetCurrentClassLogger();
		private readonly IBarsMySqlRepository barsMySqlRepository;
		private readonly IAccountService accountService;
		private readonly ICacheService cacheService;

		private readonly Dictionary<string, Dictionary<string, Symbol>> symbolsCache;
		private DateTime lastUpdateSymbols;

		public StatisticCalculator(IBarsMySqlRepository barsMySqlRepository, IAccountService accountService, ICacheService cacheService)
		{
			this.barsMySqlRepository = barsMySqlRepository;
			this.accountService = accountService;
			this.cacheService = cacheService;
			symbolsCache = new Dictionary<string, Dictionary<string, Symbol>>();
			lastUpdateSymbols = new DateTime();
		}

		public AccountDayStatistic CalculateStatistic(MT4AccountInfo accountInfo, DateTime date, mt4_trades[] orders, AccountDayStatistic lastStatisticModel)
		{
			var statistic = CalculateStatisticForClosedOrders(date, date.AddDays(1), accountInfo, orders, lastStatisticModel);

			CalculateProfitPercent(statistic, accountInfo.ServerName, date, date.AddDays(1).AddSeconds(-1), orders, lastStatisticModel);

			return statistic;
		}

		private AccountDayStatistic CalculateStatisticForClosedOrders(DateTime date, DateTime endOfDay, MT4AccountInfo accountInfo, IEnumerable<mt4_trades> allOrders, AccountDayStatistic previousStatistic)
		{
			var orders = allOrders
							.Where(order => order.LOGIN == accountInfo.Login &&
											((order.CLOSE_TIME >= date && order.CLOSE_TIME < endOfDay) ||
											(order.CLOSE_TIME == Constants.BeginEpoch && order.OPEN_TIME < endOfDay) ||
											(order.CLOSE_TIME >= endOfDay && order.OPEN_TIME < endOfDay)))
							.ToArray();

			var res = new AccountDayStatistic();

			res.Date = date;
			res.AccountId = accountInfo.AccountId;
			res.ClientAccountId = accountInfo.ClientId;

			res.CurrentBalance = previousStatistic.CurrentBalance +
				(decimal)orders
					.Where(trades => trades.CMD == (int)OrderType.BALANCE || trades.CMD == (int)OrderType.CREDIT || trades.CMD == (int)OrderType.BUY || trades.CMD == (int)OrderType.SELL)
					.Sum(trade => trade.PROFIT + trade.COMMISSION + trade.SWAPS + trade.TAXES);

			res.BalancePerDay = orders.Where(trades => trades.CMD == (int)OrderType.BALANCE || trades.CMD == (int)OrderType.CREDIT).Sum(trades => trades.PROFIT);

			var tradingOrders = orders
									.Where(trades => trades.CMD == (int)OrderType.BUY || trades.CMD == (int)OrderType.SELL)
									.Select(trades => trades)
									.ToArray();
			var closedOrders = tradingOrders
								.Where(trades => trades.CLOSE_TIME != Constants.BeginEpoch && trades.CLOSE_TIME < endOfDay)
								.Select(trades => trades)
								.ToArray();

			var openedOrders = tradingOrders
								.Where(trades => trades.OPEN_TIME >= date && trades.OPEN_TIME < endOfDay)
								.Select(trades => trades)
								.ToArray();

			var performance = (double)previousStatistic.RiskTotal;
			foreach (var openOrder in openedOrders)
			{
				var currentBalance = closedOrders.Where(x => x.CLOSE_TIME < openOrder.OPEN_TIME).Sum(x => x.PROFIT + x.TAXES + x.SWAPS + x.COMMISSION) +
									 orders.Where(x => (x.CMD == 5 || x.CMD == 6) && x.CLOSE_TIME < openOrder.OPEN_TIME).Sum(x => x.PROFIT);

				var orderPerformance = (openOrder.PROFIT + openOrder.SWAPS + openOrder.TAXES + openOrder.COMMISSION) / ((double)previousStatistic.CurrentBalance + currentBalance) + 1;

				if (Math.Abs(orderPerformance) > 0.01)
					performance = (performance + 1) * orderPerformance - 1;
			}
			res.RiskTotal = (decimal)performance;

			// Closed per day
			res.ClosedProfitInPointsPerDay =
				closedOrders
					.Where(order => order.CMD == (int)OrderType.BUY || order.CMD == (int)OrderType.SELL)
					.Sum(trade => trade.CMD == (int)OrderType.BUY
						? (int)((trade.CLOSE_PRICE - trade.OPEN_PRICE) / cacheService.GetSymbolCoefficient(trade.SYMBOL, trade.DIGITS))
						: (int)((trade.OPEN_PRICE - trade.CLOSE_PRICE) / cacheService.GetSymbolCoefficient(trade.SYMBOL, trade.DIGITS)));
			res.ProfitTradesCountPerDay = closedOrders.Count(order => order.PROFIT + order.COMMISSION + order.SWAPS + order.TAXES > 0.0);
			res.ClosedLoseTradesCountPerDay = closedOrders.Count(order => order.PROFIT + order.COMMISSION + order.SWAPS + order.TAXES <= 0.0);
			res.ClosedProfitPerDay = closedOrders.Sum(order => order.PROFIT + order.COMMISSION + order.SWAPS + order.TAXES);

			//res.ClosedProfitInPercentsPerDay = Math.Abs(previousStatistic.CurrentBalance) < Constants.Tolerance
			//	? 0.0
			//	: ((res.ClosedProfitPerDay / (double)previousStatistic.CurrentBalance) * 100.0);

			// Closed total
			res.ClosedLoseTradesTotal = previousStatistic.ClosedLoseTradesTotal + res.ClosedLoseTradesCountPerDay;
			//res.ClosedProfitInPercentsTotal = previousStatistic.ClosedProfitInPercentsTotal + res.ClosedProfitInPercentsPerDay;
			res.ClosedProfitInPointsTotal = previousStatistic.ClosedProfitInPointsTotal + res.ClosedProfitInPointsPerDay;
			res.ClosedProfitTotal = previousStatistic.ClosedProfitTotal + res.ClosedProfitPerDay;
			res.ClosedProfitTradesTotal = previousStatistic.ClosedProfitTradesTotal + res.ProfitTradesCountPerDay;

			res.OpenedTradesCountPerDay = tradingOrders.Count(trades => trades.OPEN_TIME >= date);
			res.OpenedTradesCountCurrent = tradingOrders.Count(order => order.CLOSE_TIME == Constants.BeginEpoch);
			res.OpenedTradesCountTotal = previousStatistic.OpenedTradesCountTotal + res.OpenedTradesCountPerDay;

			var tradeTimePerDay = closedOrders.Sum(trades => (trades.CLOSE_TIME - trades.OPEN_TIME).TotalMinutes);
			var closedTradesTotal = previousStatistic.ClosedProfitTradesTotal + previousStatistic.ClosedLoseTradesTotal;
			res.AvarageTradeTimeInMinutes = Math.Abs(tradeTimePerDay) <= 0.001
				? 0
				: tradeTimePerDay / (res.ProfitTradesCountPerDay + res.ClosedLoseTradesCountPerDay);
			var totalTradeTime = previousStatistic.AvarageTradeTimeInMinutesTotal * (closedTradesTotal) + tradeTimePerDay;
			res.AvarageTradeTimeInMinutesTotal = Math.Abs(totalTradeTime) <= 0.001
				? 0
				: totalTradeTime / (res.ClosedLoseTradesTotal + res.ClosedProfitTradesTotal);

			res.VolumePerDay = closedOrders.Sum(trades => trades.VOLUME);
			res.VolumeEfficiencyPerDay = Math.Abs(res.VolumePerDay) < 0.0001 ? 0.0 : res.ClosedProfitPerDay / res.VolumePerDay;

			res.MaxOpenOrders = res.OpenedTradesCountPerDay > previousStatistic.MaxOpenOrders
				? res.OpenedTradesCountPerDay
				: previousStatistic.MaxOpenOrders;

			res.TradeAmountTotal = previousStatistic.TradeAmountTotal +
				orders
					.Where(trades => (trades.CMD == (int)OrderType.BUY || trades.CMD == (int)OrderType.SELL) && trades.CLOSE_TIME != Constants.BeginEpoch)
					.Sum(trades => (decimal)trades.VOLUME);

			return res;
		}

		private void CalculateProfitPercent(AccountDayStatistic statistic, string serverName, DateTime date, DateTime endOfDay, IEnumerable<mt4_trades> orders, AccountDayStatistic previousStatistic)
		{
			var equity = previousStatistic.CurrentEquity;
			var balance = previousStatistic.CurrentBalance;
			var percents = new List<decimal>();
			var currentEquity = 0m;
			var tradesByPeriods = GetTradingByPeriods(orders, date, endOfDay);

			for (var i = 0; i < tradesByPeriods.Length; i++)
			{
				var endPeriod = tradesByPeriods.Count() == 1 || i >= tradesByPeriods.Length - 1 || !tradesByPeriods[i + 1].Any()
					? date.AddDays(1).AddSeconds(-1)
					: tradesByPeriods[i + 1].First().OPEN_TIME;

				var newBalance = balance;
				decimal depWithBeforeTrading;
				GetDayBalanceEquity(tradesByPeriods[i], endPeriod, ref newBalance, out currentEquity, out depWithBeforeTrading, serverName);

				var profitPercent = Math.Abs(equity + depWithBeforeTrading) < Constants.Tolerance
					? 0m
					: (currentEquity - (equity + depWithBeforeTrading)) / (equity + depWithBeforeTrading) * 100m;

				percents.Add(profitPercent);

				balance = newBalance;
				equity = currentEquity;
			}

			statistic.CurrentBalance = balance;
			statistic.CurrentEquity = currentEquity;

			statistic.ClosedProfitInPercentsPerDay = (double)percents.Sum();
			statistic.ClosedProfitInPercentsTotal = previousStatistic.ClosedProfitInPercentsTotal + statistic.ClosedProfitInPercentsPerDay;
			statistic.VolatilityPerDay = Math.Abs(statistic.ClosedProfitInPercentsPerDay);
			statistic.MaxDailyDrowdown = statistic.ClosedProfitInPercentsPerDay < previousStatistic.MaxDailyDrowdown
				? statistic.ClosedProfitInPercentsPerDay
				: previousStatistic.MaxDailyDrowdown;
		}

		private mt4_trades[][] GetTradingByPeriods(IEnumerable<mt4_trades> allTrades, DateTime dateFrom, DateTime dateTo)
		{
			var daily = allTrades
							.Where(t => (t.OPEN_TIME >= dateFrom && t.OPEN_TIME <= dateTo) ||
										(t.CLOSE_TIME >= dateFrom && t.CLOSE_TIME <= dateTo) ||
										(t.OPEN_TIME < dateFrom && (t.CLOSE_TIME >= dateTo || t.CLOSE_TIME == Constants.BeginEpoch)))
							.ToArray();

			if (daily.All(x => x.CMD != (int)OrderType.BALANCE && x.CMD != (int)OrderType.CREDIT))
			{
				return new[] { daily };
			}

			var result = new List<mt4_trades[]>();

			var periods = new List<Tuple<DateTime, DateTime>>();
			var balances = daily.Where(x => x.CMD == (int)OrderType.BALANCE || x.CMD == (int)OrderType.CREDIT).ToList();

			periods.Add(new Tuple<DateTime, DateTime>(dateFrom, balances.First().OPEN_TIME));
			balances.RemoveAt(0);
			foreach (var balance in balances)
			{
				periods.Add(new Tuple<DateTime, DateTime>(periods.Last().Item2, balance.OPEN_TIME));
			}
			periods.Add(new Tuple<DateTime, DateTime>(periods.Last().Item2, dateTo.AddSeconds(1)));

			foreach (var period in periods)
			{
				var periodTrade = daily
									.Where(x => (x.OPEN_TIME >= period.Item1 && x.OPEN_TIME < period.Item2) ||
												(x.CLOSE_TIME >= period.Item1 && x.CLOSE_TIME < period.Item2) ||
												(x.OPEN_TIME < period.Item1 && (x.CLOSE_TIME >= period.Item2 || x.CLOSE_TIME == Constants.BeginEpoch)))
									.OrderByDescending(x => x.CMD)
									.ToArray();

				// if balance operation and closing order in same time (hotfix for )
				if (periodTrade.Any() && periodTrade.First().CMD == (int)OrderType.BALANCE)
				{
					var closeDate = periodTrade.First().CLOSE_TIME;
					periodTrade = periodTrade
						.Where(x => x.CMD == (int)OrderType.BALANCE || x.CMD == (int)OrderType.CREDIT || x.CLOSE_TIME > closeDate || x.CLOSE_TIME == Constants.BeginEpoch)
						.ToArray();
				}

				if (periodTrade.Any())
					result.Add(periodTrade);
			}

			return result.ToArray();
		}

		private void GetDayBalanceEquity(IEnumerable<mt4_trades> orders, DateTime date, ref decimal balance, out decimal equity, out decimal depWithBeforeTrading, string serverName)
		{
			depWithBeforeTrading = 0m;
			var floating = 0m;

			foreach (var order in orders)
			{
				if (order.CLOSE_TIME != Constants.BeginEpoch && order.CLOSE_TIME <= date &&
					(order.CMD == (int)OrderType.BUY || order.CMD == (int)OrderType.SELL || order.CMD == (int)OrderType.BALANCE || order.CMD == (int)OrderType.CREDIT))
				{
					balance += (decimal)(order.PROFIT + order.SWAPS + order.COMMISSION);

					if (order.CMD == (int)OrderType.BALANCE || order.CMD == (int)OrderType.CREDIT)
					{
						depWithBeforeTrading += (decimal)(order.PROFIT + order.SWAPS + order.COMMISSION);
					}
				}

				if ((order.CLOSE_TIME == Constants.BeginEpoch || order.CLOSE_TIME > date) && (order.CMD == (int)OrderType.BUY || order.CMD == (int)OrderType.SELL))
				{
					floating += GetFloating(date, order, serverName);
				}
			}

			equity = balance + floating;
		}

		private decimal GetFloating(DateTime date, mt4_trades order, string serverName)
		{
			double dayCloseBarPrice;
			if (!GetHistoryPrice(serverName, order.SYMBOL, date, out dayCloseBarPrice))
				return 0m;

			var spread = order.CMD == (int)OrderType.SELL
				? GetSymbolSpread(serverName, order.SYMBOL)
				: 0;

			var contractSize = order.SYMBOL == Constants.Silver1 || order.SYMBOL == Constants.Gold1 ||
								  order.SYMBOL == Constants.Silver2 || order.SYMBOL == Constants.Gold2 ||
								  (order.SYMBOL.Length == 6 && order.SYMBOL.Substring(3, 3) == Constants.Jpy)
				? 100
				: 10000;

			var cost = CalculatePipValue(1, order.SYMBOL, date, serverName);

			var profit = order.CMD == (int)OrderType.BUY
				? (dayCloseBarPrice - order.OPEN_PRICE) * contractSize * cost * (order.VOLUME / 100)			// buy
				: (order.OPEN_PRICE - dayCloseBarPrice - spread) * contractSize * cost * (order.VOLUME / 100);	// sell

			return (decimal)profit;
		}

		private double CalculatePipValue(double amount, string symbol, DateTime date, string serverName)
		{
			double result;
			int lots;
			switch (symbol)
			{
				case Constants.Gold1:
				case Constants.Gold2:
					lots = 1;
					break;
				case Constants.Silver1:
				case Constants.Silver2:
					lots = 50;
					break;
				default:
					if (symbol.Length >= 6)
					{
						lots = symbol.Substring(3, 3) == Constants.Jpy ? 1000 : 10;
					}
					else
					{
						lots = 1;  // if unknown
					}
					break;
			}

			double price;
			if (symbol == Constants.Gold2 || symbol == Constants.Silver2)  // same as xUSD
			{
				result = amount * lots;
			}
			else if (symbol.Length < 6)
			{
				result = amount * lots;  // if unknown cfd
				logger.Debug("Unknown symbol: {0}", symbol);
			}
			else if (symbol.Substring(3, 3) == Constants.Usd)  // xUSD
			{
				result = amount * lots;
			}
			else if (symbol.Substring(0, 3) == Constants.Usd)  // USDx
			{
				result = GetHistoryPrice(serverName, symbol, date, out price)
					? amount * lots / price
					: 0.0;
			}
			else
			{
				// if symbol: GBPCAD
				var tmpSymbol = symbol.Substring(3, 3) + Constants.Usd;  // GBPCAD -> CADUSD
				if (SymbolExits(serverName, tmpSymbol))
				{
					result = GetHistoryPrice(serverName, symbol, date, out price)
						? amount * lots * price
						: 0.0;
				}
				else  // GBPCAD -> USDCAD
				{
					tmpSymbol = Constants.Usd + symbol.Substring(3, 3);
					if (SymbolExits(serverName, tmpSymbol))
					{
						result = GetHistoryPrice(serverName, symbol, date, out price)
							? amount * lots / price
							: 0.0;
					}
					else
					{
						result = 0;  // if unknown
						logger.Debug("Unknown symbol: {0}", symbol);
					}
				}
			}

			if (double.IsNaN(result) || double.IsInfinity(result))
				return 0;

			return Math.Round(result, 2);
		}

		private bool SymbolExits(string serverName, string symbol)
		{
			UpdateSymbolsCache(serverName);

			lock (symbolsCache)
			{
				return symbolsCache.ContainsKey(serverName) && symbolsCache[serverName].ContainsKey(symbol);
			}
		}

		private bool GetHistoryPrice(string serverName, string symbol, DateTime date, out double price)
		{
			return barsMySqlRepository.GetOpenPrice(serverName, symbol, date, out price);
		}

		private double GetSymbolSpread(string serverName, string symbol)
		{
			UpdateSymbolsCache(serverName);

			lock (symbolsCache)
			{
				Symbol symbolInfo;
				return symbolsCache.ContainsKey(serverName) && symbolsCache[serverName].TryGetValue(symbol, out symbolInfo)
					? symbolInfo.Spread * (1 / Math.Pow(10, symbolInfo.Digits))
					: 0;
			}
		}

		private void UpdateSymbolsCache(string serverName)
		{
			lock (symbolsCache)
			{
				if (symbolsCache.Any() && symbolsCache.ContainsKey(serverName) && (DateTime.Now - lastUpdateSymbols).TotalDays < 1)
				{
					return;
				}

				var symbols = accountService.GetSymbolsInfo(serverName);
				if (!symbols.IsSuccess)
					throw new Exception(symbols.Error);

				lastUpdateSymbols = DateTime.Now;
				if (symbolsCache.ContainsKey(serverName))
				{
					symbolsCache[serverName].Clear();
					symbolsCache[serverName] = symbols.Result.ToDictionary(x => x.Name, x => x);
				}
				else
				{
					symbolsCache.Add(serverName, symbols.Result.ToDictionary(x => x.Name, x => x));
				}
			}
		}

		public static SymbolDayStatistic[] CalculateSymbolDayStatistics(SymbolData[] symbolsData)
		{
			var symbolDayStatistics = new List<SymbolDayStatistic>();

			var symbolsTotalDayStatistics = new List<SymbolsTotalDayStatistic>();
			foreach (var symbolData in symbolsData)
			{
				foreach (var barData in symbolData.BarsData)
				{
					var barType = barData.BarType;

					foreach (var barDateData in barData.BarDatesData)
					{
						var symbolsTotalDayStatistic = symbolsTotalDayStatistics.FirstOrDefault(
							totalDayStatistic => totalDayStatistic.BarType == barType && totalDayStatistic.DateTime == barDateData.DateTime);

						if (symbolsTotalDayStatistic == null)
						{
							symbolsTotalDayStatistic = new SymbolsTotalDayStatistic
														{
															BarType = barType,
															DateTime = barDateData.DateTime
														};
							symbolsTotalDayStatistics.Add(symbolsTotalDayStatistic);
						}


						symbolsTotalDayStatistic.TotalCountTrades += barDateData.Orders.
							Count(order => order.CMD == (int)OrderType.BUY || order.CMD == (int)OrderType.SELL);
						symbolsTotalDayStatistic.TotalVolume += barDateData.Orders.
							Where(order => order.CMD == (int)OrderType.BUY || order.CMD == (int)OrderType.SELL).Sum(order => Convert.ToInt32(order.VOLUME));
						symbolsTotalDayStatistic.TotalCountDeferredTrades += barDateData.Orders.
							Count(order => order.CMD == (int)OrderType.BUY_LIMIT || order.CMD == (int)OrderType.BUY_STOP ||
										   order.CMD == (int)OrderType.SELL_LIMIT || order.CMD == (int)OrderType.SELL_STOP);
					}
				}
			}

			foreach (var symbolData in symbolsData)
			{
				string symbol = symbolData.Symbol;
				foreach (var barData in symbolData.BarsData)
				{
					var barType = barData.BarType;

					foreach (var barDateData in barData.BarDatesData)
					{
						var symbolDayStatistic = symbolDayStatistics.FirstOrDefault(
							symbolDayStat =>
								symbolDayStat.Symbol == symbol && symbolDayStat.BarType == barType &&
								symbolDayStat.DateTime == barDateData.DateTime);

						if (symbolDayStatistic == null)
						{
							symbolDayStatistic = new SymbolDayStatistic
												{
													Symbol = symbol,
													BarType = barType,
													DateTime = barDateData.DateTime
												};

							symbolDayStatistics.Add(symbolDayStatistic);
						}

						var ordersBuy = barDateData.Orders.Where(order => order.CMD == (int)OrderType.BUY).ToArray();
						var ordersSell = barDateData.Orders.Where(order => order.CMD == (int)OrderType.SELL).ToArray();
						var ordersBuyLimit = barDateData.Orders.Where(order => order.CMD == (int)OrderType.BUY_LIMIT).ToArray();
						var ordersBuyStop = barDateData.Orders.Where(order => order.CMD == (int)OrderType.BUY_STOP).ToArray();
						var ordersSellLimit = barDateData.Orders.Where(order => order.CMD == (int)OrderType.SELL_LIMIT).ToArray();
						var ordersSellStop = barDateData.Orders.Where(order => order.CMD == (int)OrderType.SELL_STOP).ToArray();

						symbolDayStatistic.CountTrades += ordersBuy.Length + ordersSell.Length;
						symbolDayStatistic.BuyVolume += ordersBuy.Sum(order => Convert.ToInt32(order.VOLUME));
						symbolDayStatistic.SellVolume += ordersSell.Sum(order => Convert.ToInt32(order.VOLUME));
						symbolDayStatistic.CountDeferredTrades += ordersBuyLimit.Length + ordersBuyStop.Length +
																  ordersSellLimit.Length + ordersSellStop.Length;

						symbolDayStatistic.AverageBuyPrice = symbolDayStatistic.BuyVolume != 0
							? ordersBuy.Sum(order => order.VOLUME * order.OPEN_PRICE) /
							  symbolDayStatistic.BuyVolume
							: 0;

						symbolDayStatistic.AverageSellPrice = symbolDayStatistic.SellVolume != 0
							? ordersSell.Sum(order => order.VOLUME * order.OPEN_PRICE) /
							  symbolDayStatistic.SellVolume
							: 0;

						int ordersBuyLimitVolume = ordersBuyLimit.Sum(order => Convert.ToInt32(order.VOLUME));
						symbolDayStatistic.AverageBuyLimitPrice = ordersBuyLimitVolume != 0
							? ordersBuyLimit.Sum(order => order.VOLUME * order.OPEN_PRICE) /
							  ordersBuyLimit.Sum(order => order.VOLUME)
							: 0;

						int ordersBuyStopVolume = ordersBuyStop.Sum(order => Convert.ToInt32(order.VOLUME));
						symbolDayStatistic.AverageBuyStopPrice = ordersBuyStopVolume != 0
							? ordersBuyStop.Sum(order => order.VOLUME * order.OPEN_PRICE) /
							  ordersBuyStopVolume
							: 0;

						int ordersSellLimitVolume = ordersSellLimit.Sum(order => Convert.ToInt32(order.VOLUME));
						symbolDayStatistic.AverageSellLimitPrice = ordersSellLimitVolume != 0
							? ordersSellLimit.Sum(order => order.VOLUME * order.OPEN_PRICE) /
							  ordersSellLimitVolume
							: 0;

						int ordersSellStopVolume = ordersSellStop.Sum(order => Convert.ToInt32(order.VOLUME));
						symbolDayStatistic.AverageSellStopPrice = ordersSellStopVolume != 0
							? ordersSellStop.Sum(order => order.VOLUME * order.OPEN_PRICE) /
							  ordersSellStopVolume
							: 0;


						symbolDayStatistic.AveragePrice = (symbolDayStatistic.BuyVolume + symbolDayStatistic.SellVolume) != 0
							? ordersBuy.Union(ordersSell).
								Sum(order => order.VOLUME * order.OPEN_PRICE) /
							  (symbolDayStatistic.BuyVolume + symbolDayStatistic.SellVolume)
							: 0;
					}
				}
			}

			foreach (var symbolDayStatistic in symbolDayStatistics)
			{
				var symbolsTotalDayStatistic = symbolsTotalDayStatistics.First(symbolsTotalDayStat =>
					symbolsTotalDayStat.BarType == symbolDayStatistic.BarType &&
					symbolsTotalDayStat.DateTime == symbolDayStatistic.DateTime);

				symbolDayStatistic.PercentSymbolToTotalTrades = symbolsTotalDayStatistic.TotalCountTrades != 0
					? (double)symbolDayStatistic.CountTrades /
					  symbolsTotalDayStatistic.TotalCountTrades
					: 0;

				symbolDayStatistic.PercentBuyToSellVolume = (symbolDayStatistic.BuyVolume + symbolDayStatistic.SellVolume) != 0
					? (double)(symbolDayStatistic.BuyVolume - symbolDayStatistic.SellVolume) /
					  (symbolDayStatistic.BuyVolume + symbolDayStatistic.SellVolume)
					: 0;

				symbolDayStatistic.PercentSymbolToTotalVolume = symbolsTotalDayStatistic.TotalVolume != 0
					? (double)(symbolDayStatistic.BuyVolume + symbolDayStatistic.SellVolume) /
					  symbolsTotalDayStatistic.TotalVolume
					: 0;
				symbolDayStatistic.PercentSymbolToTotalDeferredTrades = symbolsTotalDayStatistic.TotalCountDeferredTrades != 0
					? (double)symbolDayStatistic.CountDeferredTrades /
					  symbolsTotalDayStatistic.TotalCountDeferredTrades
					: 0;

				symbolDayStatistic.PercentBuyToBuySellVolume = (symbolDayStatistic.BuyVolume + symbolDayStatistic.SellVolume) != 0
					? (double)symbolDayStatistic.BuyVolume /
					  (symbolDayStatistic.BuyVolume + symbolDayStatistic.SellVolume)
					: 0;
			}

			return symbolDayStatistics.ToArray();
		}
	}
}

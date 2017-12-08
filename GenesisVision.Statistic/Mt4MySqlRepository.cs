using System;
using System.Collections.Generic;
using System.Linq;
using DataModel;
using LinqToDB.DataProvider.MySql;
using StatisticService.Entities.Enums;
using StatisticService.Logic.AccountServiceReference;
using StatisticService.Logic.Helpers;
using StatisticService.Logic.Interfaces;
using StatisticService.Logic.Models;

namespace StatisticService.Logic
{
	public class Mt4MySqlRepository : IMt4MySqlRepository
	{
		private readonly string connectionString;
		private readonly double multiplier;

		public Mt4MySqlRepository(string host, int port, string dbName, string user, string password, double multiplier)
		{
			connectionString = string.Format("Server={0};Port={1};Database={2};Uid={3};Pwd={4};charset=utf8;",
				host, port, dbName, user, password);
			this.multiplier = multiplier;
		}

		public mt4_trades[] GetOrders(DateTime prevDate)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.mt4_trades
							join u in db.mt4_users on t.LOGIN equals u.LOGIN
							where u.STATUS == "2" &&
								(t.CMD == (int)OrderType.BUY || t.CMD == (int)OrderType.SELL || t.CMD == (int)OrderType.BALANCE) &&
								(t.CLOSE_TIME >= prevDate || t.OPEN_TIME >= prevDate || t.CLOSE_TIME == Constants.BeginEpoch)
							select t;

				return FixTradesProfit(query.ToArray());
			}
		}

		public mt4_trades[] GetOrdersForDailyStatistic(int login, DateTime prevDate)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.mt4_trades
							where t.LOGIN == login &&
								(t.CMD == (int)OrderType.BUY || t.CMD == (int)OrderType.SELL || t.CMD == (int)OrderType.BALANCE || t.CMD == (int)OrderType.CREDIT) &&
								(t.CLOSE_TIME >= prevDate || t.OPEN_TIME >= prevDate || t.CLOSE_TIME == Constants.BeginEpoch)
							select t;

				return FixTradesProfit(query.ToArray());
			}
		}

		public mt4_trades[] GetOrders(int login, DateTime prevDate)
		{
			using (var db = GetConnection())
			{
				var query = db.mt4_trades
							.Where(order => order.LOGIN == login && order.OPEN_TIME >= prevDate && order.CLOSE_TIME != Constants.BeginEpoch &&
									(order.CMD == (int)OrderType.BUY || order.CMD == (int)OrderType.SELL))
							.OrderByDescending(trades => trades.CLOSE_TIME)
							.Select(order => order);
				return FixTradesProfit(query.ToArray());
			}
		}

		public mt4_trades[] GetOrders(int login, DateTime prevDate, int skip, int take)
		{
			using (var db = GetConnection())
			{
				var query = db.mt4_trades
					.Where(order => order.LOGIN == login
						&& order.OPEN_TIME >= prevDate
						&& order.CLOSE_TIME != Constants.BeginEpoch
						&& (order.CMD == (int)OrderType.BUY
							|| order.CMD == (int)OrderType.SELL))
					.OrderByDescending(trades => trades.CLOSE_TIME)
					.Select(order => order)
					.Skip(skip)
					.Take(take);
				return FixTradesProfit(query.ToArray());
			}
		}

		public mt4_trades[] GetAccountsClosedOrders(int count, int[] logins)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.mt4_trades
							join u in db.mt4_users on t.LOGIN equals u.LOGIN
							where (t.CMD == (int)OrderType.BUY || t.CMD == (int)OrderType.SELL) &&
							t.CLOSE_TIME != Constants.BeginEpoch &&
							logins.Contains(t.LOGIN)
							orderby t.CLOSE_TIME descending
							select t;
				return FixTradesProfit(query.Take(count).ToArray());
			}
		}

		public mt4_trades[] GetAccountsAllClosedOrders(int[] logins)
		{
			using (var db = GetConnection())
			{
				var query = from t in db.mt4_trades
							join u in db.mt4_users on t.LOGIN equals u.LOGIN
							where (t.CMD == (int)OrderType.BUY || t.CMD == (int)OrderType.SELL) &&
							t.CLOSE_TIME != Constants.BeginEpoch &&
							logins.Contains(t.LOGIN)
							select t;
				return FixTradesProfit(query.ToArray());
			}
		}

		public SymbolData[] GetSymbolsData(UnrealGroup[] unrealGroups, string[] symbols, DateTime fromDate, DateTime toDate)
		{
			var symbolsData = new List<SymbolData>();

			var unrealGroupsNoMask = unrealGroups.Where(unrealGroup => !unrealGroup.IsMask).Select(unrealGroup => unrealGroup.Name);
			var unrealGroupsMask = unrealGroups.Where(unrealGroup => unrealGroup.IsMask).Select(unrealGroup => unrealGroup.Name);

			mt4_trades[] allOrders;
			using (var db = GetConnection())
			{
				var ordersWithGroup = db.mt4_trades
					.Where(order => order.OPEN_TIME > Constants.FirstDate2018 &&
						(order.OPEN_TIME < toDate && ((order.CLOSE_TIME >= fromDate) || order.CLOSE_TIME == Constants.BeginEpoch)) &&
						(!db.mt4_users.Any(user => user.LOGIN == order.LOGIN && unrealGroupsNoMask.Contains(user.GROUP.ToLower()))))
					.Select(order => new
						{
							Order = order,
							Group = db.mt4_users.First(user => user.LOGIN == order.LOGIN).GROUP
						})
					.ToArray();

				allOrders = ordersWithGroup
					.Where(orderWithGroup => !unrealGroupsMask.Any(unrealGroupMask => orderWithGroup.Group.ToLower().Contains(unrealGroupMask)))
					.Select(orderWithGroup => orderWithGroup.Order)
					.ToArray();
			}

			foreach (var symbol in symbols)
			{
				var barsData = new List<BarData>();
				var bars = Enum.GetNames(typeof(BarType));
				foreach (var bar in bars)
				{
					var barDatesData = new List<BarDateData>();

					var barType = (BarType)Enum.Parse(typeof(BarType), bar);

					var fromBarDate = fromDate;
					while (fromBarDate < toDate)
					{
						DateTime nextBarDate;

						switch (barType)
						{
							case BarType.M5: nextBarDate = fromBarDate.AddMinutes(5); break;
							case BarType.M15: nextBarDate = fromBarDate.AddMinutes(15); break;
							case BarType.M30: nextBarDate = fromBarDate.AddMinutes(30); break;
							case BarType.H1: nextBarDate = fromBarDate.AddHours(1); break;
							case BarType.H4: nextBarDate = fromBarDate.AddHours(4); break;
							case BarType.D1: nextBarDate = fromBarDate.AddDays(1); break;
							case BarType.W1:
								fromBarDate = fromBarDate.AddDays(-(int)fromBarDate.DayOfWeek + 1);
								nextBarDate = fromBarDate.AddDays(7);
								break;
							case BarType.MN:
								fromBarDate = new DateTime(fromBarDate.Year, fromBarDate.Month, 1);
								nextBarDate = fromBarDate.AddMonths(1);
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}

						var barOrders = allOrders
							.Where(order => order.SYMBOL.Equals(symbol) &&
											(order.OPEN_TIME < nextBarDate &&
											 (order.CLOSE_TIME >= fromBarDate ||
											  order.CLOSE_TIME.Date == Constants.BeginEpoch)))
							.ToArray();

						barDatesData.Add(new BarDateData
						{
							DateTime = fromBarDate,
							Orders = barOrders
						});

						fromBarDate = nextBarDate;
					}

					barsData.Add(new BarData
					{
						BarType = barType,
						BarDatesData = barDatesData.ToArray()
					});
				}

				var symbolData = new SymbolData
				{
					Symbol = symbol,
					BarsData = barsData.ToArray()
				};

				symbolsData.Add(symbolData);
			}

			return symbolsData.ToArray();
		}

		public MT4Activity[] GetOrders(int count)
		{
			using (var db = GetConnection())
			{
				var queryOpen = (from t in db.mt4_trades
								 join u in db.mt4_users on t.LOGIN equals u.LOGIN
								 where u.STATUS == "2" && (t.CMD == (int)OrderType.BUY || t.CMD == (int)OrderType.SELL)
								 orderby t.OPEN_TIME descending
								 select t).Take((int)(count * 0.1)).ToArray();

				var queryCloseWin = (from t in db.mt4_trades
									 join u in db.mt4_users on t.LOGIN equals u.LOGIN
									 where u.STATUS == "2" && (t.CMD == (int)OrderType.BUY || t.CMD == (int)OrderType.SELL) && t.PROFIT > 0
									 orderby t.CLOSE_TIME descending
									 select t).Take((int)(count * 0.8)).ToArray();

				var queryCloseLoss = (from t in db.mt4_trades
									  join u in db.mt4_users on t.LOGIN equals u.LOGIN
									  where u.STATUS == "2" && (t.CMD == (int)OrderType.BUY || t.CMD == (int)OrderType.SELL) && t.PROFIT < 0
									  orderby t.CLOSE_TIME descending
									  select t).Take((int)(count * 0.1)).ToArray();

				var queryUnion = FixTradesProfit(queryOpen
									.Union(queryCloseWin)
									.Union(queryCloseLoss)
									.GroupBy(trades => trades.TICKET)
									.Select(tradeses => tradeses.First())
									.ToArray());

				var t1 = queryUnion.Select(trades => new { trades, time = trades.OPEN_TIME, type = OrderAction.Open });
				var t2 = queryUnion.Select(trades => new { trades, time = trades.CLOSE_TIME, type = OrderAction.Close });
				var resUnion = t1.Union(t2).OrderByDescending(arg => arg.time).Select(arg => new MT4Activity { Mt4Trades = arg.trades, OrderAction = arg.type });

				return resUnion.Take(count).ToArray();
			}
		}

		public mt4_trades[] GetAllOrders(int login)
		{
			using (var db = GetConnection())
			{
				return FixTradesProfit(db.mt4_trades.Where(trades => trades.LOGIN == login).ToArray());
			}
		}

		public Dictionary<int, mt4_users> GetAccounts()
		{
			using (var db = GetConnection())
			{
				var query = db.mt4_users
					.Where(users => users.STATUS == "2")
					.Select(users => users);
				return query.ToDictionary(users => users.LOGIN, FixUserProfit);
			}
		}

		public mt4_trades[] GetAccountPeriodTrades(int[] logins, DateTime dateFrom, DateTime dateTo, int skip, int take)
		{
			using (var db = GetConnection())
			{
				var query = from trade in db.mt4_trades
							where logins.Contains(trade.LOGIN) &&
								  trade.OPEN_TIME >= dateFrom &&
								  trade.CLOSE_TIME != Constants.BeginEpoch &&
								  trade.CLOSE_TIME <= dateTo &&
								  (trade.CMD == (int)OrderType.BUY
									|| trade.CMD == (int)OrderType.SELL)
							select trade;

				return FixTradesProfit(query.Skip(skip).Take(take).ToArray());
			}
		}

		public mt4_trades[] GetAccountPeriodTrades(int[] logins, DateTime? dateFrom, DateTime? dateTo)
		{
			using (var db = GetConnection())
			{
				return FixTradesProfit(db.mt4_trades
										.Where(trade => logins.Contains(trade.LOGIN) &&
											(dateFrom == null || trade.OPEN_TIME >= dateFrom) &&
											trade.CLOSE_TIME != Constants.BeginEpoch &&
											(dateTo == null || trade.CLOSE_TIME <= dateTo) &&
											(trade.CMD == (int)OrderType.BUY || trade.CMD == (int)OrderType.SELL))
										.ToArray());
			}
		}

		public mt4_trades[] GetAccountPeriodTrades(int[] logins, DateTime dateFrom, DateTime dateTo)
		{
			using (var db = GetConnection())
			{
				var query = from trade in db.mt4_trades
							where logins.Contains(trade.LOGIN) &&
								  trade.OPEN_TIME >= dateFrom &&
								  trade.CLOSE_TIME != Constants.BeginEpoch &&
								  trade.CLOSE_TIME <= dateTo &&
								  (trade.CMD == (int)OrderType.BUY
									|| trade.CMD == (int)OrderType.SELL)
							select trade;

				return FixTradesProfit(query.ToArray());
			}
		}

		public mt4_users GetAccount(int login)
		{
			using (var db = GetConnection())
			{
				return FixUserProfit(db.mt4_users.FirstOrDefault(users => users.LOGIN == login));
			}
		}

		public Tuple<int, double>[] GetAccountsBalances(int[] logins)
		{
			using (var db = GetConnection())
			{
				return db.mt4_users
						.Where(x => logins.Contains(x.LOGIN))
						.Select(x => new Tuple<int, double>(x.LOGIN, x.BALANCE / multiplier))
						.ToArray();
			}
		}

		public Tuple<int, int>[] GetAccountsVolume(int[] logins, DateTime dateFrom, DateTime dateTo)
		{
			using (var db = GetConnection())
			{
				var query = from trade in db.mt4_trades
							where logins.Contains(trade.LOGIN) &&
								  trade.OPEN_TIME >= dateFrom &&
								  trade.CLOSE_TIME != Constants.BeginEpoch &&
								  trade.CLOSE_TIME <= dateTo &&
								  trade.CMD != (int)OrderType.BALANCE
							group trade by trade.LOGIN
								into g
								select new Tuple<int, int>(g.Key, g.Sum(p => Convert.ToInt32(p.VOLUME / multiplier)));

				return query.ToArray();
			}
		}

		public OpenTradeRatio[] GetOpenTradesRatio(int count)
		{
			using (var db = GetConnection())
			{
				var trades = db.mt4_trades.Where(trade => trade.CLOSE_TIME == Constants.BeginEpoch).ToArray();
				var symbols = trades.Select(x => x.SYMBOL).Distinct().ToArray();

				var result = symbols.ToDictionary(s => s, s => new TradeVolume
																{
																	Buy = trades.Count(x => x.SYMBOL == s && x.CMD == (int)OrderType.BUY),
																	Sell = trades.Count(x => x.SYMBOL == s && x.CMD == (int)OrderType.SELL)
																});

				var converter = new Func<KeyValuePair<string, TradeVolume>, OpenTradeRatio>(pair =>
				{
					var volume = pair.Value.Buy + pair.Value.Sell;
					var buyRatio = volume == 0 ? 0 : pair.Value.Buy * 100 / volume;
					return new OpenTradeRatio
						{
							Symbol = pair.Key,
							Volume = (int)volume,
							BuyRatio = (int)buyRatio,
							SellRatio = 100 - (int)buyRatio
						};
				});

				return result.Select(converter).OrderByDescending(ratio => ratio.Volume).Take(count).ToArray();
			}
		}

		private MetaTraderDB GetConnection()
		{
			return new MetaTraderDB(new MySqlDataProvider(), connectionString) { CommandTimeout = 600 };
		}

		private mt4_trades[] FixTradesProfit(mt4_trades[] trades)
		{
			if (multiplier == 1)
				return trades;

			foreach (var trade in trades)
			{
				trade.PROFIT /= multiplier;
				trade.SWAPS /= multiplier;
				trade.COMMISSION /= multiplier;
				trade.COMMISSION_AGENT /= multiplier;
				trade.TAXES /= multiplier;
				trade.VOLUME /= multiplier;
			}
			return trades;
		}

		private mt4_users FixUserProfit(mt4_users user)
		{
			if (multiplier == 1)
				return user;

			user.BALANCE /= multiplier;
			user.PREVMONTHBALANCE /= multiplier;
			user.PREVBALANCE /= multiplier;
			user.CREDIT /= multiplier;
			user.EQUITY /= multiplier;
			user.MARGIN /= multiplier;
			user.MARGIN_FREE /= multiplier;

			return user;
		}
	}
}

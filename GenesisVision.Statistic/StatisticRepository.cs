using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Helpers.Security;
using StatisticService.Entities;
using StatisticService.Entities.Enums;
using StatisticService.Logic.AccountServiceReference;
using StatisticService.Logic.CommissionServiceReference;
using StatisticService.Logic.Enums;
using StatisticService.Logic.Interfaces;
using StatisticService.Logic.Models;
using NLog;

namespace StatisticService.Logic
{
	public class StatisticRepository : IStatisticRepository
	{
		private readonly Logger logger = LogManager.GetCurrentClassLogger();

		public enum StatisticType
		{
			ProfitInPercentTotal,
			ProfitInPointsTotal
		}

		#region Public methods

		public StatisticRepository()
		{
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
		}

		public stat_statistics GetCurrentStatistic(long accountId)
		{
			using (var db = GetConnection())
			{
				return db.stat_statistics
					 .Where(statistic => statistic.date.Date == DateTime.Now.Date)
					 .First(statistic => statistic.account_id == accountId);
			}
		}

		public stat_statistics GetLastStatistic(long accountId)
		{
			using (var db = GetConnection())
			{
				return db.stat_statistics
					.Where(stat => stat.account_id == accountId)
					.OrderByDescending(statistic => statistic.date)
					.FirstOrDefault();
			}
		}

		public void InsertAccountDayStatisticNew(stat_statistics[] statistics)
		{
			using (var db = GetConnection())
			{
				const string insert = "INSERT INTO stat_statistics " +
									"(date, account_id, client_account_id, " +
									"balance_total, closed_profit_per_day, volume_per_day, trades_opened_per_day, " +
									"open_trades_total, closed_profit_total, closed_profit_in_points_total, " +
									"closed_profit_in_percents_per_d, closed_profit_in_percents_total, volume_efficiency_per_day, " +
									"closed_profit_trades_total, closed_profit_trades_per_day, closed_lose_trades_total, " +
									"closed_lose_trades_per_day, risk_total, volatility_per_day, balance_per_day, " +
									"avarage_trade_time_per_day, avarage_trade_time_total, closed_profit_in_points_per_day, " +
									"max_daily_drowdown, max_open_orders, trade_amount_total, opened_trades_current, current_equity) " +
									"VALUES ";
				var skip = 0;
				do
				{
					var cmd = string.Format("{0}{1};", insert, string.Join(", ", statistics
						.Skip(skip)
						.Take(30)
						.Select(s => string.Format(" ('{0}', {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, " +
													"{12}, {13}, {14}, {15}, {16}, {17}, {18}, {19}, {20}, {21}, {22}, " +
													"{23}, {24}, {25}, {26}, {27})",
							DateToSqlString(s.date), s.account_id, s.client_account_id, s.balance_total, s.closed_profit_per_day,
							s.volume_per_day, s.trades_opened_per_day, s.open_trades_total, s.closed_profit_total,
							s.closed_profit_in_points_total, s.closed_profit_in_percents_per_d, s.closed_profit_in_percents_total,
							s.volume_efficiency_per_day, s.closed_profit_trades_total, s.closed_profit_trades_per_day,
							s.closed_lose_trades_total,
							s.closed_lose_trades_per_day, s.risk_total, s.volatility_per_day, s.balance_per_day,
							s.avarage_trade_time_per_day, s.avarage_trade_time_total, s.closed_profit_in_points_per_day,
							s.max_daily_drowdown, s.max_open_orders, s.trade_amount_total, s.opened_trades_current,
							s.current_equity))));

					var res = db.Execute(cmd);
					skip += 30;
				} while (skip < statistics.Count());
			}
		}

		public void InsertSymbolDayStatistic(stat_symbol_statistics[] symbolDayStatistics)
		{
			using (var db = GetConnection())
			{
				const string insert = "INSERT INTO stat_symbol_statistics " +
									"(symbol, datetime, bar_type, percent_symbol_to_total_trades, " +
									"percent_buy_to_sell_volume, percent_symbol_to_total_volume, " +
									"percent_symbol_to_total_deferre, percent_buy_to_buysell_volume, " +
									"average_buy_price, average_sell_price, average_buy_limit_price, average_buy_stop_price, " +
									"average_sell_limit_price, average_sell_stop_price, average_price, adding_date) " +
									"VALUES ";
				var skip = 0;
				do
				{
					var cmd = string.Format("{0}{1};", insert, string.Join(", ", symbolDayStatistics
								.Skip(skip)
								.Take(30)
								.Select(s => string.Format(" ('{0}', '{1}', {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, '{15}')",
										s.symbol, DateToSqlString(s.datetime), s.bar_type, s.percent_symbol_to_total_trades, s.percent_buy_to_sell_volume,
										s.percent_symbol_to_total_volume, s.percent_symbol_to_total_deferre, s.percent_buy_to_buysell_volume,
										s.average_buy_price, s.average_sell_price, s.average_buy_limit_price, s.average_buy_stop_price,
										s.average_sell_limit_price, s.average_sell_stop_price, s.average_price, DateToSqlString(s.adding_date)))));

					var res = db.Execute(cmd);
					skip += 30;
				} while (skip < symbolDayStatistics.Count());

				//db.BulkCopy(symbolDayStatistics);
			}
		}

		public void RemoveAllSymbolDayStatistic()
		{
			using (var db = GetConnection())
			{
				db.stat_symbol_statistics.Delete();
			}
		}

		public void InsertSymbol(stat_symbols symbol)
		{
			using (var db = GetConnection())
			{
				var res = db.Execute(string.Format("INSERT INTO stat_symbols (symbol_name) VALUES ('{0}');", symbol.symbol_name));

				//db.Insert(symbol);
			}
		}

		public stat_symbol_statistics GetSymbolDayStatistic(string symbol, BarType barType, DateTime dateTime)
		{
			using (var db = GetConnection())
			{
				return db.stat_symbol_statistics
					.Where(s => s.symbol == symbol && s.bar_type == (short)barType && s.datetime == dateTime)
					.OrderByDescending(s => s.adding_date)
					.FirstOrDefault();
			}
		}

		public stat_symbol_statistics[] GetSymbolDayStatistic(string symbol, BarType barType, DateTime fromDate, DateTime toDate)
		{
			using (var db = GetConnection())
			{
				short bar = (short)barType;
				var statistics = db.stat_symbol_statistics
					.Where(symbolStatistic =>
						symbolStatistic.symbol == symbol && symbolStatistic.bar_type == bar &&
						symbolStatistic.datetime >= fromDate && symbolStatistic.datetime <= toDate)
					.ToList();

				for (int i = 0; i < statistics.Count; )
				{
					var stat1 = statistics[i];
					for (int j = 0; j < statistics.Count; j++)
					{
						var stat2 = statistics[j];
						if (stat2.adding_date > stat1.adding_date && stat2.symbol == stat1.symbol && stat2.bar_type == stat1.bar_type && stat2.datetime == stat1.datetime)
						{
							statistics.RemoveAt(i);
							break;
						}
					}

					i++;
				}

				return statistics.ToArray();
			}
		}

		public Dictionary<long, stat_statistics> GetCurrentStatistics()
		{
			using (var db = GetConnection())
			{
				var query = from s1 in db.stat_statistics
							from s2 in db.stat_statistics.Where(s2 => s2.account_id == s1.account_id && s2.date > s1.date).DefaultIfEmpty()
							where s2.date == null
							select s1;

				return query.ToDictionary(s => s.account_id, s => s);
			}
		}

		public stat_statistics[] GetStatistics(long accountId, DateTime beginDate, DateTime endDate)
		{
			using (var db = GetConnection())
			{
				return db.stat_statistics
						.Select(statistic => statistic)
						.Where(s => s.account_id == accountId && s.date >= beginDate && s.date <= endDate)
						.OrderBy(s => s.date)
						.ToArray();
			}
		}

		public stat_statistics[] GetStatisticsAccounts(long[] accountsId, DateTime beginDate, DateTime endDate)
		{
			using (var db = GetConnection())
			{
				return db.stat_statistics
						.Where(s => accountsId.Contains(s.account_id) && s.date >= beginDate && s.date <= endDate)
						.OrderBy(s => s.date)
						.ToArray();
			}
		}

		public stat_opened_accounts GetOpenedAccounts()
		{
			using (var db = GetConnection())
			{
				return db.stat_opened_accounts.First();
			}
		}

		public stat_statistics[] GetCurrentAccountsStatistic(long[] accountids)
		{
			using (var db = GetConnection())
			{
				var query = from s1 in db.stat_statistics
							from s2 in db.stat_statistics.Where(s2 => s2.account_id == s1.account_id && s2.date > s1.date).DefaultIfEmpty()
							where s2.date == null && accountids.Contains(s1.account_id)
							select s1;

				return query.ToArray();
			}
		}

		public void ClearStatistic(long accountId)
		{
			using (var db = GetConnection())
				db.stat_statistics.Delete(statistic => statistic.account_id == accountId);
		}

		public void ClearStatisticOfDeletedAccounts(long[] realAccountsId)
		{
			using (var db = GetConnection())
			{
				var existAccounts = db.stat_statistics.Select(x => x.account_id).Distinct().ToArray();
				var accountsForDelete = existAccounts.Where(x => !realAccountsId.Contains(x)).ToArray();

				if (accountsForDelete.Any())
				{
					logger.Trace("Remove old statistic for {1} accounts: {0}", string.Join(", ", accountsForDelete), accountsForDelete.Length);
					db.stat_statistics.Delete(x => accountsForDelete.Contains(x.account_id));
				}
			}
		}

		public List<double> GetDecimatedChart(long accountId, int pointsCount, StatisticType type)
		{
			using (var db = GetConnection())
			{
				var result = new List<double> { 0 };

				var statistic = db.stat_statistics
								.Where(x => x.account_id == accountId)
								.OrderBy(x => x.date)
								.ToArray();

				var list = new List<stat_statistics[]>();
				var step = statistic.Length <= pointsCount
					? 1
					: statistic.Length % pointsCount >= pointsCount / 3
						? statistic.Length / pointsCount + 1
						: statistic.Length / pointsCount;

				var count = 0;
				do
				{
					list.Add(statistic.Skip(count).Take(step).ToArray());
					count += step;
				} while (count < statistic.Length);

				if (!list.Any() || !list.First().Any())
					return result;

				switch (type)
				{
					case StatisticType.ProfitInPercentTotal:
						result = list
							.Select(x => x.Average(y => (double)y.closed_profit_in_percents_total))
							.ToList();
						break;
					case StatisticType.ProfitInPointsTotal:
						result = list
							.Select(x => x.Average(y => (double)y.closed_profit_in_points_total))
							.ToList();
						break;
				}
				return result;
			}
		}

		public string[] GetSymbols()
		{
			using (var db = GetConnection())
			{
				return db.stat_symbols
					.Select(symbol => symbol.symbol_name)
					.ToArray();
			}
		}

		public stat_statistics[] GetLastStatistic()
		{
			using (var db = GetConnection())
			{
				var res = from s1 in db.stat_statistics
						  from s2 in db.stat_statistics.Where(s2 => s2.account_id == s1.account_id && s2.date > s1.date).DefaultIfEmpty()
						  where s2.date == null
						  select s1;

				return res.ToArray();
			}
		}

		public stat_symbol_coefficients[] GetSymbolCoefficients()
		{
			using (var db = GetConnection())
			{
				return db.stat_symbol_coefficients.ToArray();
			}
		}

		#endregion

		#region Private methods

		private DB GetConnection()
		{
			var hashedConStr = ConfigurationManager.ConnectionStrings[""].ToString();
			var connectionString = Encrypter.DecryptConnectionString(hashedConStr);
			return new DB(connectionString, true);
		}

		private DB GetRootConnection()
		{
			var hashedConStr = ConfigurationManager.ConnectionStrings["Root"].ToString();
			var connectionString = Encrypter.DecryptConnectionString(hashedConStr);
			return new DB(connectionString, true);
		}

		private string DateToSqlString(DateTime date)
		{
			return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}

		#endregion
	}
}

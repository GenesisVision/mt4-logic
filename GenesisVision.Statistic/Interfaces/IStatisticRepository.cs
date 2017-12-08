using System;
using System.Collections.Generic;
using DataModel;
using StatisticService.Entities;
using StatisticService.Entities.Enums;
using StatisticService.Logic.Models;

namespace StatisticService.Logic.Interfaces
{
	public interface IStatisticRepository
	{
		stat_statistics GetCurrentStatistic(long accountId);

		stat_statistics GetLastStatistic(long accountId);

		Dictionary<long, stat_statistics> GetCurrentStatistics();

		stat_statistics[] GetStatistics(long accountId, DateTime beginDate, DateTime endDate);

		stat_statistics[] GetStatisticsAccounts(long[] accountId, DateTime beginDate, DateTime endDate);

		stat_opened_accounts GetOpenedAccounts();

		stat_statistics[] GetCurrentAccountsStatistic(long[] accountids);

		Dictionary<long, decimal> GetBonusesTradeAmount(BonusTradeAmount[] bonuses);

		void ClearStatistic(long accountId);

		List<double> GetDecimatedChart(long accountId, int pointsCount, StatisticRepository.StatisticType type);

		string[] GetSymbols();

		void InsertAccountDayStatisticNew(stat_statistics[] statistics);

		void InsertSymbolDayStatistic(stat_symbol_statistics[] symbolDayStatistics);

		stat_symbol_statistics GetSymbolDayStatistic(string symbol, BarType barType, DateTime dateTime);

		stat_symbol_statistics[] GetSymbolDayStatistic(string symbol, BarType barType, DateTime fromDate, DateTime toDate);

		void RemoveAllSymbolDayStatistic();

		void InsertSymbol(stat_symbols symbol);

		Dictionary<short, string> GetAllCountries();

		stat_statistics[] GetLastStatistic();

		stat_symbol_coefficients[] GetSymbolCoefficients();

		void ClearStatisticOfDeletedAccounts(long[] realAccountsId);
	}
}

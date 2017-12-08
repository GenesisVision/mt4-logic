using System;
using System.Collections.Generic;
using StatisticService.Logic.Models;

namespace StatisticService.Logic.Interfaces
{
	public interface IStatisticService
	{
		/// <summary>
		/// Get day statistic for client
		/// </summary>
		/// <param name="accountId">Account id</param>
		AccountDayStatistic GetDayStatistic(long accountId);

		/// <summary>
		/// Get top traders
		/// </summary>
		/// <returns></returns>
		Trader[] GetTradersRating();

		/// <summary>
		/// Get top signal providers
		/// </summary>
		/// <returns></returns>
		SignalProvider[] GetSignalProvidersRating();

		/// <summary>
		/// Get top signal providers
		/// </summary>
		/// <returns></returns>
		Manager[] GetManagersRating();

		/// <summary>
		/// Get account history
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="beginDate"></param>
		/// <returns></returns>
		OrderData[] GetHistory(long accountId, DateTime beginDate);

		/// <summary>
		/// Get account history (skip, take)
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="beginDate"></param>
		/// <param name="skip"></param>
		/// <param name="take"></param>
		/// <returns></returns>
		OrderData[] GetHistory(long accountId, DateTime beginDate, int skip, int take);

		/// <summary>
		/// Get history for given period
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="beginDate"></param>
		/// <param name="endDate"></param>
		/// <param name="skip"></param>
		/// <param name="take"></param>
		/// <returns></returns>
		OrderData[] GetPeriodHistory(long accountId, DateTime beginDate, DateTime endDate, int skip, int take);

		/// <summary>
		/// Get account history length
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="beginDate"></param>
		/// <returns></returns>
		int GetHistoryLength(long accountId, DateTime beginDate);

		/// <summary>
		/// Get profit statistic in deposit currency
		/// </summary>
		/// <param name="accountId">Account id</param>
		/// <param name="beginDate">Begin date</param>
		/// <param name="endDate">End date</param>
		Tuple<DateTime, decimal>[] GetProfitStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get profit statistic accounts in deposit currency
		/// </summary>
		/// <param name="accountsId">Accounts id</param>
		/// <param name="beginDate">Begin date</param>
		/// <param name="endDate">End date</param>
		StatisticsAccount[] GetProfitStatisticsAccounts(long[] accountsId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get profit statistic in points
		/// </summary>
		/// <param name="accountId">Account id</param>
		/// <param name="beginDate">Begin date</param>
		/// <param name="endDate">End date</param>
		Tuple<DateTime, int>[] GetProfitInPointsStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get profit statistic in percent total
		/// </summary>
		/// <param name="accountId">Account id</param>
		/// <param name="beginDate">Begin date</param>
		/// <param name="endDate">End date</param>
		Tuple<DateTime, decimal>[] GetProfitInPercentsTotalStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get profit statistic in points total
		/// </summary>
		/// <param name="accountId">Account id</param>
		/// <param name="beginDate">Begin date</param>
		/// <param name="endDate">End date</param>
		Tuple<DateTime, int>[] GetProfitInPointsTotalStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get profit statistic in percents
		/// </summary>
		/// <param name="accountId">Account id</param>
		/// <param name="beginDate">Begin date</param>
		/// <param name="endDate">End date</param>
		Tuple<DateTime, decimal>[] GetProfitInPercentsStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get Profit per day statistic
		/// </summary>
		/// <returns>Statistic</returns>
		Tuple<DateTime, float>[] GetProfitPerDayStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get profit total statistic
		/// </summary>
		/// <returns>Statistic</returns>
		Tuple<DateTime, float>[] GetProfitTotalStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get volatility sattistic
		/// </summary>
		/// <returns>Statistic</returns>
		Tuple<DateTime, float>[] GetVolatilityStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get volume efficiency day statistic
		/// </summary>
		/// <returns></returns>
		Tuple<DateTime, float>[] GetVolumeEfficiencyDayStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get new trades statistic
		/// </summary>
		/// <returns>Statistic</returns>
		Tuple<DateTime, int>[] GetNewTradesStatistic(long accountId, DateTime beginDate, DateTime endDate);

		/// <summary>
		/// Get opened accounts statistic
		/// </summary>
		/// <returns></returns>
		OpenedAccounts GetOpenedAccountsStatistic();

		/// <summary>
		/// Gets basic statistic
		/// </summary>
		/// <param name="accountsIds">Accounts id's</param>
		/// <returns>Statistics</returns>
		BasicStatistic[] GetBasicStatisticsCurrent(long[] accountsIds);

		/// <summary>
		/// Return list of last trading activity
		/// </summary>
		/// <param name="count">Count of activities</param>
		/// <returns>Collection of activities</returns>
		Activity[] GetLastActivities(int count);

		/// <summary>
		/// Return list of last trading activity of given accounts
		/// </summary>
		/// <param name="count"></param>
		/// <param name="accountIds"></param>
		/// <returns></returns>
		Activity[] GetAccountsActivities(int count, long[] accountIds);

		/// <summary>
		/// Get trade amount for given accounts
		/// </summary>
		/// <param name="bonuses">Account id, Bonus id and Bonus open date</param>
		/// <returns>pair bonus id - trade amount</returns>
		Dictionary<long, decimal> GetBonusesTradeAmount(BonusTradeAmount[] bonuses);

		/// <summary>
		/// Calculate or reculculate statistic for account
		/// </summary>
		/// <param name="clientId">Client id</param>
		void CalculateStatistic(long clientId);

		/// <summary>
		/// Calculate statistic account
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		void CalculateStatisticAccount(long accountId);

		/// <summary>
		/// Get closed orders volume for given logins in period
		/// </summary>
		/// <param name="serverName"></param>
		/// <param name="logins"></param>
		/// <param name="dateFrom"></param>
		/// <param name="dateTo"></param>
		/// <returns>Item1 - login, Item2 - total volume</returns>
		Tuple<int, int>[] GetTradesVolume(string serverName, int[] logins, DateTime dateFrom, DateTime dateTo);

		/// <summary>
		/// Get closed orders profit in points for given logins in period
		/// </summary>
		/// <param name="serverName"></param>
		/// <param name="logins"></param>
		/// <param name="dateFrom"></param>
		/// <param name="dateTo"></param>
		/// <returns>Item1 - login, Item2 - total profit in points</returns>
		Tuple<int, int>[] GetTradesProfitInPoints(string serverName, int[] logins, DateTime dateFrom, DateTime dateTo);

		/// <summary>
		/// Get open trades ratio
		/// </summary>
		/// <param name="count"></param>
		/// <returns>Item1 - login, Item2 - total profit in points</returns>
		OpenTradeRatio[] GetOpenTradesRatio(int count);

		/// <summary>
		/// Get report statuses
		/// </summary>
		/// <param name="onlyActive">only calculating now reports</param>
		/// <returns></returns>
		ReportStatus[] GetReportStatuses(bool onlyActive);

		/// <summary>
		/// Get report status
		/// </summary>
		/// <param name="handle">handle</param>
		/// <returns>status (null if not exist)</returns>
		ReportStatus GetReportStatus(int handle);
	}
}

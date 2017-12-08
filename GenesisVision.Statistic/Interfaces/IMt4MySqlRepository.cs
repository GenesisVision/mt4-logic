using System;
using System.Collections.Generic;
using DataModel;
using StatisticService.Logic.Models;

namespace StatisticService.Logic.Interfaces
{
	public interface IMt4MySqlRepository
	{
		mt4_trades[] GetOrders(DateTime prevDate);

		mt4_trades[] GetOrders(int login, DateTime prevDate);

		mt4_trades[] GetOrdersForDailyStatistic(int login, DateTime prevDate);

		mt4_trades[] GetOrders(int login, DateTime prevDate, int skip, int take);

		mt4_trades[] GetAccountsClosedOrders(int count, int[] logins);

		mt4_trades[] GetAccountsAllClosedOrders(int[] logins);

		MT4Activity[] GetOrders(int count);

		SymbolData[] GetSymbolsData(UnrealGroup[] unrealGroup, string[] symbols, DateTime fromDate, DateTime toDate);

		mt4_trades[] GetAllOrders(int login);

		Dictionary<int, mt4_users> GetAccounts();

		Tuple<int, int>[] GetAccountsVolume(int[] logins, DateTime dateFrom, DateTime dateTo);

		mt4_trades[] GetAccountPeriodTrades(int[] logins, DateTime dateFrom, DateTime dateTo, int skip, int take);

		mt4_trades[] GetAccountPeriodTrades(int[] logins, DateTime dateFrom, DateTime dateTo);

		mt4_trades[] GetAccountPeriodTrades(int[] login, DateTime? dateFrom, DateTime? dateTo);

		mt4_users GetAccount(int login);

		Tuple<int, double>[] GetAccountsBalances(int[] logins);

		OpenTradeRatio[] GetOpenTradesRatio(int count);
	}
}

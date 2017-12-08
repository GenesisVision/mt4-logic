using System;

namespace StatisticService.Logic.Interfaces
{
	public interface IBarsMySqlRepository
	{
		bool GetOpenPrice(string serverName, string symbol, DateTime date, out double price);
	}
}

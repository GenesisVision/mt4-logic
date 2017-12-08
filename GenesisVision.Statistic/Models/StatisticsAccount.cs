using System;

namespace StatisticService.Logic.Models
{
	public class StatisticsAccount
	{
		public long AccountId { get; set; }
		public Tuple<DateTime, decimal>[] Statistics { get; set; }
	}
}

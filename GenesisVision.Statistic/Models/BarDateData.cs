using System;
using DataModel;

namespace StatisticService.Logic.Models
{
	public class BarDateData
	{
		public DateTime DateTime { get; set; }
		public mt4_trades[] Orders { get; set; }
	}
}

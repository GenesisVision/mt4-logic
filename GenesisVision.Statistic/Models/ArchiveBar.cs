using System;
using StatisticService.Logic.Enums;

namespace StatisticService.Logic.Models
{
	public class ArchiveBar
	{
		public DateTime time { get; set; }

		public double open { get; set; }

		public double high { get; set; }

		public double low { get; set; }

		public double close { get; set; }

		public double vol { get; set; }

		public Period period { get; set; }
	}

	
}

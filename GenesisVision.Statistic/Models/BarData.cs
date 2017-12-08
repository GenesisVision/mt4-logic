using StatisticService.Entities;
using StatisticService.Entities.Enums;

namespace StatisticService.Logic.Models
{
	public class BarData
	{
		public BarType BarType { get; set; }
		public BarDateData[] BarDatesData { get; set; }
	}
}

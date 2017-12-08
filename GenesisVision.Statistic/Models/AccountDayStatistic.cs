using System;
using DataModel;

namespace StatisticService.Logic.Models
{
	public class AccountDayStatistic : ICloneable 
    {
		public long AccountId { get; set; }
		public long ClientAccountId { get; set; }
		public decimal CurrentBalance { get; set; }
		public double BalancePerDay { get; set; }
		public double ClosedProfitPerDay { get; set; }
		public double VolumePerDay { get; set; }
		public int OpenedTradesCountPerDay { get; set; }
		public int OpenedTradesCountCurrent { get; set; }
		public int OpenedTradesCountTotal { get; set; }
		public double ClosedProfitInPointsPerDay { get; set; }
		public double ClosedProfitInPercentsPerDay { get; set; }
		public int ProfitTradesCountPerDay { get; set; }
		public int ClosedLoseTradesCountPerDay { get; set; }
		public double AvarageTradeTimeInMinutes { get; set; }
		public double AvarageTradeTimeInMinutesTotal { get; set; }
		public double VolumeEfficiencyPerDay { get; set; }
		public double VolatilityPerDay { get; set; }
		public double ClosedProfitTotal { get; set; }
		public double ClosedProfitInPointsTotal { get; set; }
		public double ClosedProfitInPercentsTotal { get; set; }
		public int ClosedProfitTradesTotal { get; set; }
		public int ClosedLoseTradesTotal { get; set; }
		public double MaxDailyDrowdown { get; set; }
		public int MaxOpenOrders { get; set; }
		public decimal TradeAmountTotal { get; set; }
		public decimal CurrentEquity { get; set; }
		public decimal RiskTotal { get; set; }
		public DateTime Date { get; set; }
		
		public object Clone()
		{
			return MemberwiseClone();
		}
    }
}

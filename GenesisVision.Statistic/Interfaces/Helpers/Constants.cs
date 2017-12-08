using System;

namespace StatisticService.Logic.Helpers
{
	public static class Constants
	{
		public static readonly DateTime BeginEpoch = new DateTime(1970, 1, 1, 0, 0, 0);
		public static readonly DateTime FirstDate2018 = new DateTime(2018, 1, 1, 0, 0, 0);
		public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

		public const string Gold1 = "XAUUSD";
		public const string Gold2 = "GOLD";
		public const string Silver1 = "XAGUSD";
		public const string Silver2 = "SILVER";
		public const string Usd = "USD";
		public const string Jpy = "JPY";

		public const decimal Tolerance = 0.0001m;
		public const int PointsCountInSmallChart = 15;
		public const int OpenTradesRatioCachingAmount = 10;
		public const int ActivitiesCachingAmount = 10;
	}
}

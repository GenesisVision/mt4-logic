using System;

namespace TournamentService.Logic.Helpers
{
	public static class TimeHelper
	{
		public static DateTime RoundUp(this DateTime dt, TimeSpan d)
		{
			return new DateTime(((dt.Ticks + d.Ticks - 1) / d.Ticks) * d.Ticks);
		}

		public static DateTime Round(this DateTime dateTime, TimeSpan interval)
		{
			var halfIntervelTicks = ((interval.Ticks + 1) >> 1);

			return dateTime.AddTicks(halfIntervelTicks - ((dateTime.Ticks + halfIntervelTicks) % interval.Ticks));
		}
	}
}

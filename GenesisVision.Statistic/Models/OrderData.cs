using System;
using System.Runtime.Serialization;

namespace StatisticService.Logic.Models
{
	public enum OrderType { BUY, SELL, BUY_LIMIT, SELL_LIMIT, BUY_STOP, SELL_STOP, BALANCE, CREDIT };
	public enum OrderAction { Close, Open }

	public class OrderData
	{
		
		public int OrderId { get; set; }
		
		public int Login { get; set; }
		
		public string Symbol { get; set; }
		
		public int Digits { get; set; }
		
		public OrderType Cmd { get; set; }
		
		public int Volume { get; set; }
		
		public double RealVolume { get; set; }
		
		public double OpenPrice { get; set; }
		
		public double ClosePrice { get; set; }
		
		public double Profit { get; set; }
		
		public double Swap { get; set; }
		
		public double Commission { get; set; }
		
		public DateTime OpenTime { get; set; }
		
		public DateTime CloseTime { get; set; }
	}
}

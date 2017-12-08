using System;
using SignalService.Entities.Enums;
using SignalService.Models;
using ProtoTypes;

namespace SignalService
{
	public static class SignalResolveLogic
	{
		public static ExecutionOrder Resolve(MT4TradeSignal signal, int login, double balance, double equity, SubscriptionSettings settings)
		{
			//Close only
			if(((SubscriptionStatus)settings.Status == SubscriptionStatus.CloseOnly && signal.ActionType != ActionType.Close)) return null;
			//Side
			if (((SignalOrderType) settings.OrdersType == SignalOrderType.Buy && signal.Side == TradeSide.Sell) ||
			    ((SignalOrderType) settings.OrdersType == SignalOrderType.Sell && signal.Side == TradeSide.Buy)) return null;
			
			var execution = new ExecutionOrder
			{
				ActionType = signal.ActionType,
				Login = login,
				//Reverse
				Side = GetSide(settings.Reverse, signal.Side),
				Symbol = signal.Symbol,
				OrderID = signal.OrderID
			};
			
			//Max volume
			execution.Volume = CalculateVolume(signal, balance, equity, settings);
			if(settings.MaxVolume != -1)
				execution.Volume = execution.Volume > (settings.MaxVolume / 100d) ? (settings.MaxVolume / 100d) : execution.Volume;
			execution.Commission = 1.7d;
			return execution;
		}

		/// <summary>
		/// Calculate volume for slave account
		/// </summary>
		/// <param name="signal">Signal initiator</param>
		/// <param name="equity"></param>
		/// <param name="settings">Subscription settigns</param>
		/// <param name="balance"></param>
		/// <returns>Volume</returns>
		public static double CalculateVolume(MT4TradeSignal signal, double balance, double equity, SubscriptionSettings settings)
		{
			switch ((SubscriptionType)settings.SignalType)
			{
				case SubscriptionType.Fixed:
					return settings.MaxVolume / 100d;
				case SubscriptionType.ByBalance:
					if (Math.Abs(balance) < 0.000001) return double.NaN; // Balance is empty
					return signal.Volume * (balance / signal.Balance) * settings.Risk;
				case SubscriptionType.ByEquity:
					if (Math.Abs(equity) < 0.000001) return double.NaN; // Balance is empty
					return signal.Volume * (equity / signal.Equity) * settings.Risk;
				case SubscriptionType.ByMultiplier:
					return signal.Volume * settings.Multiplier;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static TradeSide GetSide(bool reverse, TradeSide providerSide)
		{
			if (!reverse) return providerSide;
			return providerSide == TradeSide.Buy ? TradeSide.Sell : TradeSide.Buy;
		}
	}
}

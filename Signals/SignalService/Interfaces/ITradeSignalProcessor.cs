using System;
using ProtoTypes;

namespace SignalService.Interfaces
{
	public interface ITradeSignalProcessor
	{
		/// <summary>
		/// Signal observer
		/// </summary>
		void SignalOnNext(Tuple<string, MT4TradeSignal> signal);

		/// <summary>
		/// TradeSignals to execute orders
		/// </summary>
		event Action<Tuple<string, ExecutionSignal>> ExecutionSignals;
	}
}

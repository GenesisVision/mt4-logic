using System;
using ProtoTypes;

namespace SignalService.Interfaces
{
	public interface IServerController
	{
		/// <summary>
		/// Close order
		/// </summary>
		/// <param name="serverName"></param>
		/// <param name="orderId"></param>
		void CloseOrder(string serverName, int orderId);

		/// <summary>
		/// Observable collection of trade signals
		/// </summary>
		event Action<Tuple<string, MT4TradeSignal>> TradeSignals;

		/// <summary>
		/// Observable collection of requests
		/// </summary>
		event Action<Tuple<string, Request>> RequestSignals;

		/// <summary>
		/// Observable of orders status
		/// </summary>
		event Action<Tuple<string, OrdersStatusResponse>> OrdersStatus;

		/// <summary>
		/// Observer of signals
		/// </summary>
		void SignalOnNext(Tuple<string, Signal> signal);

		/// <summary>
		/// Observer of execution signals
		/// </summary>
		void ExecutionSignalOnNext(Tuple<string, ExecutionSignal> signal);

		/// <summary>
		/// Observer of orders status 
		/// </summary>
		void OrdersStatusRequestsOnNext(Tuple<string, OrdersStatusRequest> request);
	}
}

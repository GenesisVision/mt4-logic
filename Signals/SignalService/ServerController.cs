using System;
using SignalService.Interfaces;
using ProtoTypes;

namespace SignalService
{
	public class ServerController : IServerController
	{
		#region Fields

		#endregion

		#region Construction

		public ServerController()
		{
		}

		#endregion

		#region Public methods

		public void CloseOrder(string serverName, int orderId)
		{
			var ex = new ExecutionSignal();
			ex.Orders.Add(
				new ExecutionOrder
				{
					ActionType = ActionType.Close,
					OrderID = orderId
				});
			ex.Destination = serverName;
			ExecutionSignalOnNext(new Tuple<string, ExecutionSignal>(serverName, ex));
		}

		public event Action<Tuple<string, MT4TradeSignal>> TradeSignals;

		public event Action<Tuple<string, Request>> RequestSignals;

		public event Action<Tuple<string, OrdersStatusResponse>> OrdersStatus;

		public void SignalOnNext(Tuple<string, Signal> signal)
		{
			var source = signal.Item1;
			switch (signal.Item2.Type)
			{
				case SignalType.TradeSignal:
					var sign = ProtoExtension.DeSerialize<MT4TradeSignal>(signal.Item2.Content);
					if (TradeSignals != null)
						TradeSignals(new Tuple<string, MT4TradeSignal>(source, sign));
					break;
				case SignalType.SignalOrdersStatus:
					var stats = ProtoExtension.DeSerialize<OrdersStatusResponse>(signal.Item2.Content);
					if (OrdersStatus != null)
						OrdersStatus(new Tuple<string, OrdersStatusResponse>(source, stats));
					break;
				case SignalType.ConnectSignal:
					SignalService.Logger.Info("Connect signal received");
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void ExecutionSignalOnNext(Tuple<string, ExecutionSignal> tuple)
		{
			var signal = new Request
			{
				destination = tuple.Item1,
				requestType = RequestType.ExecutionRequestType,
				Content = tuple.Item2.Serialize(),
			};
			if (RequestSignals != null)
				RequestSignals(new Tuple<string, Request>(tuple.Item1, signal));
		}

		public void OrdersStatusRequestsOnNext(Tuple<string, OrdersStatusRequest> request)
		{
			var signal = new Request
			{
				destination = request.Item1,
				requestType = RequestType.OrderStatusRequestType,
				Content = request.Item2.Serialize()
			};
			if (RequestSignals != null)
				RequestSignals(new Tuple<string, Request>(request.Item1, signal));
		}

		#endregion

	}
}

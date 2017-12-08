using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SignalService.AccountService;
using SignalService.Interfaces;
using SignalService.Models;
using MT4ServersRouter;
using MT4ServersRouter.MT4Service;
using ProtoTypes;

namespace SignalService
{
	public class TradeSignalProcessor : ITradeSignalProcessor
	{
		#region Fields

		private readonly ISignalServiceRepository signalServiceRepository;
		private readonly IAccountService accountService;
		private readonly IMT4Router serversRouter;

		private Dictionary<string, Dictionary<int, long>> mt4AccountsDictionary = new Dictionary<string, Dictionary<int, long>>();
		private Dictionary<long, Tuple<string, int>> AccountsDictionary = new Dictionary<long, Tuple<string, int>>();
		private Dictionary<int, string> serversDictionary = new Dictionary<int, string>();
		public static Dictionary<OrderModel, List<OrderModel>> OpenedOrdersDictionary = new Dictionary<OrderModel, List<OrderModel>>();

		#endregion

		#region Construction

		public TradeSignalProcessor(ISignalServiceRepository signalServiceRepository, IMT4Router serversController, IAccountService accountService)
		{
			this.signalServiceRepository = signalServiceRepository;
			this.accountService = accountService;
			serversRouter = serversController;

			FillServersDictionary();
			FillAccountsDictionary();
		}

		#endregion

		#region Private methods

		private void Signalhandler(Tuple<string, MT4TradeSignal> tuple)
		{
			try
			{
				var signal = tuple.Item2;
				var comment = signal.Comment.Split('_');

				if (comment[0] == "Sub")
					ProcessSubscriberSignal(signal, Convert.ToInt32(comment[2]), Convert.ToInt64(comment[1]));
				else
					ProcessProviderSignal(signal);
			}
			catch (Exception e)
			{
				SignalService.Logger.Error(e);
			}
		}

		private void ProcessSubscriberSignal(MT4TradeSignal signal, int providerOrderId, long providerId)
		{
			var providerServer = AccountsDictionary[providerId].Item1;
			var providerOrder = new OrderModel { OrderId = providerOrderId, Server = providerServer };
			var subscriberOrder = new OrderModel { OrderId = signal.OrderID, Server = signal.Server };

			var subscriber = signalServiceRepository.GetSubscriberByLogin(signal.Login,
				serversDictionary.First(x => x.Value == signal.Server).Key);
			if (subscriber == null)
			{
				SignalService.Logger.Error("Subscriber not found");
				return;
			}

			if (!mt4AccountsDictionary[signal.Server].ContainsKey(signal.Login))
				mt4AccountsDictionary[signal.Server].Add(signal.Login, subscriber.id);

			lock (OpenedOrdersDictionary)
			{
				var contains = OpenedOrdersDictionary.ContainsKey(providerOrder);
				if (!contains)
				{
					SignalService.Logger.Error("OpenedOrdersDictionary don't contains providerOrder (OrderId: {0}, Server: {1}", providerOrder.OrderId, providerOrder.Server);
					return;
				}
				if (signal.ActionType == ActionType.Open)
				{
					OpenedOrdersDictionary[providerOrder].Add(subscriberOrder);
				}
				else
				{
					OpenedOrdersDictionary[providerOrder].Remove(subscriberOrder);
					if (OpenedOrdersDictionary[providerOrder].Count == 0)
						OpenedOrdersDictionary.Remove(providerOrder);
				}
			}
		}

		private void ProcessProviderSignal(MT4TradeSignal signal)
		{
			var accountTypes = serversDictionary.Where(x => x.Value == signal.Server).Select(x => x.Key);
			var provider = signalServiceRepository.GetProvidersByLogin(signal.Login)
				.FirstOrDefault(p => accountTypes.Contains(p.account_type_id));

			if (provider == null)
			{
				SignalService.Logger.Info("ProviderSignal\nProvider is null. Login: {0}, Server: {1}", signal.Login, signal.Server);
				return;
			}

			var builder = new StringBuilder();
			builder.AppendLine("ProviderSignal");
			builder.AppendLine(String.Format("Login: {0}", signal.Login));
			builder.AppendLine(String.Format("Server: {0}", signal.Server));
			builder.AppendLine(String.Format("DateTime: {0}", new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(signal.DateTime)));
			builder.AppendLine(String.Format("ActionType: {0}", signal.ActionType));
			builder.AppendLine(String.Format("Order: id - {0}, side - {1}, symbol - {2}, volume - {3}, tp - {4}, sl - {5}, balance - {6}, profit - {7}, comment - {8}, commission - {9}",
				signal.OrderID, signal.Side, signal.Symbol, signal.Volume, signal.TakeProfit, signal.StopLoss, signal.Balance, signal.Profit, signal.Comment, signal.ProviderCommission));
			SignalService.Logger.Info(builder.ToString());

			if (!mt4AccountsDictionary[signal.Server].ContainsKey(signal.Login))
				mt4AccountsDictionary[signal.Server].Add(signal.Login, provider.id);


			var signalsToExecute = new Dictionary<string, ExecutionSignal>();

			if (signal.ActionType == ActionType.Open)
			{
				var subscribers = signalServiceRepository.Subscribers(provider.id);

				if (!subscribers.Any())
					return;

				lock (OpenedOrdersDictionary)
				{
					OpenedOrdersDictionary.Add(new OrderModel { OrderId = signal.OrderID, Server = signal.Server },
						new List<OrderModel>());
				}

				foreach (var subscriber in subscribers)
				{
					var server = serversDictionary[subscriber.Subscriber.account_type_id];

					if (!signalsToExecute.ContainsKey(server))
						signalsToExecute.Add(server, new ExecutionSignal());

					MarginLevel marginLevel;
					try
					{
						marginLevel = serversRouter.Get(server).GetMarginLevel(subscriber.Subscriber.login);
					}
					catch (Exception e)
					{
						SignalService.Logger.Error("Exception in GetMarginLevel for {0} login:\n {1}", subscriber.Subscriber.login, e.StackTrace);
						continue;
					}

					var exec = SignalResolveLogic.Resolve(signal, subscriber.Subscriber.login, marginLevel.balance, marginLevel.equity, new SubscriptionSettings(subscriber.Subscription));
					if (exec == null)
					{
						SignalService.Logger.Info("SignalResolveLogic ExecutionOrder is null");
						continue;
					}
					if (provider.commission != null) exec.Commission = (double)provider.commission;

					SignalService.Logger.Info("ExecutionOrder. OrderID: {0}, Volume: {1}, Commission: {2}, Side: {3}", exec.OrderID, exec.Volume, exec.Commission, exec.Side);

					signalsToExecute[server].Orders.Add(exec);
					// ToDo static key word
					signalsToExecute[server].Comment = String.Format("Sub_{0}_{1}", provider.id, signal.OrderID);
					signalsToExecute[server].Destination = signal.Server;
				}
			}
			else
			{
				lock (OpenedOrdersDictionary)
				{
					if (!OpenedOrdersDictionary.ContainsKey(new OrderModel { OrderId = signal.OrderID, Server = signal.Server }))
						//ToDo: Think about this
						return;
					foreach (var subOrder in OpenedOrdersDictionary[new OrderModel { OrderId = signal.OrderID, Server = signal.Server }])
					{
						var orderId = subOrder.OrderId;
						var server = subOrder.Server;

						if (!signalsToExecute.ContainsKey(server))
							signalsToExecute.Add(server, new ExecutionSignal());

						signalsToExecute[server].Orders.Add(new ExecutionOrder { ActionType = ActionType.Close, OrderID = orderId, Symbol = string.Empty });
						signalsToExecute[server].Comment = string.Empty;
						signalsToExecute[server].Destination = server;
					}
				}
			}

			foreach (var executionSignal in signalsToExecute)
			{
				if (ExecutionSignals != null)
					ExecutionSignals(new Tuple<string, ExecutionSignal>(executionSignal.Key, executionSignal.Value));
			}

		}

		private void FillAccountsDictionary()
		{
			try
			{
				var accounts = signalServiceRepository.GetAccountsMt4Location();

				foreach (var account in accounts)
				{
					var serverName = serversDictionary[account.AccountType];

					if (mt4AccountsDictionary.ContainsKey(serverName))
						mt4AccountsDictionary[serverName].Add(account.Login, account.AccountId);
					else
						mt4AccountsDictionary.Add(serverName, new Dictionary<int, long> { { account.Login, account.AccountId } });

					AccountsDictionary.Add(account.AccountId, new Tuple<string, int>(serverName, account.Login));
				}
			}
			catch (Exception e)
			{
				SignalService.Logger.Error(e.Message);
			}
		}

		private void FillServersDictionary()
		{
			try
			{
				var servers = accountService.GetAccountTypesWithServers();
				if (!servers.IsSuccess) throw new Exception(servers.Error);

				serversDictionary = servers.Result.ToDictionary(x => (int)x.AccountType, x => x.ServerName);
			}
			catch (Exception e)
			{
				SignalService.Logger.Error(e.Message);
			}
		}

		#endregion

		public void SignalOnNext(Tuple<string, MT4TradeSignal> signal)
		{
			Signalhandler(signal);
		}

		public event Action<Tuple<string, ExecutionSignal>> ExecutionSignals;
	}
}

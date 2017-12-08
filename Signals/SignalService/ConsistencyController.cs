using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SignalService.AccountService;
using SignalService.Interfaces;
using SignalService.Models;
using ProtoTypes;

namespace SignalService
{
	public class ConsistencyController
	{
		#region Fields

		private readonly TimeSpan waitHandleTimeout = TimeSpan.FromMinutes(1);

		private ConcurrentDictionary<string, Dictionary<int, List<OrderStatus>>> orderStatusesDictionary;
		private Dictionary<long, Tuple<string, int>> accountsDictionary;
		private Dictionary<OrderModel, List<OrderModel>> openedOrderDictionary;
		private Dictionary<int, string> serversDictionary;
		private Dictionary<string, EventWaitHandle> handlersDictionary;

		private readonly IAccountService accountService;
		private readonly ISignalServiceRepository repository;
		private readonly IServerController serverController;

		#endregion

		#region Constructor

		public ConsistencyController(ISignalServiceRepository repository, IServerController serverController, IAccountService accountService)
		{
			this.accountService = accountService;
			this.repository = repository;
			this.serverController = serverController;

			serverController.OrdersStatus += HandleOrderStatusResponse;
		}

		#endregion

		#region Public methods

		public void Validate()
		{
			try
			{
				InitDictionaries();

				SignalService.Logger.Debug("Consistency validate started...");

				var serversResult = accountService.GetAccountTypesWithServers();
				if (!serversResult.IsSuccess)
				{
					SignalService.Logger.Error(serversResult.Error);
					return;
				}
				serversDictionary = serversResult.Result.ToDictionary(x => (int)x.AccountType, x => x.ServerName);

				var serverNames = serversDictionary.Values.Distinct().ToArray();

				handlersDictionary = serverNames.ToDictionary(x => x,
					x => new EventWaitHandle(false, EventResetMode.ManualReset));

				var mt4Locations = repository.GetAccountsMt4Location();

				accountsDictionary = mt4Locations.ToDictionary(x => x.AccountId,
					x => new Tuple<string, int>(serversDictionary[x.AccountType], x.Login));

				var waitHandles = new WaitHandle[serverNames.Count()];

				for (var i = 0; i < serverNames.Count(); i++)
				{
					var j = i;
					var server = serverNames[j];
					var thread = new Thread(() =>
					{
						var typeIds = serversDictionary.Where(x => x.Value == server).Select(x => x.Key).ToList();
						var request = new OrdersStatusRequest();
						request.logins.AddRange(mt4Locations.Where(x => typeIds.Contains(x.AccountType)).Select(x => x.Login));
						serverController.OrdersStatusRequestsOnNext(new Tuple<string, OrdersStatusRequest>(server, request));
						SignalService.Logger.Debug("Orders status request sended to server - {0}", server);
					});
					waitHandles[j] = handlersDictionary[server];
					thread.Start();
				}

				var result = WaitHandle.WaitAll(waitHandles, waitHandleTimeout);
				if (!result)
				{
					SignalService.Logger.Error("Some responses from mt4 were not handled");
					return;
				}

				CheckConsistency();

				TradeSignalProcessor.OpenedOrdersDictionary = openedOrderDictionary;
			}
			catch (Exception e)
			{
				SignalService.Logger.Error("Consistency validation exception: {0}", e.ToString());
			}
		}

		#endregion

		#region Private methods

		private void HandleOrderStatusResponse(Tuple<string, OrdersStatusResponse> tuple)
		{
			SignalService.Logger.Debug("Orsers status response come from {0}", tuple.Item1);
			var serverName = tuple.Item1;
			var response = tuple.Item2;

			orderStatusesDictionary.TryAdd(serverName,
				response.ordersStatus.ToDictionary(account => account.login, account => account.orderStatus.ToList()));

			handlersDictionary[tuple.Item1].Set();
		}

		private void InitDictionaries()
		{
			openedOrderDictionary = new Dictionary<OrderModel, List<OrderModel>>();
			orderStatusesDictionary = new ConcurrentDictionary<string, Dictionary<int, List<OrderStatus>>>();
			serversDictionary = new Dictionary<int, string>();
			accountsDictionary = new Dictionary<long, Tuple<string, int>>();
		}

		private void CheckConsistency()
		{
			foreach (var serverOrderStatus in orderStatusesDictionary)
			{
				foreach (var accountStatus in serverOrderStatus.Value)
				{
					foreach (var orderStatus in accountStatus.Value)
					{
						var comment = orderStatus.Comment.Split('_');

						if (comment[0] != "Sub") continue;
						if (!accountsDictionary.ContainsKey(Convert.ToInt32(comment[1]))) continue;

						var providerLocation = accountsDictionary[Convert.ToInt32(comment[1])];

						if (orderStatusesDictionary[providerLocation.Item1].ContainsKey(providerLocation.Item2))
							if (orderStatusesDictionary[providerLocation.Item1][providerLocation.Item2]
								.Any(x => x.OrderId == Convert.ToInt32(comment[2])))
							{
								var providerOrder = new OrderModel { OrderId = Convert.ToInt32(comment[2]), Server = providerLocation.Item1 };
								var subscriberOrder = new OrderModel { OrderId = orderStatus.OrderId, Server = serverOrderStatus.Key };

								if (openedOrderDictionary.ContainsKey(providerOrder))
									openedOrderDictionary[providerOrder].Add(subscriberOrder);
								else
									openedOrderDictionary.Add(providerOrder, new List<OrderModel> { subscriberOrder });
								continue;
							}
						var server = serversDictionary.First(x => x.Value == serverOrderStatus.Key).Key;

						SignalService.Logger.Error("Found not closed order. server - {0}, orderId - {1}", server, orderStatus.OrderId);
						accountService.CloseOrder(orderStatus.OrderId, server);
					}
				}
			}
		}


		#endregion
	}
}

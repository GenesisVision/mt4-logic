using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using Helpers;
using Helpers.ResultCodes;
using SignalService.AccountService;
using SignalService.Entities.Enums;
using SignalService.Interfaces;
using SignalService.Models;
using MT4ServersRouter;
using NLog;
using ProtoTypes;

namespace SignalService
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
	public class SignalService : ISignalService
	{
		#region Fields

		private readonly ISignalServiceRepository signalServiceRepository;
		private readonly IAccountService accService;
		public static Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly Dictionary<long, DateTime?> clientsLastUpdate = new Dictionary<long, DateTime?>();
		private readonly IZeroMqServer server;

		private const int consistencyControlInterval = 60 * 5; // Interval in seconds

		#endregion

		#region Constructor

		public SignalService(ISignalServiceRepository signalServiceRepository, IAccountService accService, IZeroMqServer server)
		{
			Logger.Info("Starting signal service...");
			Logger.Info("Starting wcf service...");

			this.signalServiceRepository = signalServiceRepository;
			this.accService = accService;
			this.server = server;
		}

		#endregion

		#region Public methods

		public void StartServer()
		{
			Logger.Info("Starting zeroMq server...");
			var serverController = new ServerController();
			server.Signals += serverController.SignalOnNext;

			var router = CreateRouter(signalServiceRepository);
            
			var tradeSignalProcessor = new TradeSignalProcessor(signalServiceRepository, router, accService);

			serverController.TradeSignals += tradeSignalProcessor.SignalOnNext;

			tradeSignalProcessor.ExecutionSignals += serverController.ExecutionSignalOnNext;

			// Logging
			server.Signals += LogSignal;
			//serverController.TradeSignals += LogTradeSignal;
			tradeSignalProcessor.ExecutionSignals += LogExecutionSignal;
			serverController.RequestSignals += server.Request;

			server.Start();

			var consistencyController = new ConsistencyController(signalServiceRepository, serverController, accService);

			Observable.Timer(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(consistencyControlInterval))
				.Subscribe(l =>
				{
					var thread = new Thread(consistencyController.Validate);
					thread.Start();
				});
		}

		public OperationResult AddProvider(long accountId, string nickname, string description, decimal commission)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Add logger, account id - {0}, nickname - {1}", accountId, nickname);

				var accountInformation = accService.GetMt4AccountInfo(accountId);
				if (!accountInformation.IsSuccess)
					throw new OperationException(accountInformation.Error, accountInformation.Code);
				var statuses = accService.GetAccountStatuses(accountId);
				if (!statuses.IsSuccess)
					throw new OperationException(statuses.Error, statuses.Code);
				if (statuses.Result.Has(AccountStatuses.IsPropTrading))
					throw new OperationException("Not available", ResultCode.SiteOperationNotAvailable);

				signalServiceRepository.AddProvider(accountInformation.Result.ClientId,
					accountId,
					nickname,
					true,
					(int)accountInformation.Result.AccountTypeId,
					accountInformation.Result.Login,
					description,
					accountInformation.Result.Avatar,
					accountInformation.Result.Currency,
					commission);

				Logger.Trace("Provider added (account {0})", accountId);
			});
		}

		public OperationResult DeleteProvider(long accountId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				var operationResult = accService.DeleteAccount(accountId);
				if (!operationResult.IsSuccess) throw new OperationException(operationResult.Error, operationResult.Code);

				signalServiceRepository.DeleteProvider(accountId);

				Logger.Trace("Provider removed (account {0})", accountId);
			});
		}

		public OperationResult DeleteSubscriber(long accountId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				var operationResult = accService.DeleteAccount(accountId);
				if (!operationResult.IsSuccess) throw new OperationException(operationResult.Error, operationResult.Code);

				signalServiceRepository.DeleteSubscriber(accountId);

				Logger.Trace("Subscriber removed (account {0})", accountId);
			});
		}

		public OperationResult ChangeProviderAvatar(long accountId, string newAvatar)
		{
			Logger.Trace("Change provider avatar, account id - {0}", accountId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.ChangeProviderAvatar(accountId, newAvatar));
		}

		public OperationResult ChangeSubscriberAvatar(long accountId, string newAvatar)
		{
			Logger.Trace("Change subscriber avatar, account id - {0}", accountId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.ChangeSubscriberAvatar(accountId, newAvatar));
		}

		public OperationResult ChangeProviderDescription(long accountId, string newDescription)
		{
			Logger.Trace("Change provider description, account id - {0}", accountId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.ChangeProviderDescription(accountId, newDescription));
		}

		public OperationResult Subscribe(long slaveId, long masterId, SubscriptionSettings settings)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				var statuses = accService.GetAccountStatuses(slaveId);
				if (!statuses.IsSuccess)
					throw new OperationException(statuses.Error, statuses.Code);
				if (statuses.Result.Has(AccountStatuses.IsPropTrading))
					throw new OperationException("Not available", ResultCode.SiteOperationNotAvailable);

				Logger.Trace("Subscribe, slave id - {0}, master id - {1}", slaveId, masterId);
				var accountInformation = accService.ChangeAccountRole(slaveId, AccountRole.SignalSubscriber);
				if (!accountInformation.IsSuccess) throw new OperationException(accountInformation.Error, accountInformation.Code);

				signalServiceRepository.SignalSubscription((short)SubscriptionStatus.On, slaveId, masterId, settings,
					accountInformation.Result);
			});
		}

		public OperationResult SubscribeByNickname(long slaveId, string masterNickname, SubscriptionSettings settings)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Subscribe, slave id - {0}, master nickname - {1}", slaveId, masterNickname);
				var accountInformation = accService.ChangeAccountRole(slaveId, AccountRole.SignalSubscriber);

				if (!accountInformation.IsSuccess)
					throw new OperationException(accountInformation.Error, accountInformation.Code);
				var statuses = accService.GetAccountStatuses(slaveId);
				if (!statuses.IsSuccess)
					throw new OperationException(statuses.Error, statuses.Code);
				if (statuses.Result.Has(AccountStatuses.IsPropTrading))
					throw new OperationException("Not available", ResultCode.SiteOperationNotAvailable);

				signalServiceRepository.SignalSubscription((short)SubscriptionStatus.On, slaveId, masterNickname, settings,
					accountInformation.Result);
			});
		}

		public OperationResult<List<ProviderInfo>> GetProvidersList(long subscriberId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Debug("Get providers list for {0}", subscriberId);
				var providers = signalServiceRepository.GetProvidersBySubscriber(subscriberId);
				var result = new List<ProviderInfo>();
				var openOrders = accService.GetOpenOrders(subscriberId);
				if (!openOrders.IsSuccess) throw new OperationException(openOrders.Error, openOrders.Code);

				foreach (var signalProvider in providers)
				{
					var provider = new ProviderInfo
					{
						AccountId = signalProvider.id,
						Nickname = signalProvider.nickname,
						Avatar = signalProvider.avatar,
					};
					foreach (var trade in openOrders.Result)
					{
						var providerId = TradeProvider(trade.Comment);
						if (providerId == provider.AccountId)
							provider.Profit += trade.CurrentProfit;
					}
					result.Add(provider);
				}
				return result;
			});
		}


		public OperationResult Unsubscribe(long slaveId, long masterId)
		{
			Logger.Trace("Unsubscribe, slave id - {0}, master id -{1}", slaveId, masterId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.Unsubscribe(slaveId, masterId));
		}

		public OperationResult<ProviderFullInformation[]> GetProviders(long clientId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get providers, client id - {0}", clientId);

				var providers = signalServiceRepository.GetProvider(clientId);
				var accsInfo = accService.GetAccountsInfo(providers.Select(information => information.AccountId).ToArray());
				if (!accsInfo.IsSuccess) throw new OperationException(accsInfo.Error, accsInfo.Code);

				foreach (var info in accsInfo.Result)
				{
					var acc = providers.First(information => information.AccountId == info.AccountId);
					acc.Balance = (decimal)info.Balance;
					acc.Equity = (decimal)info.Equity;
					acc.Leverage = info.Leverage;
					acc.Profit = (decimal)info.Equity - (decimal)info.Balance;
					acc.WorkingDays = info.WorkingDays;
				}
				return providers;
			});
		}

		public OperationResult<Provider[]> GetAllProviders()
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get all providers");

				var providers = signalServiceRepository.GetAllProviders();

				var providersResult = new List<Provider>(providers.Length);

				providersResult.AddRange(providers.Select(signalProvider => new Provider
					{
						AccountId = signalProvider.id,
						IsSubscribe = false,
						Login = signalProvider.login,
						Nickname = signalProvider.nickname
					}));

				return providersResult.ToArray();
			});
		}

		public OperationResult<Delivery[]> GetDeliveries(long clientId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get deliveries. clientId = {0}", clientId);
				var deliveries = signalServiceRepository.GetDeliveries(clientId);

				var accountInfos = accService.GetAccountsInfo(deliveries.Select(x => (long)x.Provider.id).ToArray());
				if (!accountInfos.IsSuccess) throw new OperationException(accountInfos.Error, accountInfos.Code);

				var deliveriesResults = new List<Delivery>();

				foreach (var accountInfo in accountInfos.Result)
				{
					var provider = deliveries.FirstOrDefault(x => x.Provider.id == accountInfo.AccountId);
					if (provider == null) throw new Exception("Provider not found!");

					var deliveryResult = new Delivery
						{
							Login = accountInfo.Login,
							Avatar = accountInfo.Avatar,
							AccountId = accountInfo.AccountId,
							AccountType = accountInfo.AccountType,
							Balance = (decimal)accountInfo.Balance,
							Commission = provider.Provider.commission ?? 0.0m,
							Nickname = provider.Provider.nickname,
							WorkingDays = 0,
							Procent = 100 + (((decimal)accountInfo.Balance / 100) * (decimal)(accountInfo.Equity - accountInfo.Balance)),
							Equity = (decimal)accountInfo.Equity,
							Profit = (decimal)(accountInfo.Equity - accountInfo.Balance),
							Currency = accountInfo.Currency,
							Leverage = accountInfo.Leverage,
							IsVisible = provider.Provider.isvisible,
							RatingValue = provider.Provider.rating_value,
							RatingCount = provider.Provider.rating_count,
							SubscribersCount = deliveries.
								Where(x => x.Provider.id == provider.Provider.id && x.Subscription != null).
								Select(x => x.Subscription).Count(),

							Subscribers = new List<AccountConnection>(deliveries.
								Where(x => x.Provider.id == provider.Provider.id && x.Subscription != null).Select(x => new AccountConnection
								{
									AccountId = x.Subscriber.id,
									Avatar = x.Subscriber.avatar
								}))
						};

					deliveriesResults.Add(deliveryResult);
				}

				return deliveriesResults.ToArray();
			});
		}

		public OperationResult<Subscription[]> GetSubscriptions(long clientId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get subscriptions, client id - {0}", clientId);
				var subscriptions = signalServiceRepository.GetSubscriptions(clientId);

				var subscriptionsResult = new List<Subscription>();

				var accountIds = subscriptions.Select(x => x.Subscriber.id).Distinct();

				foreach (var accountId in accountIds)
				{
					var subscriptionResult = new Subscription
					{
						AccountId = accountId,
						Profit = 0.0m,
						SubscribersCount = 0,
						Providers = new List<AccountConnection>()
					};

					subscriptionsResult.Add(subscriptionResult);

					foreach (var subscription in subscriptions.Where(x => x.Subscriber.id == accountId))
					{
						subscriptionResult.Profit += subscription.Subscription.slave_profit;
						subscriptionResult.SubscribersCount++;
						subscriptionResult.Providers.Add(new AccountConnection
						{
							AccountId = subscription.Provider.id,
							Avatar = subscription.Provider.avatar,
							Nickname = subscription.Provider.nickname
						});
					}
				}

				return subscriptionsResult.ToArray();
			});
		}

		public OperationResult<SubscriptionData[]> GetProviderSubscribers(long providerId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get provider subscriptions, provider id - {0}", providerId);
				var subscriptions = signalServiceRepository.GetProviderSubscriptions(providerId);


				var accountInfos = accService.GetAccountsInfo(subscriptions.Select(x => (long)x.Subscriber.id).Distinct().ToArray());
				if (!accountInfos.IsSuccess) throw new OperationException(accountInfos.Error, accountInfos.Code);

				var subscriptionsResult = new List<SubscriptionData>();

				foreach (var accountInfo in accountInfos.Result)
				{
					var info = accountInfo;
					var subscription = subscriptions.First(x => x.Subscriber.id == info.AccountId);

					var subscriptionResult = new SubscriptionData
					{
						Login = accountInfo.Login,
						ClientId = subscription.Subscriber.client_account_id,
						Avatar = accountInfo.Avatar,
						AccountId = accountInfo.AccountId,
						AccountType = accountInfo.AccountType,
						Balance = (decimal)accountInfo.Balance,
						Nickname = subscription.Subscriber.nickname,
						ProviderNickname = subscription.Provider.nickname,
						Equity = (decimal)accountInfo.Equity,
						WorkingDays = info.WorkingDays,
						Leverage = accountInfo.Leverage,
						Currency = accountInfo.Currency,
						Profit = subscription.Subscription.slave_profit,
						Procent = 100 + (((decimal)accountInfo.Balance / 100) * (decimal)(accountInfo.Equity - accountInfo.Balance))
					};

					subscriptionsResult.Add(subscriptionResult);
				}
				return subscriptionsResult.ToArray();
			});
		}

		public OperationResult<ProviderData[]> GetSubscriberProviders(long subscriberId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get subscriber providers, subscriber id - {0}", subscriberId);
				var subscriptions = signalServiceRepository.GetSubscriberProviders(subscriberId);

				var accountInfos = accService.GetAccountsInfo(subscriptions.Select(x => (long)x.Provider.id).Distinct().ToArray());
				if (!accountInfos.IsSuccess) throw new OperationException(accountInfos.Error, accountInfos.Code);

				var providerDatas = new List<ProviderData>();

				foreach (var accountInfo in accountInfos.Result)
				{
					var info = accountInfo;
					var provider = subscriptions.First(x => x.Provider.id == info.AccountId);

					var providerData = new ProviderData
					{
						Login = accountInfo.Login,
						ClientId = provider.Provider.client_account_id,
						Avatar = accountInfo.Avatar,
						AccountId = accountInfo.AccountId,
						AccountType = accountInfo.AccountType,
						Balance = (decimal)accountInfo.Balance,
						Nickname = provider.Provider.nickname,
						SubscriberNickname = provider.Subscriber.nickname,
						Equity = (decimal)accountInfo.Equity,
						WorkingDays = info.WorkingDays,
						Leverage = accountInfo.Leverage,
						Currency = accountInfo.Currency,
						Profit = provider.Subscription.slave_profit,
						Procent = 100 + (((decimal)accountInfo.Balance / 100) * (decimal)(accountInfo.Equity - accountInfo.Balance))
					};

					providerDatas.Add(providerData);
				}

				return providerDatas.ToArray();
			});
		}

		public OperationResult UpdateProviderRating(long accountId, float? rating, int count)
		{
			Logger.Trace("Update rating, account id - {0}", accountId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.UpdateProviderRating(accountId, rating, count));
		}

		public OperationResult UpdateSubscriberRating(long accountId, float? rating, int count)
		{
			Logger.Trace("Update rating, account id - {0}", accountId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.UpdateSubscriberRating(accountId, rating, count));
		}

		public OperationResult<SubscriptionSettings> GetSubscriptionSettings(long masterId, long slaveId)
		{
			Logger.Trace("Get subscription settings for master {0} and slave {1}", masterId, slaveId);
			return InvokeOperations.InvokeOperation(() => new SubscriptionSettings(signalServiceRepository.GetSignalSymbolSubscription(masterId, slaveId)));
		}

		public OperationResult<Tuple<string, SubscriberOrders[]>> GetSubscriberOpenedOrders(long subscriberId)
		{
			Logger.Trace("Get subscriber {0} opened orders", subscriberId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var ordersDictionary = new Dictionary<long, List<Trade>>();
				var providers = signalServiceRepository.GetProvidersBySubscriber(subscriberId);
				var subscriber = signalServiceRepository.GetSubscriber(subscriberId);

				var orders = accService.GetOpenOrders(subscriberId);
				if (!orders.IsSuccess) throw new OperationException(orders.Error, orders.Code);

				foreach (var order in orders.Result)
				{
					var providerId = TradeProvider(order.Comment);

					if (ordersDictionary.ContainsKey(providerId))
						ordersDictionary[providerId].Add(order);
					else
						ordersDictionary.Add(providerId, new List<Trade> { order });
				}

				return new Tuple<string, SubscriberOrders[]>(subscriber.currency, ordersDictionary.Select(order => new SubscriberOrders
				{
					MasterId = order.Key != 0 ? order.Key : subscriber.id,
					MasterAvatar = order.Key != 0 ? providers.First(x => x.id == order.Key).avatar : subscriber.avatar,
					MasterNickname = order.Key != 0 ? providers.First(x => x.id == order.Key).nickname : subscriber.nickname,
					TotalProfit = MathHelper.FairRound(order.Value.Sum(x => x.CurrentProfit)),
					OpenedOrders = order.Value
				}).ToArray());
			});
		}

		public OperationResult<Tuple<bool, bool>> IsSubscriptionListUpdate(long clientId, DateTime lastUpdateDate)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Is subscription list update, client id - {0}", clientId);
				if (!clientsLastUpdate.ContainsKey(clientId) ||
					!clientsLastUpdate[clientId].HasValue) throw new Exception("Key not found");

				var serverLastUpdateDate = clientsLastUpdate[clientId].Value;

				// ToDo : check what to update
				if (serverLastUpdateDate < lastUpdateDate) throw new Exception();

				return new Tuple<bool, bool>(true, true);
			});
		}

		public OperationResult<Dictionary<Int16, String>> GetSignalSubscriptionTypes()
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get signal subscription types");
				var result = signalServiceRepository.GetSignalSubscriptionTypes();
				return result.ToDictionary(type => type.id, type => type.name);
			});
		}

		public OperationResult<Dictionary<Int16, String>> GetSignalOrderTypes()
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get signal order types");
				var result = signalServiceRepository.GetSignalOrderTypes();
				return result.ToDictionary(type => type.id, type => type.name);
			});
		}

		//public OperationResult<Trade[]> GetOpenTrades(int login, AccountType type)
		//{
		//	try
		//	{
		//		var server = signalServiceRepository.GetServerName(login, type);
		//		return new OperationResult<Trade[]> { IsSuccess = true, Result = accService.GetOpenOrders(server, login).Result };
		//	}
		//	catch (Exception ex)
		//	{
		//		return new OperationResult<Trade[]> { IsSuccess = false, Error = ex.Message };
		//	}
		//}

		public OperationResult SubscriptionSettingsUpdate(long slaveId, long masterId, SubscriptionSettings settings)
		{
			Logger.Trace("Subscription settings update, slave id - {0}, master id - {1}", slaveId, masterId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.SignalSubscriptionSettingsUpdate(slaveId, masterId, settings));
		}

		public OperationResult<ProviderSettings[]> GetProvidersWithSettings(long tradingAccountId)
		{
			Logger.Trace("Get providers with settings, account id - {0}", tradingAccountId);
			return InvokeOperations.InvokeOperation(() => signalServiceRepository.GetProviderSettings(tradingAccountId));
		}

		public OperationResult<SubscriptionConnection[]> GetSubscriptionConnections(long clientId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get subscription connections, client id - {0}", clientId);
				var connection = signalServiceRepository.GetSubscriptionConnection(clientId);

				var connectionResult =
					connection.
					Select(x => new SubscriptionConnection { ProviderId = x.Provider.id, SubscriberId = x.Subscriber.id })
						.ToArray();

				return connectionResult;
			});
		}

		public OperationResult<Provider[]> GetProvidersBySubscriber(long subscriberId)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Get providers for subscriber, id - {0}", subscriberId);

				var providers = signalServiceRepository.GetProvidersBySubscriber(subscriberId);

				var providersResult = new List<Provider>();

				providersResult.AddRange(providers.Select(signalProvider => new Provider
				{
					AccountId = signalProvider.id,
					IsSubscribe = false,
					Login = signalProvider.login,
					Nickname = signalProvider.nickname,
					AccountType = (AccountType)signalProvider.account_type_id
				}));

				return providersResult.ToArray();
			});
		}

		public OperationResult ChangeProviderVisibility(long accountId, bool value)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Provider visibility for {0} set to {1}", accountId, value);

				signalServiceRepository.ChangeProviderVisibility(accountId, value);
			});
		}

		public OperationResult ChangeSubscriberVisibility(long accountId, bool value)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Subscriber visibility for {0} set to {1}", accountId, value);

				signalServiceRepository.ChangeSubscriberVisibility(accountId, value);
			});
		}

		/// <summary>
		/// Get subscribers with opened orders
		/// Not for wcf
		/// </summary>
		//public Dictionary<long, signal_opened_orders[]> GetAllSubscribersWithOpenedOrders()
		//{
		//	return signalServiceRepository.GetAllSubscribersWithOpenedOrders();
		//}
		#endregion

		#region Private methods

		private long TradeProvider(string comment)
		{
			var items = comment.Split('_');
			return items[0] != "Sub" ? 0 : Convert.ToInt64(items[1]);
		}

		private MT4Router CreateRouter(ISignalServiceRepository signalServiceRepository)
		{
			OperationResult<ServerConfiguration[]> serversData;

			while (true)
			{
				try
				{
					serversData = accService.GetServerConfigurations();
					if (!serversData.IsSuccess)
						throw new Exception(serversData.Error);

					break;
				}
				catch (EndpointNotFoundException)
				{
					Thread.Sleep(1000);
				}
			}

			var res = serversData.Result;
			var names = res.Select(x => x.Name).ToArray();
			var confs = res.Select(x => x.Ip).ToArray();
			var ports = res.Select(x => x.Port).ToArray();
			return new MT4Router(names, confs, ports);
		}


		#endregion

		#region Logging

		private static void LogExecutionSignal(Tuple<string, ExecutionSignal> tuple)
		{
			var builder = new StringBuilder();
			builder.AppendLine("Execution signal");
			builder.AppendLine(String.Format("To: {0}", tuple.Item1));

			var signal = tuple.Item2;
			builder.AppendLine(String.Format("Destination: {0}", signal.Destination));
			builder.AppendLine(String.Format("Orders count: {0}", signal.Orders.Count));
			foreach (var order in signal.Orders)
			{
				builder.AppendLine(String.Format("Order: id - {0}, action - {1}, side - {2}, symbol - {3}, volume - {4}, commission - {5} ",
					order.OrderID, order.ActionType, order.Side, order.Symbol, order.Volume, order.Commission));
			}
			Logger.Info(builder.ToString());
		}

		private static void LogTradeSignal(Tuple<string, MT4TradeSignal> tuple)
		{
			var builder = new StringBuilder();
			builder.AppendLine("TradeSignal");
			builder.AppendLine(String.Format("From: {0}", tuple.Item1));
			var trade = tuple.Item2;
			builder.AppendLine(String.Format("Login: {0}", trade.Login));
			builder.AppendLine(String.Format("DateTime: {0}", new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(trade.DateTime)));
			builder.AppendLine(String.Format("Server: {0}", trade.Server));

			builder.AppendLine(String.Format("ActionType: {0}", trade.ActionType));
			builder.AppendLine(String.Format("Order: id - {0}, side - {1}, symbol - {2}, volume - {3}, tp - {4}, sl - {5}, balance - {6}, profit - {7}, comment - {8}, commission - {9}",
				trade.OrderID, trade.Side, trade.Symbol, trade.Volume, trade.TakeProfit, trade.StopLoss, trade.Balance, trade.Profit, trade.Comment, trade.ProviderCommission));
			Logger.Info(builder.ToString());
		}
		private static void LogSignal(Tuple<string, Signal> signal)
		{
			Logger.Info("Signal come. ClientId: {3}, Type: {0}, Source: {1}. Description: {2}", signal.Item2.Type, signal.Item2.Source, signal.Item2.Description, signal.Item1);
		}

		#endregion
	}
}

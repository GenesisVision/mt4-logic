using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using DataModel;
using LinqToDB;
using Helpers;
using Helpers.Security;
using SignalService.AccountService;
using SignalService.Entities.Enums;
using SignalService.Interfaces;
using SignalService.Models;
using SignalService.Models.Repository;
using Enumerable = System.Linq.Enumerable;
using Mt4AccountLocation = SignalService.Models.Mt4AccountLocation;
using ProviderSettings = SignalService.Models.ProviderSettings;
using Queryable = System.Linq.Queryable;

namespace SignalService
{
	// ToDo : accountId
	public class SignalServiceRepository : ISignalServiceRepository
	{
		#region Fields

		private readonly Cache<long, signal_providers> providerCache = new Cache<long, signal_providers>();
		private readonly Cache<long, signal_subscribers> subscriberCache = new Cache<long, signal_subscribers>();
		private readonly Cache<int, signal_subscriptions> subscriptionCache = new Cache<int, signal_subscriptions>();

		#endregion

		#region Constructor

		public SignalServiceRepository()
		{
			InitCache();
		}

		#endregion

		#region Insert/Update operations

		public void AddProvider(long clientId, long accountId, string nickname, bool isEnabled, int accountTypeId, int login, string description, string avatar, string currency, decimal commission)
		{
			using (var db = GetDBConnect())
			{
				var provider = new signal_providers
				{
					id = (int)accountId,
					commission = commission,
					account_type_id = accountTypeId,
					client_account_id = clientId,
					login = login,
					nickname = nickname,
					isenabled = isEnabled,
					isdeleted = false,
					avatar = avatar,
					currency = currency
				};

				db.Insert(provider);
				providerCache.InsertOrUpdate(accountId, provider);
			}
		}

		public void DeleteProvider(long providerId)
		{
			using (var db = GetDBConnect())
			{
				try
				{
					db.BeginTransaction();

					Queryable.Where(db.signal_providers, provider => provider.id == providerId)
						.Set(provider => provider.isdeleted, true)
						.Update();

					Queryable.Where(db.signal_subscriptions, sub => sub.account_master_id == providerId)
						.Set(sub => sub.status, (short)SubscriptionStatus.Off)
						.Update();

					var providerVal = providerCache.Get(providerId);
					providerVal.isdeleted = true;
					providerCache.InsertOrUpdate(providerId, providerVal);

					var subscriptions = subscriptionCache.Filter(x => x.account_master_id == providerId);
					foreach (var subscription in subscriptions)
					{
						subscription.status = (short)SubscriptionStatus.Off;
						subscriptionCache.InsertOrUpdate(subscription.id, subscription);
					}

					db.CommitTransaction();
				}
				catch (Exception)
				{
					db.RollbackTransaction();
					throw;
				}
			}
		}

		public void DeleteSubscriber(long subscriberId)
		{
			using (var db = GetDBConnect())
			{
				try
				{
					db.BeginTransaction();

					Queryable.Where(db.signal_providers, subscriber => subscriber.id == subscriberId)
						.Set(subscriber => subscriber.isdeleted, true)
						.Update();

					Queryable.Where(db.signal_subscriptions, sub => sub.account_slave_id == subscriberId)
						.Set(sub => sub.status, (short)SubscriptionStatus.Off)
						.Update();

					var subscriberVal = subscriberCache.Get(subscriberId);
					subscriberVal.isdeleted = true;
					subscriberCache.InsertOrUpdate(subscriberId, subscriberVal);

					db.CommitTransaction();
				}
				catch (Exception)
				{
					db.RollbackTransaction();
					throw;
				}
			}
		}

		public void AddCommission(long slaveId, long masterId, decimal commission, int orderId)
		{
			using (var db = GetDBConnect())
			{
				var subscriptionId = Enumerable.First(subscriptionCache.Filter(x => x.account_slave_id == slaveId && x.account_master_id == masterId)).id;

				db.Insert(new signal_commission
				{
					subscription_id = subscriptionId,
					amount = commission,
					date = DateTime.Now,
					order_id = orderId,
					is_accrued = false
				});
			}
		}

		public void AddProfit(long slaveId, long masterId, decimal profit)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_subscriptions, sub => sub.account_master_id == masterId && sub.account_slave_id == slaveId)
					.Set(sub => sub.slave_profit, sub => sub.slave_profit + profit)
					.Update();

				var signalSubscription =
					Queryable.First(db.signal_subscriptions, sub => sub.account_slave_id == slaveId && sub.account_master_id == masterId);

				subscriptionCache.InsertOrUpdate(signalSubscription.id, signalSubscription);
			}
		}

		public void SignalSubscription(short status, long accountSlaveId, long accountMasterId, ISubscribeSettings settings, AccountInfo result)
		{
			using (var db = GetDBConnect())
			{
				try
				{
					db.BeginTransaction();

					var signalSubscriber = new signal_subscribers
					{
						id = (int)accountSlaveId,
						client_account_id = result.ClientId,
						account_type_id = result.AccountTypeId,
						isenabled = result.IsEnabled,
						isdeleted = false,
						login = result.Login,
						nickname = result.Nickname,
						avatar = result.Avatar,
						currency = result.Currency
					};

					db.InsertOrReplace(signalSubscriber);
					subscriberCache.InsertOrUpdate(accountSlaveId, signalSubscriber);

					var signalSubscription = new signal_subscriptions
					{
						status = (short)SubscriptionStatus.On,
						account_slave_id = accountSlaveId,
						account_master_id = accountMasterId,
						slave_profit = 0,
						subscription_type = (short)settings.SignalType,
						multiplier = settings.Multiplier,
						reverse = settings.Reverse,
						risk = settings.Risk,
						max_order_count = settings.MaxOrderCount,
						max_volume = settings.MaxVolume,
						order_type = (short)settings.OrdersType
					};

					var id = (int)db.InsertWithIdentity(signalSubscription);
					subscriptionCache.InsertOrUpdate(id, signalSubscription);

					db.CommitTransaction();
				}
				catch (Exception)
				{
					db.RollbackTransaction();
					throw;
				}
			}
		}

		public void SignalSubscription(short status, long accountSlaveId, string masterNickname, ISubscribeSettings settings,
			AccountInfo result)
		{
			using (var db = GetDBConnect())
			{
				try
				{
					db.BeginTransaction();

					var signalSubscriber = new signal_subscribers
					{
						id = (int)accountSlaveId,
						client_account_id = result.ClientId,
						account_type_id = result.AccountTypeId,
						isenabled = result.IsEnabled,
						isdeleted = false,
						login = result.Login,
						nickname = result.Nickname,
						avatar = result.Avatar,
						currency = result.Currency
					};

					db.InsertOrReplace(signalSubscriber);
					subscriberCache.InsertOrUpdate(accountSlaveId, signalSubscriber);

					var signalSubscription = new signal_subscriptions
					{
						status = (short)SubscriptionStatus.On,
						account_slave_id = accountSlaveId,
						account_master_id = Queryable.First(db.signal_providers, source => source.nickname.ToLower() == masterNickname.ToLower()).id,
						slave_profit = 0,
						subscription_type = (short)settings.SignalType,
						multiplier = settings.Multiplier,
						reverse = settings.Reverse,
						risk = settings.Risk,
						max_order_count = settings.MaxOrderCount,
						max_volume = settings.MaxVolume,
						order_type = (short)settings.OrdersType
					};

					var id = Convert.ToInt32(db.InsertWithIdentity(signalSubscription));
					subscriptionCache.InsertOrUpdate(id, signalSubscription);

					db.CommitTransaction();
				}
				catch (Exception)
				{
					db.RollbackTransaction();
					throw;
				}
			}
		}

		public void Unsubscribe(long slaveId, long masterId)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_subscriptions, x => x.account_slave_id == slaveId && x.account_master_id == masterId)
					.Set(x => x.status, (short)SubscriptionStatus.Off)
					.Update();

				var subscriptionVal = Enumerable.First(subscriptionCache.Filter(x => x.account_slave_id == slaveId && x.account_master_id == masterId));
				subscriptionVal.status = (short)SubscriptionStatus.Off;
				subscriptionCache.InsertOrUpdate(subscriptionVal.id, subscriptionVal);
			}
		}

		public void ChangeProviderVisibility(long accountId, bool value)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_providers, acc => acc.id == accountId)
					.Set(acc => acc.isvisible, value)
					.Update();

				var providerVal = providerCache.Get(accountId);
				providerVal.isvisible = value;
				providerCache.InsertOrUpdate(accountId, providerVal);
			}
		}

		public void ChangeSubscriberVisibility(long accountId, bool value)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_subscribers, acc => acc.id == accountId)
					.Set(acc => acc.isvisible, value)
					.Update();
			}

			var subscriberVal = subscriberCache.Get(accountId);
			subscriberVal.isvisible = value;
			subscriberCache.InsertOrUpdate(accountId, subscriberVal);
		}

		public void ChangeProviderAvatar(long accountId, string newAvatar)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_providers, acc => acc.id == accountId)
					.Set(acc => acc.avatar, newAvatar)
					.Update();
			}

			var providerVal = providerCache.Get(accountId);
			providerVal.avatar = newAvatar;
			providerCache.InsertOrUpdate(accountId, providerVal);
		}

		public void ChangeSubscriberAvatar(long accountId, string newAvatar)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_subscribers, acc => acc.id == accountId)
					.Set(acc => acc.avatar, newAvatar)
					.Update();

				var subscriberVal = subscriberCache.Get(accountId);
				subscriberVal.avatar = newAvatar;
				subscriberCache.InsertOrUpdate(accountId, subscriberVal);
			}
		}

		public void ChangeProviderDescription(long accountId, string newDescription)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_providers, acc => acc.id == accountId)
					.Set(acc => acc.description, newDescription)
					.Update();

				var providerVal = providerCache.Get(accountId);
				providerVal.description = newDescription;
				providerCache.InsertOrUpdate(accountId, providerVal);
			}
		}

		public void SignalSubscriptionSettingsUpdate(Int64 accountSlaveId, Int64 accountMasterId, ISubscribeSettings settings)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_subscriptions, sub => sub.account_slave_id == accountSlaveId && sub.account_master_id == accountMasterId)
					.Set(p => p.subscription_type, (short)settings.SignalType)
					.Set(p => p.multiplier, settings.Multiplier)
					.Set(p => p.reverse, settings.Reverse)
					.Set(p => p.risk, settings.Risk)
					.Set(p => p.max_order_count, settings.MaxOrderCount)
					.Set(p => p.max_volume, settings.MaxVolume)
					.Set(p => p.order_type, (short)settings.OrdersType)
					.Set(p => p.status, (short)settings.Status)
					.Update();

				var settingsVal = Enumerable.First(subscriptionCache.Filter(x => x.account_slave_id == accountSlaveId && x.account_master_id == accountMasterId));
				settingsVal.subscription_type = (short)settings.SignalType;
				settingsVal.multiplier = settings.Multiplier;
				settingsVal.reverse = settings.Reverse;
				settingsVal.risk = settings.Risk;
				settingsVal.max_order_count = settings.MaxOrderCount;
				settingsVal.max_volume = settings.MaxVolume;
				settingsVal.order_type = (short)settings.OrdersType;
				settingsVal.status = (short)settings.Status;
				subscriptionCache.InsertOrUpdate(settingsVal.id, settingsVal);
			}
		}

		public void UpdateProviderRating(long accountId, float? rating, int count)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_providers, acc => acc.id == accountId)
					.Set(acc => acc.rating_value, rating)
					.Set(acc => acc.rating_count, count)
					.Update();

				var providerVal = providerCache.Get(accountId);
				providerVal.rating_value = rating;
				providerVal.rating_count = count;
				providerCache.InsertOrUpdate(accountId, providerVal);
			}
		}

		public void UpdateSubscriberRating(long accountId, float? rating, int count)
		{
			using (var db = GetDBConnect())
			{
				Queryable.Where(db.signal_subscribers, acc => acc.id == accountId)
					.Set(acc => acc.rating_value, rating)
					.Set(acc => acc.rating_count, count)
					.Update();

				var subscriberVal = subscriberCache.Get(accountId);
				subscriberVal.rating_value = rating;
				subscriberVal.rating_count = count;
				subscriberCache.InsertOrUpdate(accountId, subscriberVal);
			}
		}

		#endregion

		#region Get operations

		public SubscriberWithSettingsModel[] Subscribers(long accountId)
		{
			//var query = from subscriber in db.signal_subscribers
			//			join subscription in db.signal_subscriptions on subscriber.id equals subscription.account_slave_id
			//			where subscription.account_master_id == accountId && (subscription.status == (short)SubscriptionStatus.On || subscription.status == (short)SubscriptionStatus.CloseOnly)
			//			select new { subscription, subscriber };

			//var res = query.Select(
			//		subscription =>
			//			new Tuple<signal_subscribers, ISubscribeSettings>(subscription.subscriber,
			//				new SubscriptionSettings(subscription.subscription))).ToArray();

			return Enumerable.ToArray(Enumerable.Select(subscriptionCache.Filter(x => x.account_master_id == accountId), subscription => new SubscriberWithSettingsModel
			{
				Subscriber = subscriberCache.Get(subscription.account_slave_id),
				Subscription = subscription
			}));
		}

		public SubscriberModel[] GetSubscriptions(long clientId)
		{
			using (var db = GetDBConnect())
			{
				var subscribers = from signalSubscriber in db.signal_subscribers

								  join subscription in db.signal_subscriptions
										on signalSubscriber.id equals subscription.account_slave_id
										into subscriptionNull
								  from subscription in Enumerable.DefaultIfEmpty(subscriptionNull)

								  join provider in db.signal_providers
									  on subscription.account_master_id equals provider.id
									  into providerNull
								  from provider in Enumerable.DefaultIfEmpty(providerNull)

								  where signalSubscriber.client_account_id == clientId && (subscription.status == (short)SubscriptionStatus.On || subscription.status == (short)SubscriptionStatus.CloseOnly)

								  select new SubscriberModel
								  {
									  Subscriber = signalSubscriber,
									  Subscription = subscription,
									  Provider = provider
								  };

				return subscribers.ToArray();
			}
		}

		public ProviderModel[] GetDeliveries(long clientId)
		{
			using (var db = GetDBConnect())
			{
				var providers = from signalProvider in db.signal_providers

								join subscription in db.signal_subscriptions
									on signalProvider.id equals subscription.account_master_id
									into subscriptionNull
								from subscription in Enumerable.DefaultIfEmpty(subscriptionNull)

								join subscriber in db.signal_subscribers
									on subscription.account_slave_id equals subscriber.id
									into subscriberNull
								from subscriber in Enumerable.DefaultIfEmpty(subscriberNull)

								where signalProvider.client_account_id == clientId
									  && (subscription == null || subscription.status == (short)SubscriptionStatus.On || subscription.status == (short)SubscriptionStatus.CloseOnly)
									  && !signalProvider.isdeleted

								select new ProviderModel
								{
									Provider = signalProvider,
									Subscription = subscription,
									Subscriber = subscriber
								};

				return providers.ToArray();
			}
		}

		public ProviderFullInformation[] GetProvider(long clientId)
		{
			using (var db = GetDBConnect())
			{
				var providers = Enumerable.ToArray(Queryable.Select(Queryable.Where(db.signal_providers, x => x.client_account_id == clientId), x => new ProviderFullInformation
				{
					AccountId = x.id,
					AccountType = (AccountType)x.account_type_id,
					Avatar = x.avatar,
					ClientId = x.client_account_id,
					Currency = x.currency,
					IsVisible = x.isvisible,
					Login = x.login,
					Nickname = x.nickname,
					RatingValue = x.rating_value == null ? 0.0f : x.rating_value.Value,
				}));

				foreach (var provider in providers)
				{
					provider.Subscribers = Enumerable.ToList<SubscriberData>((from subscriber in db.signal_subscribers
																			  join subscription in db.signal_subscriptions on subscriber.id equals subscription.account_slave_id
																			  where subscription.account_master_id == provider.AccountId
																			  select new SubscriberData
																			  {
																				  AccountId = subscriber.id,
																				  Avatar = subscriber.avatar,
																				  ClientId = subscriber.client_account_id,
																				  Nickname = subscriber.nickname
																			  }));
				}

				return providers;
			}
		}

		public signal_providers[] GetProvidersBySubscriber(long subscriberId)
		{
			using (var db = GetDBConnect())
			{
				var query = from provider in db.signal_providers
							join subscription in db.signal_subscriptions
								on provider.id equals subscription.account_master_id
							where subscription.account_slave_id == subscriberId
							&& subscription.status == (short)SubscriptionStatus.On || subscription.status == (short)SubscriptionStatus.CloseOnly
							&& !provider.isdeleted
							select provider;
				return query.ToArray();
			}
		}

		public signal_providers[] GetAllProviders()
		{
			using (var db = GetDBConnect())
			{
				return Enumerable.ToArray(Queryable.Where(db.signal_providers, provider => !provider.isdeleted));
			}
		}

		public IEnumerable<signal_subscription_types> GetSignalSubscriptionTypes()
		{
			using (var db = GetDBConnect())
			{
				return Enumerable.ToList<signal_subscription_types>((from sst in db.signal_subscription_types select sst));
			}
		}

		public IEnumerable<signal_order_types> GetSignalOrderTypes()
		{
			using (var db = GetDBConnect())
			{
				return Enumerable.ToList<signal_order_types>((from sot in db.signal_order_types select sot));
			}
		}

		public ProviderSettings[] GetProviderSettings(long tradingAccountId)
		{
			using (var db = GetDBConnect())
			{
				var query =
					from subs in db.signal_subscriptions
					join provider in db.signal_providers on subs.account_master_id equals provider.id
					join sub in db.signal_subscribers on subs.account_slave_id equals sub.id
					join type in db.account_types on provider.account_type_id equals type.id
					join server in db.trading_servers on type.server_id equals server.id
					where sub.id == tradingAccountId
					select new
					{
						masterLogin = provider.login,
						masterServer = server.name,
						Nickname = provider.nickname,
						settings = subs,
						reverse = subs.reverse,
						status = subs.status
					};
				var result = query.Select(r => new ProviderSettings
				{
					Login = r.masterLogin,
					ServerName = r.masterServer,
					Nickname = r.Nickname,
					Reverse = r.reverse,
					Status = r.status, // add poteryashka ^^
					CloseOnly = r.status == (short)SubscriptionStatus.CloseOnly,
					Settings = new SubscriptionSettings(r.settings)
				}).ToArray();
				return result;
			}
		}

		public SubscriptionConnectionModel[] GetSubscriptionConnection(long clientId)
		{
			using (var db = GetDBConnect())
			{
				return (from subscription in db.signal_subscriptions
						join subscriber in db.signal_subscribers
							on subscription.account_slave_id equals subscriber.id
						join provider in db.signal_providers
							on subscription.account_master_id equals provider.id

						where (subscription.status == (short)SubscriptionStatus.On || subscription.status == (short)SubscriptionStatus.CloseOnly)
						&& !provider.isdeleted
						&& !subscriber.isdeleted

						select new SubscriptionConnectionModel
						{
							Provider = provider,
							Subscriber = subscriber
						}).ToArray();
			}
		}

		public SubscriberModel[] GetProviderSubscriptions(long providerId)
		{
			using (var db = GetDBConnect())
			{
				var subscribers = from subscriber in db.signal_subscribers

								  join subscription in db.signal_subscriptions
									  on subscriber.id equals subscription.account_slave_id

								  join provider in db.signal_providers
								   on subscription.account_master_id equals provider.id

								  where provider.id == providerId
										   && subscription != null
										   && (subscription.status == (short)SubscriptionStatus.On || subscription.status == (short)SubscriptionStatus.CloseOnly)

								  select new SubscriberModel
								  {
									  Subscriber = subscriber,
									  Subscription = subscription,
									  Provider = provider
								  };

				return subscribers.ToArray();
			}
		}

		public SubscriberModel[] GetSubscriberProviders(long subscriberId)
		{
			using (var db = GetDBConnect())
			{
				var subscribers = from signalSubscriber in db.signal_subscribers


								  join subscription in db.signal_subscriptions
									  on signalSubscriber.id equals subscription.account_slave_id
									  into subscriptionNull
								  from subscription in Enumerable.DefaultIfEmpty(subscriptionNull)

								  join provider in db.signal_providers
									  on subscription.account_master_id equals provider.id
									  into providerNull
								  from provider in Enumerable.DefaultIfEmpty(providerNull)

								  where signalSubscriber.id == subscriberId
										&& subscription != null
										&& (subscription.status == (short)SubscriptionStatus.On || subscription.status == (short)SubscriptionStatus.CloseOnly)

								  select new SubscriberModel
								  {
									  Subscriber = signalSubscriber,
									  Subscription = subscription,
									  Provider = provider
								  };

				return subscribers.ToArray();
			}
		}

		public Dictionary<long, Tuple<signal_subscribers, signal_opened_orders[]>> GetAllSubscribersWithOpenedOrders()
		{
			using (var db = GetDBConnect())
			{
				var query = from subscribers in db.signal_subscribers
							join ordersMap in db.signal_subscribers_opened_order on subscribers.id equals ordersMap.subscriber
							join orders in db.signal_opened_orders on ordersMap.order_id equals orders.id
							group orders by subscribers
								into g
								select new { subscriber = g.Key, orders = g.ToArray() };
				var res = query.ToDictionary(arg => arg.subscriber.id, arg => new Tuple<signal_subscribers, signal_opened_orders[]>(arg.subscriber, arg.orders));
				return res;
			}
		}

		public signal_subscribers GetSubscriber(long id)
		{
			using (var db = GetDBConnect())
			{
				return db.signal_subscribers.First(subscriber => subscriber.id == id);
			}
		}

		public signal_subscriptions GetSignalSymbolSubscription(long masterId, long slaveId)
		{
			using (var db = GetDBConnect())
			{
				return db.signal_subscriptions.FirstOrDefault(x => x.account_master_id == masterId && x.account_slave_id == slaveId);
			}
		}

		public signal_providers[] GetProvidersByLogin(int login)
		{
			return providerCache.Filter(provider => provider.login == login);
		}

		public signal_subscribers GetSubscriberByLogin(int login, int accountType)
		{
			return subscriberCache.Filter(subscriber => subscriber.account_type_id == accountType && subscriber.login == login).FirstOrDefault();
		}

		public Mt4AccountLocation[] GetAccountsMt4Location()
		{
			using (var db = GetDBConnect())
			{
				var query = (from provider in db.signal_providers
							 where !provider.isdeleted
							 select new Mt4AccountLocation
							 {
								 AccountId = provider.id,
								 AccountType = provider.account_type_id,
								 Login = provider.login
							 })
					.Union
					(from subscriber in db.signal_subscribers
					 where !subscriber.isdeleted
					 select new Mt4AccountLocation
					 {
						 AccountId = subscriber.id,
						 AccountType = subscriber.account_type_id,
						 Login = subscriber.login
					 });

				return query.ToArray();
			}
		}

		public Dictionary<long, List<signal_commission>> GetNotAccruedCommission()
		{
			using (var db = GetDBConnect())
			{
				var qa = (from commission in db.signal_commission
						  join subscription in db.signal_subscriptions on commission.subscription_id equals subscription.id
						  where !commission.is_accrued
						  select new { subscription, commission }).ToArray();

				var result = qa.GroupBy(x => x.subscription.account_master_id, x => x.commission);

				return result.ToDictionary(x => x.Key, x => x.ToList());
			}
		}

		public void MarkCommissions(int[] commissionIds, bool isAccrued)
		{
			using (var db = GetDBConnect())
			{
				db.signal_commission.Where(x => commissionIds.Contains(x.id))
					.Set(x => x.is_accrued, isAccrued)
					.Update();
			}
		}

		public void InitCache()
		{
			SignalService.Logger.Info("Repository cache initializing...");

			using (var db = GetDBConnect())
			{
				var providers = db.signal_providers.Where(x => !x.isdeleted)
					.ToDictionary(provider => provider.id, provider => provider);

				var subscribers = db.signal_subscribers.Where(x => !x.isdeleted)
					.ToDictionary(subscriber => subscriber.id, subscriber => subscriber);

				var subscriptions = db.signal_subscriptions.Where(x => x.status == (short)SubscriptionStatus.On || x.status == (short)SubscriptionStatus.CloseOnly)
					.ToDictionary(subscription => subscription.id, subscription => subscription);

				providerCache.Init(providers);
				subscriberCache.Init(subscribers);
				subscriptionCache.Init(subscriptions);
			}

			SignalService.Logger.Info("Repository cache initialized");
		}

		#endregion

		#region Private methods

		private static DB GetDBConnect()
		{
			var hashedConStr = ConfigurationManager.ConnectionStrings["DB"].ToString();
			var connectionString = Encrypter.DecryptConnectionString(hashedConStr);
			return new DB(connectionString, true);
		}

		#endregion
	}
}

using System;
using System.Collections.Generic;
using DataModel;
using SignalService.AccountService;
using SignalService.Models;
using SignalService.Models.Repository;
using Mt4AccountLocation = SignalService.Models.Mt4AccountLocation;

namespace SignalService.Interfaces
{
	public interface ISignalServiceRepository
	{
		/// <summary>
		/// Get subscribers for account
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns>Tuple of: server name, accountID, subscription settings</returns>
		SubscriberWithSettingsModel[] Subscribers(long accountId);

		/// <summary>
		/// Add signal provider
		/// </summary>
		/// <param name="clientId"></param>
		/// <param name="accountId"></param>
		/// <param name="nickname">Nickname of provider</param>
		/// <param name="isEnabled"></param>
		/// <param name="accountTypeId"></param>
		/// <param name="login"></param>
		/// <param name="description"></param>
		/// <param name="avatar"></param>
		/// <param name="currency"></param>
		/// <param name="commission"></param>
		void AddProvider(long clientId, long accountId, string nickname, bool isEnabled, int accountTypeId, int login, string description, string avatar, string currency, decimal commission);

		/// <summary>
		/// Mark porvider as deleted + unscribe all subscribers
		/// </summary>
		/// <param name="tradingAccountId">Account id</param>
		void DeleteProvider(long tradingAccountId);

		/// <summary>
		/// Mark subscriber as deleted + unscribe
		/// </summary>
		/// <param name="tradingAccountId"></param>
		void DeleteSubscriber(long tradingAccountId);

		/// <summary>
		/// Add commission to trading account
		/// </summary>
		/// <param name="slaveId"></param>
		/// <param name="masterId"></param>
		/// <param name="commission"></param>
		/// <param name="orderId"></param>
		void AddCommission(long slaveId, long masterId, decimal commission, int orderId);

		/// <summary>
		/// Add profit to slave
		/// </summary>
		/// <param name="slaveId"></param>
		/// <param name="masterId"></param>
		/// <param name="profit"></param>
		void AddProfit(long slaveId, long masterId, decimal profit);

		/// <summary>
		/// Subscribe
		/// </summary>
		/// <param name="status"></param>
		/// <param name="accountSlaveId"></param>
		/// <param name="accountMasterId"></param>
		/// <param name="settings"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		void SignalSubscription(short status, long accountSlaveId, long accountMasterId, ISubscribeSettings settings, AccountInfo result);

		/// <summary>
		/// Subscribe
		/// </summary>
		/// <param name="status"></param>
		/// <param name="accountSlaveId"></param>
		/// <param name="masterNickname"></param>
		/// <param name="settings"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		void SignalSubscription(short status, long accountSlaveId, string masterNickname, ISubscribeSettings settings, AccountInfo result);

		/// <summary>
		/// Get subscription types
		/// </summary>
		/// <returns></returns>
		IEnumerable<signal_subscription_types> GetSignalSubscriptionTypes();

		/// <summary>
		/// Get signal provider
		/// </summary>
		ProviderFullInformation[] GetProvider(long clientId);

		/// <summary>
		/// Get providers for subscriber
		/// </summary>
		/// <param name="subscriberId"></param>
		/// <returns></returns>
		signal_providers[] GetProvidersBySubscriber(long subscriberId);

		signal_providers[] GetAllProviders();

		/// <summary>
		/// Get signal order types 
		/// </summary>
		/// <returns></returns>
		IEnumerable<signal_order_types> GetSignalOrderTypes();

		/// <summary>
		/// Get subscriptions for account
		/// </summary>
		/// <returns></returns>
		SubscriberModel[] GetSubscriptions(long clientId);

		/// <summary>
		/// Get deliveries for account
		/// </summary>
		/// <param name="accountIds"></param>
		/// <returns></returns>
		ProviderModel[] GetDeliveries(long clientId);

		void SignalSubscriptionSettingsUpdate(Int64 accountSlaveId, Int64 accountMasterId, ISubscribeSettings settings);

		ProviderSettings[] GetProviderSettings(long tradingAccountId);

		void Unsubscribe(long slaveId, long masterId);
		SubscriptionConnectionModel[] GetSubscriptionConnection(long clientId);

		void ChangeProviderVisibility(long accountId, bool value);

		void ChangeSubscriberVisibility(long accountId, bool value);
		void ChangeProviderAvatar(long accountId, string newAvatar);
		void ChangeSubscriberAvatar(long accountId, string newAvatar);
		void ChangeProviderDescription(long accountId, string newDescription);

		SubscriberModel[] GetProviderSubscriptions(long providerId);

		SubscriberModel[] GetSubscriberProviders(long subscriberId);

		void UpdateProviderRating(long accountId, float? rating, int count);

		void UpdateSubscriberRating(long accountId, float? rating, int count);

		/// <summary>
		/// Get subscribers with opened orders
		/// </summary>
		Dictionary<long, Tuple<signal_subscribers, signal_opened_orders[]>> GetAllSubscribersWithOpenedOrders();

		signal_subscribers GetSubscriber(long id);

		signal_subscriptions GetSignalSymbolSubscription(long masterId, long slaveId);

		signal_providers[] GetProvidersByLogin(int login);

		signal_subscribers GetSubscriberByLogin(int login, int accountType);

		Mt4AccountLocation[] GetAccountsMt4Location();

		Dictionary<long, List<signal_commission>> GetNotAccruedCommission();

		void MarkCommissions(int[] commissionIds, bool isAccrued);
	}
}

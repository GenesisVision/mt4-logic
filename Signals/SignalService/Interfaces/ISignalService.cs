using System;
using System.Collections.Generic;
using System.ServiceModel;
using Helpers;
using SignalService.Models;

namespace SignalService.Interfaces
{
	public interface ISignalService
	{
		void AddProvider(long accountId, string nickname, string description, decimal commission);

		void Subscribe(long slaveId, long masterId, SubscriptionSettings settings);

		void SubscribeByNickname(long slaveId, string masterNickname, SubscriptionSettings settings);

		List<ProviderInfo> GetProvidersList(long subscriberId);

		void Unsubscribe(long slaveId, long masterId);

		Delivery[] GetDeliveries(long clientId);

		Provider[] GetAllProviders();

		ProviderFullInformation[] GetProviders(long clientId);

		Subscription[] GetSubscriptions(long clientId);

		/// <summary>
		/// Checks if list of subscriptions was updated
		/// </summary>
		/// <param name="clientId">Of this client</param>
		/// <param name="lastUpdateDate">Checks with last update date</param>
		/// <returns>First: whole list update, Second: only numbers update</returns>
		Tuple<bool, bool> IsSubscriptionListUpdate(long clientId, DateTime lastUpdateDate);

		Dictionary<Int16, String> GetSignalSubscriptionTypes();

		Dictionary<Int16, String> GetSignalOrderTypes();

		//[OperationContract]
		//OperationResult<Trade[]> GetOpenTrades(int login, AccountType type);

		void SubscriptionSettingsUpdate(long slaveId, long masterId, SubscriptionSettings settings);

		ProviderSettings[] GetProvidersWithSettings(long tradingAccountId);

		/// <summary>
		/// Get Subscriber connection with provider
		/// </summary>
		/// <param name="clientId">client id</param>
		/// <returns>Scubription connection wth subscriber and provider</returns>
		SubscriptionConnection[] GetSubscriptionConnections(long clientId);

		/// <summary>
		/// Get all providers of given subscriber
		/// </summary>
		/// <param name="subscriberId">Subscriber's trading account id</param>
		/// <returns></returns>
		Provider[] GetProvidersBySubscriber(long subscriberId);

		void ChangeProviderVisibility(long accountId, bool value);

		void ChangeSubscriberVisibility(long accountId, bool value);

		void DeleteProvider(long accountId);

		void DeleteSubscriber(long accountId);

		void ChangeProviderAvatar(long accountId, string newAvatar);

		void ChangeSubscriberAvatar(long accountId, string newAvatar);

		void ChangeProviderDescription(long accountId, string newDescription);

		SubscriptionData[] GetProviderSubscribers(long providerId);

		ProviderData[] GetSubscriberProviders(long subscriberId);

		void UpdateProviderRating(long accountId, float? rating, int count);

		void UpdateSubscriberRating(long accountId, float? rating, int count);

		SubscriptionSettings GetSubscriptionSettings(long masterId, long slaveId);

		Tuple<string, SubscriberOrders[]> GetSubscriberOpenedOrders(long subscriberId);
	}
}

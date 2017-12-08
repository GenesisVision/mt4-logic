using System;
using System.Configuration;
using System.Linq;
using GenesisVision.TrustManagement;
using GenesisVision.TrustManagement.Models;
using Helpers;
using Helpers.ResultCodes;
using NLog;

namespace Service.Logic
{
	public class TrustService 
	{
		#region Fields

		private readonly IRepository repository;

		public static Logger Logger = LogManager.GetCurrentClassLogger();
		private TrustManager Manager;
		private readonly IStatisticService statisticService;

		#endregion

		#region Construction

		public TrustService(IRepository repository, IStatisticService statisticService)
		{
			Logger.Trace(" service construction");
			this.repository = repository;
			this.statisticService = statisticService;
			this.accountService = accountService;
			Manager = new TrustManager(repository, statisticService);
		}

		#endregion

		#region Public Methods

		public MasterSettings GetMasterSettings(long masterId)
		{
			Logger.Trace("Get master settings for {0}", masterId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var master = repository.GetMaster(masterId);
				//var masterDeposit = repository.GetMasterInvestSum(masterId);
				//var investDeposit = repository.GetInvestSumByMaster(masterId);
				var settings = new MasterSettings
				{
					NextProcessing = master.date_next_processing,
					Period = master.period,
					ManagementFee = master.fee_management,
					SuccessFee = master.fee_success,
					MasterAmount = master.amount_own,
					//InvestorsCount = repository.GetInvestorsCount(masterId),
					Nickname = master.nickname,
					//MasterDeposit = masterDeposit,
					//InvestorsDeposit = investDeposit,
					MinimalAmount = master.amount_min
				};

				return settings;
			});
		}

		public InvestSettings GetInvestSettings(long investorId)
		{
			Logger.Trace("Get invest settings, investor id - {0}", investorId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var investSettings = repository.GetInvestSettings(investorId);
				var investor = investSettings.Item1;
				var master = investSettings.Item2;
				if (investor.isdeleted || master.isdeleted)
					throw new Exception("Account is deleted");
				var deposit = repository.GetInvestSumByInvestor(investor.id);
				var settings = new InvestSettings
				{
					Reinvest = investor.reinvest_percent,
					Amount = investor.amount,
					MasterId = master.trading_account_id,
					MasterNickname = master.nickname,
					CurrentProfit = 0,
					NextProcessing = master.date_next_processing,
					Period = master.period,
					Deposit = deposit
				};
				return settings;
			});
		}

		public MasterData[] GetMasters(long clientId)
		{
			Logger.Trace("Get master accounts info for client {0}", clientId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var mastersInfo = repository.GetMasters(clientId);

				foreach (var m in mastersInfo)
				{
					var masterInvestors = repository.GetMasterInvestors(m.AccountId);
					m.Investors = masterInvestors.Select(x => new MasterInvestorData
						{
							Avatar = x.avatar,
							ClientId = x.client_account_id,
							InvestorId = x.id
						}).ToList();
					m.InvestorsCount = masterInvestors.Count(x => x.status != (short)AccountStatus.PendingIn);
					m.InvestorsIncoming = masterInvestors.Count(x => x.status == (short)AccountStatus.PendingIn) -
										  masterInvestors.Count(x => x.status == (short)AccountStatus.PendingOut);
					m.Investments += masterInvestors.Sum(x => x.amount);
					m.InvestmentsIncoming = repository.GetInvestSumByMaster(m.AccountId) +
											repository.GetInvestSumByMasterInvestors(m.AccountId);
				}

				return mastersInfo;
			});
		}

		public MasterData[] GetClientsMastersAdmin(long[] clientsId)
		{
			Logger.Trace("Get masters accounts info for clients for admin");
			return InvokeOperations.InvokeOperation(() =>
			{
				var mastersInfo = repository.GetClientsMastersAdmin(clientsId);

				foreach (var m in mastersInfo)
				{
					var masterInvestors = repository.GetMasterInvestors(m.AccountId);
					m.Investors = masterInvestors.Select(x => new MasterInvestorData
					{
						Avatar = x.avatar,
						ClientId = x.client_account_id,
						InvestorId = x.id
					}).ToList();
					m.InvestorsCount = masterInvestors.Count(x => x.status != (short)AccountStatus.PendingIn);
					m.InvestorsIncoming = masterInvestors.Count(x => x.status == (short)AccountStatus.PendingIn) -
										  masterInvestors.Count(x => x.status == (short)AccountStatus.PendingOut);
					m.Investments += masterInvestors.Sum(x => x.amount);
					m.InvestmentsIncoming = repository.GetInvestSumByMaster(m.AccountId) +
											repository.GetInvestSumByMasterInvestors(m.AccountId);
				}

				return mastersInfo;
			});
		}

		public MasterInformation GetMasterInformation(long masterId)
		{
			Logger.Trace("Get master investors for {0}", masterId);

			return InvokeOperations.InvokeOperation(() => new MasterInformation
			{
				Settings = repository.GetMasterSettings(masterId),
				InvestorsInfo = repository.GetInvestorsInfo(masterId)
			});
		}

		public void CreateMasterAccount(MasterAccountSettings settings)
		{
			Logger.Trace("Create  master account");
			return InvokeOperations.InvokeOperation(() =>
			{
				var statuses = accountService.GetAccountStatuses(settings.TradingAccountId);
				if (!statuses.IsSuccess)
					throw new OperationException(statuses.Error, statuses.Code);
				if (statuses.Result.Has(AccountStatuses.IsPropTrading))
					throw new OperationException("Not available", ResultCode.SiteOperationNotAvailable);
				if (settings.AmountOwn < Convert.ToInt32(ConfigurationManager.AppSettings["MinimalMasterInvestAmount"]))
					throw new OperationException("Not enough money ", ResultCode.NotEnoughMoney);
				repository.CreateMasterAccount(settings);

				statisticService.CalculateStatisticAccount(settings.TradingAccountId);
			});
		}

		public InvestorData[] GetInvestors(long clientId)
		{
			Logger.Trace("Get investors for client: {0}", clientId);
			return InvokeOperations.InvokeOperation(() => repository.GetInvestors(clientId));
		}

		public void InvestorInvest(long id, decimal amount)
		{
			Logger.Trace("Invest request. invistorId - {0}, amount - {1}", id, amount);
			return InvokeOperations.InvokeOperation(() =>
			{
				if (amount < 0) throw new Exception("Amount should be positive");

				var amountInvested = repository.GetInvestSumByInvestor(id);
				if (amountInvested < 0)
					amount += Math.Abs(amountInvested);

				repository.InvestRequest(id, amount, false);
			});
		}

		public void MasterInvest(long id, decimal amount)
		{
			Logger.Trace("Invest request. MasterId - {0}, amount - {1}", id, amount);
			return InvokeOperations.InvokeOperation(() =>
			{
				if (amount < 0) throw new Exception("Amount should be positive");

				var amountInvested = repository.GetInvestSumByMaster(id);
				if (amountInvested < 0)
					amount += Math.Abs(amountInvested);

				repository.InvestRequest(id, amount, true);
			});
		}

		public long InvestorCreateAndInvest(long clientId, long masterId, int walletId, decimal amount, short reinvestPercent)
		{
			Logger.Trace("Check existance and invest");
			return InvokeOperations.InvokeOperation(() =>
			{
				if (amount < 0) throw new Exception("Amount should be positive");
				var master = repository.GetMaster(masterId);
				if (master.client_account_id == clientId) throw new OperationException("You can't invest to your own account", ResultCode.CantInvestToYourOwnAccount);
				var investor = repository.GetInvestor(clientId, masterId);
				if (investor != null && !investor.isdeleted) throw new OperationException("You already invested to that master", ResultCode.AlreadyInvested);

				return repository.CreateInvestorAccount(clientId, masterId, walletId, amount, reinvestPercent);
				//else
				//{
				//	repository.InvestRequest(investor.id, amount, false);
				//	return new InvestorData();
				//}
			});
		}

		public WithdrawData InvestorWithdraw(long id, decimal amount)
		{
			Logger.Trace("Withraw request. investorId - {0}, amount - {1}", id, amount);
			amount = Math.Abs(amount);
			return InvokeOperations.InvokeOperation(() =>
			{
				var investor = repository.GetInvestorById(id);
				var amountInInvestRequests = repository.GetInvestSumByInvestor(id);
				repository.WithdrawRequest(id, -amount, false);

				var withdrawData = new WithdrawData
				{
					WalletId = investor.wallet_id,
					AmountAvailable = amountInInvestRequests <= 0
						? 0.0m
						: amount < amountInInvestRequests ? amount : amountInInvestRequests
				};

				return withdrawData;
			});
		}

		public WithdrawData MasterWithdraw(long id, decimal amount)
		{
			Logger.Trace("Withraw request. masterId - {0}, amount - {1}", id, amount);
			amount = Math.Abs(amount);
			return InvokeOperations.InvokeOperation(() =>
			{
				var master = repository.GetMaster(id);
				var amountInInvestRequests = repository.GetInvestSumByMaster(id);
				repository.WithdrawRequest(id, -amount, true);

				var withdrawData = new WithdrawData
				{
					WalletId = master.wallet_id,
					AmountAvailable = amountInInvestRequests <= 0
						? 0.0m
						: amount < amountInInvestRequests ? amount : amountInInvestRequests
				};

				return withdrawData;
			});
		}

		public void ChangeMasterVisibility(long accountId, bool value)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Master visibility for {0} set to {1}", accountId, value);

				repository.ChangeMasterVisibility(accountId, value);
			});
		}

		public void ChangeSlaveVisibility(long accountId, bool value)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Slave visibility for {0} set to {1}", accountId, value);

				repository.ChangeSlaveVisibility(accountId, value);
			});
		}

		public void ChangeMasterAvatar(long accountId, string newAvatar)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Change master {0} avatar", accountId);

				repository.ChangeMasterAvatar(accountId, newAvatar);
			});
		}

		public void ChangeSlaveAvatar(long accountId, string newAvatar)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Change master {0} avatar", accountId);

				repository.ChangeSlaveAvatar(accountId, newAvatar);
			});
		}

		public void ChangeMasterDescription(long accountId, string newDescription)
		{
			return InvokeOperations.InvokeOperation(() =>
			{
				Logger.Trace("Change master {0} description", accountId);

				repository.ChangeMasterDescription(accountId, newDescription);
			});
		}

		public void UpdateRating(long accountId, float? rating, int count)
		{
			Logger.Trace("Update rating, account id - {0}", accountId);
			return InvokeOperations.InvokeOperation(() => repository.UpdateRating(accountId, rating, count));
		}

		public bool CheckInvestorBelonging(long investorId, long clientId)
		{
			Logger.Trace("Check investor {0} belonging to client {1}", investorId, clientId);
			return InvokeOperations.InvokeOperation(() => repository.CheckInvestorBelonging(investorId, clientId));
		}

		public int GetIvestorsCount(long masterId)
		{
			Logger.Trace("Get investors amount for master {0}", masterId);
			return
				InvokeOperations.InvokeOperation(
					() => repository.GetMasterInvestors(masterId).Count(x => x.status == (short)AccountStatus.In));
		}

		public InvestorStatistic[] GetInvestorsStatisticForPeriod(DateTime from, DateTime to)
		{
			Logger.Trace("Get investors statistic: {0} {1}", from, to);
			return InvokeOperations.InvokeOperation(() => repository.GetInvestorsStatisticForPeriod(from, to));
		}

		public void CloseMaster(long accountId)
		{
			Logger.Trace("Close master account {0}", accountId);
			return InvokeOperations.InvokeOperation(() => repository.CloseMasterRequest(accountId));
		}

		public void CloseInvestorAndWithdraw(long accountId)
		{
			Logger.Trace("Close investor account {0}", accountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				var amountInInvestRequests = repository.GetInvestSumByInvestor(accountId);
				if (amountInInvestRequests > 0)
					throw new OperationException("Withdraw money that you've already invested", ResultCode.CloseInvestorWithInvestRequests);
				var investor = repository.GetInvestorById(accountId);
				repository.WithdrawRequest(accountId, -investor.amount, false);
				repository.CloseInvestorRequest(accountId);
			});
		}

		public void UpdateMasterDate(long tradingAccountId)
		{
			Logger.Trace("Update master date {0}", tradingAccountId);
			return InvokeOperations.InvokeOperation(() =>
			{
				lock (TrustManager.lockHandler)
				{
					repository.UpdateMasterDate(tradingAccountId, DateTime.Now);
				}
				Manager.Handler();
			});
		}

		#endregion
	}
}

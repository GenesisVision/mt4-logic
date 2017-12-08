using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GenesisVision.TrustManagement;
using Helpers;
using Service.Entities.Enums;
using Service.Logic.Interfaces;
using Service.Logic.Models.Manager;
using Service.Logic.StatisticServiceReference;
using AccountStatus = Service.Entities.Enums.AccountStatus;

namespace Service.Logic
{
	public class TrustManager
	{
		#region Fields

		private readonly IAccountService accountService;
		private readonly IRepository repository;
		private readonly IStatisticService statisticService;
		private const int sheduleInterval = 120; // Interval in seconds

		public static readonly object lockHandler = new object();

		#endregion

		#region Construction

		public TrustManager(IRepository repository, IAccountService accountService, IStatisticService statisticService)
		{
			this.accountService = accountService;
			this.repository = repository;
			this.statisticService = statisticService;
			Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(sheduleInterval))
				.Subscribe(timestamped => Handler());
		}

		#endregion

		#region Public Methods

		public void Handler()
		{
			lock (lockHandler)
			{
				try
				{
					TrustService.Logger.Info("Planned  handling started. Time: {0}", DateTime.Now);

					var ids = repository.GetMasterIdsForProcessing(DateTime.Now);
					DisableAccounts(ids);
					CloseAllOrders(ids);

					TrustService.Logger.Info("Processing masters {0}", string.Join(",", ids));
					var mastersWithInvestors = repository.GetMasterWithInvestors(ids);
					Parallel.ForEach(mastersWithInvestors, Process);

					EnableAccounts(ids);

					TrustService.Logger.Info(" handling ended");
				}
				catch (Exception e)
				{
					TrustService.Logger.Error(" handler error: {0}", e.Message);
				}
			}
		}

		#endregion

		#region Private Methods

		private void DisableAccounts(long[] accountIds)
		{
			try
			{
				TrustService.Logger.Info("Disable trading accounts with ids: {0}", string.Join(",", accountIds));
				if (accountIds.Length == 0)
				{
					TrustService.Logger.Info("accountIds.Length == 0");
					return;
				}
				var result = accountService.EnableTradingAccounts(accountIds, false);
				if (!result.IsSuccess) throw new Exception(result.Error);

				TrustService.Logger.Info("Trading disabled");
			}
			catch (Exception ex)
			{
				TrustService.Logger.Error(ex.Message);
			}
		}

		private void EnableAccounts(long[] accountIds)
		{
			try
			{
				TrustService.Logger.Info("Enable trading accounts with ids: {0}", string.Join(",", accountIds));
				if (accountIds.Length == 0)
				{
					TrustService.Logger.Info("accountIds.Length == 0");
					return;
				}
				var result = accountService.EnableTradingAccounts(accountIds, true);
				if (!result.IsSuccess) throw new Exception(result.Error);

				TrustService.Logger.Info("Trading enabled");
			}
			catch (Exception ex)
			{
				TrustService.Logger.Error(ex.Message);
			}
		}

		private void CloseAllOrders(long[] accountIds)
		{
			try
			{
				TrustService.Logger.Info("Close all orders for trading accounts with ids: {0}", string.Join(",", accountIds));
				if (accountIds.Length == 0)
				{
					TrustService.Logger.Info("accountIds.Length == 0");
					return;
				}
				var result = accountService.CloseAllOrders(accountIds, " payment period");
				if (!result.IsSuccess) throw new Exception(result.Error);

				TrustService.Logger.Info("Orders closed");
			}
			catch (Exception ex)
			{
				TrustService.Logger.Error(ex.Message);
			}
		}

		private void Process(MasterWithInvestors master)
		{
			try
			{
				switch (master.AccountStatus)
				{
					case AccountStatus.PendingIn:
					case AccountStatus.In:
						ProcessMasterStaying(master);
						break;
					case AccountStatus.PendingOut:
						ProcessMasterLeaving(master);
						break;
				}
			}
			catch (Exception e)
			{
				TrustService.Logger.Error(" processing error: {0}", e.Message);
			}
		}

		//Process In or PendingIn masters
		private void ProcessMasterStaying(MasterWithInvestors master)
		{
			var info = accountService.GetAccountInfo(master.TradingAccountId);
			if (!info.IsSuccess) throw new Exception(info.Error);

			TrustService.Logger.Info("Current balance of master {0} is {1}", master.TradingAccountId, info.Result.Balance);

			if (master.AccountStatus == AccountStatus.PendingIn)
			{
				master.AmountOwn = (decimal)info.Result.Balance;
			}
			else
			{
				var totalAmountAtStart = master.Investors.Where(investor => investor != null).Sum(investor => investor.amount) + master.AmountOwn;

				TrustService.Logger.Info("Total amount at the beginning of period for master {0} is {1}", master.TradingAccountId, totalAmountAtStart);

				var profit = (decimal)info.Result.Balance - totalAmountAtStart;
				TrustService.Logger.Info("Total profit for account {0} is {1}", master.TradingAccountId, profit);

				ProfitCalculation(master, profit);
			}

			var masterChangeModel = NextPeriodParametersCalculation(master.TradingAccountId, master.AccountStatus, master.WalletId, master.AmountOwn, InvestmentsCalculation(master.TradingAccountId, master.AmountOwn, true), true);
			var investorsChangeModel = master.Investors.Select(investor => NextPeriodParametersCalculation(investor.id, (AccountStatus)investor.status, investor.wallet_id, investor.amount, InvestmentsCalculation(investor.id, investor.amount, false), false)).ToList();

			var amountTotal = investorsChangeModel.Sum(x => x.AmountOwn) + masterChangeModel.AmountOwn;

			TrustService.Logger.Info("Master {0} balance for next period is {1}", master.TradingAccountId, amountTotal);

			masterChangeModel.Percent = amountTotal != 0 ? masterChangeModel.AmountOwn / amountTotal * 100 : 0;
			foreach (var investorChangeModel in investorsChangeModel)
			{
				investorChangeModel.Percent = amountTotal != 0 ? investorChangeModel.AmountOwn / amountTotal * 100 : 0;
			}

			accountService.ChangeAccountBalance(master.TradingAccountId, amountTotal - (decimal)info.Result.Balance, " processing");

			repository.ChangeParameters(masterChangeModel, investorsChangeModel, master.DateNextProcessing, master.DateNextProcessing.AddDays(master.Period));
		}

		//Process PendingOut masters
		private void ProcessMasterLeaving(MasterWithInvestors master)
		{
			var info = accountService.GetAccountInfo(master.TradingAccountId);
			if (!info.IsSuccess) throw new Exception(info.Error);

			TrustService.Logger.Info("Current balance of master {0} is {1}", master.TradingAccountId, info.Result.Balance);

			var totalAmountAtStart = master.Investors.Where(investor => investor != null).Sum(investor => investor.amount) + master.AmountOwn;

			TrustService.Logger.Info("Total amount at the beginning of period for master {0} is {1}", master.TradingAccountId, totalAmountAtStart);

			var profit = (decimal)info.Result.Balance - totalAmountAtStart;
			TrustService.Logger.Info("Total profit for account {0} is {1}", master.TradingAccountId, profit);

			ProfitCalculation(master, profit);

			var masterInvestments = InvestmentsCalculation(master.TradingAccountId, master.AmountOwn, true);
			if (masterInvestments > 0) master.AmountOwn += masterInvestments;

			TrustService.Logger.Info("Incoming amount to wallet for master {0} is {1}", master.TradingAccountId, master.AmountOwn);
            
			var accountResult = accountService.ChangeAccountBalance(master.TradingAccountId, (decimal)-info.Result.Balance, " closure");
			if (!accountResult.IsSuccess) throw new Exception(accountResult.Error);

			foreach (var investor in master.Investors)
			{
				var invesments = InvestmentsCalculation(investor.id, investor.amount, false);
				if (invesments > 0) investor.amount += invesments;

				TrustService.Logger.Info("Incoming amount to wallet for investor {0} is {1}", investor.id, investor.amount);
			}

			repository.CloseMasterWithInvestors(master.TradingAccountId, master.Investors.Select(x => (long)x.id).ToArray());
			accountService.DeleteAccount(master.TradingAccountId);
		}

		//Updates AmountOwn for master and investors due to Profit, Reinvestment, ManagementFee, SuccessFee
		private void ProfitCalculation(MasterWithInvestors master, decimal profit)
		{
			decimal totalProfitInvestors = master.Investors.Sum(investor => MathHelper.UnfairRound(profit * investor.percent / 100));
			master.AmountOwn += MathHelper.UnfairRound(profit - totalProfitInvestors);

			foreach (var investor in master.Investors)
			{
				if (investor.status == (short)AccountStatus.PendingIn) continue;

				var investorFullProfit = MathHelper.UnfairRound(profit * investor.percent / 100);

				TrustService.Logger.Info("Profit fot investor {0} is {1}", investor.id, investorFullProfit);

				var managementFee = MathHelper.UnfairRound(master.FeeManagement / (365m / master.Period) * investor.amount / 100);

				if (profit > 0)
				{
					var totalProfit = repository.GetTotalInvestorProfit(investor.id, master.TradingAccountId);

					TrustService.Logger.Info("Total profit for investor {0} for all previous periods is {1}", investor.id,
						totalProfit);

					if (investorFullProfit + totalProfit > 0)
					{
						var successFee = totalProfit > 0
							? MathHelper.UnfairRound(investorFullProfit * master.FeeSuccess / 100)
							: MathHelper.UnfairRound((investorFullProfit + totalProfit) * master.FeeSuccess / 100);

						master.AmountOwn += successFee;
						var netProfit = investorFullProfit - successFee - managementFee;

						var reinvest = MathHelper.UnfairRound(netProfit * (decimal)investor.reinvest_percent / 100);

						investor.amount += reinvest;

						TrustService.Logger.Info("Values for investor {0} : Net Profit - {1}, Success Fee - {2}, Reinvest Amount - {3}",
							investor.id, netProfit, successFee, reinvest);
					}
				}
				else
				{
					investor.amount += (investorFullProfit - managementFee);
				}

				master.AmountOwn += managementFee;

				repository.WriteStatistics(investor.id, master.TradingAccountId, investorFullProfit, profit, master.DateNextProcessing.AddDays(-master.Period), master.DateNextProcessing);
			}
		}

		//Calculate new values for AmountOwn and AccountStatus + process withdrawal
		private ParametersChangeModel NextPeriodParametersCalculation(long accountId, AccountStatus accountStatus, long walletId, decimal amountOwn, decimal investmentAmount, bool isMaster)
		{
			var changeModel = new ParametersChangeModel
			{
				Id = accountId,
				AmountOwn = amountOwn,
				PrevAccountStatus = accountStatus,
				NewAccountStatus =
					accountStatus == AccountStatus.In || accountStatus == AccountStatus.PendingIn
						? AccountStatus.In
						: AccountStatus.Out
			};
            
			changeModel.AmountOwn += investmentAmount;

			//delete investors with 0
			if (!isMaster && changeModel.AmountOwn == 0)
				changeModel.NewAccountStatus = AccountStatus.Out;

			return changeModel;
		}

		//Calcualte real investments amount, available for user
		private decimal InvestmentsCalculation(long accountId, decimal amountOwn, bool isMaster)
		{
			var amountRequested = isMaster ? repository.GetInvestSumByMaster(accountId) : repository.GetInvestSumByInvestor(accountId);

			if (amountRequested >= 0) return amountRequested;

			var realAmount = Math.Abs(amountRequested) > amountOwn
				? -amountOwn
				: amountRequested;

			TrustService.Logger.Info("{0} {1} invested amount is {2}", isMaster ? "Master" : "Investor", accountId, realAmount);

			return realAmount;
		}

		#endregion

	}
}

using GenesisVision.TrustManagement.Models;
using System;
using System.Collections.Generic;

namespace GenesisVision.TrustManagement
{
    public interface IRepository
    {
        /// <summary>
        /// Get master data
        /// </summary>
        /// <param name="masterId"></param>
        /// <returns></returns>
        MasterData GetMaster(string masterId);

        /// <summary>
        /// Get investor by id
        /// </summary>
        /// <returns></returns>
        InvestorData GetInvestorById(string investorId);

        /// <summary>
        /// Get total amount of investors for given master
        /// </summary>
        /// <param name="masterId"></param>
        /// <returns></returns>
        int GetInvestorsCount(string masterId);

        /// <summary>
        /// Gets master investors inf
        /// </summary>
        /// <param name="masterAccountId">Master trading account</param>
        /// <returns></returns>
        InvestorInformation[] GetInvestorsInfo(string masterAccountId);

        /// <summary>
        /// Set setting to  master accounts
        /// </summary>
        /// <param name="settings">Data</param>
        void CreateMasterAccount(MasterAccountSettings settings);

        /// <summary>
        /// Create investor account
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="masterId"></param>
        /// <param name="walletId"></param>
        /// <param name="amount"></param>
        /// <param name="reinvestPercent">reinvestment settings</param>
        /// <returns></returns>
        long CreateInvestorAccount(long clientId, long masterId, int walletId, decimal amount, short reinvestPercent);

        /// <summary>
        /// Get total profit of given master-investor pair for all time
        /// </summary>
        /// <param name="investorId"></param>
        /// <param name="masterId"></param>
        /// <returns></returns>
        decimal GetTotalInvestorProfit(long investorId, long masterId);

        /// <summary>
        /// Get all invest data for investor
        /// </summary>
        /// <param name="clientId">client id</param>
        /// <returns></returns>
        InvestorData[] GetInvestors(long clientId);

        /// <summary>
        /// Create request to invest money
        /// </summary>
        /// <param name="id"></param>
        /// <param name="amount"></param>
        void InvestRequest(long id, decimal amount, bool isMaster);

        /// <summary>
        /// Put request for closure of master account
        /// </summary>
        /// <param name="accountId"></param>
        void CloseMasterRequest(long accountId);

        /// <summary>
        /// Put request for closure of investor account
        /// </summary>
        /// <param name="accountId"></param>
        void CloseInvestorRequest(long accountId);
        /// <summary>
        /// Withraw money. If exist request to invest - withdraw from them, otherwise push request to withdraw
        /// </summary>
        /// <param name="id">investor account id</param>
        /// <param name="amount">Amount</param>
        void WithdrawRequest(long id, decimal amount, bool isMaster);


        /// <summary>
        /// Get total invest sum bu client id
        /// </summary>
        decimal GetInvestSumByInvestor(long investorId);


        /// <summary>
        /// Amount of in/out investors for next period for master accounts of given master
        /// </summary>
        /// <param name="id">trading id</param>
        /// <returns></returns>
        InvestorData[] GetMasterInvestors(long id);

        /// <summary>
        /// Check, what inverstor is exist for master
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="masterId"></param>
        /// <returns></returns>
        bool InverstorIsExist(long clientId, long masterId);

        decimal GetInvestSumByMaster(long masterId);

        decimal GetInvestSumByMasterInvestors(long masterId);

        void CloseMasterWithInvestors(long masterId, long[] investorIds);

        /// <summary>
        /// Get masters by next_processing date
        /// </summary>
        /// <param name="date"></param>
        long[] GetMasterIdsForProcessing(DateTime date);

        /// <summary>
        /// Update next date for master
        /// </summary>
        /// <param name="tradingAccountId"></param>
        /// <param name="nextProcessing"></param>
        void UpdateMasterDate(long tradingAccountId, DateTime nextProcessing);

        void WriteStatistics(long investorId, long masterId, decimal profit, decimal totalProfit, DateTime beginDate, DateTime endDate);

        MasterSettings GetMasterSettings(long masterId);

        void ChangeMasterVisibility(long accountId, bool value);

        void ChangeSlaveVisibility(long accountId, bool value);

        void ChangeMasterAvatar(long accountId, string newAvatar);

        void ChangeSlaveAvatar(long accountId, string newAvatar);

        void ChangeMasterDescription(long accountId, string newDescription);

        void UpdateRating(long accountId, float? rating, int count);

        bool CheckInvestorBelonging(long investorId, long clientId);

    }
}

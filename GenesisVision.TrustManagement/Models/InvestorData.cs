using System;
using System.Collections.Generic;
using System.Text;

namespace GenesisVision.TrustManagement.Models
{
    public class InvestorData
    {
        public long MasterId { get; set; }

        public string MasterAvatar { get; set; }

        public long MasterClientId { get; set; }

        public string MasterNickname { get; set; }

        public float? MasterRatingValue { get; set; }

        public int? MasterRatingCount { get; set; }

        public long InvestorId { get; set; }

        public decimal ProfitProportion { get; set; }

        public DateTime DateNextPeriod { get; set; }

        public decimal ProfitTotal { get; set; }

        public decimal Investments { get; set; }

        public decimal InvestmentsPending { get; set; }

        public int WorkingDays { get; set; }

        public string AccountType { get; set; }

        public bool IsVisible { get; set; }

    }
}

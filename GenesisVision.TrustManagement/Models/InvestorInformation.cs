using System;
using System.Collections.Generic;
using System.Text;

namespace GenesisVision.TrustManagement.Models
{
    public class InvestorInformation
    {
        public long InvestorId { get; set; }

        public long ClientId { get; set; }

        public string Avatar { get; set; }

        public string Nickname { get; set; }

        public decimal Profit { get; set; }

        public int TradingDays { get; set; }

        public decimal AmountInvested { get; set; }

        public decimal AmountInvestedPending { get; set; }
    }
}

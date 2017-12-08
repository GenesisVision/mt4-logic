using System;
using System.Collections.Generic;
using System.Text;

namespace GenesisVision.TrustManagement.Models
{
    public class InvestSettings
    {
        public short? Reinvest { get; set; }

        public long MasterId { get; set; }

        public string MasterNickname { get; set; }

        public short Period { get; set; }

        public DateTime NextProcessing { get; set; }

        public decimal Amount { get; set; }

        public decimal TotalProfit { get; set; }

        public decimal CurrentProfit { get; set; }

        public decimal PreviousProfit { get; set; }

        public decimal Deposit { get; set; }

    }
}

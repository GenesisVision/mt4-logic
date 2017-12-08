using System;

namespace GenesisVision.TrustManagement.Models
{
    public class MasterSettings
    {
        public long MasterId { get; set; }

        public DateTime NextProcessing { get; set; }

        public DateTime DateStart { get; set; }

        public string Nickname { get; set; }

        public short Period { get; set; }

        public decimal ManagementFee { get; set; }

        public decimal SuccessFee { get; set; }

        public decimal MinimalAmount { get; set; }

        public decimal MasterAmount { get; set; }

        public decimal TotalAmount { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace GenesisVision.TrustManagement.Models
{
    public class MasterData
    {
        public long ClientId { get; set; }
        public long AccountId { get; set; }

        public int Login { get; set; }

        public long AccountType { get; set; }

        public int InvestorsCount { get; set; }

        public int InvestorsIncoming { get; set; }

        public decimal Investments { get; set; }

        public decimal InvestmentsIncoming { get; set; }

        public DateTime DateNextProcessing { get; set; }

        public bool IsVisible { get; set; }

        public string Nickname { get; set; }

        public string Avatar { get; set; }

        public float? RatingValue { get; set; }

        public int? RatingCount { get; set; }
    }
}

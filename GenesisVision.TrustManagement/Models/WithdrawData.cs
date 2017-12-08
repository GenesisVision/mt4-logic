using System;
using System.Collections.Generic;
using System.Text;

namespace GenesisVision.TrustManagement.Models
{
    public class WithdrawData
    {
        public decimal AmountAvailable { get; set; }

        public long WalletId { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace GenesisVision.TrustManagement.Models
{
    public class MasterAccountSettings
    {
        public Int64 TradingAccountId { get; set; }
        public Int64 AccountTypeId { get; set; }
        public String Currency { get; set; }
        public Int16 Period { get; set; }
        public DateTime DateNextProcessing { get; set; }
        public Decimal AmountOwn { get; set; }
        public Decimal AmountMin { get; set; }
        public Int32 FeeMenagement { get; set; }
        public Int32 FeeSuccess { get; set; }
        public Boolean ConfirmationRequired { get; set; }
        public Int32 WalletId { get; set; }
        public String NickName { get; set; }
        public String Description { get; set; }
        public DateTime DateOpen { get; set; }
        public DateTime DateClose { get; set; }
        public Int64 ClientAccountId { get; set; }
        public Int32 Login { get; set; }
        public Boolean IsDeleted { get; set; }
        public Boolean IsEnabled { get; set; }
        public Int16 Status { get; set; }
        public String Avatar { get; set; }
    }
}

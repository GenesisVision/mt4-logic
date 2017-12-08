namespace TournamentService.Logic.Models
{
	public class ReferralStatistic
	{
		public long ReferralId { get; set; }

		public bool Approved { get; set; }

		public int Points { get; set; }

		public decimal CommissionReceived { get; set; }

		public string CommissionCurrency { get; set; }
	}
}
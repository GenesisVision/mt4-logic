using System;

namespace TournamentService.Logic.Models
{
	public class RoundData
	{
		public string TournamentId { get; set; }

		public string TournamentName { get; set; }

		public long RoundId { get; set; }

		public decimal EntryFee { get; set; }

		public decimal BaseDeposit { get; set; }

		public string Currency { get; set; }

		public DateTime Start { get; set; }

		public DateTime End { get; set; }

		public long DefaultAccountType { get; set; }

		public RoundStatus RoundStatus { get; set; }

		public long Number { get; set; }

		public bool AreOrdersVisible { get; set; }

		public bool IsDemo { get; set; }
        
	}

	public enum RoundStatus
	{
		OpenedForRegistration,
		IsStarted,
		IsFinished
	}
}
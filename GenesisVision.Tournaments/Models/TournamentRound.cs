using System;

namespace TournamentService.Logic.Models
{
	public class TournamentRound
	{
		public bool IsDemo { get; set; }
        
		public bool EntryAfterStart { get; set; }

		public RoundStatus Status { get; set; }

		public bool IsParticipating { get; set; }

		public string TournamentId { get; set; }

		public long TournamentRoundId { get; set; }

		public long TournamentTypeId { get; set; }

		public string TournamentTypeName { get; set; }

		public int AccountTypeId { get; set; }

		public long RoundNumber { get; set; }

		public string Name { get; set; }

		public decimal PrizeFound { get; set; }

		public decimal EntryFee { get; set; }

		public DateTime Start { get; set; }

		public DateTime End { get; set; }

		public long ParticipantsCount { get; set; }

		public long ParticipantsNeed { get; set; }

		public decimal BaseDeposit { get; set; }

		public string SmallLogo { get; set; }

		public string BigLogo { get; set; }
	}
}
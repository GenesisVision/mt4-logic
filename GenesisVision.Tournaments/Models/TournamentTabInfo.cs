using System;

namespace TournamentService.Logic.Models
{
	public class TournamentTabInfo
	{
		public string TournamentId { get; set; }
		public string Logo { get; set; }
		public string Name { get; set; }
		public string TournamentType { get; set; }
		public long RoundId { get; set; }
		public long RoundNumber { get; set; }
		public bool IsStarted { get; set; }
		public bool AreOrdersVisible { get; set; }
		public int ParticipantsNumber { get; set; }
		public int ParticipantsNeed { get; set; }
		public DateTime BeginDate { get; set; }
		public DateTime EndDate { get; set; }
		public int Place { get; set; }
		public decimal Prize { get; set; }
		public bool IsBanned { get; set; }
	}
}

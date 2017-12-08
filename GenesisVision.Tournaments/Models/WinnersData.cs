using System;

namespace TournamentService.Logic.Models
{
	public class WinnersData
	{
		public string Avatar { get; set; }

		public string Nickname { get; set; }

		public string TournamentName { get; set; }

		public DateTime DateFinished { get; set; }

		public decimal Award { get; set; }

		public long AccountId { get; set; }
	}
}

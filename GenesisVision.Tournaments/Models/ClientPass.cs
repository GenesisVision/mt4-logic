using System;

namespace TournamentService.Logic.Models
{
	public class ClientPass
	{
		public long Id { get; set; }

		public long ClientId { get; set; }

		public Pass Pass { get; set; }

		public bool IsRelevant { get; set; }

		public DateTime? ExpireDate { get; set; }
	}
}

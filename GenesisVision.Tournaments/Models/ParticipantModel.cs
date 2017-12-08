using System;

namespace TournamentService.Logic.Models
{
	public class ParticipantModel
	{
		public long Id { get; set; }
		public long AccountTypeId { get; set; }
		public string Avatar { get; set; }
		public long ClientAccountId { get; set; }
		public DateTime? DateDescriptionUpdated { get; set; }
		public string Description { get; set; }
		public bool Isvisible { get; set; }
		public int Login { get; set; }
		public string Nickname { get; set; }
		public int? RatingCount { get; set; }
		public float? RatingValue { get; set; }
		public bool IsBot { get; set; }
	}
}
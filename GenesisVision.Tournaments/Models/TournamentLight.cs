namespace TournamentService.Logic.Models
{
	public class TournamentLight
	{
		public string Id { get; set; }

		public string SmallLogo { get; set; }

		public string Name { get; set; }

		public bool IsStarted { get; set; }

		public string TypeName { get; set; }

		public int TypeId { get; set; }
	}
}
using System;
using TournamentService.Entities.Enums;

namespace TournamentService.Logic.Models
{
	public class MyTournament
	{
		public string Id { get; set; }
		public TournamentsType TournamentType { get; set; }
		public long RoundId { get; set; }
		public long RoundNumber { get; set; }
		public long RoundParticipantId { get; set; }
		public string Name { get; set; }
		public string Logo { get; set; }
		public string Avatar { get; set; }
		public decimal PrizeFund { get; set; }
		public float Rating { get; set; }
		public DateTime BeginDate { get; set; }
		public DateTime EndDate { get; set; }
		public int Duration { get; set; }
		public int ParticipantsNumber { get; set; }
		public int ParticipantsNeed { get; set; }
		public long TradingAccountId { get; set; }
		public int Login { get; set; }
		public decimal BaseDeposit { get; set; }
		public decimal EntryFee { get; set; }
		public TournamentCalculationType CalculationType { get; set; }
		public decimal Profit { get; set; }
		public decimal Volume { get; set; }
		public int Pips { get; set; }
		public int Place { get; set; }
		public bool IsRunning { get; set; }
		public bool IsDemo { get; set; }
		public string Nickname { get; set; }
		public long ClientId { get; set; }
		public decimal? Prize { get; set; }
	}
}

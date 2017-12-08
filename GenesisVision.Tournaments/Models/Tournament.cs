using System;
using System.Runtime.Serialization;

namespace TournamentService.Logic.Models
{
	[DataContract]
	public class Tournament
	{
		[DataMember]
		public string TournamentId { get; set; }

		[DataMember]
		public long RoundId { get; set; }

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public string FullDescription { get; set; }

		[DataMember]
		public string ShortDescription { get; set; }

		[DataMember]
		public string BigLogo { get; set; }

		[DataMember]
		public string SmallLogo { get; set; }

		[DataMember]
		public decimal EntryFee { get; set; }

		[DataMember]
		public DateTime DateStart { get; set; }

		[DataMember]
		public DateTime DateEnd { get; set; }

		[DataMember]
		public int PrizeFund { get; set; }

		[DataMember]
		public int TypeId { get; set; }
	}
}
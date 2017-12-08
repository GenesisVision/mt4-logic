using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TournamentService.Entities.Enums;

namespace TournamentService.Logic.Models
{
	public class TournamentInformation
	{
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
		public int DayInterval { get; set; }

		[DataMember]
		public int TimeDuration { get; set; }

		[DataMember]
		public int TypeId { get; set; }

		[DataMember]
		public List<RatingPrice> Ratings { get; set; }

		[DataMember]
		public List<AccountType> AccountTypes { get; set; }

		[DataMember]
		public int PrizeFund { get; set; }

		[DataMember]
		public int PrizePlaces { get; set; }
		
		[DataMember]
		public bool IsDemo { get; set; }

		[DataMember]
		public string IdName { get; set; }

		[DataMember]
		public decimal BaseDeposit { get; set; }

		[DataMember]
		public bool EntryAfterStart { get; set; }

		[DataMember]
		public int ParticipantsNeed { get; set; }

		[DataMember]
		public TournamentCalculationType CalculationType { get; set; }
	}
}
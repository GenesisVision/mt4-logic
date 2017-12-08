using System;
using ProtoTypes;

namespace SignalService.Interfaces
{
	public interface IRequestable
	{
		/// <summary>
		/// Execute trade
		/// </summary>
		void Request(Tuple<string, Request> request);
	}
}

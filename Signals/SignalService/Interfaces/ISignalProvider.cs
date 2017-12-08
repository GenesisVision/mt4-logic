using System;
using ProtoTypes;

namespace SignalService.Interfaces
{
	public interface ISignalProvider
	{
		/// <summary>
		/// Observable collection of trade signals
		/// </summary>
		event Action<Tuple<string, Signal>> Signals;
	}
}

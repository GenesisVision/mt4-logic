using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using ProtoTypes;

namespace SignalService
{
	public class TradeSocialSignalProcessor
	{
		public static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        

		#region Construction

		public TradeSocialSignalProcessor()
		{
		}

		#endregion

		#region Public methods

		public void HandleSignals(IEnumerable<Tuple<string, MT4TradeSignal>> signalCollection)
		{
		}

		#endregion
	}
}

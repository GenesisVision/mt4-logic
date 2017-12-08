namespace SignalService.Interfaces
{
	public interface IZeroMqServer:  ISignalProvider, IRequestable
	{
		void Start();
	}
}
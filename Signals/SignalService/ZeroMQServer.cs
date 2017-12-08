using System;
using System.IO;
using System.Text;
using System.Threading;
using SignalService.Interfaces;
using NetMQ;
using NetMQ.zmq;
using ProtoTypes;
using Poller = NetMQ.Poller;

namespace SignalService
{
	public class ZeroMqServer : IZeroMqServer, IDisposable
	{
		#region Fields

		/// <summary>
		/// Context of netMQ
		/// </summary>
		private readonly NetMQContext zeroMQContext;

		/// <summary>
		/// Zmq poller (for async request handling)
		/// </summary>
		private Poller poller;

		/// <summary>
		/// Router socket
		/// </summary>
		private NetMQSocket router;

		private bool disposed;

		#endregion

		#region Construction

		/// <summary>
		/// Default constructor
		/// Start server on 7021 port
		/// </summary>
		public ZeroMqServer(string address)
		{
			zeroMQContext = NetMQContext.Create();
			Address = "tcp://" + address;
		}

		/// <summary>
		/// Finalizer
		/// </summary>
		~ZeroMqServer()
		{
			Dispose();
		}

		#endregion

		#region Public methods

		public void Start()
		{
			SignalService.Logger.Info("Bind zeroMQ socket to address: {0}", Address);
			router = zeroMQContext.CreateSocket(ZmqSocketType.Router);
			router.Bind(Address);
			router.ReceiveReady += RouterOnReceiveReady;
			//poller = new Poller();
			//poller.AddSocket(router);

			//new Thread(() => poller.Start()).Start(); // TODO stop thread
			new Thread(PollerThread).Start();
		}

		private void PollerThread()
		{
			while (true)
			{
				try
				{
					if (poller == null || !poller.IsStarted)
					{
						SignalService.Logger.Info("Start NetMQ Poller");
						poller = new Poller();
						poller.AddSocket(router);
						poller.Start();
					}

				}
				catch (Exception e)
				{
					SignalService.Logger.Error("NetMQ Poller Thread Exception.\n{0}", e.StackTrace);
					if (poller != null)
					{
						poller.Stop();
						poller.Dispose();
					}
				}

			}
		}
		public void Stop()
		{
			poller.Stop();
			poller.Dispose();
			router.ReceiveReady -= RouterOnReceiveReady;
			router.Close();
		}

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			zeroMQContext.Dispose();
		}

		/// <summary>
		/// Request observer
		/// </summary>
		public void Request(Tuple<string, Request> request)
		{
			try
			{
				var data = request.Item2.Serialize();
				if (data == null)
					throw new InvalidDataException("Serialize error");
				var id = Encoding.UTF8.GetBytes(request.Item1);
				router.SendMore(id);
				router.Send(data);
			}
			catch (Exception ex)
			{
				SignalService.Logger.Error("Error: {0}", ex.Message);
			}
		}

		#endregion

		#region Events

		/// <summary>
		/// Incoming signals
		/// </summary>
		public event Action<Tuple<string, Signal>> Signals;

		#endregion

		#region private methods

		private void RouterOnReceiveReady(object sender, NetMQSocketEventArgs netMqSocketEventArgs)
		{
			try
			{
				var socket = netMqSocketEventArgs.Socket;
				var message = socket.ReceiveMessage();
				var clientId = Encoding.UTF8.GetString(message.First.Buffer);
				var mess = message.Last.Buffer;
				var signal = ProtoExtension.DeSerialize<Signal>(mess);
				if (signal != null)
				{
					if (signal.Type == SignalType.ConnectSignal)
					{
						SignalService.Logger.Info("Connect message from client {0} ", clientId);
						SendConnectedMessage(socket, clientId);
					}
					if (Signals != null)
						Signals((new Tuple<string, Signal>(clientId, signal)));
				}
			}
			catch (Exception ex)
			{
				SignalService.Logger.Error("Error: {0}", ex.Message);
			}
		}

		private void SendConnectedMessage(NetMQSocket socket, string clientId)
		{
			var connected = new Request { requestType = RequestType.Connected, destination = clientId };
			var data = connected.Serialize();
			var id = Encoding.UTF8.GetBytes(clientId);

			router.SendMore(id);
			router.Send(data);
			SignalService.Logger.Info("Connected message sended to client {0}", clientId);
		}

		#endregion

		#region Properties

		public string Address { get; private set; }

		#endregion
	}
}
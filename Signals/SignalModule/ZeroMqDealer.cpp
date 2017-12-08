#include <thread>
#include <mutex>
#include <iostream>

#include "ZeroMqDealer.h"
#include "proto\Signal.pb.h"

bool temp = false;

class ZeroMqDealer_pimpl // ()
{
	/// Construction
public:
	ZeroMqDealer_pimpl() :
		context(1)
		//, socket (context, ZMQ_DEALER)
		, isStarted(false)
	{
	}

	/// Public methods
public:
	/// Connect to router
	void Connect(std::string host, std::string port, std::string serverName)
	{
		if (socket != NULL)
			delete socket;
		socket = new zmq::socket_t(context, ZMQ_DEALER);
		socket->setsockopt(ZMQ_IDENTITY, serverName.c_str(), serverName.size());
		int linger = 0;
		socket->setsockopt(ZMQ_LINGER, &linger, sizeof(linger));
		auto address = std::string("tcp://" + host + ":" + port);
		socket->connect(address.c_str());
		isStarted = true;
		ProtoTypes::Signal signal;
		signal.set_type(ProtoTypes::ConnectSignal);
		signal.set_source(serverName);
		signal.set_content("Connect");
		auto connectMess = signal.SerializeAsString();
		Send(connectMess);
	}

	/// Close connection
	void Close()
	{
		isStarted = false;
		if (socket->connected())
		{
			socket->close();
		}
	}


	/// Loop of handling messages
	void Poll()
	{
		try
		{
			std::cout << "Poll started" << std::endl;
			queueHandlingThread = std::thread(std::bind(&ZeroMqDealer_pimpl::QueueLoop, this));
			zmq::message_t message;
			while (isStarted)
			{
				try
				{
					bool sendingRes = false;
					while (isStarted && !sendingQueue.IsEmpty())
					{
						auto mess = sendingQueue.Pop();
						if (isStarted)
						{
							socket->send(&mess[0], mess.size(), ZMQ_DONTWAIT);
						}
					}
					if (isStarted)
					{
						auto res = true;
						res = socket->recv(&message, ZMQ_DONTWAIT);
						if (res)
						{
							if (message.size() != 0)
							{
								std::string mess((char*)message.data(), message.size());
								receivedQueue.Push(mess);
							}
						}
						else
						{
							Sleep(1);
						}
					}
				}
				catch (std::exception &ex)
				{
					std::cout << "Exception in poll: " << ex.what();
				}
			}
			queueHandlingThread.join();
		}
		catch (std::exception &ex)
		{
			std::cout << "Exception in poll: " << ex.what();
		}
	}

	/// Subscribe on messages
	void Subscribe(std::function<void(std::string)> func)
	{
		messageHandler = func;
	}

	/// Send message (add to queue)
	void Send(std::string &mess)
	{
		sendingQueue.Push(mess);
	}

	/// Private methods
private:
	void QueueLoop()
	{
		while (isStarted)
		{
			if (!receivedQueue.IsEmpty())
			{
				auto mess = receivedQueue.Pop();
				if (messageHandler)
					messageHandler(mess);
			}
			else
				Sleep(1);
		}
	}

	/// Private fields
private:
	/// Zero mq context
	zmq::context_t context;
	/// Dealer socket
	zmq::socket_t *socket;
	/// Is started
	bool isStarted;

	ConcurrentQueue receivedQueue;
	ConcurrentQueue sendingQueue;

	/// Message handler
	std::function<void(std::string)> messageHandler;

	std::thread queueHandlingThread;
};

ZeroMqDealer::ZeroMqDealer() :
pimpl(new ZeroMqDealer_pimpl())
{
}

/// Connect to router
void ZeroMqDealer::Connect(std::string host, std::string port, std::string serverName)
{
	pimpl->Connect(host, port, serverName);
}

/// Close connection
void ZeroMqDealer::Close()
{
	pimpl->Close();
}

/// Loop of handling messages
void ZeroMqDealer::Poll()
{
	pimpl->Poll();
}

/// Subscribe on messages
void ZeroMqDealer::Subscribe(std::function<void(std::string)> func)
{
	pimpl->Subscribe(func);
}

/// Send message (add to queue)
void ZeroMqDealer::Send(std::string &mess)
{
	pimpl->Send(mess);
}


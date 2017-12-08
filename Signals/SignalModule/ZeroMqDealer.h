#ifndef _ZERO_MQ_DEALER_H
#define _ZERO_MQ_DEALER_H

#include "ConcurrentQueue.h"
#include "proto\Request.pb.h"
#include "include\zmq.hpp"
class ZeroMqDealer_pimpl;

/// Class-connection with signal router
class ZeroMqDealer
{
	/// Construction / destruction
public:
	/// Default constructor
	ZeroMqDealer();

	/// Public methods
public:
	/// Connect to router
	void Connect(std::string host, std::string port, std::string serverName);

	/// Close connection
	void Close();

	/// Loop of handling messages
	void Poll();

	/// Subscribe on messages
	void Subscribe(std::function<void(std::string)> func);

	/// Send message (add to queue)
	void Send(std::string &mess);

private:
	std::auto_ptr<ZeroMqDealer_pimpl> pimpl;
};

#endif //
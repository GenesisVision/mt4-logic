#ifndef _MESSAGE_HANDLER_H_
#define _MESSAGE_HANDLER_H_
#include "ProtocolStructs.h"

#include <functional>

class SignalModule_pimpl;

class SignalModule
{
/// Construction / destruction
public:
	SignalModule();

/// Public methods
public:

	/// Initialize connection settings
	void Init(std::string host, std::string port, std::string serverName);

	/// Start signal module
	void Start();

	/// Stop signal module
	void Stop();

	/// Handle raw message
	void HandleMessage(std::string mess);

	/// Subscribe on Order status request
	void SubscribeOnOrderStatusRequest(std::function<void(std::vector<int>&)> func);

	///Subscribe on Order status request
	void SubscribeOnExecuteSignal(std::function<void(ExecutionSignal)> func);

	/// Send orders status response
	void SendOrdersStatusResponse(OrdersStatusResponse& response);
	
	/// Send trade signal
	void SendTradeSignal(MT4TradeSignal &tradeSignal);

private:
	std::auto_ptr<SignalModule_pimpl> pimpl;
	
};


#endif //_MESSAGE_HANDLER_H_
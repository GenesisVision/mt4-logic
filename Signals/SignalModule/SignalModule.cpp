#include "proto\Request.pb.h"
#include "proto\RequestOrdersStatus.pb.h"
#include "proto\SignalOrdersStatus.pb.h"
#include "proto\Signal.pb.h"
#include "proto\SignalMT4Trade.pb.h"
#include "proto\RequestExecution.pb.h"

#include "SignalModule.h"
#include "ZeroMqDealer.h"

#include <iostream>
#include <thread>

class SignalModule_pimpl
{
/// Public methods
public:
	/// Initialize connection settings
	void Init(std::string host, std::string port, std::string serverName)
	{
		this->serverName = serverName;
		this->host = host;
		this->port = port;
	}
	/// Start signal module
	void Start()
	{
		try
		{
			if (isStarted)
				Stop();
			dealer.Connect(host, port, serverName);
			dealer.Subscribe(std::function<void(std::string)>(std::bind(&SignalModule_pimpl::HandleMessage, this, std::placeholders::_1)));
			isStarted = true;

			poller = std::thread(std::bind(&SignalModule_pimpl::PollerThread, this));
			//heartbeatThread = std::thread(std::bind(&SignalModule_pimpl::HeartbeatThread, this));
		}
		catch (std::exception &ex)
		{
			std::cout << "Error: " << ex.what();
		}
	}
	/// Stop signal module
	void Stop()
	{
		try
		{
			if (isStarted)
			{
				isStarted = false;

				dealer.Close();
				poller.join();
			}
			//heartbeatThread.join();
		}
		catch (std::exception &ex)
		{
			std::cout << "Error: " << ex.what();
		}
	}
	/// Handle raw message
	void HandleMessage(std::string mess)
	{
		ProtoTypes::Request request;
		if(!request.ParseFromString(mess))
		{
			std::cout << "Error deserialize messages" << std::endl;
		}
		switch(request.requesttype())
		{
		case ProtoTypes::RequestType::OrderStatusRequestType:
			{
				ProtoTypes::OrdersStatusRequest statusRequest;
				if(!statusRequest.ParseFromString(request.content()))
				{
					std::cout << "Error deserialize OrdersStatusRequest" << std::endl;
				}
				HandleOrderStatusRequest(statusRequest);
				break;
			}
		case ProtoTypes::RequestType::ExecutionRequestType:
			{
				ProtoTypes::ExecutionSignal executionSignal;
				if(!executionSignal.ParseFromString(request.content()))
				{
					std::cout << "Error deserialize ExecutionRequest" << std::endl;
				}
				ExecutionSignal signal = ProtoToExecutionSignal(executionSignal);
				HandleExecutionRequest(signal);
				break;
			}
			// Handling other messages put here
		default:
			break;
		}
	}
	///Subscribe on Order status request
	void SubscribeOnOrderStatusRequest(std::function<void(std::vector<int>&)> func)
	{
		statusRequestHandler = func;
	}
	///Subscribe on Order status request
	void SubscribeOnExecuteSignal(std::function<void(ExecutionSignal)> func)
	{
		executionSignalHandler = func;
	}
	/// Send orders status response
	void SendOrdersStatusResponse(OrdersStatusResponse& response)
	{
		ProtoTypes::OrdersStatusResponse proto = OrdersStatusResponseToProto(response);
		auto content = proto.SerializeAsString();
		SendSignal(ProtoTypes::SignalOrdersStatus, content);
	}
	/// Send trade signal
	void SendTradeSignal(MT4TradeSignal &tradeSignal)
	{
		ProtoTypes::MT4TradeSignal proto = MT4TradeSignalToProto(tradeSignal);
		auto content = proto.SerializeAsString();
		SendSignal(ProtoTypes::TradeSignal, content);
	}
///Private methods
private:

	ProtoTypes::MT4TradeSignal MT4TradeSignalToProto(MT4TradeSignal &tradeSignal)
	{
		ProtoTypes::MT4TradeSignal proto;
		proto.set_side(tradeSignal.Side == 
			TradeSide::Buy ? ProtoTypes::TradeSide::Buy : ProtoTypes::TradeSide::Sell);
		proto.set_actiontype(tradeSignal.ActionType == 
			ActionType::Open ? ProtoTypes::ActionType::Open : ProtoTypes::ActionType::Close);
		proto.set_datetime(tradeSignal.DateTime);
		proto.set_equity(tradeSignal.Equity);
		proto.set_balance(tradeSignal.Balance);
		proto.set_volume(tradeSignal.Volume);
		proto.set_symbol(tradeSignal.Symbol);
		if(tradeSignal.StopLoss != 0.0)
			proto.set_stoploss(tradeSignal.StopLoss);
		if(tradeSignal.TakeProfit != 0.0)
			proto.set_takeprofit(tradeSignal.TakeProfit);
		proto.set_login(tradeSignal.Login);
		proto.set_server(tradeSignal.Server);
		proto.set_orderid(tradeSignal.OrderID);
		proto.set_comment(tradeSignal.Comment);
		proto.set_profit(tradeSignal.Profit);
		proto.set_providercommission(tradeSignal.ProviderCommission);

		std::cout << "MT4TradeSignalToProto commission " << tradeSignal.ProviderCommission << std::endl;

		return proto;
	}

	ProtoTypes::OrdersStatusResponse OrdersStatusResponseToProto(OrdersStatusResponse& response)
	{		

		ProtoTypes::OrdersStatusResponse proto;

		for(int i = 0, n = response.OrdersStatus.size();  i < n; ++i)
		{
			auto ordersStatus = proto.add_ordersstatus();
			ordersStatus->set_login(response.OrdersStatus[i].Login);
			
			int size = response.OrdersStatus[i].Status.size();

			for (int j = 0; j < size; j++)
			{				
				auto orderStatus = ordersStatus->add_orderstatus();
				orderStatus->set_orderid(response.OrdersStatus[i].Status[j].OrderID);
				orderStatus->set_side(response.OrdersStatus[i].Status[j].Side == 
					TradeSide::Buy ? ProtoTypes::TradeSide::Buy : ProtoTypes::TradeSide::Sell);
				orderStatus->set_datetime(response.OrdersStatus[i].Status[j].DateTime);
				orderStatus->set_volume(response.OrdersStatus[i].Status[j].Volume);
				orderStatus->set_symbol(response.OrdersStatus[i].Status[j].Symbol);
				orderStatus->set_comment(response.OrdersStatus[i].Status[j].Comment);
				if(response.OrdersStatus[i].Status[j].StopLoss != 0.0)
					orderStatus->set_stoploss(response.OrdersStatus[i].Status[j].StopLoss);
				if(response.OrdersStatus[i].Status[j].TakeProfit != 0.0)
					orderStatus->set_takeprofit(response.OrdersStatus[i].Status[j].TakeProfit);
				
			}
		}

		for (int i = 0, n = response.OrdersStatus.size(); i < n; ++i)
		{
			int size = response.OrdersStatus[i].Status.size();

		}
		
		return proto;
	}

	ExecutionSignal ProtoToExecutionSignal(ProtoTypes::ExecutionSignal signal)
	{
		ExecutionSignal executionSignal;
		executionSignal.comment = signal.comment();
		for(int i = 0, n = signal.orders_size(); i < n; ++i)
		{
			ExecutionOrder order;
			order.Login = signal.orders(i).login();
			order.ActionType = 
				signal.orders(i).actiontype() == ProtoTypes::ActionType::Open ? ActionType::Open : ActionType::Close;
			order.TradeSide = signal.orders(i).side() == ProtoTypes::TradeSide::Buy ? TradeSide::Buy : TradeSide::Sell;
			order.Symbol = signal.orders(i).symbol();
			order.Volume = signal.orders(i).volume();
			order.Commission = signal.orders(i).commission();
			if(signal.orders(i).has_orderid())
			{
				order.OrderID = signal.orders(i).orderid();
			}
			executionSignal.Orders.push_back(order);
		}
		return executionSignal;
	}
	
	void PollerThread()
	{
		dealer.Poll();
	}

	void HeartbeatThread()
	{
		std::cout << "HeartbeatThread started" << std::endl;
		std::string mess("connect");
		while (isStarted)
		{
			SendSignal(ProtoTypes::SignalType::ConnectSignal, mess);
			Sleep(100);
		}
		std::cout << "HeartbeatThread finished" << std::endl;
	}

	void SendSignal(ProtoTypes::SignalType signalType, std::string &content)
	{
		ProtoTypes::Signal signal;
		signal.set_type(signalType);
		signal.set_source(serverName);
		signal.set_content(content);
		auto mess = signal.SerializeAsString();

		Send(mess);
	}

	void HandleOrderStatusRequest(ProtoTypes::OrdersStatusRequest request)
	{
		std::vector<int> logins;
		for(int i = 0, n = request.logins_size(); i < n; ++i)
		{
			int login = request.logins(i);
			logins.push_back(login);
		}
		if(statusRequestHandler)
			statusRequestHandler(logins);
	}

	void HandleExecutionRequest(ExecutionSignal executionSignal)
	{
		if(executionSignalHandler)
			executionSignalHandler(executionSignal);
	}

	void Send(std::string& mess)
	{
		dealer.Send(mess);
	}

/// Private fields
private:
	std::function<void(std::vector<int>&)> statusRequestHandler;
	std::function<void(ExecutionSignal)> executionSignalHandler;
	std::string serverName;
	std::string host;
	std::string port;
	ZeroMqDealer dealer;
	std::thread poller;
	std::thread heartbeatThread;
	bool isStarted = false;
};


SignalModule::SignalModule() : 
	pimpl(new SignalModule_pimpl())
{}
/// Initialize connection settings
void SignalModule::Init(std::string host, std::string port, std::string serverName)
{
	pimpl->Init(host, port, serverName);
}

/// Start signal module
void SignalModule::Start()
{
	pimpl->Start();
}

void SignalModule::Stop()
{
	pimpl->Stop();
}

/// Handle raw message
void SignalModule::HandleMessage(std::string mess)
{
	pimpl->HandleMessage(mess);
}

/// Subscribe on Order status request
void SignalModule::SubscribeOnOrderStatusRequest(std::function<void(std::vector<int>&)> func)
{
	pimpl->SubscribeOnOrderStatusRequest(func);
}

/// Subscribe on Order status request
void SignalModule::SubscribeOnExecuteSignal(std::function<void(ExecutionSignal)> func)
{
	pimpl->SubscribeOnExecuteSignal(func);
}



/// Send orders status response
void SignalModule::SendOrdersStatusResponse(OrdersStatusResponse& response)
{
	pimpl->SendOrdersStatusResponse(response);
}

/// Send trade signal
void SignalModule::SendTradeSignal(MT4TradeSignal &tradeSignal)
{
	pimpl->SendTradeSignal(tradeSignal);
}






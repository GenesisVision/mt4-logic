//+------------------------------------------------------------------+
//|                                 4 Levels Agent Commission plugin |
//|                    Copyright © 2009-2010, Tools For Brokers Inc. |
//|                                     http://www.tools4brokers.com |
//+------------------------------------------------------------------+
#include "stdafx.h"
#include "Processor.h"
#include "Logger.h"



std::vector<std::string> split(std::string line, char delim)
{
	std::vector<std::string> items;
	
	std::string item;
	std::stringstream ssItems(line);		
	while(std::getline(ssItems, item, delim)) items.push_back( item.c_str() );	
	return items;
}


CProcessor::CProcessor(void)
{
	started = false;
}

CProcessor::~CProcessor(void)
{
}

bool CProcessor::SetServerInterface(MT4Server *server) 
{
	if(server != NULL)
	{
		this->server = server;
		return true;
	} else
	{	
		return false;
	}
}

void CProcessor::Clear()
{
	std::cout << "Start clearing" << std::endl;
	module.Stop();
	started = false;
	std::cout << "Cleared" << std::endl;
}

void CProcessor::Initialize(std::string host, std::string port, std::string serverName, bool debug, bool autoExecution)
{
	if(server == NULL) 
	{
		return;
	}

	if(!started)
	{
		this->serverName = serverName;
		module.Init(host, port, serverName);
		module.Start();			
		module.SubscribeOnOrderStatusRequest(std::function<void(std::vector<int>)>(std::bind(&CProcessor::OnTradesRequest, this, std::placeholders::_1)));
		module.SubscribeOnExecuteSignal(std::function<void(ExecutionSignal)>(std::bind(&CProcessor::OnExecuteSignalRequest, this, std::placeholders::_1)));
		started = true;
	}

	cs.Lock();
	this->autoExecution = autoExecution;
	debugMode = debug;
	sendLogMessage("Plugin initialized", CmdOK);
	cs.Unlock();
}

void CProcessor::addExecutedCommand(OpenOrderSignal signal, int request_id)
{
	openRequestsLock.Lock();
	openRequests.insert(std::make_pair(request_id, signal));
	openRequestsLock.Unlock();
}

void CProcessor::addExecutedCommand(CloseOrderSignal signal, int request_id)
{
	closeRequestsLock.Lock();
	closeRequests.insert(std::make_pair(request_id, signal));
	closeRequestsLock.Unlock();
}

void CProcessor::OnExecuteSignalRequest(ExecutionSignal signal)
{
	char message[256];
	sprintf_s(message, 256, "OnExecuteSignalRequest, orders: %d", signal.Orders.size());
	sendLogMessage(message, CmdOK);
	for(int i = 0; i < signal.Orders.size(); i++)
	{
		auto order = signal.Orders[i];
		switch(order.ActionType)
		{
		case ActionType::Open:
			{
				char orderinfo[256];
				sprintf_s(orderinfo, 256, "OnExecuteSignalRequest, comm: %0.2f", order.Commission);
				sendLogMessage(orderinfo, CmdOK);

				//char comment[32];
				//sprintf_s(comment, 32, "Signal_%d_%d", signal.InitiatorTradingAccountId, signal.InitiatorOrderId);
								 
				auto request = OpenOrderSignal(order.Login, order.Symbol, order.Volume, 
					order.TradeSide == TradeSide::Buy ? OP_BUY : OP_SELL, signal.comment, server, order.Commission);
				auto res = request.Run(autoExecution);
				if(res == 0)
					sendLogMessage("No request added", CmdOK);
				else
				{
					addExecutedCommand(request, res);
				}
			}
			break;
		case ActionType::Close:
			{
				//CloseOrderSignal(order.OrderID, server).Run();
				auto request = CloseOrderSignal(order.OrderID, server);
				auto res = request.Run(autoExecution);
				if(res == 0)
					sendLogMessage("No request added", CmdOK);
				else
				{
					addExecutedCommand(request, res);
				}
			}
			break;
		default:
			sendLogMessage("Invalid action type", CmdErr);
		}
	}
}

void CProcessor::sendOpenSignal(UserInfo &ui, TradeRecord *trade)
{
	if(trade->cmd > OP_SELL) return;

	double equity, margin, free_margin;
	server->TradesMarginInfo(&ui, &margin, &free_margin, &equity);
	
	MT4TradeSignal signal;
	signal.Side = trade->cmd == OP_BUY ? TradeSide::Buy : TradeSide::Sell;
	signal.ActionType = ActionType::Open;
	signal.DateTime = trade->open_time;
	signal.Equity = equity;
	signal.Balance = ui.balance;
	signal.Volume = 0.01 * trade->volume;
	signal.Symbol = trade->symbol;
	signal.Login = trade->login;
	signal.Server = serverName;
	signal.StopLoss = trade->sl;
	signal.TakeProfit = trade->tp;
	signal.OrderID = trade->order;
	signal.Comment = trade->comment;
	signal.ProviderCommission = trade->conv_rates[1] * server->TradesCalcRates(ui.group, ui.grp.currency, "USD");

	char orderinfo[256];
	sprintf_s(orderinfo, 256, "sendOpenSignal, comm: %0.2f", signal.ProviderCommission);
	sendLogMessage(orderinfo, CmdOK);

	module.SendTradeSignal(signal);
}

void CProcessor::sendCloseSignal(UserInfo &ui, TradeRecord *trade)
{
	if(trade->cmd > OP_SELL) return;
	
	double equity, margin, free_margin;
	server->TradesMarginInfo(&ui, &margin, &free_margin, &equity);	

	MT4TradeSignal signal;
	signal.Side = trade->cmd == OP_BUY ? TradeSide::Buy : TradeSide::Sell;
	signal.ActionType = ActionType::Close;	

	signal.OrderID = trade->order;
	signal.DateTime = trade->close_time;
	signal.Equity = equity; 
	signal.Balance = ui.balance;
	signal.Volume = 0.01 * trade->volume;
	signal.Symbol = trade->symbol;
	signal.Login = trade->login;
	signal.Server = serverName;
	signal.StopLoss = trade->sl;
	signal.TakeProfit = trade->tp;
	signal.Comment = trade->comment;
	signal.Profit = trade->profit;


	module.SendTradeSignal(signal);
}
	

void CProcessor::OnNewTrade(UserInfo ui, TradeRecord *trade, int mode)
{
	if(mode == OPEN_RESTORE) return;

	sendOpenSignal(ui, trade);
}

void CProcessor::OnTradeUpdate(UserInfo ui, TradeRecord *trade, int mode)
{
	switch(mode)
	{
		case UPDATE_ACTIVATE:	
				sendOpenSignal(ui, trade);
		break;
		case UPDATE_CLOSE:	
		case UPDATE_DELETE:	
				sendCloseSignal(ui, trade);
		break;		
	}
}
	
void CProcessor::OnTradesRequest(std::vector<int> logins)
{
	OrdersStatusResponse responce;

	for(int i = 0; i < logins.size(); i++)
	{
		
		AccountOrdersStatus status;
		status.Login = logins[i];

		std::vector<TradeRecord> orders = LoadOpenedOrders(logins[i]);
		for(int j = 0; j < orders.size(); j++)
		{
			OrderStatus order;
			order.DateTime = orders[j].close_time;
			order.Volume = 0.01 * orders[j].volume;
			order.Symbol = orders[j].symbol;
			order.StopLoss = orders[j].sl;
			order.TakeProfit = orders[j].tp;
			order.OrderID = orders[j].order;
			order.Side = orders[j].cmd == OP_BUY ? TradeSide::Buy : TradeSide::Sell;
			order.Comment = orders[j].comment;
			status.Status.push_back(order);
		}
		responce.OrdersStatus.push_back(status);
	}

	module.SendOrdersStatusResponse(responce);
}

std::vector<TradeRecord> CProcessor::LoadOpenedOrders(int login)
{
	std::vector<TradeRecord> opened_trades;

	UserInfo ui = BaseExecutionSignal::UserInfoLoad(login, server);
	int total = 0;
	TradeRecord* trades = server->OrdersGetOpen(&ui, &total);

	for(int i = 0; i < total; i++)
	{
		opened_trades.push_back(trades[i]);
	}

	HEAP_FREE(trades);
	return opened_trades;
}

void CProcessor::sendLogMessage(std::string text, int type) 
{
	LogMessage(text, type, server);
}

int  CProcessor::OnDealerConfirm(const int id, const UserInfo *us,double *prices)
{
	HandleDealerAnswer(id, prices[0], prices[1], RequestResultType::Confirm);	
	return TRUE;
}

int  CProcessor::OnDealerRequote(const int id,const UserInfo *us,double *prices,const int in_stream)
{
	//HandleDealerAnswer(id, prices[0], prices[1], RequestResultType::Requote);
	return TRUE;
}

int  CProcessor::OnDealerReset(const int id,const UserInfo *us,const char flag)
{
	//HandleDealerAnswer(id, 0.0, 0.0, RequestResultType::Reset);
	return TRUE;
}

void CProcessor::HandleDealerAnswer(int id, double bid, double ask, RequestResultType type)
{
	DWORD thread;	
	RequestResult *r = new RequestResult();
	r->ask = ask;
	r->bid = bid;
	r->id = id;
	r->type = type;
	r->pParam = this;
	CreateThread(NULL, 0, DealerAnswer, r, 0, &thread);
}

void CProcessor::OnDealerAnswer(RequestResult result)
{
	sendLogMessage("new dealer confirm hook", CmdOK);
	openRequestsLock.Lock();
	if(openRequests.find(result.id) != openRequests.end() )
	{
		openRequests[result.id].Execute(result.bid, result.ask);
		openRequests.erase(result.id);
	}
	openRequestsLock.Unlock();

	closeRequestsLock.Lock();
	if(closeRequests.find(result.id) != closeRequests.end() )
	{
		closeRequests[result.id].Execute(result.bid, result.ask);
		closeRequests.erase(result.id);
	}
	closeRequestsLock.Unlock();
}

DWORD WINAPI CProcessor::DealerAnswer(LPVOID pParam)
{
	RequestResult *result = (RequestResult*)pParam;
	auto self = (CProcessor*)result->pParam;
	self->OnDealerAnswer(*result);
	delete result;
	return 0;
}

CProcessor processor;

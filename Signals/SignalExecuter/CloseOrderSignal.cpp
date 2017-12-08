#include "stdafx.h"
#include "CloseOrderSignal.h"
#include "Logger.h"

CloseOrderSignal::CloseOrderSignal(int ticket, MT4Server* server) 
{
	close_price = (trade.cmd == OP_BUY ? currentBid : currentAsk);
	server->OrdersGet(ticket, &trade);
	PrepeareData(trade.login, trade.symbol, server);
}

CloseOrderSignal::CloseOrderSignal()
{

}

CloseOrderSignal::~CloseOrderSignal(void)
{

}

bool CloseOrderSignal::CheckParametres()
{	
	TradeTransInfo trans = {0};

	if( trade.order <= 0 || trade.volume <= 0 || server == NULL || trade.login == 0) 
	{		

		char message[256];
		sprintf_s(message, 256, "Invalid order data. Login: %i, Order: %i, Volume: %i", trade.login, trade.order, trade.volume);
		LogMessage(message, CmdErr, server);
		return false;
	}

	if(trade.close_time != 0)
	{	
		LogMessage("Invalid close time", CmdErr, server);
		return false;
	}
	
	trans.order = trade.order;
	trans.volume = trade.volume;
	trans.price = close_price;

	if(server->TradesCheckTickSize(close_price, &symbol) == FALSE)
	{	
		LogMessage("Invalid tick size", CmdErr, server);
		return false;
	}

	if(server->TradesCheckSecurity(&symbol, &ui.grp)!=RET_OK)
	{	
		LogMessage("Invalid symbol for current group", CmdErr, server);
		return false;
	}
	//--- check volume
	if(server->TradesCheckVolume(&trans, &symbol, &ui.grp, TRUE) != RET_OK)
	{	
		LogMessage("Invalid volume for group", CmdErr, server);
		return false;
	}
	//--- check stops
	if(server->TradesCheckFreezed(&symbol, &ui.grp, &trade) != RET_OK)
	{		
		LogMessage("Position freezed", CmdErr, server);
		return false;
	}

	return true;
}

bool CloseOrderSignal::Execute(double bid, double ask)
{
	close_price = (trade.cmd == OP_BUY ? bid : ask);

	TradeTransInfo trans = {0};
	trans.order = trade.order;
	trans.volume = trade.volume;
	trans.price = close_price;
	COPY_STR(trans.comment, trade.comment);
	
	if(server->OrdersClose(&trans, &ui) == FALSE)
	{
		return false;
	}
	
	return false;
}

RequestInfo CloseOrderSignal::GenerateRequest()
{
	return RequestInfo();
}
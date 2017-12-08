#include "stdafx.h"
#include "OpenOrderSignal.h"
#include "Logger.h"

OpenOrderSignal::OpenOrderSignal()
{

}

OpenOrderSignal::OpenOrderSignal(int login, std::string symbol, double volume, int cmd, std::string comment, MT4Server* server, double commission)
{
	std::ostringstream strs2;
	strs2 << commission;
	std::string str2 = strs2.str();
	LogMessage(str2, CmdOK, server);

	PrepeareData(login, symbol, server);

	if(volume < 0) 
	{
		volume *= - ui.balance;
	}

	this->volume = 100 * volume;

	this->provider_commission = commission;

	this->cmd = cmd;
	this->comment = comment;

	open_price = (cmd == OP_SELL ? currentBid : currentAsk);

	CalculateVolume();

#ifdef DEBUG
	char message[256];
	sprintf_s(message, 256, "OpenOrderSignal. InputVolume: %i, Volume: %i", volume, this->volume);
	LogMessage(message, CmdOK, server);
#endif
}
void OpenOrderSignal::CalculateVolume()
{
	int min_lot = ui.grp.secgroups[symbol.type].lot_min;
	int step = ui.grp.secgroups[symbol.type].lot_step;
	volume = volume - volume % step;
	if (volume < min_lot)
	{
		volume = 0;
	}
}

OpenOrderSignal::~OpenOrderSignal(void)
{
}

bool OpenOrderSignal::CheckParametres()
{
	if( volume <= 0 || server == NULL || ui.login == 0) 
	{		
		char message[256];
		sprintf_s(message, 256, "Invalid order data. Login: %i, Volume: %i", ui.login, volume);
		LogMessage(message, CmdErr, server);
		return false;
	}
	
	if(cmd != OP_BUY && cmd != OP_SELL)
	{
		LogMessage("Invalid order type", CmdErr, server);
		return false;
	}

	
	double profit=0, margin=0, freemargin=0, prevmargin=0;

	//Make all checks here.
	TradeTransInfo trans = {0};
	
	if(symbol.long_only != FALSE && cmd==OP_SELL)
	{		
		LogMessage("Long only avaliable", CmdErr, server);
		return false;
	}
	//--- check close only
	if(symbol.trade==TRADE_CLOSE)
	{		
		LogMessage("Close only avaliable", CmdErr, server);
		return false;
	}
	//--- prepare transaction for checks
	trans.cmd   =cmd;
	trans.volume = volume;
	trans.price = open_price;
	trans.sl = 0.0;
	trans.tp = 0.0;
	trans.expiration = 0;
	
	COPY_STR(trans.symbol, symbol.symbol);
	//--- check tick size
	if(server->TradesCheckTickSize(open_price, &symbol)==FALSE)
	{		
		LogMessage("Invalid tick size", CmdErr, server);
		return false;
	}
	//--- check secutiry
	if(server->TradesCheckSecurity(&symbol, &ui.grp)!=RET_OK)
	{		
		LogMessage("Invalid symbol for current group", CmdErr, server);
		return false;
	}
	//--- check volume
	if(server->TradesCheckVolume(&trans,&symbol,&ui.grp,TRUE)!=RET_OK)
	{		
		LogMessage("Invalid volume for group", CmdErr, server);
		return false;
	}
	//--- check stops
	if(server->TradesCheckStops(&trans,&symbol,&ui.grp,NULL)!=RET_OK)
	{	
		LogMessage("Invalid SL\TP", CmdErr, server);
		return false;
	}
	//--- check margin
	margin = server->TradesMarginCheck(&ui, &trans, &profit, &freemargin, &prevmargin);
	if((freemargin+ui.grp.credit)<0 && (symbol.margin_hedged_strong!=FALSE || prevmargin<=margin))
	{		
		LogMessage("Not enough money", CmdErr, server);
		return false;
	}

	return true;

}

bool OpenOrderSignal::Execute(double bid, double ask)
{
	open_price = (cmd == OP_SELL ? bid : ask);

	TradeTransInfo trans = { 0 };
	trans.cmd = cmd;
	trans.volume = volume;
	trans.price = open_price;
	trans.sl = 0.0;
	trans.tp = 0.0;

	COPY_STR(trans.symbol, symbol.symbol);
	COPY_STR(trans.comment, comment.c_str());

	//
	//int order = server->OrdersAdd(&trade, &ui, &symbol);
	/*
	int order =	server->OrdersOpen(&trans, &ui);
	if(order == 0)
	{
	LogMessage("MT4 was not able to create order", CmdErr, server);
	return false;
	}*/

	TradeRecord trade = { 0 };
	trade.login = ui.login;
	trade.cmd = cmd;
	trade.open_price = open_price;
	trade.volume = volume;
	
	

	trade.close_price = (cmd == OP_BUY ? bid : ask);
	trade.open_time = server->TradeTime();
	trade.storage = symbol.spread;
	trade.digits = symbol.digits;
	COPY_STR(trade.comment, comment.c_str());
	COPY_STR(trade.symbol, symbol.symbol);

	double signalCommission = GetTradingCommission(volume, provider_commission);

	if (strstr(trade.comment, "Sub") != NULL)
	{
		//server->TradesCommission(&trade, ui.group, &symbol);
		
		//trade.commission += signalCommission;
		
		std::ostringstream strs;
		strs << signalCommission;
		std::string str = strs.str();

		LogMessage(str, CmdOK, server);
		//trade.reserved[0] = signalCommission;
	}

	trade.conv_rates[1] = signalCommission;
	int order = server->OrdersAdd(&trade, &ui, &symbol);
	if (order == 0)
	{
		LogMessage("MT4 was not able to create order", CmdErr, server);
		return false;
	}
	else
	{		
		server->OrdersGet(order, &trade);		
		trade.commission -= signalCommission;
		trade.conv_rates[1] = 0;
		server->OrdersUpdate(&trade, &ui, UPDATE_NORMAL);		
	}
	//here we need to send commission notification
	//signalCommission * server->TradesCalcRates(ui.group, ui.grp.currency, "USD");

	return true;
}

double OpenOrderSignal::GetTradingCommission(int volume, double provider_commission)
{
	if (strstr(symbol.symbol, "bo") != NULL || strstr(symbol.symbol, "bin") != NULL)
	{
		//for binaries we take $1 dollar of commission for each 10 dollars of bid. volume = bid * 100.
		//return (0.001 * volume) * server->TradesCalcRates(ui.group, "USD", ui.grp.currency);
		return 0.0;
	}
	else
	{
		std::string sym = symbol.symbol;
		std::string cur = sym.substr(3, 3);
		double rate = server->TradesCalcRates(ui.group, cur.c_str(), ui.grp.currency);

		char info[256];
		sprintf_s(info, 25, "%s %s %0.4f", cur.c_str(), ui.grp.currency, rate);

		LogMessage(info, CmdOK, server);

		if (rate <= 0)
		{
			rate = server->TradesCalcRates(ui.group, "USD", ui.grp.currency);
		}

		if (symbol.digits == 3 || symbol.digits == 5)
		{
			rate *= 10;
		}

		double point_price_per_lot = symbol.contract_size * symbol.point * rate;
		
		return provider_commission * point_price_per_lot * (0.01 * volume);
	}
}

RequestInfo OpenOrderSignal::GenerateRequest()
{
	RequestInfo request = {0};

	request.login = ui.login;		
	request.balance = ui.balance;
	request.credit = ui.credit;	
	COPY_STR(request.group, ui.group);
	request.prices[0] = currentBid;
	request.prices[1] = currentAsk;	
	
	request.trade.cmd = cmd;
	request.trade.crc = 999;	
	request.trade.expiration = 0;
	request.trade.ie_deviation = 1.0;
	request.trade.orderby = ui.login;
	request.trade.price = open_price;
	request.trade.sl = 0.0;
	request.trade.tp = 0.0;
	request.trade.volume = volume;
	COPY_STR(request.trade.symbol, symbol.symbol);
	COPY_STR(request.trade.comment, comment.c_str());
	
	request.trade.type = TT_ORDER_MK_OPEN;	

	request.id = 0;
	request.manager = 0;
	request.status = DC_REQUEST;
	request.time = GetTickCount();	

	if(RET_OK != CallTradeTransactionForAllPlugins(&request.trade, &ui, &request.id))
	{
		LogMessage("CallTradeTransactionForAllPlugins failed", CmdErr, server);
		request.login = 0;
	}

	return request;
}
#pragma once 
#include "baseexecutionsignal.h"

class CloseOrderSignal :
	public BaseExecutionSignal
{
public:
	CloseOrderSignal(int ticket, MT4Server* server);
	CloseOrderSignal();
	~CloseOrderSignal(void);
	virtual bool Execute(double bid, double ask);
protected:
	double close_price;

	TradeRecord trade;
	virtual bool CheckParametres();
	
	virtual RequestInfo GenerateRequest();
};


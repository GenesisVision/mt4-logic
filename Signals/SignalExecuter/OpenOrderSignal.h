#pragma once
#include "mt4part\MT4ServerEmulator.h"
#include "BaseExecutionSignal.h"

class OpenOrderSignal : public BaseExecutionSignal
{
public:
	OpenOrderSignal();
	OpenOrderSignal(int login, std::string symbol, double volume, int cmd, std::string comment, MT4Server* server, double commission);
	~OpenOrderSignal(void);
	virtual bool Execute(double bid, double ask);
private:	
	double GetTradingCommission(int volume, double provider_commission);
	double equity, margin, free_margin, profit, prevmargin, open_price, provider_commission;
	void CalculateVolume();
	std::string comment;

	int cmd;
	int volume;

	virtual bool CheckParametres();		
	virtual RequestInfo GenerateRequest();
};



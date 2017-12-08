#pragma once
#include <map>
#include <string>
#include "common/Sync.h"
#include "MT4ServerEmulator.h"
#include "CloseOrderSignal.h"
#include "OpenOrderSignal.h"
#include <string>
#include <iostream>
#include "SignalModule.h"

#define PLUGIN_NAME "Executer"

std::vector<std::string> split(std::string line, char delim);

struct SConfigData 
{
	std::vector<std::string> groups;
	std::vector<std::string> symbols;
	double value;

	SConfigData(std::string groups, std::string symbols, double value) 
	{
		this->groups = split(groups, ',');
		this->symbols = split(symbols, ',');
		this->value = value;
	}
};

enum RequestResultType { Confirm = 0, Requote, Reset };

struct RequestResult
{
	RequestResultType type;
	double bid;
	double ask;
	int id;
	LPVOID pParam;
};

class CProcessor
{
public:
	CProcessor(void);
	~CProcessor(void);

	void Initialize(std::string host, std::string port, std::string serverName, bool debug, bool autoExecution);	
	void OnNewTrade(UserInfo ui, TradeRecord *trade, int mode);
	void OnTradeUpdate(UserInfo ui, TradeRecord *trade, int mode);
	void OnTradesRequest(std::vector<int> logins);
	void OnExecuteSignalRequest(ExecutionSignal signal);
	void Clear();
	bool SetServerInterface(MT4Server *server);	

	int  OnDealerConfirm(const int id,const UserInfo *us,double *prices);
	int  OnDealerRequote(const int id,const UserInfo *us,double *prices,const int in_stream);
	int  OnDealerReset(const int id,const UserInfo *us,const char flag);

private:	
	std::vector<TradeRecord> CProcessor::LoadOpenedOrders(int login);
	void sendLogMessage(std::string text, int type);
	void sendOpenSignal(UserInfo &ui, TradeRecord *trade);
	void sendCloseSignal(UserInfo &ui, TradeRecord *trade);
	void addExecutedCommand(OpenOrderSignal signal, int request_id);
	void addExecutedCommand(CloseOrderSignal signal, int request_id);
	void HandleDealerAnswer(int id, double bid, double ask, RequestResultType type);

	void OnDealerAnswer(RequestResult result);
	static DWORD WINAPI DealerAnswer(LPVOID pParam);

	std::string serverName;
	MT4Server *server;	
	bool debugMode;
	CSync cs;

	CSync openRequestsLock;
	std::map<int, OpenOrderSignal> openRequests;

	CSync closeRequestsLock;
	std::map<int, CloseOrderSignal> closeRequests;

	SignalModule module;
	volatile bool started, autoExecution;
};

extern CProcessor processor;
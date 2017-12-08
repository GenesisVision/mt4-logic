#pragma once
#include "mt4part\MT4ServerEmulator.h"
#include <set>

typedef int (APIENTRY *MtSrvTradeTransactionCall)(TradeTransInfo* trans, const UserInfo *user, int *request_id);

struct ConPlugin
  {
   char              file[256];                    // plugin file name
   PluginInfo        info;                         // plugin description
   int               enabled;                      // plugin enabled/disabled
   int               configurable;                 // is plugin configurable
   int               manager_access;               // plugin can be accessed from manager terminal
   int               reserved[62];                 // reserved
  };

class BaseExecutionSignal
{
public:
	BaseExecutionSignal();
	~BaseExecutionSignal(void);	

	static UserInfo UserInfoLoad(int login, MT4Server* server);
	int Run(bool autoExecution);	
	virtual bool Execute(double bid, double ask) = 0;
protected:	

	double currentBid, currentAsk;
	ConSymbol symbol;
	UserInfo ui;	
	MT4Server* server;
	
	void PrepeareData(int login, std::string symbol, MT4Server* server);

	virtual bool CheckParametres() = 0;
	
	virtual RequestInfo GenerateRequest() = 0;

	int CallTradeTransactionForAllPlugins(TradeTransInfo* trans, const UserInfo *user, int *request_id);
	int CallTradeTransaction(std::string plugin, TradeTransInfo* trans, const UserInfo *user, int *request_id);

private:
	
	std::set<std::string> LoadPlugins();
	void UserInfoLoad(int login);
	void SymbolLoad(std::string symbol);
	void PricesLoad(std::string symbol);
};


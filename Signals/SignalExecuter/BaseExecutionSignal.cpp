#include "stdafx.h"
#include "BaseExecutionSignal.h"
#include <iostream>
#include "Logger.h"


BaseExecutionSignal::BaseExecutionSignal()
{

}

void BaseExecutionSignal::PrepeareData(int login, std::string symbol, MT4Server* server)
{
	this->server = server;
	UserInfoLoad(login);
	SymbolLoad(symbol);
	PricesLoad(symbol);
}

BaseExecutionSignal::~BaseExecutionSignal(void)
{

}

UserInfo BaseExecutionSignal::UserInfoLoad(int login, MT4Server* server)
{
	UserInfo ui;
	UserRecord ur;
	server->ClientsUserInfo(login, &ur);

	//clean up the resulting struct
	ZeroMemory(&ui, sizeof(UserInfo));
	//Retrieve the full user information
	//Fill some data
	ui.login				= ur.login;
	ui.enable				= ur.enable;
	ui.enable_read_only	= ur.enable_read_only;
	ui.leverage			= ur.leverage;
	ui.agent_account		= ur.agent_account;
	ui.credit				= ur.credit;
	ui.balance				= ur.balance;
	ui.prevbalance			= ur.prevbalance;
	//Fill the group name
	COPY_STR(ui.group, ur.group);
	server->GroupsGet(ui.group, &ui.grp);

	return ui;
}

void BaseExecutionSignal::SymbolLoad(std::string symbol)
{
	server->SymbolsGet(symbol.c_str(), &this->symbol);
}

void BaseExecutionSignal::PricesLoad(std::string symbol)
{
	double prices[2];
	if(server->HistoryPricesGroup(symbol.c_str(), &ui.grp, prices) == RET_OK)
	{		
		currentBid = prices[0];
		currentAsk = prices[1];
	}
}

void BaseExecutionSignal::UserInfoLoad(int login)
{	
	ui = BaseExecutionSignal::UserInfoLoad(login, server);	
}

int BaseExecutionSignal::Run(bool autoExecution)
{
	LogMessage("Starting order operation", CmdOK, server);	
	if(!CheckParametres())
	{
		LogMessage("CheckParametres fAiled", CmdErr, server);
		return 0;
	}
	
	if(autoExecution)
	{
		if(!Execute(currentBid, currentAsk))
		{
			LogMessage("Execute failed", CmdOK, server);
		}		
	}
	else
	{
		RequestInfo request = GenerateRequest();
	
		if(server->RequestsAdd(&request, FALSE, &request.id) != RET_TRADE_ACCEPTED || request.login == 0)
		{
			LogMessage("Execute failed", CmdErr, server);
			return 0;
		}

		LogMessage("Operation success", CmdOK, server);
		return request.id;
	}	
	return 0;
}

int BaseExecutionSignal::CallTradeTransactionForAllPlugins(TradeTransInfo* trans, const UserInfo *user, int *request_id)
{
	int result = RET_OK;
	std::set<std::string> plugins = LoadPlugins();
	for(std::set<std::string>::iterator p = plugins.begin(); p != plugins.end(); p++)
	{
		std::string fileName = path;//"C:\\MetaTraderServer4\\plugins\\";
		fileName.append("\\plugins\\");
		fileName.append(*p);
		int localResult = CallTradeTransaction(fileName, trans, user, request_id);
		if(localResult != RET_OK)
		{
			return localResult;
		}
	}
	return result;
}

int BaseExecutionSignal::CallTradeTransaction(std::string plugin, TradeTransInfo* trans, const UserInfo *user, int *request_id)
{
	HMODULE dll = LoadLibrary(plugin.c_str());
	if(dll != NULL)
	{
		int ret = RET_OK;
		MtSrvTradeTransactionCall function = (MtSrvTradeTransactionCall)GetProcAddress(dll, "MtSrvTradeTransaction");
		if(NULL != function)
		{
			//SendLogMessage(CmdOK, "Calling MtSrvTradeTransaction for %s", plugin.c_str());
			ret = function(trans, user, request_id);
		}
		FreeLibrary(dll);
		return ret;
	}
	

	return RET_OK;
}

std::set<std::string> BaseExecutionSignal::LoadPlugins()
{
	//SendLogMessage(CmdOK, "Loading plugins");
	static std::set<std::string> plugins;
	if(plugins.size() == 0)
	{
		std::string fileName = path; 
		fileName.append("\\config\\plugins.ini");
		//"C:\\MetaTraderServer4\\config\\plugins.ini";
		FILE *configFile;
		errno_t err;
		err = fopen_s(&configFile, fileName.c_str(),"r+b");
		if(err == 0)
		{
			fseek(configFile, 4, SEEK_SET);
			int iterations = 0;
			while(feof(configFile) == 0 && iterations++ < 100)
			{
				ConPlugin plugin;		
				fread(&plugin, sizeof(ConPlugin), 1, configFile);
				if(plugin.enabled == 1 && strstr(plugin.file, ".dll") != NULL && strstr(plugin.file, "gurucollector") == NULL  && strstr(plugin.file, "datacollector") == NULL)
				{
					//SendLogMessage(CmdOK, "Plugin loaded %s", plugin.file);
					plugins.insert(plugin.file);
				}
			}
			fclose(configFile);
		}
	}
	//SendLogMessage(CmdOK, "Plugins loaded %d", plugins.size());
	return plugins;
}
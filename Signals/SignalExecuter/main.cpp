//+------------------------------------------------------------------+
//|                                 4 Levels Agent Commission plugin |
//|                    Copyright © 2009-2010, Tools For Brokers Inc. |
//|                                     http://www.tools4brokers.com |
//+------------------------------------------------------------------+
#include "stdafx.h"
#include "mt4part/Configuration.h"
#include "mt4part/Processor.h"
#include <iostream>
#include "mt4part/MT4ServerEmulator.h"

MT4Server* PluginServer = NULL;
void UpdatePluginConifg();

//#ifndef _DEBUG
BOOL APIENTRY DllMain(HANDLE hModule,DWORD  ul_reason_for_call,LPVOID /*lpReserved*/)
{
	char tmp[256], *cp;	
	switch(ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		GetModuleFileName( (HMODULE)hModule, tmp, sizeof(tmp) - 1);		
		GetModuleFileName( (HMODULE)hModule, tmp, sizeof(tmp) - 1);		

		if( (cp = strrchr(tmp,'.') ) != NULL) *cp = 0;			

		path = tmp;
		path = path.substr(0, path.find_last_of('\\'));
		path = path.substr(0, path.find_last_of('\\'));

		strcat_s(tmp, 256, ".ini");			
		ExtConfig.Load(tmp);
		break;
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
		break;
	case DLL_PROCESS_DETACH:
		break;
	}	
	return(TRUE);
}
//#endif

int APIENTRY MtSrvStartup(CServerInterface *server)
{

	if(server == NULL) 
	{
		return(FALSE);
	}

	if( server->Version() != ServerApiVersion) 
	{
		return(FALSE);
	}	

	processor.SetServerInterface(server);
	UpdatePluginConifg();	
	
	return(TRUE);
}

void APIENTRY        MtSrvCleanup(void)
{
	processor.Clear();
}

void APIENTRY MtSrvAbout(PluginInfo *info)
{	
	PluginInfo ExtPluginInfo = {"Signals Executer", 100, " Broekr", {0} };
	if( info != NULL ) memcpy(info, &ExtPluginInfo, sizeof(PluginInfo) );	
}

void UpdatePluginConifg() 
{ 
 int debugMode;
 ExtConfig.GetInteger(0, "debugMode", &debugMode, "1");

 char host[64];
 ExtConfig.GetString(1, "host", host, 64, "127.0.0.1");

 char port[64];
 ExtConfig.GetString(2, "port", port, 64, "2222");

 char name[64];
 ExtConfig.GetString(3, "name", name, 64, "Server");
 
 int autoExecution;
 ExtConfig.GetInteger(0, "autoExecution", &autoExecution, "1");
 processor.Clear();
 processor.Initialize(host, port, name, debugMode > 0, autoExecution > 0);
 std::cout << "Signal executer reinit" << std::endl;

}

int APIENTRY MtSrvPluginCfgAdd(const PluginCfg *cfg)
{	
	int res = ExtConfig.Add(0,cfg);
	UpdatePluginConifg();	
	return(res);
}

int APIENTRY MtSrvPluginCfgSet(const PluginCfg *values,const int total)
{
	int res = ExtConfig.Set(values,total);
	UpdatePluginConifg();	
	return(res);
}

int APIENTRY MtSrvPluginCfgDelete(LPCSTR name)
{   
	int res = ExtConfig.Delete(name);
	UpdatePluginConifg();
	return(res);
}

int APIENTRY MtSrvPluginCfgGet(LPCSTR name,PluginCfg *cfg)      { return ExtConfig.Get(name,cfg);   }
int APIENTRY MtSrvPluginCfgNext(const int index,PluginCfg *cfg) { return ExtConfig.Next(index,cfg); }
int APIENTRY MtSrvPluginCfgTotal()                              { return ExtConfig.Total();         }

void APIENTRY MtSrvTradesUpdate(TradeRecord *trade,UserInfo *user,const int mode)
{
	processor.OnTradeUpdate(*user, trade, mode);

}

void APIENTRY MtSrvTradesAddExt(TradeRecord *trade,const UserInfo *user,const ConSymbol *symb,const int mode)
{
	processor.OnNewTrade(*user, trade, mode);
}

int APIENTRY MtSrvDealerConfirm(const int id,const UserInfo *us,double *prices)
{	
	int result = processor.OnDealerConfirm(id, us, prices);	
	return result;
}

int  APIENTRY MtSrvDealerRequote(const int id,const UserInfo *us,double *prices,const int in_stream)
{	
	int result = processor.OnDealerRequote(id, us, prices, in_stream);	
	return result;
}

int  APIENTRY MtSrvDealerReset(const int id,const UserInfo *us,const char flag)
{	
	int result = processor.OnDealerReset(id, us, flag);
	return result;
}
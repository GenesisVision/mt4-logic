#include "StdAfx.h"
#include "MT4ServerEmulator.h"


MT4ServerEmulator::MT4ServerEmulator(CServerInterface *mt4)
{
	this->mt4 = mt4;
	if(mt4 == NULL)
	{
		Start(); //Start emulation
	}
}


MT4ServerEmulator::~MT4ServerEmulator(void)
{
	if(mt4 != NULL)
	{
		Stop();
	}
}

void MT4ServerEmulator::InitiateHook(Hook* hook)
{
	hookSync.Lock();
	hooks.push_back(hook);
	hookSync.Unlock();
}

int MT4ServerEmulator::ThreadFunction()
{
	Hook *hook = NULL;
	hookSync.Lock();
	if(hooks.size() > 0)
	{
		hook = *(hooks.begin());
		hooks.pop_front();
		if(hook->delay == 0)
		{
			hooks.push_back(hook);
		}
	}
	hookSync.Unlock();

	if(hook != NULL)
	{
		Sleep(1000);
		hook->Run();
	}
	return 0;
}

/*
int  __stdcall MT4ServerEmulator::Version(void)
{
	if(mt4 == NULL)
	{
		return 0;
	}
	else
	{
		return mt4->Version();
	}

}

//---            common functions
*/
time_t      __stdcall MT4ServerEmulator::TradeTime(void)
{
	if(mt4 == NULL)
	{
		return time(NULL);
	}
	else
	{
		return mt4->TradeTime();
	}

}
/*
//--- firewall config access
int  __stdcall MT4ServerEmulator::AccessAdd(const int pos,const ConAccess *acc)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->AccessAdd(pos, acc);
	}

}

int  __stdcall MT4ServerEmulator::AccessDelete(const int pos)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->AccessDelete(pos);
	}

}

int  __stdcall MT4ServerEmulator::AccessNext(const int pos,ConAccess *acc)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->AccessNext(pos, acc);
	}

}

int  __stdcall MT4ServerEmulator::AccessShift(const int pos,const int shift)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->AccessShift(pos, shift);
	}

}

//--- common config access
void __stdcall MT4ServerEmulator::CommonGet(ConCommon *info)
{
	if(mt4 != NULL)
	{
		mt4->CommonGet(info);
	}
	return;
}

void __stdcall MT4ServerEmulator::CommonSet(const ConCommon *info)
{
	if(mt4 != NULL)
	{
		mt4->CommonSet(info);
	}
	return;
}

//--- time config access
void __stdcall MT4ServerEmulator::TimeGet(ConTime *info)
{
	return;
}

void __stdcall MT4ServerEmulator::TimeSet(const ConTime *info)
{
	if(mt4 != NULL)
	{
		mt4->TimeSet(info);
	}
	return;
}

//--- backup config access
void __stdcall MT4ServerEmulator::BackupGet(ConBackup *info)
{
	if(mt4 != NULL)
	{
		mt4->BackupGet(info);
	}
	return;
}

int  __stdcall MT4ServerEmulator::BackupSet(const ConBackup *info)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->BackupSet(info);
	}

}

//--- feeders config access
int  __stdcall MT4ServerEmulator::FeedersAdd(const ConFeeder *feeder)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->FeedersAdd(feeder);
	}

}

int  __stdcall MT4ServerEmulator::FeedersDelete(const int pos)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->FeedersDelete(pos);
	}

}

int  __stdcall MT4ServerEmulator::FeedersNext(const int pos,ConFeeder *feeder)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->FeedersNext(pos, feeder);
	}

}

int  __stdcall MT4ServerEmulator::FeedersGet(LPCSTR name,ConFeeder *feeder)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->FeedersGet(name, feeder);
	}

}

int __stdcall MT4ServerEmulator::FeedersShift(const int pos,const int shift)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->FeedersShift(pos, shift);
	}

}

int __stdcall MT4ServerEmulator::FeedersEnable(LPCSTR name,const int mode)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->FeedersEnable(name, mode);
	}

}

//--- groups config access
int  __stdcall MT4ServerEmulator::GroupsAdd(ConGroup *group)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->GroupsAdd(group);
	}

}

int  __stdcall MT4ServerEmulator::GroupsDelete(const int pos)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->GroupsDelete(pos);
	}
}
*/
int  __stdcall MT4ServerEmulator::GroupsNext(const int pos,ConGroup *group)
{
	if(mt4 == NULL)
	{
		return FALSE;
	}
	else
	{
		return mt4->GroupsNext(pos, group);
	}
}

int  __stdcall MT4ServerEmulator::GroupsGet(LPCSTR name,ConGroup *group)
{
	if(mt4 == NULL)
	{
		COPY_STR(group->group, "real-1");
		group->enable = 1;
		group->secgroups[1].comm_agent = 20.0;
		group->secmargins[1].margin_divider = 15.23;
		group->reserved[25] = 1232;
		return TRUE;
	}
	else
	{
		return mt4->GroupsGet(name, group);
	}
}
/*
int  __stdcall MT4ServerEmulator::GroupsShift(const int pos,const int shift)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->GroupsShift(pos, shift);
	}
}

//--- holidays config access
int  __stdcall MT4ServerEmulator::HolidaysAdd(const int pos,ConHoliday *live)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->HolidaysAdd(pos, live);
	}

}

int  __stdcall MT4ServerEmulator::HolidaysDelete(const int pos)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->HolidaysDelete(pos);
	}

}

int  __stdcall MT4ServerEmulator::HolidaysNext(const int pos,ConHoliday *day)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->HolidaysNext(pos, day);
	}

}

int  __stdcall MT4ServerEmulator::HolidaysShift(const int pos,const int shift)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->HolidaysShift(pos, shift);
	}

}

//--- live update config access
int  __stdcall MT4ServerEmulator::LiveUpdateAdd(ConLiveUpdate *live)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->LiveUpdateAdd(live);
	}

}

int  __stdcall MT4ServerEmulator::LiveUpdateDelete(const int pos)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::LiveUpdateNext(const int pos,ConLiveUpdate *live)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::LiveUpdateGet(LPCSTR server,const int type,ConLiveUpdate *live)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

//--- managers config access
int  __stdcall MT4ServerEmulator::ManagersAdd(ConManager *man)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::ManagersDelete(const int pos)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
int  __stdcall MT4ServerEmulator::ManagersNext(const int pos,ConManager *manager)
{
	if(mt4 == NULL)
	{
		return FALSE;
	}
	else
	{
		return mt4->ManagersNext(pos, manager);
	}

}
/*
int  __stdcall MT4ServerEmulator::ManagersGet(const int login,ConManager *manager)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::ManagersShift(const int pos,const int shift)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::ManagersIsDemo(LPCSTR group,LPCSTR sec,const int volume)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

//--- symbols config access
int  __stdcall MT4ServerEmulator::SymbolsAdd(ConSymbol *sec)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::SymbolsDelete(const int pos)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
int  __stdcall MT4ServerEmulator::SymbolsNext(const int pos, ConSymbol *sec)
{
	if(mt4 == NULL)
	{
		if(pos == 0)
		{
			ConSymbol symbol = {0};
			COPY_STR(symbol.symbol, "EURUSD");
			symbol.digits = 6;
			
			symbol.sessions[0].quote[0].open_hour = 12;
			symbol.sessions[0].quote[0].align[6] = 22;

			symbol.sessions[0].quote[1].open_hour = 32;
			symbol.sessions[0].quote[1].align[6] = 42;
			
			symbol.spread = 4;
			symbol.long_only = 123;
			symbol.swap_openprice = 5423;
			symbol.expiration = 5436;
			symbol.count_original = 4343;
			symbol.realtime = 5454;
			symbol.profit_mode = 323314;
			symbol.spread = 1234556;
			symbol.margin_initial = 13423;
			symbol.expiration = 5436;
			symbol.profit_mode = 12;
			symbol.filter_smoothing = 12;
			symbol.logging = 543;
			symbol.contract_size = 543.12;
			symbol.exemode = 123;
			symbol.swap_rollover3days = 124;
			symbol.swap_type = 547;
			symbol.swap_long = 232.4;
			symbol.value_date = 1232;
			symbol.unused[21] = 5554;
			*sec = symbol; 
			return TRUE;
		}
		
		if(pos == 1)
		{
			ConSymbol symbol = {0};
			COPY_STR(symbol.symbol, "GBPUSD");
			symbol.digits = 4;
			symbol.sessions[1].trade[1].open = 224;
			symbol.sessions[1].quote[1].open = 22;
			*sec = symbol; 
			return TRUE;
		}
		return FALSE;
	}
	else
	{
		return mt4->SymbolsNext(pos, sec);
	}

}

int  __stdcall MT4ServerEmulator::SymbolsGet(LPCSTR symbol,ConSymbol *security)
{
	if(mt4 == NULL)
	{
		ConSymbol c;
		strcpy(c.symbol, symbol);
		*security = c;
		return FALSE;
	}
	else
	{
		return mt4->SymbolsGet(symbol, security);
	}

}
/*
int  __stdcall MT4ServerEmulator::SymbolsShift(const int pos,const int shift)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::SymbolsGroupsGet(const int index, ConSymbolGroup* group)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::SymbolsGroupsSet(const int index, ConSymbolGroup* group)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
//--- log access
void __stdcall MT4ServerEmulator::LogsOut(const int code,LPCSTR ip,LPCSTR msg)
{
	printf("%d %s\n", code, msg);
	return;
}
/*
//--- client base access-you should use HEAP_FREE on resulted arrays
int  __stdcall MT4ServerEmulator::ClientsTotal(void)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::ClientsAddUser(UserRecord *inf)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::ClientsDeleteUser(const int login)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
int  __stdcall MT4ServerEmulator::ClientsUserInfo(const int login, UserRecord *inf)
{
	if(mt4 == NULL)
	{
		COPY_STR(inf->address, "test");
		COPY_STR(inf->group, "demoforex");
		COPY_STR(inf->publickey, "demoforex");
		COPY_STR(inf->api_data, "api_data");
		inf->login = 1006;
		inf->regdate = 516;
		inf->enable_reserved[1] = 123;
		return TRUE;
	}
	else
	{
		return mt4->ClientsUserInfo(login, inf);
	}

}
/*
int  __stdcall MT4ServerEmulator::ClientsUserUpdate(const UserRecord *inf)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/

int  __stdcall MT4ServerEmulator::ClientsCheckPass(const int login,LPCSTR password,const int investor)
{
	if(mt4 == NULL)
	{
		//printf("Emulator: Password checked\n");
		return TRUE;
	}
	else 
	{
		return mt4->ClientsCheckPass(login, password, investor);
	}
}

/*
int  __stdcall MT4ServerEmulator::ClientsChangePass(const int login,LPCSTR password,const int change_investor,const int drop_key)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::ClientsChangeBalance(const int login,const ConGroup *grp,const double value,LPCSTR comment)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::ClientsChangeCredit(const int login,const ConGroup *grp,const double value,const time_t date,LPCSTR comment)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

UserRecord* __stdcall MT4ServerEmulator::ClientsAllUsers(int *totalusers)
{
	return NULL;
}

UserRecord* __stdcall MT4ServerEmulator::ClientsGroupsUsers(int *totalusers,LPCSTR groups)
{
	return NULL;
}
*/
//--- request base access
int  __stdcall MT4ServerEmulator::RequestsAdd(RequestInfo *request,const int isdemo,int *request_id)
{
	static int id = 1;
	if(mt4 == NULL)
	{
		//printf("Emulator: Request added\n");
		*request_id = id;
		InitiateHook(new DealerConfirmHook(id, 1000) );
		id++;
		return RET_TRADE_ACCEPTED;
	}
	else
	{
		return mt4->RequestsAdd(request, isdemo, request_id);
	}

}
/*
int  __stdcall MT4ServerEmulator::RequestsGet(int *key,RequestInfo *req,const int maxreq)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::RequestsFindAbsolete(const int login,LPCSTR symbol,const int volume,double *prices,DWORD *ctm,int *manager)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::RequestsPrices(const int id,const UserInfo *us,double *prices,const int in_stream)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::RequestsConfirm(const int id,const UserInfo *us,double *prices)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::RequestsRequote(const int id,const UserInfo *us,double *prices,const int in_stream)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::RequestsReset(const int id,const UserInfo *us,const char flag)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
//--- orders base access-you should use HEAP_FREE on resulted arrays
int  __stdcall MT4ServerEmulator::OrdersAdd(const TradeRecord *start,UserInfo* user,const ConSymbol *symb)
{
	if(mt4 == NULL)
	{
		return 1001;
	}
	else
	{
		return mt4->OrdersAdd(start, user, symb);
	}
}

int  __stdcall MT4ServerEmulator::OrdersUpdate(TradeRecord *order,UserInfo* user,const int mode)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->OrdersUpdate(order, user, mode);
	}
}

//---
int  __stdcall MT4ServerEmulator::OrdersGet(const int ticket,TradeRecord *order)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->OrdersGet(ticket, order);
	}
}

/*
TradeRecord*__stdcall MT4ServerEmulator::OrdersGet(const time_t from,const time_t to,const int *logins,const int count,int* total)
{
	return NULL;
}
*/
TradeRecord*__stdcall MT4ServerEmulator::OrdersGetOpen(const UserInfo* user,int* total)
{
	TradeRecord temp = {0};
	*total = 2;
	TradeRecord* trade = (TradeRecord*)HEAP_ALLOC(sizeof(TradeRecord) * 2);
	trade[0] = temp;
	trade[0].login = 1006;
	trade[0].order = 12353;
	trade[0].reserved[4] = 12345;
	COPY_STR(trade[0].comment, "test order");

	trade[1] = temp;
	trade[1].login = 1006;
	trade[1].order = 32538;
	trade[1].reserved[4] = 2345;
	COPY_STR(trade[1].comment, "test order2");
	return trade;
}

TradeRecord*__stdcall MT4ServerEmulator::OrdersGetClosed(const time_t from,const time_t to,const int *logins,const int count,int* total)
{
	return NULL;
}

//--- trade info access
int  __stdcall MT4ServerEmulator::TradesCalcProfit(LPCSTR group,TradeRecord *tpi)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->TradesCalcProfit(group, tpi);
	}

}

int  __stdcall MT4ServerEmulator::TradesMarginInfo(UserInfo *user,double *margin,double *freemargin,double *equity)
{
	if(mt4 == NULL)
	{
		*margin = 0.0;
		*freemargin = 0.0;
		*equity = 0.0;
		return RET_OK;
	}
	else
	{
		return mt4->TradesMarginInfo(user, margin, freemargin, equity);
	}
}
/*
//--- history center access-you should use HEAP_FREE on resulted arrays
void __stdcall MT4ServerEmulator::HistoryAddTick(FeedData *tick)
{
	return;
}

int  __stdcall MT4ServerEmulator::HistoryLastTicks(LPCSTR symbol,TickAPI *ticks,const int ticks_max)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
int  __stdcall MT4ServerEmulator::HistoryPrices(LPCSTR symbol,double *prices,time_t *ctm,int *dir)
{
	if(mt4 == NULL)
	{
		prices[0] = 1.0;
		prices[1] = 1.1;
		return RET_OK;
	}
	else
	{
		return mt4->HistoryPrices(symbol, prices, ctm, dir);
	}

}


int  __stdcall MT4ServerEmulator::HistoryPricesGroup(LPCSTR symbol,const ConGroup *grp,double *prices)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->HistoryPricesGroup(symbol, grp, prices);
	}

}
/*
int  __stdcall MT4ServerEmulator::HistoryPricesGroup(RequestInfo *request,double *prices)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator::HistoryUpdateObsolete(LPCSTR symbol,const int period,void *rt,const int total,const int updatemode)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

void*       __stdcall MT4ServerEmulator::HistoryQuotesObsolete(LPCSTR symbol,const int period,int *count)
{
	return NULL;
}

void __stdcall MT4ServerEmulator::HistorySync(void)
{
	return;
}

//--- mail&news base access
int   __stdcall MT4ServerEmulator::MailSend(MailBoxHeader *mail,int *logins,const int total)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int   __stdcall MT4ServerEmulator::NewsSend(FeedData *feeddata)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

//--- main server access
void  __stdcall MT4ServerEmulator::ServerRestart(void)
{
	return;
}

//--- daily base access-you should use HEAP_FREE on resulted arrays!
DailyReport* __stdcall MT4ServerEmulator::DailyGet(LPCSTR group,const time_t from,const time_t to,int* logins,const int logins_total,int *daily_total)
{
	return NULL;
}

//--- select & free request from request queue
int   __stdcall MT4ServerEmulator::RequestsLock(const int id,const int manager)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int   __stdcall MT4ServerEmulator::RequestsFree(const int id,const int manager)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
//--- check available margin
double       __stdcall MT4ServerEmulator::TradesMarginCheck(const UserInfo *user,const TradeTransInfo *trade,double *profit,double *freemargin,double *new_margin)
{
	if(mt4 == NULL)
	{
		*freemargin = 1000;
		return 0.0;
	}
	else
	{
		return mt4->TradesMarginCheck(user, trade, profit, freemargin, new_margin);
	}

}
/*
//--- high level order operations
*/
int   __stdcall MT4ServerEmulator::OrdersOpen(const TradeTransInfo *trans,UserInfo *user)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->OrdersOpen(trans, user);
	}

}

int   __stdcall MT4ServerEmulator::OrdersClose(const TradeTransInfo *trans,UserInfo *user)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->OrdersClose(trans, user);
	}

}
/*
int   __stdcall MT4ServerEmulator::OrdersCloseBy(const TradeTransInfo *trans,UserInfo *user)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

//--- additional trade functions
double       __stdcall MT4ServerEmulator::TradesCalcRates(LPCSTR group,LPCSTR from,LPCSTR to)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

double       __stdcall MT4ServerEmulator::TradesCalcConvertation(LPCSTR group,const int margin_mode,const double price,const ConSymbol *symbol)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

double       __stdcall MT4ServerEmulator::TradesCommissionAgent(TradeRecord *trade,const ConSymbol *symbol,const UserInfo *user)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

void  __stdcall MT4ServerEmulator::TradesCommission(TradeRecord *trade,LPCSTR group,const ConSymbol *symbol)
{
	return;
}

int   __stdcall MT4ServerEmulator::TradesFindLogin(const int order)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

//--- special checks
int   __stdcall MT4ServerEmulator::TradesCheckSessions(const ConSymbol *symbol,const time_t ctm)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
int   __stdcall MT4ServerEmulator::TradesCheckStops(const TradeTransInfo *trans,const ConSymbol *symbol,const ConGroup *group,const TradeRecord *trade)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->TradesCheckStops(trans, symbol, group, trade);
	}

}

int   __stdcall MT4ServerEmulator::TradesCheckFreezed(const ConSymbol *symbol,const ConGroup *group,const TradeRecord *trade)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->TradesCheckFreezed(symbol, group, trade);
	}

}

int   __stdcall MT4ServerEmulator::TradesCheckSecurity(const ConSymbol *symbol,const ConGroup *group)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->TradesCheckSecurity(symbol, group);
	}

}

int   __stdcall MT4ServerEmulator::TradesCheckVolume(const TradeTransInfo *trans,const ConSymbol *symbol,const ConGroup *group,const int check_min)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->TradesCheckVolume(trans, symbol, group, check_min);
	}

}

int   __stdcall MT4ServerEmulator::TradesCheckTickSize(const double price,const ConSymbol *symbol)
{
	if(mt4 == NULL)
	{
		return TRUE;
	}
	else
	{
		return mt4->TradesCheckTickSize(price, symbol);
	}

}

RateInfo*   __stdcall MT4ServerEmulator:: HistoryQuotes(LPCSTR symbol,const int period,int *count)
{
	RateInfo *info = new RateInfo[7];
	for(int i = 0; i < 7; i++)
	{
		info[i].vol = 0;
		info[i].ctm = 200 + i;
		info[i].open = 12345;
		info[i].close = 12346;
		info[i].high = 12347;
		info[i].low = 12344;
	}
	*count = 7;
	return info;
}

/*
//--- extension
int  __stdcall MT4ServerEmulator:: RequestsFind(const int login,LPCSTR symbol,const int volume,const UCHAR type,const UCHAR cmd,double *prices,DWORD *ctm,int *manager)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}

int  __stdcall MT4ServerEmulator:: HistoryUpdate(LPCSTR symbol,const int period,RateInfo *rt,const int total,const int updatemode)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}



//--- tick database access
TickAPI*    __stdcall MT4ServerEmulator:: HistoryTicksGet(LPCSTR symbol,const time_t from,const time_t to,const char ticks_flags,int* total)
{
	return NULL;
}

//--- request server logs
char*       __stdcall MT4ServerEmulator:: LogsRequest(const LogRequest *request,int *size)
{
	return "";
}

//---- check account's balance
int  __stdcall MT4ServerEmulator:: ClientsCheckBalance(const int login,int fix_flag,double* difference)
{
	if(mt4 == NULL)
	{
		return RET_OK;
	}
	else
	{
		return mt4->();
	}

}
*/
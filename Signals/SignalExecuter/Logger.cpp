#include "StdAfx.h"
#include "Logger.h"

void LogMessage(std::string text, int type, MT4Server *server) 
{
	server->LogsOut(type, "SignalExecutor", text.c_str() );
}
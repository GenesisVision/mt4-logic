//#pragma once

#ifndef LOGGER
#define LOGGER

#include <string>
#include <string>
#include "mt4part\MT4ServerEmulator.h"

void LogMessage(std::string text, int type, MT4Server *server);

#endif
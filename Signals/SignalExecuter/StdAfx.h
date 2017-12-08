#pragma once

#ifndef _WIN32_WINNT            // Specifies that the minimum required platform is Windows Vista.
#define _WIN32_WINNT 0x0600     // Change this to the appropriate value to target other versions of Windows.
#endif

#define _USE_32BIT_TIME_T
#define WIN32_LEAN_AND_MEAN      // Exclude rarely-used stuff from Windows headers

#define COPY_STR(dst,src) { strncpy(dst,src,sizeof(dst)-1); dst[sizeof(dst)-1]=0; }

#include <time.h>
#include <stdio.h>
#include "windows.h"
#include <vector>
#include <map>
#include <sstream>
#include <string>
#include <math.h>
#include "mt4part/MT4ServerAPI.h"
extern std::string path;
//+------------------------------------------------------------------+

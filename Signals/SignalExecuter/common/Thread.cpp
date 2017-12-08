#include "StdAfx.h"
#include "Thread.h"

Thread::Thread(void)
{
}


Thread::~Thread(void)
{
}

int Thread::startMainThread()
{
	bMainThread = true;
	DWORD thread;	
	mainThread = CreateThread(NULL, 0, MainThread, this, 0, &thread);
	return 0;
}

DWORD WINAPI Thread::MainThread(LPVOID pParam)
{
	Thread *self = (Thread*)pParam;
	while(self->bMainThread)
	{
		self->ThreadFunction();
		Sleep(1);
	}
	return 0;
}

void Thread::Start()
{
	startMainThread();
}

void Thread::Stop()
{
	endMainThread();
}

int Thread::endMainThread()
{
	bMainThread = false;
	WaitForSingleObject(mainThread, 2000);
	return 0;
}

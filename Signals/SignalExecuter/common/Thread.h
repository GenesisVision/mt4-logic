#pragma once
#include "windows.h"

class Thread abstract
{
public:
	Thread(void);
	~Thread(void);
	void Stop();
	void Start();
private:
	static DWORD WINAPI MainThread(LPVOID pParam);
	int startMainThread();
	int endMainThread();
	HANDLE mainThread;
	volatile bool bMainThread;
protected:
	virtual int ThreadFunction() = 0;
};


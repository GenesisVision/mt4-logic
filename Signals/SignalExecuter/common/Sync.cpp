#include "stdafx.h"
#include "Sync.h"

CSync::CSync() 
{ 
	ZeroMemory(&m_cs,sizeof(m_cs)); InitializeCriticalSection(&m_cs); 
}
CSync::~CSync() 
{
	DeleteCriticalSection(&m_cs);   ZeroMemory(&m_cs,sizeof(m_cs));   
}

void CSync::Lock() 
{
	EnterCriticalSection(&m_cs); 
}

void CSync::Unlock() 
{
	LeaveCriticalSection(&m_cs); 
}
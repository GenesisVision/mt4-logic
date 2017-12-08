#pragma once

class CSync
  {
private:
   CRITICAL_SECTION  m_cs;
public:
	CSync();
	~CSync();
	void Lock();
	void Unlock();
  };
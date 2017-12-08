#ifndef _CONCURRENT_QUEUE_H_
#define _CONCURRENT_QUEUE_H_

#include <queue>
#include <vector>
#include <mutex>

class ScopedLock
{
public:
	ScopedLock(std::mutex &mutex)
		: _mutex(mutex)
	{
		_mutex.lock();
	}
	~ScopedLock()
	{
		_mutex.unlock();
	}

private:
	std::mutex &_mutex;
};


class ConcurrentQueue
{
/// Public methods
public:
	/// Push message in queue
	void Push(std::string mess);
	/// Pop element from queue
	std::string Pop();
	/// Queue is empty
	bool IsEmpty();
///Fields
private:
	/// Queue with messages
	std::queue<std::string> queue;
	/// Queue mutex
	std::mutex mutex;
};

#endif //_CONCURRENT_QUEUE_H_

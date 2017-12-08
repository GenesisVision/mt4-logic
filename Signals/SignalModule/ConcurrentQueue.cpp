#include "ConcurrentQueue.h"

void ConcurrentQueue::Push(std::string mess)
{
	ScopedLock lock(mutex);
	queue.push(mess);
}

std::string ConcurrentQueue::Pop()
{
	ScopedLock lock(mutex);
	if(queue.empty())
		return ""; // XXX Return empty string
	auto res = queue.front();
	queue.pop();
	return res;
}

bool ConcurrentQueue::IsEmpty()
{
	ScopedLock lock(mutex);
	return queue.empty();
}
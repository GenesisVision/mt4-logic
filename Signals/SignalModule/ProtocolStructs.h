#ifndef _PROTOCOL_STRUCTS_H
#define _PROTOCOL_STRUCTS_H

#include <string>
#include <vector>

enum ActionType
{
	Open = 0,
	Close = 1
};

enum TradeSide
{
	Buy = 0,
	Sell = 1
};

struct MT4TradeSignal
{
public:
	TradeSide Side;
	ActionType ActionType;
	__int64 DateTime;
	double Equity;
	double Balance;
	double Volume;
	std::string Symbol;
	double StopLoss;
	double TakeProfit;
	int Login;
	std::string Server;
	int OrderID;
	std::string Comment;
	double Profit;
	double ProviderCommission;
};

struct OrderStatus
{
public:
	int OrderID;
	TradeSide Side;
	__int64 DateTime;
	double Volume;
	std::string Symbol;
	double StopLoss;
	double TakeProfit;
	std::string Comment;
};

struct AccountOrdersStatus
{
	int Login;
	std::vector<OrderStatus> Status;
};

struct OrdersStatusResponse
{
	std::vector<AccountOrdersStatus> OrdersStatus;
};

struct ExecutionOrder
{
	int Login;
	ActionType ActionType;
	TradeSide TradeSide;
	double Volume;
	std::string Symbol;
	int OrderID;
	double Commission;
};

struct ExecutionSignal
{
	std::string comment;
	std::vector<ExecutionOrder> Orders;
};

#endif //_PROTOCOL_STRUCTS_H
using System;

namespace SignalService.Models
{
	public class OrderModel : IEquatable<OrderModel>
	{
		public int OrderId { get; set; }

		public string Server { get; set; }

		public override int GetHashCode()
		{
			return string.Format("{0}_{1}", Server, OrderId).GetHashCode();
		}

		public bool Equals(OrderModel other)
		{
			return (OrderId == other.OrderId && Server == other.Server);
		}
	}
}

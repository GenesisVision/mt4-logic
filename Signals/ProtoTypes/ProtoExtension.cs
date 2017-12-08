using System;
using System.IO;
using ProtoBuf;

namespace ProtoTypes
{
	public static class ProtoExtension
	{
		/// <summary>
		/// Serialize signal to byte array
		/// </summary>
		/// <returns>Serialized signal</returns>
		public static byte[] Serialize<T>(this T t)
		{
			try
			{
				using (var stream = new MemoryStream())
				{
					Serializer.Serialize(stream, t);
					return stream.ToArray();
				}
			}
			catch (Exception ex)
			{
				return null;
			}
		}

		/// <summary>
		/// Deserialize signal from byte array
		/// </summary>
		/// <param name="data">Byte array</param>
		/// <returns>Object</returns>
		public static T DeSerialize<T>(byte[] data) where T : class
		{
			try
			{
				using (var stream = new MemoryStream(data))
				{
					return Serializer.Deserialize<T>(stream);
				}
			}
			catch (Exception ex)
			{
				return null;
			}
		}
	}
}

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DemonDragon.StateMachine
{
	/// <summary>
	/// Compares states by reference, never by value. This is what makes the machine instance-keyed:
	/// two instances of the same state class stay distinct even if the class overrides Equals.
	/// </summary>
	internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

		public bool Equals(T x, T y) => ReferenceEquals(x, y);

		public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
	}
}

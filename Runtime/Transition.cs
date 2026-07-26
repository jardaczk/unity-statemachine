using System;

namespace DemonDragon.StateMachine
{
	/// <summary>
	/// One edge of the machine: target state + the condition that fires it.
	/// Created through <see cref="StateMachineBuilder{TContext}"/>; the order in which transitions are
	/// registered is their priority.
	/// </summary>
	internal sealed class Transition<TContext>
	{
		public readonly IState<TContext> To;
		public readonly Func<bool> Condition;

		/// <summary>Optional label for debugging / diagnostics.</summary>
		public readonly string Name;

		/// <summary>When true this transition ignores <see cref="IState{TContext}.CanExit"/>.</summary>
		public bool Force { get; set; }

		public Transition(IState<TContext> to, Func<bool> condition, string name)
		{
			To = to ?? throw new ArgumentNullException(nameof(to));
			Condition = condition ?? throw new ArgumentNullException(nameof(condition));
			Name = name;
		}
	}
}

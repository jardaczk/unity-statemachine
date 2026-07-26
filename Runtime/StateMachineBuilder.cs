using System;
using System.Collections.Generic;

namespace DemonDragon.StateMachine
{
	/// <summary>
	/// Fluent builder for <see cref="StateMachine{TContext}"/>.
	/// <code>
	/// var fsm = new StateMachineBuilder&lt;PlayerContext&gt;()
	///     .Initial(idle)
	///     .From(idle)
	///         .To(fall).When(() =&gt; !motor.IsGrounded)
	///         .To(jump).When(input.JumpPressed)
	///     .From(idle, run, jump, fall)          // one edge from several sources
	///         .To(attack).When(input.AttackPressed)
	///     .FromAny()
	///         .To(dead).When(() =&gt; player.IsDead).Forced()
	///     .Build();
	/// </code>
	/// Transitions fire in the order they are registered, so register the more specific condition
	/// first (e.g. "jump + ledge ahead → climb" before plain "jump").
	/// </summary>
	public sealed class StateMachineBuilder<TContext>
	{
		readonly Dictionary<IState<TContext>, List<Transition<TContext>>> transitions =
			new Dictionary<IState<TContext>, List<Transition<TContext>>>(ReferenceComparer<IState<TContext>>.Instance);
		readonly List<Transition<TContext>> anyTransitions = new List<Transition<TContext>>();
		readonly List<Transition<TContext>> lastAdded = new List<Transition<TContext>>();

		IState<TContext> initial;
		IState<TContext>[] sources;
		bool fromAny;
		IState<TContext> pendingTarget;

		/// <summary>The state the machine starts in. Required.</summary>
		public StateMachineBuilder<TContext> Initial(IState<TContext> state)
		{
			initial = state ?? throw new ArgumentNullException(nameof(state));
			return this;
		}

		/// <summary>Selects the source state(s) that the following <see cref="To"/> edges start from.</summary>
		public StateMachineBuilder<TContext> From(params IState<TContext>[] states)
		{
			if (states == null || states.Length == 0)
				throw new ArgumentException("From(...) needs at least one state.", nameof(states));
			for (int i = 0; i < states.Length; i++)
				if (states[i] == null) throw new ArgumentException("From(...) got a null state.", nameof(states));

			sources = states;
			fromAny = false;
			pendingTarget = null;
			return this;
		}

		/// <summary>
		/// Following edges apply to every state. Any-transitions are evaluated before the current
		/// state's own transitions.
		/// </summary>
		public StateMachineBuilder<TContext> FromAny()
		{
			sources = null;
			fromAny = true;
			pendingTarget = null;
			return this;
		}

		/// <summary>Target of the edge; complete it with <see cref="When"/> or <see cref="Always"/>.</summary>
		public StateMachineBuilder<TContext> To(IState<TContext> state)
		{
			if (sources == null && !fromAny)
				throw new InvalidOperationException("Call From(...) or FromAny() before To(...).");
			pendingTarget = state ?? throw new ArgumentNullException(nameof(state));
			return this;
		}

		/// <summary>Condition that fires the pending edge. <paramref name="name"/> is for diagnostics only.</summary>
		public StateMachineBuilder<TContext> When(Func<bool> condition, string name = null)
		{
			if (pendingTarget == null)
				throw new InvalidOperationException("Call To(...) before When(...).");
			if (condition == null) throw new ArgumentNullException(nameof(condition));

			lastAdded.Clear();

			if (fromAny)
			{
				var transition = new Transition<TContext>(pendingTarget, condition, name);
				anyTransitions.Add(transition);
				lastAdded.Add(transition);
			}
			else
			{
				for (int i = 0; i < sources.Length; i++)
				{
					var transition = new Transition<TContext>(pendingTarget, condition, name);
					Outgoing(sources[i]).Add(transition);
					lastAdded.Add(transition);
				}
			}

			pendingTarget = null;
			return this;
		}

		/// <summary>
		/// Unconditional edge — useful together with <c>MinDuration</c>, where the state itself decides
		/// when it is done (fire → cooldown after the animation).
		/// </summary>
		public StateMachineBuilder<TContext> Always(string name = null) => When(() => true, name);

		/// <summary>
		/// Makes the edge(s) just registered ignore <see cref="IState{TContext}.CanExit"/> — for things
		/// that must interrupt anything, like death or a cutscene takeover.
		/// </summary>
		public StateMachineBuilder<TContext> Forced()
		{
			if (lastAdded.Count == 0)
				throw new InvalidOperationException("Forced() must follow When(...) / Always(...).");
			for (int i = 0; i < lastAdded.Count; i++)
				lastAdded[i].Force = true;
			return this;
		}

		public StateMachine<TContext> Build()
		{
			if (initial == null)
				throw new InvalidOperationException("Initial(...) was never set — the machine has no starting state.");
			if (pendingTarget != null)
				throw new InvalidOperationException($"Dangling To({pendingTarget.GetType().Name}) without When(...).");

			// copy, so a reused builder can never mutate an already built machine
			var copy = new Dictionary<IState<TContext>, List<Transition<TContext>>>(
				transitions.Count, ReferenceComparer<IState<TContext>>.Instance);
			foreach (var pair in transitions)
				copy.Add(pair.Key, new List<Transition<TContext>>(pair.Value));

			return new StateMachine<TContext>(initial, copy, new List<Transition<TContext>>(anyTransitions));
		}

		List<Transition<TContext>> Outgoing(IState<TContext> state)
		{
			if (!transitions.TryGetValue(state, out var list))
			{
				list = new List<Transition<TContext>>();
				transitions.Add(state, list);
			}
			return list;
		}
	}
}

using System;
using System.Collections.Generic;

namespace DemonDragon.StateMachine
{
	/// <summary>
	/// Strongly typed finite state machine.
	/// <para>
	/// States are keyed by <b>instance</b>, so the same state class may appear several times in one
	/// machine (two waypoint waits with different durations, the same sub-behaviour reused twice).
	/// Transitions are evaluated in <b>registration order</b>, which makes their priority explicit and
	/// deterministic — register the more specific condition first.
	/// </para>
	/// <para>
	/// Three ways to drive it, freely combinable:
	/// transitions (classic FSM), <see cref="ChangeState"/> pushed from outside, and a
	/// <see cref="Selector"/> (the Utility AI seam). A machine with no transitions at all is valid and
	/// simply executes whatever it is told to run.
	/// </para>
	/// <para>
	/// Deltas are passed in rather than read from a clock, so the machine has no engine dependency and
	/// can be unit-tested (and time-scaled) freely.
	/// </para>
	/// </summary>
	public sealed class StateMachine<TContext>
	{
		static readonly List<Transition<TContext>> NoTransitions = new List<Transition<TContext>>();

		readonly Dictionary<IState<TContext>, List<Transition<TContext>>> transitions;
		readonly List<Transition<TContext>> anyTransitions;

		IState<TContext> current;
		bool started;

		internal StateMachine(
			IState<TContext> initial,
			Dictionary<IState<TContext>, List<Transition<TContext>>> transitions,
			List<Transition<TContext>> anyTransitions)
		{
			current = initial ?? throw new ArgumentNullException(nameof(initial));
			this.transitions = transitions;
			this.anyTransitions = anyTransitions;
		}

		public IState<TContext> Current => current;

		/// <summary>Class name of the running state — for debug overlays and logging.</summary>
		public string CurrentStateName => current.GetType().Name;

		/// <summary>Raised after every state change as (previous, next); previous is null on start.</summary>
		public event Action<IState<TContext>, IState<TContext>> StateChanged;

		/// <summary>Optional external brain (Utility AI…). Consulted each tick when no transition fired.</summary>
		public IStateSelector<TContext> Selector { get; set; }

		/// <summary>
		/// Enters the initial state. Called automatically by the first tick; call it explicitly if you
		/// need entry to happen at a specific moment (after subscribing to <see cref="StateChanged"/>).
		/// </summary>
		public void Start()
		{
			if (started) return;
			started = true;
			current.Enter();
			StateChanged?.Invoke(null, current);
		}

		/// <summary>Evaluates transitions (then the selector) and updates the running state.</summary>
		public void Tick(float deltaTime)
		{
			if (!started) Start();

			var transition = FindTransition();
			if (transition != null)
				ChangeState(transition.To, transition.Force);
			else if (Selector != null)
				ChangeState(Selector.Select(current));

			current.Update(deltaTime);
		}

		/// <summary>Physics step of the running state. Transitions are evaluated in <see cref="Tick"/> only.</summary>
		public void FixedTick(float fixedDeltaTime)
		{
			if (!started) Start();
			current.FixedUpdate(fixedDeltaTime);
		}

		/// <summary>
		/// Switches state directly. Returns false when the change was refused — the target is null or
		/// already running, or the current state is not done (<see cref="IState{TContext}.CanExit"/>)
		/// and <paramref name="force"/> is false.
		/// </summary>
		public bool ChangeState(IState<TContext> next, bool force = false)
		{
			if (next == null || ReferenceEquals(next, current)) return false;
			if (!started) Start();
			if (!force && !current.CanExit) return false;

			var previous = current;
			previous.Exit();
			current = next;
			current.Enter();
			StateChanged?.Invoke(previous, current);
			return true;
		}

		Transition<TContext> FindTransition()
		{
			bool canExit = current.CanExit;

			var match = FirstMatch(anyTransitions, canExit);
			if (match != null) return match;

			var outgoing = transitions.TryGetValue(current, out var list) ? list : NoTransitions;
			return FirstMatch(outgoing, canExit);
		}

		Transition<TContext> FirstMatch(List<Transition<TContext>> candidates, bool canExit)
		{
			for (int i = 0; i < candidates.Count; i++)
			{
				var transition = candidates[i];
				if (!canExit && !transition.Force) continue;      // state refuses to be interrupted
				if (ReferenceEquals(transition.To, current)) continue;  // no self re-entry
				if (transition.Condition()) return transition;
			}
			return null;
		}
	}
}

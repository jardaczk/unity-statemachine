namespace DemonDragon.StateMachine
{
	/// <summary>
	/// Convenience base class for states: holds the typed context, tracks <see cref="StateTime"/>
	/// and implements <see cref="CanExit"/> via <see cref="MinDuration"/>.
	/// </summary>
	public abstract class State<TContext> : IState<TContext>
	{
		protected readonly TContext Context;

		protected State(TContext context) => Context = context;

		public float StateTime { get; private set; }

		/// <summary>
		/// Minimum seconds to stay in this state before it may be left. Override for uninterruptible
		/// actions (attack, dodge, landing recovery) and to give AI-driven machines hysteresis.
		/// </summary>
		protected virtual float MinDuration => 0f;

		public virtual bool CanExit => StateTime >= MinDuration;

		public virtual void Enter() => StateTime = 0f;

		public virtual void Update(float deltaTime) => StateTime += deltaTime;

		public virtual void FixedUpdate(float fixedDeltaTime) { }

		public virtual void Exit() { }
	}
}

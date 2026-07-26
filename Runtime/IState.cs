namespace DemonDragon.StateMachine
{
	/// <summary>
	/// A single state of a <see cref="StateMachine{TContext}"/>.
	/// <para>
	/// <typeparamref name="TContext"/> does not appear in any member — it is a deliberate "phantom"
	/// type parameter that makes the machine strongly typed: states written for one owner
	/// (player, enemy, trap) cannot be mixed into another owner's machine. Concrete states get the
	/// context itself through <see cref="State{TContext}"/>.
	/// </para>
	/// </summary>
	public interface IState<TContext>
	{
		/// <summary>Seconds spent in this state since <see cref="Enter"/>.</summary>
		float StateTime { get; }

		/// <summary>
		/// False while the state refuses to be left (uninterruptible animation, minimum dwell time).
		/// Non-forced transitions and selector decisions are blocked while this is false.
		/// </summary>
		bool CanExit { get; }

		void Enter();
		void Update(float deltaTime);
		void FixedUpdate(float fixedDeltaTime);
		void Exit();
	}
}

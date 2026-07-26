namespace DemonDragon.StateMachine
{
	/// <summary>
	/// Plugin point for an external "brain" that decides which state should run — the seam for
	/// Utility AI, behaviour trees or a director. The machine asks the selector each tick (after its
	/// own transitions found nothing) and executes whatever it returns.
	/// <para>
	/// Selector decisions respect <see cref="IState{TContext}.CanExit"/>, so a state can refuse to be
	/// cut short and the brain will not thrash between two similarly scored actions.
	/// </para>
	/// </summary>
	public interface IStateSelector<TContext>
	{
		/// <summary>
		/// Returns the state that should be running now, or null / <paramref name="current"/> to keep
		/// the current one.
		/// </summary>
		IState<TContext> Select(IState<TContext> current);
	}
}

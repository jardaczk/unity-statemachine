using System;
using NUnit.Framework;

namespace DemonDragon.StateMachine.Tests
{
	/// <summary>
	/// The machine is engine-agnostic (deltas are passed in), so everything here is a plain
	/// EditMode test with no scene, no MonoBehaviour and no clock.
	/// </summary>
	public class StateMachineTests
	{
		class Ctx { }

		class Probe : State<Ctx>
		{
			public readonly string Name;
			public int Entered, Exited, Updated, FixedUpdated;
			public float Min;
			protected override float MinDuration => Min;
			public Probe(Ctx c, string name = "probe") : base(c) { Name = name; }
			public override void Enter() { base.Enter(); Entered++; }
			public override void Exit() { base.Exit(); Exited++; }
			public override void Update(float dt) { base.Update(dt); Updated++; }
			public override void FixedUpdate(float fdt) { base.FixedUpdate(fdt); FixedUpdated++; }
			public override string ToString() => Name;
		}

		class Picker : IStateSelector<Ctx>
		{
			public IState<Ctx> Desired;
			public IState<Ctx> Select(IState<Ctx> current) => Desired;
		}

		Ctx ctx;
		[SetUp] public void SetUp() => ctx = new Ctx();

		Probe P(string name) => new Probe(ctx, name);

		// ─── priority ────────────────────────────────────────────────────────────

		[Test]
		public void FirstRegisteredTransitionWins()
		{
			Probe a = P("a"), b = P("b"), c = P("c");
			var fsm = new StateMachineBuilder<Ctx>().Initial(a)
				.From(a).To(b).When(() => true).To(c).When(() => true)
				.Build();

			fsm.Tick(0.1f);

			Assert.AreSame(b, fsm.Current, "registration order defines priority");
		}

		[Test]
		public void AnyTransitionBeatsLocalTransition()
		{
			Probe a = P("a"), local = P("local"), global = P("global");
			var fsm = new StateMachineBuilder<Ctx>().Initial(a)
				.From(a).To(local).When(() => true)
				.FromAny().To(global).When(() => true)
				.Build();

			fsm.Tick(0.1f);

			Assert.AreSame(global, fsm.Current);
		}

		[Test]
		public void TransitionBeatsSelector()
		{
			Probe a = P("a"), byEdge = P("edge"), bySelector = P("selector");
			var fsm = new StateMachineBuilder<Ctx>().Initial(a)
				.From(a).To(byEdge).When(() => true)
				.Build();
			fsm.Selector = new Picker { Desired = bySelector };

			fsm.Tick(0.1f);

			Assert.AreSame(byEdge, fsm.Current, "the selector is only consulted when no transition fired");
		}

		// ─── instance keying ─────────────────────────────────────────────────────

		[Test]
		public void StatesAreKeyedByInstanceNotByType()
		{
			// two instances of the same class, each with its own outgoing edge
			Probe first = P("first"), second = P("second"), viaFirst = P("viaFirst"), viaSecond = P("viaSecond");
			var fsm = new StateMachineBuilder<Ctx>().Initial(second)
				.From(first).To(viaFirst).When(() => true)
				.From(second).To(viaSecond).When(() => true)
				.Build();

			fsm.Tick(0.1f);

			Assert.AreSame(viaSecond, fsm.Current, "a type-keyed machine would have taken the first instance's edge");
		}

		// ─── interruptibility ────────────────────────────────────────────────────

		[Test]
		public void MinDurationBlocksTransitionUntilElapsed()
		{
			var slow = P("slow"); slow.Min = 1f;
			var next = P("next");
			var fsm = new StateMachineBuilder<Ctx>().Initial(slow)
				.From(slow).To(next).When(() => true)
				.Build();

			fsm.Tick(0.1f);
			Assert.AreSame(slow, fsm.Current, "must not leave before MinDuration");

			for (int i = 0; i < 20 && ReferenceEquals(fsm.Current, slow); i++) fsm.Tick(0.1f);
			Assert.AreSame(next, fsm.Current, "must leave once MinDuration elapsed");
		}

		[Test]
		public void ForcedTransitionIgnoresCanExit()
		{
			var slow = P("slow"); slow.Min = 5f;
			var dead = P("dead");
			var fsm = new StateMachineBuilder<Ctx>().Initial(slow)
				.From(slow).To(dead).When(() => true).Forced()
				.Build();

			fsm.Tick(0.1f);

			Assert.AreSame(dead, fsm.Current);
		}

		[Test]
		public void UnconditionalEdgeStillWaitsForMinDuration()
		{
			var fire = P("fire"); fire.Min = 0.5f;
			var cooldown = P("cooldown");
			var fsm = new StateMachineBuilder<Ctx>().Initial(fire)
				.From(fire).To(cooldown).Always()
				.Build();

			for (int i = 0; i < 4; i++) fsm.Tick(0.1f);
			Assert.AreSame(fire, fsm.Current);

			for (int i = 0; i < 3; i++) fsm.Tick(0.1f);
			Assert.AreSame(cooldown, fsm.Current);
		}

		// ─── external control / selector ─────────────────────────────────────────

		[Test]
		public void ChangeStateRespectsCanExitUnlessForced()
		{
			var a = P("a"); a.Min = 1f;
			var b = P("b");
			var fsm = new StateMachineBuilder<Ctx>().Initial(a).Build();   // no transitions at all
			fsm.Start();

			Assert.IsFalse(fsm.ChangeState(b), "refused while the current state cannot exit");
			Assert.AreSame(a, fsm.Current);

			Assert.IsTrue(fsm.ChangeState(b, force: true));
			Assert.AreSame(b, fsm.Current);
		}

		[Test]
		public void ChangeStateToNullOrCurrentIsNoOp()
		{
			var a = P("a");
			var fsm = new StateMachineBuilder<Ctx>().Initial(a).Build();
			fsm.Start();

			Assert.IsFalse(fsm.ChangeState(null));
			Assert.IsFalse(fsm.ChangeState(a));
			Assert.AreEqual(1, a.Entered, "no re-entry");
		}

		[Test]
		public void SelectorDrivesMachineWithoutTransitions()
		{
			Probe idle = P("idle"), flee = P("flee"), fight = P("fight");
			var picker = new Picker();
			var fsm = new StateMachineBuilder<Ctx>().Initial(idle).Build();
			fsm.Selector = picker;

			picker.Desired = flee;
			fsm.Tick(0.1f);
			Assert.AreSame(flee, fsm.Current);

			picker.Desired = null;
			fsm.Tick(0.1f);
			Assert.AreSame(flee, fsm.Current, "a null decision keeps the current state");

			picker.Desired = fight;
			fsm.Tick(0.1f);
			Assert.AreSame(fight, fsm.Current);
		}

		[Test]
		public void SelectorCannotInterruptCommittedState()
		{
			var committed = P("committed"); committed.Min = 2f;
			var other = P("other");
			var fsm = new StateMachineBuilder<Ctx>().Initial(committed).Build();
			fsm.Selector = new Picker { Desired = other };

			fsm.Tick(0.1f);

			Assert.AreSame(committed, fsm.Current, "hysteresis keeps a utility brain from thrashing");
		}

		// ─── multi-source ────────────────────────────────────────────────────────

		[Test]
		public void MultiSourceEdgeFiresFromEachSource()
		{
			Probe a = P("a"), b = P("b"), c = P("c"), fall = P("fall");
			bool grounded = true;
			var fsm = new StateMachineBuilder<Ctx>().Initial(b)
				.From(a, b, c).To(fall).When(() => !grounded)
				.Build();

			fsm.Tick(0.1f);
			Assert.AreSame(b, fsm.Current);

			grounded = false;
			fsm.Tick(0.1f);
			Assert.AreSame(fall, fsm.Current);
		}

		// ─── lifecycle ───────────────────────────────────────────────────────────

		[Test]
		public void LifecycleAndStateTime()
		{
			Probe a = P("a"), b = P("b");
			bool go = false;
			var fsm = new StateMachineBuilder<Ctx>().Initial(a)
				.From(a).To(b).When(() => go)
				.Build();

			IState<Ctx> from = null, to = null;
			int changes = 0;
			fsm.StateChanged += (p, n) => { from = p; to = n; changes++; };

			fsm.Tick(0.1f); fsm.Tick(0.1f); fsm.Tick(0.1f);
			Assert.AreEqual(1, changes, "entering the initial state raises StateChanged once");
			Assert.IsNull(from);
			Assert.AreSame(a, to);
			Assert.AreEqual(0.3f, a.StateTime, 1e-4f);

			fsm.FixedTick(0.02f);
			Assert.AreEqual(1, a.FixedUpdated);

			go = true;
			fsm.Tick(0.1f);
			Assert.AreEqual(1, a.Exited);
			Assert.AreEqual(1, b.Entered);
			Assert.AreSame(a, from);
			Assert.AreSame(b, to);
			Assert.AreEqual(0.1f, b.StateTime, 1e-4f, "StateTime resets on Enter");
		}

		[Test]
		public void SelfTransitionDoesNotReenter()
		{
			var a = P("a");
			var fsm = new StateMachineBuilder<Ctx>().Initial(a)
				.From(a).To(a).When(() => true)
				.Build();

			fsm.Tick(0.1f);
			fsm.Tick(0.1f);

			Assert.AreEqual(1, a.Entered);
		}

		[Test]
		public void CurrentStateNameIsExposedForDebugging()
		{
			var a = P("a");
			var fsm = new StateMachineBuilder<Ctx>().Initial(a).Build();
			Assert.AreEqual(nameof(Probe), fsm.CurrentStateName);
		}

		// ─── builder validation ──────────────────────────────────────────────────

		[Test]
		public void BuilderRejectsIncompleteDefinitions()
		{
			Assert.Throws<InvalidOperationException>(() => new StateMachineBuilder<Ctx>().Build());
			Assert.Throws<InvalidOperationException>(() => new StateMachineBuilder<Ctx>().To(P("x")));
			Assert.Throws<InvalidOperationException>(() => new StateMachineBuilder<Ctx>().From(P("a")).When(() => true));
			Assert.Throws<InvalidOperationException>(() => new StateMachineBuilder<Ctx>().Forced());

			var a = P("a");
			Assert.Throws<InvalidOperationException>(() =>
				new StateMachineBuilder<Ctx>().Initial(a).From(a).To(P("b")).Build());
		}
	}
}

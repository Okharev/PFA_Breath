using System;
using System.Collections.Generic;

namespace Ability.NewAbilitySystem
{
    /// <summary>
    /// The fundamental contract for all states.
    /// </summary>
    public interface IState
    {
        void OnEnter();
        void OnUpdate();
        void OnExit();
    }

    /// <summary>
    /// Represents a transition condition between states (Strategy Pattern).
    /// </summary>
    public class Transition
    {
        public IState To { get; }
        public Func<bool> Condition { get; }

        public Transition(IState to, Func<bool> condition)
        {
            To = to;
            Condition = condition;
        }
    }

    /// <summary>
    /// Evaluates transition conditions with zero runtime allocations.
    /// </summary>
    public class TransitionSequencer
    {
        private readonly List<Transition> _anyTransitions = new List<Transition>();
        private readonly Dictionary<IState, List<Transition>> _transitions = new Dictionary<IState, List<Transition>>();
        private List<Transition> _currentTransitions = new List<Transition>();
        
        // Static empty list prevents GC allocations when a state has no outgoing transitions
        private static readonly List<Transition> EmptyTransitions = new List<Transition>(0);

        public void AddTransition(IState from, IState to, Func<bool> condition)
        {
            if (!_transitions.TryGetValue(from, out var list))
            {
                list = new List<Transition>();
                _transitions[from] = list;
            }
            list.Add(new Transition(to, condition));
        }

        public void AddAnyTransition(IState to, Func<bool> condition)
        {
            _anyTransitions.Add(new Transition(to, condition));
        }

        public void ChangeState(IState state)
        {
            _currentTransitions = _transitions.GetValueOrDefault(state, EmptyTransitions);
        }

        public Transition GetReadyTransition()
        {
            // O(A) where A is AnyTransitions. Prioritized for interrupts (Death, Stun).
            for (int i = 0; i < _anyTransitions.Count; i++)
            {
                if (_anyTransitions[i].Condition()) return _anyTransitions[i];
            }

            // O(T) where T is specific transitions for the current state.
            for (int i = 0; i < _currentTransitions.Count; i++)
            {
                if (_currentTransitions[i].Condition()) return _currentTransitions[i];
            }

            return null;
        }
    }

    /// <summary>
    /// The context container that manages state execution and global state events.
    /// </summary>
    public class StateMachine
    {
        public IState CurrentState { get; private set; }
        private readonly TransitionSequencer _sequencer = new TransitionSequencer();

        // Observer Pattern: High-performance, zero-GC event broadcasting
        public event Action<IState> OnStateExited;
        public event Action<IState> OnStateEntered;

        public void Update()
        {
            var transition = _sequencer.GetReadyTransition();
            if (transition != null)
            {
                SetState(transition.To);
            }

            CurrentState?.OnUpdate();
        }

        public void SetState(IState state)
        {
            if (CurrentState == state) return;

            if (CurrentState != null)
            {
                CurrentState.OnExit();
                OnStateExited?.Invoke(CurrentState);
            }
            
            CurrentState = state;
            _sequencer.ChangeState(CurrentState);
            
            if (CurrentState != null)
            {
                CurrentState.OnEnter();
                OnStateEntered?.Invoke(CurrentState);
            }
        }

        public void AddTransition(IState from, IState to, Func<bool> condition) => _sequencer.AddTransition(from, to, condition);
        public void AddAnyTransition(IState to, Func<bool> condition) => _sequencer.AddAnyTransition(to, condition);
    }

    /// <summary>
    /// A state that owns its own isolated StateMachine, allowing infinite nesting.
    /// </summary>
    public abstract class HierarchicalState : IState
    {
        protected StateMachine SubMachine;

        protected HierarchicalState()
        {
            SubMachine = new StateMachine();
        }

        public virtual void OnEnter() { }

        public virtual void OnUpdate()
        {
            SubMachine.Update();
        }

        public virtual void OnExit()
        {
            SubMachine.CurrentState?.OnExit();
        }
    }
}
// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Immutable;

namespace ILSight.Core.Metadata
{
	/// <summary>
	/// Mutable per-column user state for the schema-driven flag filter. The popup UI
	/// changes it through its methods; <see cref="Matches"/> applies it to a row's value. <c>null</c> in
	/// <see cref="MutexSelections"/> means the user picked "any" for that group — every
	/// row passes that axis. Every change bumps <see cref="Revision"/>.
	/// </summary>
	public sealed class FilterState
	{
		private readonly Dictionary<string, ImmutableHashSet<uint>?> _groups = new();
		private readonly Dictionary<string, TriState> _flags = new();
		private int _compiledRevision = -1;
		private uint _requiredMask;
		private uint _excludedMask;

		/// <summary>What each group allows; see above.</summary>
		public IReadOnlyDictionary<string, ImmutableHashSet<uint>?> MutexSelections => _groups;

		/// <summary>What each flag is held to.</summary>
		public IReadOnlyDictionary<string, TriState> FlagStates => _flags;

		/// <summary>Whether a row needs all the required flags or any of them.</summary>
		public MatchMode Mode { get; private set; } = MatchMode.All;

		public required FlagsSchema Schema { get; init; }

		/// <summary>Changes whenever the state does, for views to notice.</summary>
		public int Revision { get; private set; }

		/// <summary>True when no constraints are active — the predicate degenerates to "match all".</summary>
		public bool IsEmpty =>
			_groups.Values.All(static s => s is null) && _flags.Values.All(static s => s == TriState.DontCare);

		/// <summary>A filter of <paramref name="schema"/> that filters nothing: every group "any", every flag
		/// don't-care.</summary>
		public static FilterState From(FlagsSchema schema)
		{
			ArgumentNullException.ThrowIfNull(schema);
			var state = new FilterState { Schema = schema };
			foreach (var group in schema.MutexGroups)
			{
				state._groups[group.Name] = null;
			}

			foreach (var flag in schema.IndependentFlags)
			{
				state._flags[flag.Name] = TriState.DontCare;
			}

			return state;
		}

		/// <summary>A filter holding what this one holds, its revision included, that changes apart from it.</summary>
		public FilterState Copy()
		{
			var copy = new FilterState { Schema = Schema, Mode = Mode, Revision = Revision };
			foreach (var (name, group) in _groups)
			{
				copy._groups[name] = group;
			}
			foreach (var (name, flag) in _flags)
			{
				copy._flags[name] = flag;
			}
			return copy;
		}

		/// <summary>Counts a change only when <paramref name="mode"/> is another than <see cref="Mode"/>.</summary>
		public void UseIndependentMode(MatchMode mode)
		{
			if (Mode == mode)
			{
				return;
			}

			Mode = mode;
			Revision++;
		}

		public void SetMutexSelection(string groupName, ImmutableHashSet<uint>? values)
		{
			_groups[groupName] = values;
			Revision++;
		}

		public void SetFlagState(string flagName, TriState flagState)
		{
			_flags[flagName] = flagState;
			Revision++;
		}

		/// <summary>Resets every group to "any" and every flag to don't-care.</summary>
		public void Clear()
		{
			foreach (var key in _groups.Keys.ToArray())
			{
				_groups[key] = null;
			}

			foreach (var key in _flags.Keys.ToArray())
			{
				_flags[key] = TriState.DontCare;
			}

			Mode = MatchMode.All;
			Revision++;
		}

		/// <summary>Whether a row whose flags are <paramref name="value"/> passes: it holds an allowed
		/// value of every narrowed group, none of the excluded flags, and all or any of the required ones.</summary>
		public bool Matches(uint value)
		{
			if (_compiledRevision != Revision)
			{
				Compile();
			}

			if ((value & _excludedMask) != 0)
			{
				return false;
			}

			foreach (var group in Schema.MutexGroups)
			{
				var allowed = _groups.GetValueOrDefault(group.Name);
				if (allowed is not null && !allowed.Contains(value & group.Mask))
				{
					return false;
				}
			}
			return _requiredMask == 0
				|| (Mode == MatchMode.All ? (value & _requiredMask) == _requiredMask : (value & _requiredMask) != 0);
		}

		private void Compile()
		{
			_requiredMask = _excludedMask = 0;
			foreach (var flag in Schema.IndependentFlags)
			{
				var wanted = _flags.GetValueOrDefault(flag.Name);
				if (wanted == TriState.Required)
				{
					_requiredMask |= flag.Bit;
				}
				else if (wanted == TriState.Excluded)
				{
					_excludedMask |= flag.Bit;
				}
			}
			_compiledRevision = Revision;
		}

		/// <summary>Applies what <see cref="FilterStatePresenter.SaveFilter"/> kept, skipping groups and flags the
		/// schema no longer has.</summary>
		public void Restore(SavedFilterState saved)
		{
			foreach (var (group, values) in saved.Groups ?? new Dictionary<string, uint[]>())
			{
				if (_groups.ContainsKey(group))
				{
					_groups[group] = values.ToImmutableHashSet();
				}
			}
			foreach (var (flag, wanted) in saved.Flags ?? new Dictionary<string, TriState>())
			{
				if (_flags.ContainsKey(flag))
				{
					_flags[flag] = wanted;
				}
			}
			Mode = saved.Mode;
			Revision++;
		}
	}
}

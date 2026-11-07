// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ILSight.Core.Metadata
{
	public enum TriState { DontCare, Required, Excluded }

	public enum MatchMode { All, Any }

	/// <summary>What a <see cref="FilterState"/> constrains, to keep between runs: the values each
	/// narrowed group allows and the flags not left don't-care, by name.</summary>
	public sealed record SavedFilterState(Dictionary<string, uint[]>? Groups, Dictionary<string, TriState>? Flags, MatchMode Mode);

	/// <summary>
	/// Mutable per-column user state for the schema-driven flag filter. The popup UI
	/// pokes at this; <see cref="FilterStatePresenter.MatchesFilter"/> applies it to a row's value. <c>null</c> in
	/// <see cref="MutexSelections"/> means the user picked "any" for that group — every
	/// row passes that axis.
	/// </summary>
	public sealed class FilterState
	{
		internal readonly Dictionary<string, ImmutableHashSet<uint>?> Groups = new();
		internal readonly Dictionary<string, TriState> Flags = new();

		/// <summary>What each group allows; see above. Only <see cref="FilterStatePresenter"/> changes this, so
		/// that <see cref="Revision"/> follows it.</summary>
		public IReadOnlyDictionary<string, ImmutableHashSet<uint>?> MutexSelections => Groups;

		/// <summary>What each flag is held to. Only <see cref="FilterStatePresenter"/> changes this, so that
		/// <see cref="Revision"/> follows it.</summary>
		public IReadOnlyDictionary<string, TriState> FlagStates => Flags;

		/// <summary>Whether a row needs all the required flags or any of them. <see cref="FilterStatePresenter.SetIndependentMode"/>
		/// changes it.</summary>
		public MatchMode Mode { get; internal set; } = MatchMode.All;

		internal int CompiledRevision = -1;
		internal uint RequiredMask;
		internal uint ExcludedMask;

		public required FlagsSchema Schema { get; init; }

		/// <summary>Changes whenever the state does, for views to notice.</summary>
		public int Revision { get; internal set; }

		/// <summary>True when no constraints are active — the predicate degenerates to "match all".</summary>
		public bool IsEmpty =>
			Groups.Values.All(static s => s is null) && Flags.Values.All(static s => s == TriState.DontCare);
}
}

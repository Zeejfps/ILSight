// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using System.Linq;

namespace ILSight.Core.Metadata
{
	/// <summary>
	/// Renders a <see cref="FilterState"/> as a one-line predicate summary
	/// ("Visibility ∈ {Public, NestedPublic} ∧ Sealed ∧ ¬Abstract"). Lives next to
	/// <see cref="FilterState.Matches"/> so the visible string and the actual matching rule
	/// stay in sync; tests assert on the string.
	/// </summary>
	public static class FilterStatePresenter
	{
		/// <summary>What <paramref name="value"/> holds: the value of each mutex group, then which
		/// of the independent flags are set.</summary>
		internal static MetadataTooltip Explain(FlagsSchema schema, uint value)
		{
			var groups = schema.MutexGroups
				.Select(g => new MetadataFlagGroup(g.Name, true, [new MetadataFlag(GetLabel(g, value & g.Mask), true)]))
				.ToList();
			if (schema.IndependentFlags.Count > 0)
            {
                groups.Add(new MetadataFlagGroup(groups.Count > 0 ? "Flags" : null, false,
					schema.IndependentFlags.Select(f => new MetadataFlag($"{f.Name} ({f.Bit:X4})", (value & f.Bit) != 0)).ToArray()));
            }

            return new MetadataTooltip(null, groups);
		}

		private static string GetLabel(MutexGroup group, uint value)
		{
			return group.Values.FirstOrDefault(v => v.Value == value)?.Label ?? $"Unknown ({value:X4})";
		}

		public static string Describe(FilterState state)
		{
			if (state.IsEmpty)
            {
                return "(any)";
            }

            var clauses = new System.Collections.Generic.List<string>();
			foreach (var group in state.Schema.MutexGroups)
			{
				if (!state.Groups.TryGetValue(group.Name, out var sel) || sel is null)
                {
                    continue;
                }

                if (sel.IsEmpty)
				{
					clauses.Add($"{group.Name} ∈ ∅");
					continue;
				}
				var labels = group.Values
					.Where(v => sel.Contains(v.Value))
					.Select(v => StripHexSuffix(v.Label))
					.ToList();
				clauses.Add(labels.Count == 1
					? $"{group.Name} = {labels[0]}"
					: $"{group.Name} ∈ {{{string.Join(", ", labels)}}}");
			}
			var requiredFlags = state.Schema.IndependentFlags
				.Where(f => state.Flags.TryGetValue(f.Name, out var t) && t == TriState.Required)
				.Select(f => f.Name)
				.ToList();
			var excludedFlags = state.Schema.IndependentFlags
				.Where(f => state.Flags.TryGetValue(f.Name, out var t) && t == TriState.Excluded)
				.Select(f => f.Name)
				.ToList();
			if (requiredFlags.Count > 0)
			{
				var joiner = state.Mode == MatchMode.All ? " ∧ " : " ∨ ";
				clauses.Add(requiredFlags.Count == 1
					? requiredFlags[0]
					: $"({string.Join(joiner, requiredFlags)})");
			}
			foreach (var name in excludedFlags)
            {
                clauses.Add($"¬{name}");
            }

            return string.Join(" ∧ ", clauses);
		}

		static string StripHexSuffix(string label)
		{
			// MutexValue labels carry a "(XXXX)" hex suffix from the inferer for disambiguation
			// in the dropdown; the predicate summary reads better without it ("Public" vs
			// "Public (0001)"). Strip the trailing parenthesised group, keep the name.
			var idx = label.LastIndexOf(' ');
			if (idx <= 0)
            {
                return label;
            }

            var rest = label.AsSpan(idx + 1);
			return rest.Length > 2 && rest[0] == '(' && rest[^1] == ')'
				? label[..idx]
				: label;
		}

		public static FilterState CreateFilterState(FlagsSchema schema)
		{
			ArgumentNullException.ThrowIfNull(schema);
			var state = new FilterState { Schema = schema };
			foreach (var group in schema.MutexGroups)
            {
                state.Groups[group.Name] = null;
            }

            foreach (var flag in schema.IndependentFlags)
            {
                state.Flags[flag.Name] = TriState.DontCare;
            }

            return state;
		}

		public static void SetIndependentMode(FilterState state, MatchMode mode)
		{
			if (state.Mode == mode)
            {
                return;
            }

            state.Mode = mode;
			state.Revision++;
		}

		public static void SetMutexSelection(FilterState state, string groupName, ImmutableHashSet<uint>? values)
		{
			state.Groups[groupName] = values;
			state.Revision++;
		}

		public static void SetFlagState(FilterState state, string flagName, TriState flagState)
		{
			state.Flags[flagName] = flagState;
			state.Revision++;
		}

		/// <summary>Resets every group to "any" and every flag to don't-care.</summary>
		public static void ClearFilter(FilterState state)
		{
			foreach (var key in state.Groups.Keys.ToArray())
            {
                state.Groups[key] = null;
            }

            foreach (var key in state.Flags.Keys.ToArray())
            {
                state.Flags[key] = TriState.DontCare;
            }

            state.Mode = MatchMode.All;
			state.Revision++;
		}

		/// <summary>Whether a row whose flags are <paramref name="value"/> passes: it holds an allowed
		/// value of every narrowed group, none of the excluded flags, and all or any of the required ones.</summary>
		public static bool MatchesFilter(FilterState state, uint value)
		{
			if (state.CompiledRevision != state.Revision)
            {
                CompileFilter(state);
            }

            if ((value & state.ExcludedMask) != 0)
            {
                return false;
            }

            foreach (var group in state.Schema.MutexGroups)
			{
				var allowed = state.Groups.GetValueOrDefault(group.Name);
				if (allowed is not null && !allowed.Contains(value & group.Mask))
                {
                    return false;
                }
            }
			return state.RequiredMask == 0
				|| (state.Mode == MatchMode.All ? (value & state.RequiredMask) == state.RequiredMask : (value & state.RequiredMask) != 0);
		}

		private static void CompileFilter(FilterState state)
		{
			state.RequiredMask = state.ExcludedMask = 0;
			foreach (var flag in state.Schema.IndependentFlags)
			{
				var wanted = state.Flags.GetValueOrDefault(flag.Name);
				if (wanted == TriState.Required)
                {
                    state.RequiredMask |= flag.Bit;
                }
                else if (wanted == TriState.Excluded)
                {
                    state.ExcludedMask |= flag.Bit;
                }
            }
			state.CompiledRevision = state.Revision;
		}

		/// <summary>The values picked in each group that has any picked, in order.</summary>
		private static Dictionary<string, uint[]> SaveGroups(FilterState state)
		{
			var groups = new Dictionary<string, uint[]>();
			foreach (var (name, values) in state.Groups)
			{
				if (values is not null)
                {
                    groups[name] = values.Order().ToArray();
                }
            }
			return groups;
		}

		/// <summary>The constraints to keep, or null when there are none.</summary>
		public static SavedFilterState? SaveFilter(FilterState state)
    {
        return state.IsEmpty
    				? null
    				: new SavedFilterState(
    					SaveGroups(state),
    					state.Flags.Where(static f => f.Value != TriState.DontCare).ToDictionary(static f => f.Key, static f => f.Value),
    					state.Mode);
    }

		/// <summary>Applies what <see cref="SaveFilter"/> kept, skipping groups and flags the schema no longer has.</summary>
		public static void RestoreFilter(FilterState state, SavedFilterState saved)
		{
			foreach (var (group, values) in saved.Groups ?? new Dictionary<string, uint[]>())
			{
				if (state.Groups.ContainsKey(group))
                {
                    state.Groups[group] = values.ToImmutableHashSet();
                }
            }
			foreach (var (flag, wanted) in saved.Flags ?? new Dictionary<string, TriState>())
			{
				if (state.Flags.ContainsKey(flag))
                {
                    state.Flags[flag] = wanted;
                }
            }
			state.Mode = saved.Mode;
			state.Revision++;
		}
}
}

// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Generic;
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
				.Select(g => new MetadataFlagGroup
				{
					Header = g.Name,
					IsChoice = true,
					Flags = [new MetadataFlag
					{
						Name = GetLabel(g, value & g.Mask),
						IsSet = true,
					}],
				})
				.ToList();
			if (schema.IndependentFlags.Count > 0)
            {
                groups.Add(new MetadataFlagGroup
                {
                    Header = groups.Count > 0 ? "Flags" : null,
                    IsChoice = false,
                    Flags = schema.IndependentFlags.Select(f => new MetadataFlag
                    {
                        Name = $"{f.Name} ({f.Bit:X4})",
                        IsSet = (value & f.Bit) != 0,
                    }).ToArray(),
                });
            }

            return new MetadataTooltip { Text = null, Groups = groups };
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
				if (!state.MutexSelections.TryGetValue(group.Name, out var sel) || sel is null)
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
				.Where(f => state.FlagStates.TryGetValue(f.Name, out var t) && t == TriState.Required)
				.Select(f => f.Name)
				.ToList();
			var excludedFlags = state.Schema.IndependentFlags
				.Where(f => state.FlagStates.TryGetValue(f.Name, out var t) && t == TriState.Excluded)
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

		/// <summary>The values picked in each group that has any picked, in order.</summary>
		private static Dictionary<string, uint[]> SaveGroups(FilterState state)
		{
			var groups = new Dictionary<string, uint[]>();
			foreach (var (name, values) in state.MutexSelections)
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
    				: new SavedFilterState
    				{
    					Groups = SaveGroups(state),
    					Flags = state.FlagStates.Where(static f => f.Value != TriState.DontCare).ToDictionary(static f => f.Key, static f => f.Value),
    					Mode = state.Mode,
    				};
    }
}
}

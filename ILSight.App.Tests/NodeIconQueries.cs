using ILSight.Core;

namespace ILSight.App.Tests;

internal static class NodeIconQueries
{
    extension(BrowseNode node)
    {
        /// <summary>The icon the tree draws the node with.</summary>
        public NodeIcon Icon => NodeIcons.Of(node.Subject);
    }
}

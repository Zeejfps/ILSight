using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The tree of <see cref="TestSession.Shared"/> as the app tests read it: the async reads of
/// <see cref="SessionQueries"/> and <see cref="NodeQueries"/>, waited for through <see cref="TestWaits"/>.</summary>
internal static class TreeQueries
{
    extension(LoadedAssembly assembly)
    {
        public BrowseNode Root => TestWaits.Wait(() => assembly.ReadRootAsync());
    }

    extension(BrowseNode node)
    {
        /// <summary>The children, built when nobody built them yet; none when they failed.</summary>
        public IReadOnlyList<BrowseNode> Children => TestWaits.Wait(() => node.ListChildrenAsync());

        /// <summary>Builds the node's children; fails the test when they can't be built.</summary>
        public void EnsureChildren()
        {
            Assert.IsType<ChildrenState.Ready>(TestWaits.Wait(() => BrowseSessions.GetChildrenAsync(TestSession.Shared, node)));
        }

        /// <summary>The node at <paramref name="path"/> under the node, one label a level.</summary>
        public BrowseNode Child(params ReadOnlySpan<string> path)
        {
            var labels = path.ToArray();
            return TestWaits.Wait(() => node.GetChildAsync(labels));
        }

        public DecompiledDocument Decompile(DecompilationLanguage language)
        {
            return TestWaits.Wait(() => node.DecompileAsync(language));
        }

        /// <summary>The table named <paramref name="name"/> in a metadata tables folder.</summary>
        public BrowseNode Table(string name)
        {
            return TestWaits.Wait(() => node.TableAsync(name));
        }
    }

    extension(AssemblyList list)
    {
        public BrowseNode? FindNode(CodeReference.Navigable reference)
        {
            return TestWaits.Wait(() => list.FindNodeAsync(reference));
        }
    }
}

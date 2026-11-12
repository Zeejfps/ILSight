using ILSight.Core;

namespace ILSight;

/// <summary>A tab's load of the node it is pointed at: what it shows of the node while it loads, once loaded or
/// failed, or while the node is away (its assembly reloading, the saved documents reopening, or gone); the language
/// and options it is decompiled with; where its view is to stand once loaded, and where it stood since. Only the newest
/// load's result lands (<see cref="TryShowLoaded"/>, <see cref="TryShowFailed"/>). Which node a tab is pointed at is
/// <see cref="Navigation"/>'s, which makes a new load for each (<see cref="TabDocument"/>): no member here points the
/// tab elsewhere, so the navigation hands the load out (<see cref="DocumentTab.Load"/>). Used on the UI thread
/// only.</summary>
public sealed class TabLoad
{
    /// <summary>The tab's work: one started cancels the one before, whose result is then dropped; once the tab is
    /// closed, what is in flight is cancelled and its result dropped. Shared by the tab's loads, as starting another
    /// cancels this one's.</summary>
    private readonly LatestWork _work;

    private TabContent _content;

    /// <summary>Where the view is to stand once the load in flight arrives.</summary>
    private Arrival _arrival;

    /// <summary>Where the view of the shown node last stood, as <see cref="Position"/> gives it.</summary>
    private ViewPosition _viewPosition;

    private DecompilationLanguage _language;

    /// <summary>The options <see cref="Content"/> was decompiled with.</summary>
    private DecompilerOptions _options = DecompilerOptions.Default;

    private int _changes;

    /// <summary>A load of no node, for a tab that shows none.</summary>
    public TabLoad(LatestWork work)
    {
        _work = work;
        _content = new TabContent.Empty();
    }

    /// <summary>A load of <paramref name="node"/>, shown loading in <paramref name="language"/> with
    /// <paramref name="options"/>, the view to stand where <paramref name="arrival"/> says once it arrives; it runs
    /// once <see cref="Start"/>ed.</summary>
    public TabLoad(LatestWork work, BrowseNode node, DecompilationLanguage language, DecompilerOptions options, Arrival arrival)
    {
        _work = work;
        _content = new TabContent.Loading(node);
        _language = language;
        _options = options;
        _arrival = arrival;
    }

    /// <summary>A load waiting for the node at <paramref name="location"/>, titled <paramref name="title"/>, which it
    /// showed in <paramref name="language"/> when the documents were saved, to be found once the assemblies are
    /// listed.</summary>
    public TabLoad(LatestWork work, NodePath location, string? title, DecompilationLanguage language)
    {
        _work = work;
        _content = new TabContent.Reloading { Location = location, Title = title };
        _language = language;
    }

    /// <summary>What the load shows: nothing, the node loading, its code, grid or picture, why it failed, or the place
    /// it waits to find the node at again.</summary>
    public TabContent Content => _content;

    /// <summary>The node shown, or null while the load waits for its place to be found again, or the node no longer
    /// exists.</summary>
    public BrowseNode? Node => _content.Match<BrowseNode?>(
        empty: static _ => null,
        loading: static loading => loading.Node,
        code: static code => code.Node,
        table: static table => table.Node,
        picture: static picture => picture.Node,
        report: static _ => null,
        diagram: static _ => null,
        comparison: static _ => null,
        failed: static failed => failed.Node,
        reloading: static _ => null,
        reopening: static _ => null,
        gone: static _ => null,
        unreadable: static _ => null);

    /// <summary>Where the load points: the path of its node, or while the node is away the path it is found again by
    /// or no longer exists at; outlives reloads of the assembly. Null when the load is of no node.</summary>
    public NodePath? Location => _content.Match<NodePath?>(
        empty: static _ => null,
        loading: static loading => loading.Node.Path,
        code: static code => code.Node.Path,
        table: static table => table.Node.Path,
        picture: static picture => picture.Node.Path,
        report: static _ => null,
        diagram: static _ => null,
        comparison: static _ => null,
        failed: static failed => failed.Node.Path,
        reloading: static reloading => reloading.Location,
        reopening: static _ => null,
        gone: static gone => gone.Location,
        unreadable: static unreadable => unreadable.Location);

    /// <summary>The code, document or markup the load shows, or an empty document while it shows none.</summary>
    public DecompiledDocument Document => _content.Match(
        empty: static _ => DecompiledDocument.Empty,
        loading: static _ => DecompiledDocument.Empty,
        code: static code => code.Document,
        table: static _ => DecompiledDocument.Empty,
        picture: static picture => picture.Source ?? DecompiledDocument.Empty,
        report: static _ => DecompiledDocument.Empty,
        diagram: static _ => DecompiledDocument.Empty,
        comparison: static _ => DecompiledDocument.Empty,
        failed: static _ => DecompiledDocument.Empty,
        reloading: static _ => DecompiledDocument.Empty,
        reopening: static _ => DecompiledDocument.Empty,
        gone: static _ => DecompiledDocument.Empty,
        unreadable: static _ => DecompiledDocument.Empty);

    /// <summary>The name of the node shown, kept while the node is away; null when the load is of no node.</summary>
    public string? Title => _content.Match<string?>(
        empty: static _ => null,
        loading: static loading => NodeText.Name(loading.Node),
        code: static code => NodeText.Name(code.Node),
        table: static table => NodeText.Name(table.Node),
        picture: static picture => NodeText.Name(picture.Node),
        report: static _ => null,
        diagram: static _ => null,
        comparison: static _ => null,
        failed: static failed => NodeText.Name(failed.Node),
        reloading: static reloading => reloading.Title,
        reopening: static _ => null,
        gone: static gone => gone.Title,
        unreadable: static unreadable => unreadable.Title);

    public DecompilationLanguage Language => _language;

    /// <summary>The options <see cref="Content"/> was decompiled with.</summary>
    public DecompilerOptions Options => _options;

    /// <summary>Where the view is to stand once the load in flight arrives.</summary>
    public Arrival Arrival => _arrival;

    public bool IsBusy => _work.IsRunning;

    /// <summary>Counts up whenever the load starts again, or lets go of its node.</summary>
    public int Changes => _changes;

    /// <summary>Where the view of the shown node stands, as the view last kept it, or null while there is none.</summary>
    public ViewPosition? Position
    {
        get
        {
            if (IsBusy)
            {
                return null;
            }

            if (Node is null)
            {
                return null;
            }

            return _viewPosition;
        }
    }

    /// <summary>Where the view of the shown node is to stand when the tab is opened again: where it stands, or while
    /// the node loads, where it is to stand once loaded.</summary>
    public ViewPosition? PositionToReopen
    {
        get
        {
            if (IsBusy)
            {
                return _arrival.Position;
            }

            return Position;
        }
    }

    /// <summary>Starts the load of the node it shows loading, cancelling what was in flight for the tab. Returns the
    /// ticket its result lands with.</summary>
    public WorkTicket Start()
    {
        _changes++;
        return _work.Start();
    }

    /// <summary>Shows <paramref name="loaded"/>, which the load <paramref name="ticket"/> was given for brought,
    /// unless a newer load started since, or the tab closed. Returns whether it landed.</summary>
    public bool TryShowLoaded(WorkTicket ticket, NodeContent loaded)
    {
        if (!_work.TryFinish(ticket))
        {
            return false;
        }

        var node = GetLoadingNode();
        _content = loaded.Match<TabContent>(
            code: code => new TabContent.Code { Node = node, Document = code.Document },
            table: table => new TabContent.Table { Node = node, Grid = table.Grid },
            picture: picture => new TabContent.Picture { Node = node, Image = picture.Image, Source = picture.Source });
        return true;
    }

    /// <summary>Shows that the load <paramref name="ticket"/> was given for failed with <paramref name="message"/>,
    /// unless a newer load started since, or the tab closed. Returns whether it landed.</summary>
    public bool TryShowFailed(WorkTicket ticket, string message)
    {
        if (!_work.TryFinish(ticket))
        {
            return false;
        }

        _content = new TabContent.Failed { Node = GetLoadingNode(), Message = message };
        return true;
    }

    /// <summary>The node loading; only a loading load can show what it brought.</summary>
    private BrowseNode GetLoadingNode()
    {
        if (_content is not TabContent.Loading loading)
        {
            throw new InvalidOperationException("Only a loading tab can show what its load brought.");
        }

        return loading.Node;
    }

    /// <summary>Where the view was to stand once the load arrived, given when it started; forgotten once taken.</summary>
    public Arrival TakeArrival()
    {
        var arrival = _arrival;
        _arrival = new Arrival();
        return arrival;
    }

    /// <summary>Keeps where the view of what the load shows stands, for <see cref="Position"/>. Not counted in
    /// <see cref="Changes"/>: it moves with every scroll, so it is saved with whatever else is, and on exit, as the
    /// window's bounds are.</summary>
    public void KeepPosition(ViewPosition position)
    {
        _viewPosition = position;
    }

    /// <summary>Lets go of the node while its assembly reloads, keeping <paramref name="location"/> to find it again,
    /// cancelling what was in flight.</summary>
    public void ShowReloading(NodePath location)
    {
        ReleaseNode(new TabContent.Reloading { Location = location, Title = Title });
    }

    /// <summary>Lets go of the node, which the assemblies no longer hold at <paramref name="location"/>, cancelling
    /// what was in flight.</summary>
    public void ShowGone(NodePath location)
    {
        ReleaseNode(new TabContent.Gone { Location = location, Title = Title });
    }

    /// <summary>Lets go of the node, whose place at <paramref name="location"/> is in <paramref name="unreadable"/>,
    /// a module whose type system the library could not read, cancelling what was in flight.</summary>
    public void ShowUnreadable(NodePath location, UnreadableModule unreadable)
    {
        ReleaseNode(new TabContent.Unreadable
        {
            Location = location,
            Title = Title,
            Module = unreadable.Module.FilePath.FileName,
            Why = unreadable.Why.Message,
        });
    }

    private void ReleaseNode(TabContent content)
    {
        _changes++;
        Cancel();
        _content = content;
    }

    /// <summary>Cancels the load in flight, if any, whose result is then dropped.</summary>
    public void Cancel()
    {
        _work.Cancel();
    }

    /// <summary>Whether the load shows <paramref name="node"/>, done loading, as <paramref name="language"/> and
    /// <paramref name="options"/> would show it.</summary>
    public bool IsShowing(BrowseNode node, DecompilationLanguage language, DecompilerOptions options)
    {
        if (!ReferenceEquals(node, Node))
        {
            return false;
        }

        if (IsBusy)
        {
            return false;
        }

        if (!node.IsCode)
        {
            return true;
        }

        return IsCurrent(language, options);
    }

    /// <summary>Whether what the load shows was decompiled in <paramref name="language"/> with options that produce
    /// the same code as <paramref name="options"/>.</summary>
    public bool IsCurrent(DecompilationLanguage language, DecompilerOptions options)
    {
        if (language != _language)
        {
            return false;
        }

        return DecompilerToggles.ProducesSameCode(_options, options, language);
    }

    /// <summary>Starts the load of the node it shows again, if it was shown in another language, or with other options,
    /// than <paramref name="language"/> and <paramref name="options"/>, keeping the view where it was when the language
    /// is the same. Returns the ticket its result lands with; null when it needs no loading again.</summary>
    public WorkTicket? StartAgainIfOutdated(DecompilationLanguage language, DecompilerOptions options)
    {
        var node = Node;
        if (node is null)
        {
            return null;
        }

        if (!node.IsCode)
        {
            return null;
        }

        if (IsCurrent(language, options))
        {
            return null;
        }

        _arrival = new Arrival { Position = Navigation.KeepPosition(Position, _language, language) };
        _language = language;
        _options = options;
        _content = new TabContent.Loading(node);
        return Start();
    }
}

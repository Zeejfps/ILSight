namespace ILSight;

/// <summary>The writing side of <see cref="AppCommandQueue"/>: what a view or feature is handed to ask the app state for
/// something once the frame is drawn. It can only ask; running what was asked is the frame edge's.</summary>
public interface IAppCommandWriter
{
    void Enqueue(AppCommand command);
}

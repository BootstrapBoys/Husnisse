namespace Husnisse.Rounds
{
    /// <summary>One unit of work a nisse can perform during a round.</summary>
    public interface IRoundTask
    {
        string Name { get; }
        bool Run(RoundContext context);
    }
}

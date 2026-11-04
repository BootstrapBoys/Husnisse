using Husnisse.Claims;

namespace Husnisse.Rounds
{
    /// <summary>
    /// One bounded pass. Budgets are spent as work happens, so a round can never do more than the
    /// configuration allows regardless of how much is waiting.
    /// </summary>
    public class RoundContext
    {
        public Claim Claim { get; }
        public Bowl.BowlPiece Bowl { get; }
        public Creature.NisseActor Nisse { get; }

        public int CollectBudget { get; private set; }
        public int SortBudget { get; private set; }
        public int MachineBudget { get; private set; }
        public int RefuelBudget { get; private set; }

        public int Collected { get; private set; }
        public int Sorted { get; private set; }

        public RoundContext(Claim claim, Bowl.BowlPiece bowl, Creature.NisseActor nisse,
            int collectBudget, int sortBudget, int machineBudget, int refuelBudget)
        {
            Claim = claim;
            Bowl = bowl;
            Nisse = nisse;
            CollectBudget = collectBudget;
            SortBudget = sortBudget;
            MachineBudget = machineBudget;
            RefuelBudget = refuelBudget;
        }

        public bool CanCollect()
        {
            return CollectBudget > 0;
        }

        public void SpendCollect()
        {
            if (CollectBudget > 0) CollectBudget--;
            Collected++;
        }

        public bool CanSort()
        {
            return SortBudget > 0;
        }

        public void SpendSort()
        {
            if (SortBudget > 0) SortBudget--;
            Sorted++;
        }

        public bool CanServiceMachine()
        {
            return MachineBudget > 0;
        }

        public void SpendMachine()
        {
            if (MachineBudget > 0) MachineBudget--;
        }

        public bool CanRefuel()
        {
            return RefuelBudget > 0;
        }

        public void SpendRefuel()
        {
            if (RefuelBudget > 0) RefuelBudget--;
        }
    }
}

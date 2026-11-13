namespace Tabby.Api.Services
{
    public class SplitCalculator
    {// Splits a total evenly between `people`, working in cents so nothing is lost.
     // Returns one amount per person. Any leftover cents go one each to the first people,
     // so the amounts always add up to exactly the total.
        public static decimal[] EqualSplit(decimal total, int people)
        {
            if (people < 1)
                throw new ArgumentOutOfRangeException(nameof(people), "At least one person is needed.");

            var totalCents = (long)Math.Round(total * 100, MidpointRounding.AwayFromZero);
            var baseCents = totalCents / people;
            var leftover = (int)(totalCents % people);

            var amounts = new decimal[people];
            for (var i = 0; i < people; i++)
            {
                var cents = baseCents + (i < leftover ? 1 : 0);
                amounts[i] = cents / 100m;
            }

            return amounts;
        }
    }
}

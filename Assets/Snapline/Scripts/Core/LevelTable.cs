namespace Snapline.Core
{
    /// <summary>
    /// Which candidate board, and how many moves more or fewer than the base budget, each puzzle level
    /// uses. WRITTEN BY `dotnet run -- levels-gen` from played measurements — do not edit by hand.
    /// </summary>
    internal static class LevelTable
    {
        /// <summary>Pairs of (variant, move adjustment), one pair per level from <see cref="Puzzles.First"/>.</summary>
        private static readonly sbyte[] Data =
        {
            4, -2, 2, 0, 7, -10, 2, -4, 5, 0, 1, -4, 0, -4, 5, 0, 0, -10, 2, -6, 
            0, -2, 1, -10, 2, -2, 0, 0, 0, 0, 1, -4, 10, -10, 4, -4, 1, -6, 5, -6, 
            1, -2, 4, 0, 1, 2, 1, -4, 6, -6, 1, -6, 9, 0, 7, -2, 5, 0, 4, -4, 
            6, -2, 5, 0, 2, -4, 12, 0, 7, -4, 7, -6, 2, -2, 3, 2, 3, -4, 6, 0, 
            0, -10, 3, -2, 8, -2, 4, 2, 7, -2, 3, -8, 5, 0, 0, 0, 10, -6, 9, 0, 
            5, -6, 3, -2, 0, 2, 0, 0, 4, -6, 0, 0, 0, -10, 4, 0, 5, 0, 0, -6, 
            1, 2, 4, 2, 8, -4, 6, -6, 5, -8, 5, -2, 3, -2, 4, 0, 1, 0, 4, -10, 
            4, 0, 2, -2, 4, -2, 3, -2, 0, -4, 1, 0, 0, -10, 4, -4, 5, 0, 1, -6, 
            8, -6, 1, 0, 0, -6, 7, -4, 7, -4, 3, -2, 1, -10, 2, -4, 4, 0, 0, 0, 
            2, 0, 4, 0, 0, 4, 0, 0, 1, 2, 8, -4, 4, 0, 1, -4, 1, 0, 3, -2, 
            4, -4, 6, -4, 0, 0, 10, -2, 7, -8, 0, -2, 3, -2, 4, -2, 1, -2, 4, -10, 
            5, -4, 5, -6, 0, 2, 10, 0, 1, -4, 2, 2, 2, -4, 7, -2, 0, -10, 4, -8, 
            0, -10, 1, 0, 3, 2, 2, 0, 12, 0, 5, 0, 4, -4, 1, -4, 0, 0, 2, 0, 
            4, -2, 9, -8, 9, 0, 4, -4, 0, 0, 5, -4, 4, 4, 1, -2, 3, -8, 1, -6, 
            4, -6, 0, 4, 1, -6, 0, 4, 1, 0, 0, -8, 2, -10, 3, -6, 0, 0, 3, -4, 
            4, 0, 5, -6, 3, -2, 1, 0, 2, 0, 3, -8, 3, 0, 0, -2, 1, -6, 1, -2, 
            1, 4, 0, 2, 1, -6, 0, -4, 3, 2, 3, -2, 4, 0, 3, 2, 3, -10, 2, -6, 
            1, -2, 4, 2, 8, -2, 1, 2, 1, 0, 4, -2, 1, 7, 2, 7, 1, 0, 0, 2, 
            4, 0, 2, 2, 0, -6, 4, -2, 5, -2, 3, -4, 10, 2, 4, 2, 1, -2, 8, 0, 
            4, -4, 2, -8, 2, 0, 0, -10, 0, -2, 4, -6, 3, -4, 7, 0, 5, -2, 4, 0, 
        };

        public static bool TryLookup(int number, out int variant, out int adjust)
        {
            int i = (number - Puzzles.First) * 2;
            if (i < 0 || i + 1 >= Data.Length)
            {
                variant = 0;
                adjust = 0;
                return false;
            }

            variant = Data[i];
            adjust = Data[i + 1];
            return true;
        }
    }
}

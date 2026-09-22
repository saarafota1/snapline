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
            4, -2, 2, 0, 7, -10, 2, -4, 5, 0, 1, -4, 0, -4, 5, 0, 7, -4, 0, -4, 
            0, -2, 1, -10, 2, -2, 0, 0, 0, 0, 3, -2, 10, -10, 4, -4, 1, -4, 5, -6, 
            0, 0, 4, 0, 1, 2, 1, -4, 6, -6, 1, -6, 9, 0, 7, -2, 5, 0, 4, -4, 
            1, -6, 1, 0, 2, -4, 12, 0, 7, -4, 7, -6, 2, -2, 4, -4, 3, -2, 6, 0, 
            0, -8, 2, 0, 8, -2, 4, 4, 7, -2, 3, -4, 5, 0, 2, -4, 9, 0, 9, 0, 
            5, -6, 3, -2, 0, 2, 0, 0, 4, -6, 0, 0, 0, -8, 4, 0, 5, 0, 0, -6, 
            1, 2, 4, 2, 8, -4, 6, -6, 5, -8, 5, -2, 7, -8, 4, 0, 1, 0, 8, -4, 
            4, 0, 2, -2, 4, -2, 3, -2, 0, -4, 1, 0, 1, -10, 6, 0, 5, 0, 1, -6, 
            8, -6, 1, 0, 0, -6, 7, -4, 7, -4, 3, -2, 1, -10, 2, -4, 4, 0, 0, 0, 
            2, 0, 4, 0, 0, 4, 0, 0, 0, -2, 2, -10, 4, 2, 1, -4, 1, 0, 1, -8, 
            4, -4, 6, -4, 0, 0, 10, -2, 7, -6, 0, 0, 3, -2, 4, -2, 3, 0, 5, -6, 
            5, -2, 5, -4, 0, 2, 10, 0, 1, -4, 1, -2, 2, -4, 3, 2, 0, -8, 4, -8, 
            0, -8, 6, -4, 3, 2, 2, 0, 12, 0, 3, -4, 2, -6, 1, -4, 7, -2, 4, -8, 
            4, -2, 6, 0, 1, 0, 4, -4, 0, 0, 6, 0, 4, 7, 1, -2, 3, -6, 5, -6, 
            4, -6, 0, 4, 1, -6, 0, 4, 1, 0, 2, -8, 3, -4, 3, -4, 6, -10, 1, -2, 
            4, 0, 5, -6, 3, -2, 4, 4, 5, 0, 3, -8, 3, 0, 0, 0, 5, -10, 1, -2, 
            3, -2, 0, 2, 1, -4, 2, 0, 2, -8, 3, 2, 4, 0, 3, 4, 3, -8, 2, -4, 
            1, -2, 2, -2, 8, -2, 6, 0, 1, 0, 4, 0, 1, 7, 2, 7, 1, 0, 1, 4, 
            4, 0, 7, 0, 0, -2, 4, 2, 4, 0, 3, -4, 10, 4, 1, 4, 1, 0, 8, 0, 
            4, -2, 2, -4, 8, 4, 6, -6, 3, 0, 4, -6, 0, -4, 6, 4, 3, 7, 4, 0, 
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

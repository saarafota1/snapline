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
            1, 0, 4, -2, 2, -4, 2, -4, 3, 0, 2, 0, 3, -4, 4, -4, 2, -2, 0, 0, 
            1, -2, 1, 0, 1, 0, 0, -6, 0, 0, 1, -4, 10, -6, 9, -6, 1, 0, 3, -6, 
            0, -8, 2, -2, 1, -4, 5, 0, 5, -4, 7, 0, 0, -2, 3, 0, 3, -4, 0, -2, 
            0, 0, 4, -2, 2, 0, 7, 0, 4, 0, 3, 0, 1, -4, 2, -4, 2, -4, 1, -4, 
            6, 0, 3, -2, 5, -2, 4, 0, 7, 0, 0, -2, 4, -2, 0, 0, 10, 0, 8, 0, 
            5, 2, 4, -4, 3, -2, 2, -2, 4, -2, 0, -4, 2, -4, 2, -4, 0, -6, 1, -8, 
            9, -4, 2, -6, 11, -4, 9, -6, 5, -6, 6, -4, 7, -10, 4, 0, 5, -4, 2, -6, 
            7, 0, 1, -4, 3, 0, 2, -2, 0, -2, 1, -10, 4, 0, 6, -6, 5, -6, 0, -2, 
            9, -6, 4, -6, 6, -6, 7, -6, 6, -4, 2, 0, 11, -6, 1, 0, 5, -4, 1, -2, 
            2, -2, 1, 0, 1, -4, 3, -4, 1, -8, 8, 0, 0, 2, 4, -4, 3, -4, 1, -4, 
            4, 0, 1, -2, 1, -2, 14, -2, 7, -6, 0, 0, 3, 4, 2, -4, 1, -2, 4, -2, 
            5, -2, 2, -2, 3, -6, 13, -2, 1, -2, 0, 0, 0, 0, 0, -4, 6, -2, 3, -4, 
            5, 0, 1, -6, 5, -6, 4, 0, 7, -2, 2, -4, 8, -4, 3, 0, 7, -2, 0, -2, 
            4, -8, 8, -4, 9, 0, 3, -2, 4, -4, 0, 0, 4, 2, 5, -4, 3, -2, 0, 0, 
            5, -2, 2, -2, 1, -8, 2, -4, 1, -6, 2, -2, 1, -8, 3, -8, 6, -4, 0, -10, 
            3, -4, 1, 4, 3, -6, 0, -6, 3, 4, 3, -6, 2, -2, 0, -4, 2, -8, 2, 0, 
            1, -2, 1, 0, 4, -6, 2, -4, 2, -2, 8, 2, 4, -2, 0, -6, 5, -4, 0, 0, 
            0, 2, 2, -4, 8, 0, 6, -8, 4, -4, 1, -4, 6, -8, 2, -4, 3, -4, 6, 0, 
            1, 0, 7, 0, 2, -6, 5, 0, 1, -2, 3, 0, 0, -4, 0, -4, 1, -4, 7, -6, 
            3, -6, 3, -8, 7, -4, 6, 7, 4, -2, 4, -2, 0, -2, 3, -2, 3, 2, 4, 2, 
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

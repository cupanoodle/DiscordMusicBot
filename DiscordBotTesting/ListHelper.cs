using System.Collections.Generic;

namespace DiscordBotTesting
{
    public static class ListHelper
    {
        public static void Swap<T>(this IList<T> list, int indexA, int indexB)
        {
            T tmp = list[indexA];
            list[indexA] = list[indexB];
            list[indexB] = tmp;
        }
    }
}

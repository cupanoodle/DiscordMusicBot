using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;

namespace DiscordBotTesting.CommandConverters
{
    class NullableBoolConverter : IArgumentConverter<bool?>
    {
        public bool TryConvert(string value, CommandContext ctx, out bool? result)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (bool.TryParse(value, out bool tmp))
                    result = tmp;
                else
                {
                    switch (value.Trim().ToLowerInvariant())
                    {
                        case "yes":
                            result = true;
                            return true;
                        case "no":
                            result = false;
                            return true;
                        case "1":
                            result = true;
                            return true;
                        case "0":
                            result = false;
                            return true;
                        default:
                            result = null;
                            return true;
                    }
                }
            }
            else
                result = null;

            return true;
        }
    }
    class NullableIntConverter : IArgumentConverter<int?>
    {
        public bool TryConvert(string value, CommandContext ctx, out int? result)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (int.TryParse(value, out int tmp))
                    result = tmp;
                else
                    result = null;
            }
            else
                result = null;

            return true;
        }
    }
}

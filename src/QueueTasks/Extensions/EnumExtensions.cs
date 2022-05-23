namespace QueueTasks.Extensions
{
    using System;
    using System.ComponentModel;
    using System.Linq;

    internal static class EnumExtensions
    {
        public static string GetDescription<TEnum>(this TEnum @enum)
            where TEnum : Enum
        {
            var info = @enum.GetType().GetField(@enum.ToString());
            var attributes = (DescriptionAttribute[])info!.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);

            return attributes?.FirstOrDefault()?.Description ?? @enum.ToString();
        }
    }
}

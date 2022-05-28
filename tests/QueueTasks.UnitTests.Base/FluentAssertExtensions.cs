namespace QueueTasks.UnitTests.Base
{
    using FluentAssertions;
    using Xunit;

    /// <summary>
    ///     Расширения для FluentAssert-функционала
    /// </summary>
    public static class FluentAssertExtensions
    {
        public static void ShouldBeEqual(this object destination, object? source)
        {
            Assert.NotNull(source);
            Assert.NotNull(destination);

            destination.Should().BeEquivalentTo(source, config => config.ExcludingMissingMembers().IgnoringCyclicReferences());
        }
    }
}

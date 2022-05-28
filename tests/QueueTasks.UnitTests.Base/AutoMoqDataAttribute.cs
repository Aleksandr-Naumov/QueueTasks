namespace QueueTasks.UnitTests.Base
{
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Xunit2;

    public class AutoMoqDataAttribute : AutoDataAttribute
    {
        public AutoMoqDataAttribute() : base(() => new Fixture().Customize(new AutoMoqCustomization()))
        {
        }
    }
}

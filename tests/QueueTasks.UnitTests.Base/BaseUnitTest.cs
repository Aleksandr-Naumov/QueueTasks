namespace QueueTasks.UnitTests.Base
{
    using System;
    using System.Diagnostics;

    using AutoFixture;

    using Xunit.Abstractions;

    /// <summary>
    ///     Базовый класс для unit-тестов
    /// </summary>
    public abstract class BaseUnitTest : IDisposable
    {
        protected readonly ITestOutputHelper Output;

        protected readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        protected readonly Fixture Fixture;

        protected BaseUnitTest(ITestOutputHelper output)
        {
            Output = output;
            Fixture = CreateFixture();
        }

        public virtual void Dispose()
        {
            Stopwatch.Stop();
            Output.WriteLine($"Execution time: {Stopwatch.Elapsed.TotalSeconds}");
        }

        public static Fixture CreateFixture()
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            return fixture;
        }
    }
}

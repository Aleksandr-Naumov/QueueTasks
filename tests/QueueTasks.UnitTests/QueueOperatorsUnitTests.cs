namespace QueueTasks.UnitTests
{
    using FluentAssertions;
    using QueueTasks.Models;
    using QueueTasks.UnitTests.Base;
    using System;
    using Xunit;
    using Xunit.Abstractions;

    public class QueueOperatorsUnitTests : BaseUnitTest
    {
        public QueueOperatorsUnitTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [AutoMoqData]
        public void TryEnqueue_Success(QueueOperators queue)
        {
            //arrange
            var operatorIds = new[] { 1.ToString(), 2.ToString(), 3.ToString() };

            //act
            foreach (var operatorId in operatorIds)
            {
                queue.TryEnqueue(operatorId);
            }

            //assert
            var firstOperatorId = queue.Peek();
            foreach (var operatorId in operatorIds)
            {
                firstOperatorId!.ShouldBeEqual(operatorId);

                firstOperatorId = queue.NextPeek(firstOperatorId!);
            }
        }

        [Theory]
        [AutoMoqData]
        public void ChangeStatusToSelects_QueueIsNotEmptyAndNoFreeOperator(QueueOperators queue)
        {
            //arrange
            var operatorId = 1.ToString();
            queue.TryEnqueue(operatorId);

            //act
            queue.ChangeStatusToSelects(operatorId);

            //assert
            queue.Peek().Should().BeNullOrEmpty();
            queue.IsEmpty().Should().BeFalse();
        }

        [Theory]
        [AutoMoqData]
        public void Dequeue_Success(QueueOperators queue)
        {
            //arrange
            var operatorIds = new[] { 1.ToString(), 2.ToString(), 3.ToString() };

            //act
            foreach (var operatorId in operatorIds)
            {
                queue.TryEnqueue(operatorId);
            }

            //assert
            foreach (var operatorId in operatorIds)
            {
                queue.Dequeue()!.ShouldBeEqual(operatorId);
            }
        }
    }
}

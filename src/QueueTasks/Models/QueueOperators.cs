namespace QueueTasks.Models
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using Enums;

    using Extensions;

    /// <summary>
    ///     Очередь из операторов
    /// </summary>
    public class QueueOperators
    {
        /// <summary>
        ///     Ключ - Id оператора,
        ///     Значение - оператор
        /// </summary>
        private readonly ConcurrentDictionary<string, Operator> _operators = new ConcurrentDictionary<string, Operator>();

        /// <summary>
        ///     Добавляет в конец очереди оператора
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>true, если оператора добавлен; false, если оператор уже есть в очереди</returns>
        public bool TryEnqueue(string operatorId) => _operators.TryAdd(operatorId, new Operator());

        /// <summary>
        ///     Удаляет и возвращает Id оператора из начала очереди оператора
        /// </summary>
        /// <returns>Id оператора, если очередь не пуста; инача null</returns>
        public string? Dequeue()
        {
            var firstOperatorId = _operators.OrderBy(x => x.Value.Time).FirstOrDefault().Key;
            if (firstOperatorId == null)
            {
                return null;
            }

            if (!_operators.TryRemove(firstOperatorId, out _))
            {
                return null;
            }

            return firstOperatorId;
        }

        /// <summary>
        ///     Возращает Id свободного оператора из начала очереди
        /// </summary>
        /// <returns>Id оператора, если очередь не пустая; иначе null</returns>
        public string? Peek() =>
            _operators
                .Where(x => x.Value.Status == OperatorStatus.Free)
                .OrderBy(x => x.Value.Time)
                .FirstOrDefault().Key;

        /// <summary>
        ///     Возращает Id оператора из начала очереди, который стоит после переданного оператора;
        /// </summary>
        /// <returns>Id оператора, если в очереди есть операторы после <paramref name="operatorId"/>; иначе null</returns>
        public string? NextPeek(string operatorId)
        {
            var @operator = _operators[operatorId];
            return _operators
                .Where(x => x.Value.Status == OperatorStatus.Free &&
                            x.Value.Time > @operator.Time)
                .OrderBy(x => x.Value.Time)
                .FirstOrDefault().Key;
        }

        /// <summary>
        ///     Удаление оператора из очереди
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>true, если оператор удален; иначе false</returns>
        public bool TryRemove(string operatorId) => _operators.TryRemove(operatorId, out _);

        /// <summary>
        ///     Проверяет есть ли оператор в очереди или нет.
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>true, если оператор стоит в очереди; иначе false</returns>
        public bool Contains(string operatorId) => _operators.ContainsKey(operatorId);

        /// <summary>
        ///     Проверяет пустая очереди или нет
        /// </summary>
        /// <returns>true, если операторов нет в очереди; иначе false</returns>
        public bool IsEmpty() => _operators.IsEmpty;

        /// <summary>
        ///     Сменить статус у оператора, если ему дали потенциальную задачу,
        ///     чтобы ему не приходили новые задачи (когда он думает взять или нет ее)
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        public void ChangeStatusToSelects(string operatorId) => _operators[operatorId].ChangeStatusToSelects();

        /// <summary>
        ///     Сменить статус у оператора, если потенциальную задачу уже назначали на другого оператора,
        ///     чтобы ему досталась следующая новая задача (оператор также остался первым в очереди)
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        public void ChangeStatusToFree(string operatorId) => _operators[operatorId].ChangeStatusToFree();

        /// <summary>
        ///     Метод получения списка операторов в очереди
        /// </summary>
        /// <returns>Информация об операторах, которые находятся в очереди</returns>
        public IEnumerable<OperatorDto> GetOperators() =>
            _operators.OrderBy(x => x.Value.Time).Select(x => new OperatorDto()
            {
                OperatorId = x.Key,
                Time = x.Value.Time.ToString("G"),
                Status = x.Value.Status.GetDescription()
            });
    }
}
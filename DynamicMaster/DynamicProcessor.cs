using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DynamicParser;
using System.Threading;
using Processor = DynamicParser.Processor;

namespace DynamicMaster
{
    /// <summary>
    /// Позволяет создать "обратную карту", т.е. сделать так, чтобы несколько систем смогли быть поданы в виде одного запроса, на одну систему.
    /// Содержит набор систем, которые должны выполнить различные запросы, выдав различные, не пересекающиеся (не похожие) друг с другом ответы.
    /// Важно то, от ответы должны быть все, которые возможны в этой системе. Тогда ответ считается положительным.
    /// </summary>
    public sealed class DynamicProcessor //Убрать этот класс! Сделать возможность включения определённых нейронов... это и будет сеть.
    {
        readonly Neuron _workNeuron;
        readonly List<Neuron> _workNeuronList = new List<Neuron>();
        readonly HashSet<char> _mainCharSet = new HashSet<char>();

        public DynamicProcessor(ProcessorContainer processorContainer)
        {
            _workNeuron = new Neuron(processorContainer);
            foreach (char c in _workNeuron.ToString())
                _mainCharSet.Add(char.ToUpper(c));
        }

        /// <summary>
        /// Проверяет, что текущий экземпляр <see cref="DynamicProcessor"/> обучен и способен выдать положительный результат хотя бы в одном случае.
        /// </summary>
        public bool IsReady { get; }

        /// <summary>
        /// Получает процессор, состоящий только из тех элементов, которые были активны в момент выполнения запроса.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        DynamicProcessor GetDynamicProcessorByQuery(Request request)
        {

        }

        //operator[] для доступа к нейронам; 0 - рабочий, Count...

        public bool FindRelation()//ищет решение для распознавания определённых карт и нерасп. определённых
        {

        }

        /// <summary>
        /// Включается, если все <see cref="Neuron"/> выдали свои (отличные друг от друга) результаты. При этом, сколько карт, столько и результатов должно быть выдано.
        /// Чем больше функций в процессоре, тем больше карт он может распознать.
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        public bool FindRelation(Processor processor)
        {
            if (processor == null)
                throw new ArgumentNullException();
            if (_workNeuronList == null || _workNeuronList.Count <= 0)
                throw new Exception($@"{nameof(FindRelation)}: Массив искомых карт пустой.");

            ThreadPool.GetMinThreads(out _, out int comPortMin);
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 3, comPortMin);
            ThreadPool.GetMaxThreads(out _, out int comPortMax);
            ThreadPool.SetMaxThreads(Environment.ProcessorCount * 15, comPortMax);

            HashSet<char> charSet = new HashSet<char>();
            object thisLock = new object();

            ForEachHelper(_workNeuronList, neuron =>
            {
                foreach (char c in neuron.FindRelation(processor))
                    lock (thisLock)
                        charSet.Add(c);
            });

            return _mainCharSet.SetEquals(charSet);
        }

        /// <summary>
        /// Создаёт новые <see cref="Neuron"/>.
        /// </summary>
        /// <param name="request"></param>
        public bool FindRelation(Request request)
        {
            if (!IsActual(request))
                throw new ArgumentException();
            Neuron neuron = _workNeuron.FindRelation(request);
            if (neuron == null)
                return false;
            _workNeuronList.Add(neuron);
            return true;
        }

        public bool IsActual(Request request) => _workNeuron.IsActual(request);

        public override string ToString() => _workNeuron.ToString();

        static void ForEachHelper<T>(IEnumerable<T> source, Action<T> action)
        {
            if (source == null)
                throw new ArgumentNullException();
            if (action == null)
                throw new ArgumentNullException();
            string errString = string.Empty, errStopped = string.Empty;
            bool exThrown = false;
            Parallel.ForEach(source, (arg, state) =>
            {
                try
                {
                    if (state.IsStopped)
                        return;
                    action(arg);
                }
                catch (Exception ex)
                {
                    try
                    {
                        errString = ex.Message;
                        exThrown = true;
                        state.Stop();
                    }
                    catch (Exception ex1)
                    {
                        errStopped = ex1.Message;
                    }
                }
            });
            if (exThrown)
                throw new Exception(string.IsNullOrWhiteSpace(errStopped) ? errString : $@"{errString}{Environment.NewLine}{errStopped}");
        }
    }
}

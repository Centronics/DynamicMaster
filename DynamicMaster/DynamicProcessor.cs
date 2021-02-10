using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    class DynamicProcessor
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

            ForEachHelper(_workNeuronList, neuron => ForEachHelper(neuron.FindRelation(processor), c =>
            {
                lock (thisLock)
                    charSet.Add(c);
            }));

            return _mainCharSet.SetEquals(charSet);
        }

        /// <summary>
        /// Создаёт новые <see cref="Neuron"/>.
        /// </summary>
        /// <param name="processors"></param>
        public void FindRelation(DynamicRequest request)
        {
            if (!IsActual(request))
                throw new ArgumentException();
            Neuron neuron = _workNeuron.FindRelation(request);
            if (neuron != null)
                _workNeuronList.Add(neuron);
        }

        public bool IsActual(DynamicRequest request) => _workNeuron.IsActual(request);

        public string Query => _workNeuron.ToString();

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

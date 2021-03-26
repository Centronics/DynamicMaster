using System;
using System.Collections.Generic;
using DynamicMosaic;
using DynamicParser;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Processor = DynamicParser.Processor;

namespace DynamicMaster
{
    public sealed class Neuron
    {
        readonly ProcessorContainer _mainContainer;
        readonly Dictionary<char, int> _procNames;
        readonly string _stringQuery;

        public Neuron(ProcessorContainer pc)
        {
            if (pc == null)
                throw new ArgumentNullException();
            if (pc.Count > char.MaxValue)
                throw new ArgumentException();

            _procNames = new Dictionary<char, int>(pc.Count);
            StringBuilder sb = new StringBuilder(pc.Count);
            ProcessorHandler ph = new ProcessorHandler();
            for (int k = 0; k < pc.Count; ++k)
            {
                char c = char.ToUpper(pc[k].Tag[0]), ck = Convert.ToChar(k);
                sb.Append(c);
                _procNames[c] = k;
                ph.Add(ProcessorHandler.RenameProcessor(pc[k], new string(ck, 1)));
            }

            _mainContainer = ph.Processors;
            _stringQuery = sb.ToString();
            WorkReflex = new Reflex(_mainContainer);
        }

        public Reflex WorkReflex { get; }

        /// <summary>
        /// Преобразует запрос из человекочитаемой формы во внутреннее его представление.
        /// </summary>
        /// <param name="query"></param>
        /// <returns>В случае ошибки возвращается <see cref="string.Empty"/>. В противном случае, строка внутреннего запроса.</returns>
        string TranslateQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException();
            StringBuilder sb = new StringBuilder(query.Length);
            foreach (char c in query)
            {
                if (!_procNames.TryGetValue(char.ToUpper(c), out int index))
                    return string.Empty;
                sb.Append(Convert.ToChar(index));
            }
            return sb.ToString();
        }

        IEnumerable<Processor> GetNewProcessors(Reflex start, Reflex finish)
        {
            if (start == null)
                throw new ArgumentNullException();
            if (finish == null)
                throw new ArgumentNullException();
            for (int k = start.Count; k < finish.Count; ++k)
            {
                Processor p = finish[k];
                yield return ProcessorHandler.RenameProcessor(p, _stringQuery[p.Tag[0]].ToString());
            }
        }

        public Neuron FindRelation(Request request)//Никакой "автоподбор" не требуется. Запоминает причины и следствия путём "перебора"... Причина и следствие могут быть любыми, отсюда - любой цвет любого пикселя на карте. Если надо поменять символ карты, можно задать такую карту без ограничений. Это и есть "счётчик".
        {
            if (!request.IsActual(ToString()))
                return null;
            ProcessorHandler ph = new ProcessorHandler();
            foreach ((Processor processor, string query) in request.Queries)
            {
                Reflex refResult = WorkReflex.FindRelation(processor, TranslateQuery(query));
                if (refResult != null)
                    ph.AddRange(GetNewProcessors(WorkReflex, refResult));
            }
            return request.IsActual(ph.ToString()) ? new Neuron(ph.Processors) : null;
        }

        public string FindRelation(Processor processor)
        {
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));

            object lockObject = new object();
            StringBuilder result = new StringBuilder(_stringQuery.Length);
            string errString = string.Empty, errStopped = string.Empty;
            bool exThrown = false, exStopped = false;

            ThreadPool.GetMinThreads(out _, out int comPortMin);
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 3, comPortMin);
            ThreadPool.GetMaxThreads(out _, out int comPortMax);
            ThreadPool.SetMaxThreads(Environment.ProcessorCount * 15, comPortMax);
            Parallel.For(0, _stringQuery.Length, (k, state) =>
            {
                try
                {
                    if (!processor.GetEqual(_mainContainer).FindRelation(Convert.ToChar(k).ToString()))
                        return;
                    lock (lockObject)
                        result.Append(_stringQuery[k]);
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
                        exStopped = true;
                    }
                }
            });
            if (exThrown)
                throw new Exception(exStopped ? $@"{errString}{Environment.NewLine}{errStopped}" : errString);
            return result.ToString();
        }

        public bool IsActual(Request request)
        {
            if (request == null)
                throw new ArgumentNullException();
            return request.IsActual(_stringQuery);
        }

        public Processor this[int index]
        {
            get
            {
                Processor p = _mainContainer[index];
                return ProcessorHandler.RenameProcessor(p, _stringQuery[p.Tag[0]].ToString());
            }
        }

        public int Count => _mainContainer.Count;

        public override string ToString() => _stringQuery;
    }
}
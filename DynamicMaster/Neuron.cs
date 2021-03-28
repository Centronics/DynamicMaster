using System;
using System.Collections.Generic;
using DynamicMosaic;
using DynamicParser;
using System.Text;
using Processor = DynamicParser.Processor;

namespace DynamicMaster
{
    public sealed class Neuron
    {
        readonly Reflex _workReflex;
        readonly Dictionary<char, char> _procNames;
        readonly string _stringQuery;

        public Neuron(ProcessorContainer pc)
        {
            if (pc == null)
                throw new ArgumentNullException();
            if (pc.Count > char.MaxValue)
                throw new ArgumentException();

            _procNames = new Dictionary<char, char>(pc.Count);
            ProcessorHandler ph = new ProcessorHandler();
            StringBuilder sb = new StringBuilder(pc.Count);
            for (char k = char.MinValue; k < pc.Count; ++k)
            {
                if (!ph.Add(ProcessorHandler.RenameProcessor(pc[k], new string(k, 1))))
                    continue;
                char c = char.ToUpper(pc[k].Tag[0]);
                _procNames[c] = k;
                sb.Append(c);
            }

            _workReflex = new Reflex(ph.Processors);
            _stringQuery = sb.ToString();
        }

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
                if (!_procNames.TryGetValue(char.ToUpper(c), out char index))
                    return string.Empty;
                sb.Append(index);
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
                Reflex refResult = _workReflex.FindRelation(processor, TranslateQuery(query));
                if (refResult != null)
                    ph.AddRange(GetNewProcessors(_workReflex, refResult));
            }
            return request.IsActual(ph.ToString()) ? new Neuron(ph.Processors) : null;
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
                Processor p = _workReflex[index];
                return ProcessorHandler.RenameProcessor(p, _stringQuery[p.Tag[0]].ToString());
            }
        }

        public int Count => _workReflex.Count;

        public override string ToString() => _stringQuery;
    }
}
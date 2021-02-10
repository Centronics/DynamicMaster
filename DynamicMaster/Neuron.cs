using System;
using System.Collections.Generic;
using DynamicMosaic;
using DynamicParser;
using System.Text;
using DynamicProcessor;
using System.Threading;
using System.Threading.Tasks;
using Processor = DynamicParser.Processor;

namespace DynamicMaster
{
    sealed class Neuron
    {
        readonly ProcessorContainer _mainContainer;
        readonly HashSet<char> _mainCharSet;
        readonly Dictionary<char, int> _procNames;
        readonly string _stringQuery;

        public Neuron(ProcessorContainer pc)
        {
            if (pc == null)
                throw new ArgumentNullException();
            if (pc.Count > char.MaxValue)
                throw new ArgumentException();

            char cb = char.ToUpper(pc[0].Tag[0]);
            StringBuilder sb = new StringBuilder(pc.Count);
            sb.Append(cb);

            _procNames = new Dictionary<char, int>(pc.Count)
            {
                [cb] = (char)0
            };
            _mainCharSet = new HashSet<char>
            {
                (char)0
            };

            _mainContainer = new ProcessorContainer(RenameProcessor(pc[0], new string((char)0, 1)));

            for (int k = 1; k < pc.Count; ++k)
            {
                char c = char.ToUpper(pc[k].Tag[0]), ck = Convert.ToChar(k);
                sb.Append(c);
                _procNames[c] = k;
                _mainCharSet.Add(ck);
                _mainContainer.Add(RenameProcessor(pc[k], ck.ToString()));
            }
            _stringQuery = sb.ToString();
        }

        /// <summary>
        /// Преобразует запрос из человекочитаемой формы во внутреннее его представление.
        /// </summary>
        /// <param name="query"></param>
        /// <returns>В случае ошибки возвращается <see cref="string.Empty"/>. В противном случае, строка внутреннего запроса.</returns>
        string TranslateQuery(string query)
        {
            StringBuilder sb = new StringBuilder(query.Length);
            foreach (char c in query)
            {
                if (!_procNames.TryGetValue(char.ToUpper(c), out int index))
                    return string.Empty;
                sb.Append(Convert.ToChar(index));
            }
            return sb.ToString();
        }

        static Processor RenameProcessor(Processor processor, string newTag)
        {
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));
            if (string.IsNullOrWhiteSpace(newTag))
                throw new ArgumentException($"\"{nameof(newTag)}\" не может быть пустым или содержать только пробел.", nameof(newTag));

            SignValue[,] sv = new SignValue[processor.Width, processor.Height];
            for (int i = 0; i < processor.Width; ++i)
                for (int j = 0; j < processor.Height; ++j)
                    sv[i, j] = processor[i, j];
            return new Processor(sv, newTag);
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
                yield return RenameProcessor(p, _stringQuery[p.Tag[0]].ToString());
            }
        }

        public Neuron FindRelation(DynamicRequest request)
        {
            if (!request.IsActual(ToString()))
                return null;
            ProcessorContainer preResult = null;
            foreach (ProcessorContainer prc in Matrixes)
            {
                foreach ((Processor processor, string query) in request.Queries)
                {
                    string internalQuery = TranslateQuery(query);
                    if (string.IsNullOrWhiteSpace(internalQuery))
                        throw new ArgumentException();
                    Reflex workReflex = new Reflex(prc);
                    Reflex refResult = workReflex.FindRelation(processor, internalQuery);
                    if (refResult == null)
                        break;
                    List<Processor> lstProcs = new List<Processor>(GetNewProcessors(workReflex, refResult));
                    if (preResult == null)
                        preResult = new ProcessorContainer(lstProcs);
                    else
                        preResult.AddRange(lstProcs);
                }
            }
            if (preResult != null)
                return new Neuron(preResult);
            return null;
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
                    processor.GetEqual(_mainContainer).FindRelation(Convert.ToChar(k).ToString());
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

        /// <summary>
        ///     Возвращает все варианты запросов для распознавания какой-либо карты.
        /// </summary>
        /// <param name="_mainContainer">Массив карт для чтения первых символов их названий. Остальные символы игнорируются.</param>
        /// <returns>Возвращает все варианты запросов для распознавания какой-либо карты.</returns>
        IEnumerable<ProcessorContainer> Matrixes
        {
            get
            {
                if (_mainContainer == null)
                    throw new ArgumentNullException(nameof(_mainContainer), $"{nameof(Matrixes)}: Массив карт равен null.");
                int mx = _mainContainer.Count;
                if (mx <= 0)
                    throw new ArgumentException($"{nameof(Matrixes)}: Массив карт пустой (ось X).", nameof(_mainContainer));
                int[] count = new int[_mainContainer.Count];
                HashSet<char> charSet = new HashSet<char>();
                do
                {
                    ProcessorContainer result = null;
                    charSet.Clear();
                    for (int x = 0; x < mx; x++)
                        if (count[x] < mx)
                        {
                            Processor p = _mainContainer[count[x]];
                            charSet.Add(char.ToUpper(p.Tag[0]));
                            if (result == null)
                                result = new ProcessorContainer(p);
                            else
                                result.Add(p);
                        }
                    if (_mainCharSet.IsSubsetOf(charSet))
                        yield return result;
                } while (ChangeCount(count));
            }
        }

        /// <summary>
        ///     Увеличивает значение старших разрядов счётчика букв, если это возможно.
        ///     Если увеличение было произведено, возвращается значение <see langword="true" />, в противном случае -
        ///     <see langword="false" />.
        /// </summary>
        /// <param name="count">Массив-счётчик.</param>
        /// <returns>
        ///     Если увеличение было произведено, возвращается значение <see langword="true" />, в противном случае -
        ///     <see langword="false" />.
        /// </returns>
        bool ChangeCount(int[] count)
        {
            if (count == null)
                throw new ArgumentNullException(nameof(count), $"{nameof(ChangeCount)}: Массив-счётчик равен null.");
            if (count.Length <= 0)
                throw new ArgumentException(
                    $"{nameof(ChangeCount)}: Длина массива-счётчика некорректна ({count.Length}).", nameof(count));
            if (_mainContainer == null)
                throw new ArgumentNullException(nameof(_mainContainer), $"{nameof(ChangeCount)}: Массив карт равен null.");
            if (_mainContainer.Count <= 0)
                throw new ArgumentException($"{nameof(ChangeCount)}: Массив карт пустой (ось X).", nameof(_mainContainer));
            if (count.Length != _mainContainer.Count)
                throw new ArgumentException(
                    $"{nameof(ChangeCount)}: Длина массива-счётчика не соответствует длине массива карт.",
                    nameof(_mainContainer));
            for (int k = count.Length - 1; k >= 0; k--)
            {
                if (count[k] > _mainContainer.Count - 1)
                    continue;
                count[k]++;
                for (int x = k + 1; x < count.Length; x++)
                    count[x] = 0;
                return true;
            }
            return false;
        }

        public bool IsActual(DynamicRequest request)
        {
            if (request == null)
                throw new ArgumentNullException();
            return request.IsActual(_stringQuery);
        }

        public override string ToString()
        {
            return _stringQuery;
        }
    }
}

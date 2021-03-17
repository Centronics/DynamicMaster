using System;
using System.Collections.Generic;
using System.Linq;
using DynamicMosaic;
using DynamicParser;
using System.Text;
using DynamicProcessor;
using System.Threading;
using System.Threading.Tasks;
using Processor = DynamicParser.Processor;

namespace DynamicMaster
{
    sealed class Neuron //сделать возможность создания сети, где определённые нейроны будут выдавать определённые сигналы
    {
        readonly ProcessorContainer _mainContainer;
        readonly HashSet<char> _mainCharSet;
        readonly Dictionary<char, int> _procNames;
        readonly string _stringQuery;

        public Neuron(ProcessorContainer pc)//проверить на совпадение карт по содержимому или названию
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
            GetWorkReflex = new Reflex(_mainContainer);
        }

        public Reflex GetWorkReflex { get; }

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

        IEnumerable<Processor> GetNewProcessors(Reflex start, Reflex finish, string query)
        {
            if (start == null)
                throw new ArgumentNullException();
            if (finish == null)
                throw new ArgumentNullException();
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException();
            HashSet<char> hs = new HashSet<char>();

            for (int k = start.Count; k < finish.Count; ++k)
            {
                Processor p = finish[k];
                hs.Add(p.Tag[0]);
                yield return RenameProcessor(p, _stringQuery[p.Tag[0]].ToString());
            }

            if (finish.Count - start.Count >= query.Length)
                yield break;

            foreach (char c in query.Where(c => !hs.Contains(c)))
            {
                for (int k = 0; k < start.Count; ++k)
                {
                    Processor p = start[k];
                    if (p.Tag[0] != c)
                        continue;
                    yield return RenameProcessor(p, _stringQuery[p.Tag[0]].ToString());
                    break;
                }
            }
        }

        public Neuron FindRelation(Request request)
        {
            if (!request.IsActual(ToString()))
                return null;
            ProcessorContainer preResult = null;
            foreach ((Processor processor, string query) in request.Queries)
            {
                string internalQuery = TranslateQuery(query);
                if (string.IsNullOrWhiteSpace(internalQuery))
                    throw new ArgumentException();
                Reflex refResult = GetWorkReflex.FindRelation(processor, internalQuery);
                if (refResult == null)
                    break;
                List<Processor> lstProcs = new List<Processor>(GetNewProcessors(GetWorkReflex, refResult, internalQuery));
                if (preResult == null)
                    preResult = new ProcessorContainer(lstProcs);
                else
                    preResult.AddRange(lstProcs);
            }
            if (preResult == null)
                return null;
            StringBuilder sb = new StringBuilder(preResult.Count);
            for (int k = 0; k < preResult.Count; ++k)
                sb.Append(preResult[k].Tag[0]);
            return request.IsActual(sb.ToString()) ? new Neuron(preResult) : null;
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

        /// <summary>
        ///     Предназначен для вычисления хеша определённой последовательности чисел типа <see cref="int" />.
        /// </summary>
        static class CRCIntCalc
        {
            /// <summary>
            ///     Таблица значений для расчёта хеша.
            ///     Вычисляется по алгоритму Далласа Максима (полином равен 49 (0x31).
            /// </summary>
            static readonly int[] Table;

            /// <summary>
            ///     Статический конструктор, рассчитывающий таблицу значений <see cref="Table" /> по алгоритму Далласа Максима (полином
            ///     равен 49 (0x31).
            /// </summary>
            static CRCIntCalc()
            {
                int[] numArray = new int[256];
                for (int index1 = 0; index1 < 256; ++index1)
                {
                    int num = index1;
                    for (int index2 = 0; index2 < 8; ++index2)
                        if ((uint)(num & 128) > 0U)
                            num = (num << 1) ^ 49;
                        else
                            num <<= 1;
                    numArray[index1] = num;
                }

                Table = numArray;
            }

            /// <summary>
            ///     Получает хеш заданной карты.
            ///     Карта не может быть равна <see langword="null" />.
            /// </summary>
            /// <param name="p">Карта, для которой необходимо вычислить значение хеша.</param>
            /// <returns>Возвращает хеш заданной карты.</returns>
            internal static int GetHash(Processor p)
            {
                if (p is null)
                    throw new ArgumentNullException(nameof(p), $@"Функция {nameof(GetHash)}.");
                return GetHash(GetInts(p));
            }

            /// <summary>
            ///     Получает значения элементов карты построчно.
            /// </summary>
            /// <param name="p">Карта, с которой необходимо получить значения элементов.</param>
            /// <returns>Возвращает значения элементов карты построчно.</returns>
            static IEnumerable<int> GetInts(Processor p)
            {
                if (p is null)
                    throw new ArgumentNullException(nameof(p), $@"Функция {nameof(GetInts)}.");
                for (int j = 0; j < p.Height; j++)
                    for (int i = 0; i < p.Width; i++)
                        yield return p[i, j].Value;

                foreach (char c in p.Tag)
                    yield return c;
            }

            /// <summary>
            ///     Получает значение хеша для заданной последовательности целых чисел <see cref="int" />.
            /// </summary>
            /// <param name="ints">Последовательность, для которой необходимо рассчитать значение хеша.</param>
            /// <returns>Возвращает значение хеша для заданной последовательности целых чисел <see cref="int" />.</returns>
            static int GetHash(IEnumerable<int> ints)
            {
                if (ints is null)
                    throw new ArgumentNullException(nameof(ints),
                        $@"Для подсчёта контрольной суммы необходимо указать массив байт. Функция {nameof(GetHash)}.");
                return ints.Aggregate(255, (current, t) => Table[(byte)(current ^ t)]);
            }
        }

        /// <summary>
        ///     Возвращает все варианты запросов для распознавания какой-либо карты.
        /// </summary>
        /// <param name="_mainContainer">Массив карт для чтения первых символов их названий. Остальные символы игнорируются.</param>
        /// <returns>Возвращает все варианты запросов для распознавания какой-либо карты.</returns>
        /*IEnumerable<ProcessorContainer> Matrixes
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
        }*/

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
        /*bool ChangeCount(int[] count)
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
        }*/

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
                return RenameProcessor(p, _stringQuery[p.Tag[0]].ToString());
            }
        }

        public int Count => _mainContainer.Count;

        public override string ToString() => _stringQuery;
    }
}

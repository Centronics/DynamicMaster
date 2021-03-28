using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynamicProcessor;
using Processor = DynamicParser.Processor;
using DynamicParser;

namespace DynamicMaster
{
    public sealed class ProcessorHandler
    {
        readonly Dictionary<int, List<Processor>> _dicProcsWithTag = new Dictionary<int, List<Processor>>();
        readonly HashSet<string> _hashProcs = new HashSet<string>();
        readonly StringBuilder _sbQuery = new StringBuilder();
        readonly HashSet<char> _procNames = new HashSet<char>();

        public ProcessorContainer Processors
        {
            get
            {
                if (IsEmpty)
                    throw new Exception($"{nameof(ProcessorHandler)}: Список карт пуст.");
                ProcessorContainer pc = null;
                foreach (Processor p in _dicProcsWithTag.Values.SelectMany(processors => processors))
                    if (pc == null)
                        pc = new ProcessorContainer(p);
                    else
                        pc.Add(p);
                return pc;
            }
        }

        public int Count { get; private set; }

        public bool IsEmpty { get; private set; } = true;

        Processor GetUniqueProcessor(Processor p)
        {
            if (!_hashProcs.Contains(p.Tag))
                return p;
            string tTag = p.Tag;
            do
            {
                tTag += '0';
            } while (_hashProcs.Contains(tTag));
            _hashProcs.Add(tTag);
            return RenameProcessor(p, tTag);
        }

        public void AddRange(IEnumerable<Processor> processors)
        {
            foreach (Processor processor in processors)
                Add(processor);
        }

        void AddProcessorName(char c)
        {
            c = char.ToUpper(c);
            if (_procNames.Contains(c))
                return;
            _sbQuery.Append(c);
            _procNames.Add(c);
        }

        public bool Add(Processor p)
        {
            int hash = CRCIntCalc.GetHash(p);
            if (_dicProcsWithTag.TryGetValue(hash, out List<Processor> prcs))
            {
                if (prcs.Any(prc => ProcessorCompare(prc, p)))
                    return false;
                prcs.Add(GetUniqueProcessor(p));
                AddProcessorName(p.Tag[0]);
                ++Count;
                IsEmpty = false;
                return true;
            }
            _dicProcsWithTag.Add(hash, new List<Processor> { GetUniqueProcessor(p) });
            AddProcessorName(p.Tag[0]);
            ++Count;
            IsEmpty = false;
            return true;
        }

        static bool ProcessorCompare(Processor p1, Processor p2)
        {
            if (p1 == null)
                throw new ArgumentNullException();
            if (p2 == null)
                throw new ArgumentNullException();
            if (p1.Height != p2.Height)
                throw new ArgumentException();
            if (p1.Width != p2.Width)
                throw new ArgumentException();
            for (int i = 0; i < p1.Width; ++i)
                for (int j = 0; j < p1.Height; ++j)
                    if (p1[i, j] != p2[i, j])
                        return false;
            return true;
        }

        public static Processor RenameProcessor(Processor processor, string newTag)
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

        public override string ToString()
        {
            return _sbQuery.ToString();
        }
    }
}
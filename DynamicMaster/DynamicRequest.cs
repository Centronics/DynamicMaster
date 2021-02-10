using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Processor = DynamicParser.Processor;

namespace DynamicMaster
{
    public class DynamicRequest
    {
        readonly List<(Processor, string)> _dQueries = new List<(Processor, string)>();
        readonly HashSet<char> _mainCharSet = new HashSet<char>();

        public DynamicRequest(IEnumerable<(Processor, string)> queries)
        {
            if (queries == null)
                throw new ArgumentNullException();

            foreach ((Processor p, string q) query in queries)
            {
                if (query.p == null)
                    throw new ArgumentNullException();
                if (string.IsNullOrWhiteSpace(query.q))
                    throw new ArgumentException();
                _dQueries.Add(query);
            }

            if (_dQueries.Count <= 0)
                throw new ArgumentException();

            RefreshCharSet();
        }

        public IEnumerable<(Processor, string)> Queries => _dQueries;

        public int Count => _dQueries.Count;

        public void Add(Processor processor, string query)
        {
            if (processor == null)
                throw new ArgumentNullException();
            if (query == null)
                throw new ArgumentNullException();
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException();
            _dQueries.Add((processor, query));
            RefreshCharSet();
        }

        public void Remove(int index)
        {
            _dQueries.RemoveAt(index);
            RefreshCharSet();
        }

        void RefreshCharSet()
        {
            _mainCharSet.Clear();
            foreach ((Processor _, string q) in _dQueries)
                _mainCharSet.Add(char.ToUpper(q[0]));
        }

        public (Processor processor, string query) this[int index]
        {
            get => _dQueries[index];
            set
            {
                if (value.processor == null)
                    throw new ArgumentNullException();
                if (value.query == null)
                    throw new ArgumentNullException();
                if (string.IsNullOrWhiteSpace(value.query))
                    throw new ArgumentException();
                _dQueries[index] = value;
            }
        }

        public bool IsActual(DynamicRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return IsActual(request.ToString());
        }

        public bool IsActual(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException($"\"{nameof(query)}\" не может быть пустым или содержать только пробел.", nameof(query));
            HashSet<char> charSet = new HashSet<char>();
            foreach (char c in query)
                charSet.Add(char.ToUpper(c));
            return _mainCharSet.SetEquals(charSet);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(_mainCharSet.Count);
            foreach (char c in _mainCharSet)
                sb.Append(c);
            return sb.ToString();
        }
    }
}

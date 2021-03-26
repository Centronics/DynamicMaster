using System;
using System.Collections.Generic;
using System.Linq;
using Processor = DynamicParser.Processor;

namespace DynamicMaster
{
    /// <summary>
    ///     Предназначен для вычисления хеша определённой последовательности чисел типа <see cref="int" />.
    /// </summary>
    public static class CRCIntCalc
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
}
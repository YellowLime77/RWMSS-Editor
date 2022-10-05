using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;



namespace WpfApp1
{
    public static class Class1
    {
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
        {
            return source.Select((item, index) => (item, index));
        }
    }
}
